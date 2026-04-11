namespace zavod.Workspace;

public sealed record WorkspaceMaterialTextExtract(
    string RelativePath,
    WorkspaceMaterialKind Kind,
    string SelectionReason,
    WorkspaceMaterialTextExtractStatus Status,
    string PreviewText,
    bool WasTruncated,
    string StatusReason);
