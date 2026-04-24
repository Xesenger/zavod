using System.Collections.Generic;

namespace zavod.Workspace;

public sealed record WorkspaceScanResult(
    WorkspaceState State,
    IReadOnlyList<string> RelevantFiles,
    IReadOnlyList<WorkspaceMaterialCandidate> MaterialCandidates,
    WorkspaceScanBudgetReport? BudgetReport = null);

public sealed record WorkspaceScanBudgetReport(
    WorkspaceScanBudget Budget,
    int VisitedFileCount,
    int IncludedRelevantFileCount,
    int SkippedLargeFileCount,
    int SkippedRelevantFileCount,
    bool IsPartial,
    IReadOnlyList<WorkspaceScanBudgetSkip> Skips);

public sealed record WorkspaceScanBudgetSkip(
    string RelativePath,
    string Reason,
    string Detail);
