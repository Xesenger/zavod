using System;
using System.Linq;
using zavod.Persistence;
using zavod.State;

namespace zavod.Execution;

public static class TaskExecutionContextBuilder
{
    public static TaskExecutionContext Build(ProjectState projectState, ShiftState shiftState)
    {
        ArgumentNullException.ThrowIfNull(projectState);
        ArgumentNullException.ThrowIfNull(shiftState);

        if (string.IsNullOrWhiteSpace(projectState.ActiveShiftId))
        {
            throw new InvalidOperationException("Active project state is required for task-bound execution.");
        }

        if (!string.Equals(projectState.ActiveShiftId, shiftState.ShiftId, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("ProjectState active shift must match ShiftState.");
        }

        if (string.IsNullOrWhiteSpace(projectState.ActiveTaskId))
        {
            throw new InvalidOperationException("ProjectState active task is required for task-bound execution.");
        }

        if (string.IsNullOrWhiteSpace(shiftState.CurrentTaskId))
        {
            throw new InvalidOperationException("ShiftState current task is required for task-bound execution.");
        }

        if (!string.Equals(projectState.ActiveTaskId, shiftState.CurrentTaskId, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("ProjectState active task must match ShiftState current task.");
        }

        var task = shiftState.Tasks.FirstOrDefault(candidate => candidate.TaskId == shiftState.CurrentTaskId);
        if (task is null)
        {
            throw new InvalidOperationException("ShiftState current task must exist in shift truth.");
        }

        return new TaskExecutionContext(shiftState.ShiftId, task.TaskId);
    }
}
