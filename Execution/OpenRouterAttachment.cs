namespace zavod.Execution;

public sealed record OpenRouterAttachment(
    string Label,
    string IntakeType,
    string FilePath,
    string Content,
    bool UsedPreviewOnly);
