using System;
using System.Linq;
using zavod.Persistence;
using zavod.State;

namespace zavod.Bootstrap;

public static class ActiveShiftResume
{
    public static ActiveShiftResumeResult Resume(ProjectState projectState)
    {
        ArgumentNullException.ThrowIfNull(projectState);

        if (string.IsNullOrWhiteSpace(projectState.ActiveShiftId))
        {
            throw new InvalidOperationException("Canonical resume requires active shift in project state.");
        }

        if (string.IsNullOrWhiteSpace(projectState.ActiveTaskId))
        {
            throw new InvalidOperationException("Canonical resume requires active task in project state.");
        }

        var shiftState = ShiftStateStorage.Load(projectState.Paths.ProjectRoot, projectState.ActiveShiftId);

        if (!string.Equals(shiftState.ShiftId, projectState.ActiveShiftId, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Canonical resume requires persisted shift truth to match project active shift.");
        }

        if (shiftState.Status != ShiftStateStatus.Active)
        {
            throw new InvalidOperationException("Canonical resume requires active shift truth.");
        }

        if (!string.Equals(shiftState.CurrentTaskId, projectState.ActiveTaskId, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Canonical resume requires project active task to match shift current task.");
        }

        var taskState = shiftState.Tasks.SingleOrDefault(task => string.Equals(task.TaskId, projectState.ActiveTaskId, StringComparison.Ordinal));
        if (taskState is null)
        {
            throw new InvalidOperationException("Canonical resume requires active task to exist in shift truth.");
        }

        return new ActiveShiftResumeResult(projectState, shiftState, taskState);
    }
}
