namespace zavod.Workspace;

public sealed record WorkspaceStructuralAnomaly(
    string Code,
    string Message,
    string? Scope = null);
