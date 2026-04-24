namespace zavod.Persistence;

public sealed record ConversationArtifactReference(
    string ArtifactId,
    string ConversationId,
    string FilePath,
    string RelativePath,
    string Preview,
    string MediaType,
    long SizeBytes);

public sealed record ConversationComposerDraftItem(
    string DraftId,
    string ConversationId,
    string? ProjectId,
    string Origin,
    string IntakeType,
    string DisplayName,
    string Preview,
    string Detail,
    long SizeBytes,
    ConversationArtifactReference Reference);
