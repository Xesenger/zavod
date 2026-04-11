namespace zavod.Workspace;

public sealed record WorkspaceMaterialPreviewCandidate(
    string RelativePath,
    WorkspaceMaterialKind Kind,
    string SelectionReason);
