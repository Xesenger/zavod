using zavod.Entry;
using zavod.Outcome;

namespace zavod.Execution;

public sealed record ShiftClosureProposal(
    ExecutionTarget Target,
    ExecutionOutcomeStatus OutcomeStatus,
    string? Message,
    ProposedShiftEffect ProposedShiftEffect,
    bool RequiresFollowup,
    bool IsClosable);
