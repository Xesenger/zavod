using System.Collections.Generic;

namespace zavod.Workspace;

public sealed record WorkspaceScanResult(
    WorkspaceState State,
    IReadOnlyList<string> RelevantFiles,
    IReadOnlyList<WorkspaceMaterialCandidate> MaterialCandidates);
