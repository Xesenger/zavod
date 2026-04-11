using System;
using System.Collections.Generic;
using System.Linq;
using zavod.Execution;
using zavod.Tooling;
using zavod.Workspace;

namespace zavod.Acceptance;

public static class AcceptanceEvidenceBuilder
{
    public static AcceptanceEvidence Build(
        WorkspaceState workspaceObservation,
        WorkspaceBaseline baseline,
        ExecutionBase executionBase,
        RuntimeSelectionDecision? runtimeSelection,
        RuntimeProfile runtimeProfile,
        string executionResultSummary,
        string changePayloadSummary,
        AcceptanceProcessEvidence processEvidence,
        ToolExecutionEnvelope? toolExecution,
        string currentWorkspaceCheckResult,
        AcceptanceClassification classification = AcceptanceClassification.Unknown)
    {
        ArgumentNullException.ThrowIfNull(workspaceObservation);
        ArgumentNullException.ThrowIfNull(baseline);
        ArgumentNullException.ThrowIfNull(executionBase);
        ArgumentNullException.ThrowIfNull(runtimeProfile);
        ArgumentException.ThrowIfNullOrWhiteSpace(executionResultSummary);
        ArgumentException.ThrowIfNullOrWhiteSpace(changePayloadSummary);
        ArgumentNullException.ThrowIfNull(processEvidence);
        ArgumentException.ThrowIfNullOrWhiteSpace(currentWorkspaceCheckResult);
        toolExecution?.Validate();

        runtimeProfile.Validate();
        runtimeProfile = runtimeProfile.Normalize();
        var runtimeSubstrate = RuntimeSubstrateBuilder.Build(runtimeProfile);
        var runtimeInterruption = processEvidence.RuntimeInterruption?.Normalize();
        runtimeSelection ??= new RuntimeSelectionDecision(
            runtimeProfile,
            IsAllowed: true,
            $"Runtime profile '{runtimeProfile.ProfileId}' was used without an explicit policy decision attached.");

        var touchedFiles = executionBase.Scope.RelativePaths
            .OrderBy(static path => path, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var inputs = new AcceptanceInputs(
            baseline.BaselineId,
            executionBase.ExecutionId,
            runtimeProfile,
            runtimeSubstrate,
            toolExecution?.Normalize(),
            runtimeInterruption,
            runtimeSelection.Reason.Trim(),
            currentWorkspaceCheckResult.Trim(),
            classification);

        var interruptionSummary = runtimeInterruption is null
            ? "none"
            : $"{runtimeInterruption.Reason}";

        var summaryLine =
            $"Acceptance evidence assembled: touched={touchedFiles.Length}, runtime={runtimeProfile.ProfileId}, family={runtimeProfile.Family}, network={runtimeSubstrate.NetworkBroker.AccessMode}, tool={(toolExecution is null ? "none" : toolExecution.ResolvedTool.ToolName)}, interruption={interruptionSummary}, workspaceHealth={workspaceObservation.Health}, drift={workspaceObservation.DriftStatus}, classification={classification}.";

        return new AcceptanceEvidence(
            executionResultSummary.Trim(),
            touchedFiles,
            workspaceObservation,
            baseline,
            executionBase,
            runtimeSubstrate,
            toolExecution?.Normalize(),
            runtimeInterruption,
            changePayloadSummary.Trim(),
            processEvidence,
            inputs,
            summaryLine);
    }

    public static AcceptanceEvidence BuildFromExecution(
        WorkspaceState workspaceObservation,
        WorkspaceBaseline baseline,
        ExecutionBase executionBase,
        RuntimeSelectionDecision? runtimeSelection,
        ExecutionRunResult runResult,
        WorkerExecutionResult? workerResult,
        AcceptanceProcessEvidence processEvidence,
        ToolExecutionEnvelope? toolExecution,
        string currentWorkspaceCheckResult,
        AcceptanceClassification classification = AcceptanceClassification.Unknown)
    {
        ArgumentNullException.ThrowIfNull(runResult);

        var executionResultSummary = BuildExecutionResultSummary(runResult, workerResult);
        var changePayloadSummary = BuildChangePayloadSummary(workerResult);

        return Build(
            workspaceObservation,
            baseline,
            executionBase,
            runtimeSelection,
            runResult.EffectiveRuntimeProfile,
            executionResultSummary,
            changePayloadSummary,
            processEvidence,
            toolExecution,
            currentWorkspaceCheckResult,
            classification);
    }

    private static string BuildExecutionResultSummary(ExecutionRunResult runResult, WorkerExecutionResult? workerResult)
    {
        if (workerResult is null)
        {
            return string.IsNullOrWhiteSpace(runResult.Outcome.Message)
                ? $"Runtime outcome: {runResult.Outcome.Status}."
                : runResult.Outcome.Message;
        }

        return $"{runResult.Outcome.Status}: {workerResult.Status}. {workerResult.Summary}";
    }

    private static string BuildChangePayloadSummary(WorkerExecutionResult? workerResult)
    {
        if (workerResult is null)
        {
            return "No worker change payload was provided.";
        }

        if (workerResult.Modifications.Count == 0)
        {
            return "Worker produced no declared file modifications.";
        }

        var changes = workerResult.Modifications
            .Select(static modification => $"{modification.ChangeKind}:{modification.Path}")
            .ToArray();

        return string.Join(", ", changes);
    }
}
