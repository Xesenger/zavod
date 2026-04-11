using zavod.Entry;

namespace zavod.Outcome;

public sealed record ExecutionOutcome(
    ExecutionTarget Target,
    ExecutionOutcomeStatus Status,
    string? Message = null);
