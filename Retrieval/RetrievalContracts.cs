using System.Collections.Generic;
using zavod.Tooling;

namespace zavod.Retrieval;

public sealed record ArtifactInventoryEntry(
    string EntryId,
    string NameOrPath,
    ArtifactInventoryEntryType Type,
    IReadOnlyList<ArtifactMetadataEntry> Metadata);

public sealed record ArtifactInventory(
    string ArtifactId,
    IReadOnlyList<ArtifactInventoryEntry> Entries);

public sealed record Candidate(
    string CandidateId,
    string SourceArtifactId,
    string Reference,
    double RelevanceScore,
    IReadOnlyList<string> Tags,
    string? Preview = null);

public sealed record RetrievalFilter(
    IReadOnlyList<string>? Extensions = null,
    IReadOnlyList<string>? PathContains = null,
    IReadOnlyList<ArtifactInventoryEntryType>? EntryTypes = null);

public sealed record RetrievalRequest(
    IReadOnlyList<IntakeArtifact> TargetArtifacts,
    IReadOnlyList<string>? IntentHints = null,
    RetrievalFilter? Filters = null,
    int MaxCandidates = 5);

public sealed record RetrievalResult(
    IReadOnlyList<Candidate> Candidates,
    string Summary,
    IReadOnlyList<ToolWarning> Warnings);

public sealed record ScopedContextReference(
    string ArtifactId,
    string Reference,
    string Label);

public sealed record ScopedContextSnippet(
    string CandidateId,
    string Content);

public sealed record ScopedContext(
    IReadOnlyList<Candidate> SelectedCandidates,
    IReadOnlyList<ScopedContextReference> SourceReferences,
    IReadOnlyList<ScopedContextSnippet> ExtractedSnippets,
    string ContextSummary);
