using zavod.Entry;
using zavod.Outcome;

namespace zavod.Execution;

public sealed record ExecutionClosureCandidate(
    ExecutionTarget Target,
    ExecutionOutcomeStatus OutcomeStatus,
    string? Message,
    bool IsClosable,
    bool RequiresFollowup,
    bool IsRejected);
