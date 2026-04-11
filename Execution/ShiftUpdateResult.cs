using zavod.Entry;
using zavod.Outcome;

namespace zavod.Execution;

public sealed record ShiftUpdateResult(
    ExecutionTarget Target,
    ExecutionOutcomeStatus OutcomeStatus,
    string? Message,
    ShiftUpdateStatus Status);
