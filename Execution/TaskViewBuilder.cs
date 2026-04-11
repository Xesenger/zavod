using System;

namespace zavod.Execution;

public static class TaskViewBuilder
{
    public static TaskView Build(RuntimeTaskState taskState)
    {
        ArgumentNullException.ThrowIfNull(taskState);

        var hasExecutionContext = taskState.HasExecutionContext;
        var hasShiftActivity = taskState.HasShiftActivity;
        var currentExecutionTarget = taskState.CurrentExecutionTarget;
        var currentOutcomeStatus = taskState.CurrentOutcomeStatus;
        var viewLine = hasExecutionContext
            ? $"Task view: {currentExecutionTarget} / {currentOutcomeStatus}. Shift activity: {hasShiftActivity}."
            : $"No task view context. Shift activity: {hasShiftActivity}.";

        return new TaskView(
            hasExecutionContext,
            hasShiftActivity,
            currentExecutionTarget,
            currentOutcomeStatus,
            viewLine);
    }
}
