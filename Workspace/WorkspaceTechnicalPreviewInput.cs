namespace zavod.Workspace;

public sealed record WorkspaceTechnicalPreviewInput(
    string RelativePath,
    string Category,
    string PreviewText,
    bool WasTruncated);
