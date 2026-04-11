using zavod.Entry;
using zavod.Outcome;

namespace zavod.Execution;

public sealed record RuntimeCapsule(
    ExecutionTarget? CurrentExecutionTarget,
    ExecutionOutcomeStatus? CurrentOutcomeStatus,
    ProjectStateSaveStatus? CurrentSaveStatus,
    bool HasShiftActivity,
    string SummaryLine);
