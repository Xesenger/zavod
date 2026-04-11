using System;

namespace zavod.Execution;

public static class TaskProjectionBundleBuilder
{
    public static TaskProjectionBundle Build(RuntimeTaskState taskState, TaskView taskView)
    {
        ArgumentNullException.ThrowIfNull(taskState);
        ArgumentNullException.ThrowIfNull(taskView);

        if (taskState.HasExecutionContext != taskView.HasExecutionContext)
        {
            throw new InvalidOperationException("Task projection bundle requires consistent HasExecutionContext values.");
        }

        if (taskState.HasShiftActivity != taskView.HasShiftActivity)
        {
            throw new InvalidOperationException("Task projection bundle requires consistent HasShiftActivity values.");
        }

        return new TaskProjectionBundle(
            taskState,
            taskView,
            taskState.HasExecutionContext,
            taskState.HasShiftActivity);
    }
}
