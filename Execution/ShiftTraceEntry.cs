using zavod.Entry;
using zavod.Outcome;

namespace zavod.Execution;

public sealed record ShiftTraceEntry(
    ExecutionTarget Target,
    ExecutionOutcomeStatus OutcomeStatus,
    ProjectStateSaveStatus SaveStatus,
    bool IsShiftRelevant,
    string? Message);
