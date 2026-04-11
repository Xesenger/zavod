using System.Collections.Generic;

namespace zavod.Workspace;

public sealed record WorkspaceScanRequest(
    string WorkspaceRoot,
    IReadOnlyList<string>? IncludePaths = null);
