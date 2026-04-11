using zavod.Entry;
using zavod.Outcome;

namespace zavod.Execution;

public sealed record RuntimeSnapshot(
    ExecutionTarget? LastExecutionTarget,
    ExecutionOutcomeStatus? LastOutcomeStatus,
    ProjectStateSaveStatus? LastSaveStatus,
    int ShiftRelevantEntriesCount,
    string? LastMessage);
