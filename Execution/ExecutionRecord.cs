using zavod.Entry;
using zavod.Outcome;

namespace zavod.Execution;

public sealed record ExecutionRecord(
    string ShiftId,
    string TaskId,
    ExecutionTarget Target,
    ExecutionOutcomeStatus OutcomeStatus,
    string? Message = null,
    RuntimeProfile? RuntimeProfile = null,
    RuntimeInterruptionRecord? RuntimeInterruption = null)
{
    public RuntimeProfile EffectiveRuntimeProfile => RuntimeProfile ?? RuntimeProfileDefaults.ScopedLocalDefault;
}
