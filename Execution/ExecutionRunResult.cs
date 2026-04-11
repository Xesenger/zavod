using zavod.Outcome;

namespace zavod.Execution;

public sealed record ExecutionRunResult(
    ExecutionOutcome Outcome,
    ExecutionRecord Record,
    RuntimeProfile? RuntimeProfile = null)
{
    public RuntimeProfile EffectiveRuntimeProfile =>
        RuntimeProfile ?? Record.RuntimeProfile ?? RuntimeProfileDefaults.ScopedLocalDefault;

    public RuntimeInterruptionRecord? RuntimeInterruption =>
        Record.RuntimeInterruption;
}
