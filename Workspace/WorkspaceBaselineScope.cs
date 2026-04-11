using System.Collections.Generic;

namespace zavod.Workspace;

public sealed record WorkspaceBaselineScope(
    IReadOnlyList<string> RootDirectories);
