using System;
using System.Collections.Generic;
using System.Linq;
using zavod.Workspace;

namespace zavod.Tooling;

public sealed class WorkspaceTool : IWorkspaceTool
{
    public ToolExecutionResult Execute(WorkspaceInspectRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        Require(!string.IsNullOrWhiteSpace(request.WorkspaceRoot), "workspace inspect", "workspace root", "Workspace root is required.");

        var scanResult = WorkspaceScanner.Scan(new WorkspaceScanRequest(request.WorkspaceRoot, request.IncludePaths));
        var state = scanResult.State;
        var baseline = WorkspaceBaselineBuilder.Build(scanResult);
        var outputs = BuildOutputs(scanResult, baseline);
        var warnings = state.StructuralAnomalies
            .Select(static anomaly => new ToolWarning(anomaly.Code, anomaly.Message))
            .ToArray();
        var diagnostics = state.Health is WorkspaceHealthStatus.Healthy
            ? null
            : new ToolDiagnostic(state.Health.ToString().ToUpperInvariant(), BuildDiagnosticMessage(state));

        return new ToolExecutionResult(
            state.Health is WorkspaceHealthStatus.Healthy,
            BuildSummary(scanResult),
            Array.Empty<IntakeArtifact>(),
            outputs,
            warnings,
            diagnostics);
    }

    private static void Require(bool condition, string area, string missingRequirement, string reason)
    {
        if (!condition)
        {
            throw new ToolingException(area, missingRequirement, reason);
        }
    }

    private static IReadOnlyList<ToolOutputItem> BuildOutputs(WorkspaceScanResult scanResult, WorkspaceBaseline baseline)
    {
        var state = scanResult.State;
        var previewCandidates = WorkspaceMaterialShortlistBuilder.Build(scanResult);
        var outputs = new List<ToolOutputItem>
        {
            new(
                "WORKSPACE_STATE",
                "workspace_state",
                $"Health={state.Health}; Drift={state.DriftStatus}; ImportKind={state.ImportKind}; RelevantFiles={state.Summary.RelevantFileCount}.",
                $"workspace://state/{state.Health.ToString().ToLowerInvariant()}"),
            new(
                baseline.BaselineId,
                "workspace_baseline",
                baseline.SummaryLine,
                $"workspace://baseline/{baseline.BaselineId}")
        };

        outputs.AddRange(state.Summary.SourceRoots.Select(static root => new ToolOutputItem(
            $"SOURCE_ROOT-{root}",
            "source_root",
            $"Detected source root '{root}'.",
            $"workspace://source-root/{root}")));

        outputs.AddRange(state.Summary.BuildRoots.Select(static root => new ToolOutputItem(
            $"BUILD_ROOT-{root}",
            "build_root",
            $"Detected build-derived root '{root}'.",
            $"workspace://build-root/{root}")));

        outputs.AddRange(state.Summary.EntryCandidates.Select(static entry => new ToolOutputItem(
            $"ENTRY-{entry}",
            "entry_candidate",
            $"Detected entry candidate '{entry}'.",
            $"workspace://entry/{entry}")));

        outputs.AddRange(scanResult.MaterialCandidates.Select(static material => new ToolOutputItem(
            $"MATERIAL-{material.RelativePath}",
            "user_material",
            $"Preserved {material.Kind} at '{material.RelativePath}'.",
            $"workspace://material/{material.RelativePath}")));

        outputs.AddRange(previewCandidates.Select(static candidate => new ToolOutputItem(
            $"MATERIAL_PREVIEW-{candidate.RelativePath}",
            "material_preview_candidate",
            $"Selected {candidate.Kind} preview candidate '{candidate.RelativePath}' ({candidate.SelectionReason}).",
            $"workspace://material-preview/{candidate.RelativePath}")));

        return outputs;
    }

    private static string BuildSummary(WorkspaceScanResult scanResult)
    {
        var state = scanResult.State;
        var anomalyCodes = state.StructuralAnomalies
            .Select(static anomaly => anomaly.Code)
            .Take(3)
            .ToArray();
        var anomalySuffix = anomalyCodes.Length == 0
            ? "anomalies=none"
            : $"anomalies={string.Join(",", anomalyCodes)}";
        var previewCandidates = WorkspaceMaterialShortlistBuilder.Build(scanResult);

        return $"Workspace scan completed: health={state.Health}, drift={state.DriftStatus}, import={state.ImportKind}, relevant={state.Summary.RelevantFileCount}, source={state.Summary.SourceFileCount}, build={state.Summary.BuildFileCount}, config={state.Summary.ConfigFileCount}, binaries={state.Summary.BinaryFileCount}, noise={state.Summary.IgnoredNoiseFileCount}, materials={scanResult.MaterialCandidates.Count}, previewCandidates={previewCandidates.Count}, roots={state.Summary.SourceRoots.Count}, buildRoots={state.Summary.BuildRoots.Count}, entries={state.Summary.EntryCandidates.Count}, {anomalySuffix}.";
    }

    private static string BuildDiagnosticMessage(WorkspaceState state)
    {
        if (state.StructuralAnomalies.Count == 0)
        {
            return $"Workspace scan ended with health status {state.Health}.";
        }

        return $"{state.Health}: {string.Join("; ", state.StructuralAnomalies.Select(static anomaly => anomaly.Message))}";
    }
}
