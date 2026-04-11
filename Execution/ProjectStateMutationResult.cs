using zavod.Persistence;

namespace zavod.Execution;

public sealed record ProjectStateMutationResult(
    ProjectState State,
    ProjectStateMutationStatus Status,
    string? Message = null);
