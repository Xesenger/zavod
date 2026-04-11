namespace zavod.Workspace;

public sealed record WorkspaceMaterialCandidate(
    string RelativePath,
    WorkspaceMaterialKind Kind);
