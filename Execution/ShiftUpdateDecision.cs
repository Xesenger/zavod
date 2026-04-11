using zavod.Entry;
using zavod.Outcome;

namespace zavod.Execution;

public sealed record ShiftUpdateDecision(
    ExecutionTarget Target,
    ExecutionOutcomeStatus OutcomeStatus,
    ShiftUpdateDecisionStatus ApplyStatus,
    string? Reason = null);
