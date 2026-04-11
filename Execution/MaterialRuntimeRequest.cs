using zavod.Workspace;

namespace zavod.Execution;

public sealed record MaterialRuntimeRequest(
    string DisplayPath,
    string? FullPath,
    WorkspaceMaterialKind Kind,
    string SelectionReason,
    int MaxChars,
    string? InlineText = null);
