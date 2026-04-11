using System;

namespace zavod.Execution;

public static class ExecutionWatchdog
{
    public static ExecutionWatchdogSnapshot Start(ExecutionWatchdogPolicy? policy = null, DateTimeOffset? startedAt = null)
    {
        policy ??= ExecutionWatchdogPolicy.Default;
        policy.Validate();

        var snapshot = new ExecutionWatchdogSnapshot(
            policy.Normalize(),
            startedAt ?? DateTimeOffset.UtcNow,
            LastHeartbeat: null,
            LastProgress: null,
            Interruption: null,
            "Execution watchdog is active.");

        snapshot.Validate();
        return snapshot.Normalize();
    }

    public static ExecutionWatchdogSnapshot RecordHeartbeat(
        ExecutionWatchdogSnapshot snapshot,
        RuntimeHeartbeat heartbeat)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(heartbeat);

        snapshot.Validate();
        heartbeat.Validate();

        return BuildSnapshot(
            snapshot,
            heartbeat.Normalize(),
            snapshot.LastProgress,
            snapshot.Interruption,
            $"Execution watchdog heartbeat updated at {heartbeat.ObservedAt:O}.");
    }

    public static ExecutionWatchdogSnapshot RecordProgress(
        ExecutionWatchdogSnapshot snapshot,
        RuntimeProgressSignal progress)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(progress);

        snapshot.Validate();
        progress.Validate();

        return BuildSnapshot(
            snapshot,
            snapshot.LastHeartbeat,
            progress.Normalize(),
            snapshot.Interruption,
            $"Execution watchdog progress updated at {progress.ObservedAt:O}.");
    }

    public static RuntimeInterruptionRecord? Evaluate(
        ExecutionWatchdogSnapshot snapshot,
        DateTimeOffset now,
        bool policyViolationObserved = false)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        snapshot.Validate();

        if (policyViolationObserved)
        {
            return BuildInterruption(snapshot.Policy, StopReason.PolicyViolation, now);
        }

        if (now - snapshot.StartedAt > snapshot.Policy.MaxRuntime)
        {
            return BuildInterruption(snapshot.Policy, StopReason.TimeoutExceeded, now);
        }

        if (snapshot.LastHeartbeat is not null && now - snapshot.LastHeartbeat.ObservedAt > snapshot.Policy.MaxHeartbeatGap)
        {
            return BuildInterruption(snapshot.Policy, StopReason.HeartbeatLost, now);
        }

        var progressAnchor = snapshot.LastProgress?.ObservedAt ?? snapshot.StartedAt;
        if (now - progressAnchor > snapshot.Policy.MaxNoProgressGap)
        {
            return BuildInterruption(snapshot.Policy, StopReason.NoProgressObserved, now);
        }

        return null;
    }

    public static ExecutionWatchdogSnapshot Interrupt(
        ExecutionWatchdogSnapshot snapshot,
        RuntimeInterruptionRecord interruption)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(interruption);

        snapshot.Validate();
        interruption.Validate();

        return BuildSnapshot(
            snapshot,
            snapshot.LastHeartbeat,
            snapshot.LastProgress,
            interruption.Normalize(),
            interruption.Summary);
    }

    private static RuntimeInterruptionRecord BuildInterruption(
        ExecutionWatchdogPolicy policy,
        StopReason reason,
        DateTimeOffset now)
    {
        var interruption = new RuntimeInterruptionRecord(
            reason,
            now,
            GracefulStopAttempted: policy.GracefulStopBeforeKill,
            HardKillRequired: reason is StopReason.TimeoutExceeded or StopReason.HeartbeatLost or StopReason.PolicyViolation,
            $"Execution watchdog interruption: reason={reason}, gracefulStop={policy.GracefulStopBeforeKill}.");

        interruption.Validate();
        return interruption.Normalize();
    }

    private static ExecutionWatchdogSnapshot BuildSnapshot(
        ExecutionWatchdogSnapshot snapshot,
        RuntimeHeartbeat? heartbeat,
        RuntimeProgressSignal? progress,
        RuntimeInterruptionRecord? interruption,
        string summary)
    {
        var updated = new ExecutionWatchdogSnapshot(
            snapshot.Policy.Normalize(),
            snapshot.StartedAt,
            heartbeat,
            progress,
            interruption,
            summary.Trim());

        updated.Validate();
        return updated.Normalize();
    }
}
