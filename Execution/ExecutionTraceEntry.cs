using zavod.Entry;
using zavod.Outcome;

namespace zavod.Execution;

public sealed record ExecutionTraceEntry(
    ExecutionTarget Target,
    ExecutionOutcomeStatus OutcomeStatus,
    ProjectStateMutationStatus MutationStatus,
    ProjectStateSaveStatus SaveStatus,
    string? Message);
