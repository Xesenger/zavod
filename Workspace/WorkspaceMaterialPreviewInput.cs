namespace zavod.Workspace;

public sealed record WorkspaceMaterialPreviewInput(
    string RelativePath,
    WorkspaceMaterialKind Kind,
    string SelectionReason,
    string PreviewText,
    bool WasTruncated,
    string? PreparationStatus = null,
    string? BackendId = null,
    string? PreparationSummary = null);
