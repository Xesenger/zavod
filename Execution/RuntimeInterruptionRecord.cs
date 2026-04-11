using System;

namespace zavod.Execution;

public sealed record RuntimeInterruptionRecord(
    StopReason Reason,
    DateTimeOffset ObservedAt,
    bool GracefulStopAttempted,
    bool HardKillRequired,
    string Summary)
{
    public RuntimeInterruptionRecord Normalize()
    {
        return this with { Summary = Summary.Trim() };
    }

    public void Validate()
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(Summary);
    }
}
