using System;
using zavod.Persistence;
using zavod.Prompting;

namespace zavod.State;

public static class ResultAbandonProcessor
{
    public static ResultAbandonResult Abandon(
        ProjectState projectState,
        ShiftState shiftState,
        TaskState taskState,
        DateTimeOffset timestamp)
    {
        ArgumentNullException.ThrowIfNull(projectState);
        ArgumentNullException.ThrowIfNull(shiftState);
        ArgumentNullException.ThrowIfNull(taskState);

        if (!string.Equals(projectState.ActiveShiftId, shiftState.ShiftId, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Result abandon requires active project shift to match target shift.");
        }

        if (!string.Equals(projectState.ActiveTaskId, taskState.TaskId, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Result abandon requires active project task to match target task.");
        }

        if (shiftState.Status != ShiftStateStatus.Active)
        {
            throw new InvalidOperationException("Result abandon requires active shift truth.");
        }

        if (!string.Equals(shiftState.CurrentTaskId, taskState.TaskId, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Result abandon requires task bound as current shift task.");
        }

        var abandonedTask = taskState.Abandon(PromptRole.ShiftLead, timestamp);
        var finalizedShift = shiftState.UpdateTask(abandonedTask) with { CurrentTaskId = null };

        var updatedProjectState = projectState with
        {
            ActiveShiftId = shiftState.ShiftId,
            ActiveTaskId = null
        };

        var persistedProjectState = ProjectStateStorage.Save(updatedProjectState);
        var shiftFilePath = ShiftStateStorage.Save(persistedProjectState.Paths.ProjectRoot, finalizedShift);

        return new ResultAbandonResult(
            persistedProjectState,
            finalizedShift,
            abandonedTask,
            shiftFilePath);
    }
}

public sealed record ResultAbandonResult(
    ProjectState ProjectState,
    ShiftState ShiftState,
    TaskState TaskState,
    string ShiftFilePath);
