using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace zavod.Acceptance;

public static class ExecutionBaseBuilder
{
    public static ExecutionBase Build(string workspaceRoot, IEnumerable<string> relativePaths, string? executionId = null)
    {
        ArgumentNullException.ThrowIfNull(workspaceRoot);
        ArgumentNullException.ThrowIfNull(relativePaths);

        var fullRoot = Path.GetFullPath(workspaceRoot.Trim());
        var touchedScope = TouchedScopeBuilder.Build(relativePaths);
        var createdAt = DateTimeOffset.UtcNow;
        var files = touchedScope.RelativePaths
            .Select(path => BuildFileEntry(fullRoot, path))
            .OrderBy(static entry => entry.RelativePath, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var stableExecutionId = string.IsNullOrWhiteSpace(executionId)
            ? $"EXECBASE-{createdAt.UtcTicks}"
            : executionId.Trim();
        var summaryLine = $"Execution base captured for {files.Length} touched file(s).";

        return new ExecutionBase(
            stableExecutionId,
            createdAt,
            touchedScope,
            files,
            summaryLine);
    }

    private static ExecutionBaseFileEntry BuildFileEntry(string workspaceRoot, string relativePath)
    {
        var fullPath = Path.GetFullPath(Path.Combine(workspaceRoot, relativePath));
        if (!IsUnderRoot(workspaceRoot, fullPath))
        {
            throw new InvalidOperationException("Touched scope path escaped workspace root.");
        }

        var fileInfo = new FileInfo(fullPath);
        return new ExecutionBaseFileEntry(
            relativePath,
            fileInfo.Exists ? fileInfo.Length : 0,
            fileInfo.Exists ? fileInfo.LastWriteTimeUtc.Ticks : 0);
    }

    private static bool IsUnderRoot(string root, string path)
    {
        var comparison = OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;
        var rootFull = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var pathFull = Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

        return string.Equals(pathFull, rootFull, comparison) ||
               pathFull.StartsWith(rootFull + Path.DirectorySeparatorChar, comparison) ||
               pathFull.StartsWith(rootFull + Path.AltDirectorySeparatorChar, comparison);
    }
}
