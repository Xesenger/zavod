using zavod.Entry;
using zavod.Outcome;

namespace zavod.Execution;

public sealed record ShiftUpdateCandidate(
    ExecutionTarget Target,
    ExecutionOutcomeStatus OutcomeStatus,
    string? Message,
    ProposedShiftEffect ProposedShiftEffect,
    bool ShouldKeepShiftOpen,
    bool IsEligibleToClose,
    bool HasRejectedOutcome);
