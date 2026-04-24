using System.Collections.Generic;

namespace zavod.Workspace;

public sealed record WorkspaceScanRequest(
    string WorkspaceRoot,
    IReadOnlyList<string>? IncludePaths = null,
    WorkspaceScanBudget? Budget = null);

public sealed record WorkspaceScanBudget(
    int MaxVisitedFiles,
    int MaxRelevantFiles,
    long MaxRelevantFileBytes)
{
    public static WorkspaceScanBudget Default { get; } = new(
        MaxVisitedFiles: 250_000,
        MaxRelevantFiles: 50_000,
        MaxRelevantFileBytes: 10L * 1024L * 1024L);
}
