using System;

namespace zavod.Execution;

public sealed record ExecutionWatchdogPolicy(
    TimeSpan MaxRuntime,
    TimeSpan MaxHeartbeatGap,
    TimeSpan MaxNoProgressGap,
    bool GracefulStopBeforeKill,
    string Summary)
{
    public static ExecutionWatchdogPolicy Default { get; } = new(
        MaxRuntime: TimeSpan.FromMinutes(10),
        MaxHeartbeatGap: TimeSpan.FromMinutes(2),
        MaxNoProgressGap: TimeSpan.FromMinutes(3),
        GracefulStopBeforeKill: true,
        "Default execution watchdog policy for bounded host-supervised runtime.");

    public ExecutionWatchdogPolicy Normalize()
    {
        return this with { Summary = Summary.Trim() };
    }

    public void Validate()
    {
        if (MaxRuntime <= TimeSpan.Zero)
        {
            throw new InvalidOperationException("Watchdog max runtime must be positive.");
        }

        if (MaxHeartbeatGap <= TimeSpan.Zero)
        {
            throw new InvalidOperationException("Watchdog heartbeat gap must be positive.");
        }

        if (MaxNoProgressGap <= TimeSpan.Zero)
        {
            throw new InvalidOperationException("Watchdog no-progress gap must be positive.");
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(Summary);
    }
}
