using System;

namespace zavod.Execution;

public static class TaskStateBuilder
{
    public static RuntimeTaskState Build(EntryPack entryPack)
    {
        ArgumentNullException.ThrowIfNull(entryPack);

        var hasExecutionContext = entryPack.HasExecutionContext;
        var hasShiftActivity = entryPack.HasShiftActivity;
        var currentExecutionTarget = entryPack.Capsule.CurrentExecutionTarget;
        var currentOutcomeStatus = entryPack.Capsule.CurrentOutcomeStatus;
        var taskLine = hasExecutionContext
            ? $"Task context: {currentExecutionTarget} / {currentOutcomeStatus}. Shift activity: {hasShiftActivity}."
            : $"No task execution context. Shift activity: {hasShiftActivity}.";

        return new RuntimeTaskState(
            hasExecutionContext,
            hasShiftActivity,
            currentExecutionTarget,
            currentOutcomeStatus,
            taskLine);
    }
}
