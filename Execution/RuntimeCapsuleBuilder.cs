using System;

namespace zavod.Execution;

public static class RuntimeCapsuleBuilder
{
    public static RuntimeCapsule Build(RuntimeSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        var hasShiftActivity = snapshot.ShiftRelevantEntriesCount > 0;
        var summaryLine = hasShiftActivity
            ? $"Last shift activity: {snapshot.LastExecutionTarget} / {snapshot.LastOutcomeStatus} / {snapshot.LastSaveStatus}."
            : "No shift activity.";

        return new RuntimeCapsule(
            snapshot.LastExecutionTarget,
            snapshot.LastOutcomeStatus,
            snapshot.LastSaveStatus,
            hasShiftActivity,
            summaryLine);
    }
}
