using System;

namespace zavod.Execution;

public sealed record ExecutionWatchdogSnapshot(
    ExecutionWatchdogPolicy Policy,
    DateTimeOffset StartedAt,
    RuntimeHeartbeat? LastHeartbeat,
    RuntimeProgressSignal? LastProgress,
    RuntimeInterruptionRecord? Interruption,
    string Summary)
{
    public ExecutionWatchdogSnapshot Normalize()
    {
        Policy.Validate();
        return this with
        {
            Policy = Policy.Normalize(),
            LastHeartbeat = LastHeartbeat?.Normalize(),
            LastProgress = LastProgress?.Normalize(),
            Interruption = Interruption?.Normalize(),
            Summary = Summary.Trim()
        };
    }

    public void Validate()
    {
        Policy.Validate();
        LastHeartbeat?.Validate();
        LastProgress?.Validate();
        Interruption?.Validate();
        ArgumentException.ThrowIfNullOrWhiteSpace(Summary);
    }
}
