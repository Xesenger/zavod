using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace zavod.Workspace;

public static class WorkspaceBaselineBuilder
{
    public static WorkspaceBaseline Build(WorkspaceScanResult scanResult)
    {
        ArgumentNullException.ThrowIfNull(scanResult);

        var state = scanResult.State;
        var relevantFiles = scanResult.RelevantFiles
            .Select(path => BuildFileEntry(state.WorkspaceRoot, path))
            .OrderBy(static entry => entry.RelativePath, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var scopeRoots = state.Summary.SourceRoots.Count > 0
            ? state.Summary.SourceRoots
            : new[] { "." };
        var scope = new WorkspaceBaselineScope(scopeRoots);
        var isPartial = !state.HasRecognizableProjectStructure;
        var baselineId = BuildBaselineId(state);
        var summaryLine = isPartial
            ? $"Partial baseline created for '{state.WorkspaceRoot}' with {relevantFiles.Length} relevant files."
            : $"Baseline created for '{state.WorkspaceRoot}' with {relevantFiles.Length} relevant files.";

        return new WorkspaceBaseline(
            baselineId,
            state.LastScanAt,
            scope,
            relevantFiles,
            isPartial,
            summaryLine);
    }

    private static WorkspaceBaselineFileEntry BuildFileEntry(string workspaceRoot, string fullPath)
    {
        var fileInfo = new FileInfo(fullPath);
        return new WorkspaceBaselineFileEntry(
            Path.GetRelativePath(workspaceRoot, fullPath),
            fileInfo.Exists ? fileInfo.Length : 0,
            fileInfo.Exists ? fileInfo.LastWriteTimeUtc.Ticks : 0);
    }

    private static string BuildBaselineId(WorkspaceState state)
    {
        var scopePart = state.HasRecognizableProjectStructure ? "FULL" : "PARTIAL";
        return $"BASELINE-{scopePart}-{state.LastScanAt.UtcTicks}";
    }
}
