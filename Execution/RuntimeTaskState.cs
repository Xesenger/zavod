using zavod.Entry;
using zavod.Outcome;

namespace zavod.Execution;

public sealed record RuntimeTaskState(
    bool HasExecutionContext,
    bool HasShiftActivity,
    ExecutionTarget? CurrentExecutionTarget,
    ExecutionOutcomeStatus? CurrentOutcomeStatus,
    string TaskLine);
