using zavod.Entry;
using zavod.Outcome;

namespace zavod.Execution;

public sealed record TaskView(
    bool HasExecutionContext,
    bool HasShiftActivity,
    ExecutionTarget? CurrentExecutionTarget,
    ExecutionOutcomeStatus? CurrentOutcomeStatus,
    string ViewLine);
