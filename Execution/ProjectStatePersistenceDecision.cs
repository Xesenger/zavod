using zavod.Persistence;

namespace zavod.Execution;

public sealed record ProjectStatePersistenceDecision(
    ProjectState State,
    ProjectStateMutationStatus MutationStatus,
    ProjectStatePersistenceDecisionStatus Status,
    bool ShouldPersist,
    string? Reason = null);
