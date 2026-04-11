using System;

namespace zavod.Execution;

public static class EntryPackBuilder
{
    public static EntryPack Build(RuntimeCapsule capsule, RuntimeSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(capsule);
        ArgumentNullException.ThrowIfNull(snapshot);

        var hasExecutionContext =
            capsule.CurrentExecutionTarget is not null &&
            capsule.CurrentOutcomeStatus is not null;
        var hasShiftActivity = capsule.HasShiftActivity;
        var entryLine = hasExecutionContext
            ? $"Execution context: {capsule.CurrentExecutionTarget} / {capsule.CurrentOutcomeStatus}. Shift activity: {hasShiftActivity}."
            : $"No execution context. Shift activity: {hasShiftActivity}.";

        return new EntryPack(
            capsule,
            snapshot,
            hasExecutionContext,
            hasShiftActivity,
            entryLine);
    }
}
