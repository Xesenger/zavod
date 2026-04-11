using System.Linq;

namespace zavod.State;

public static class ShiftSnapshotBuilder
{
    public static ShiftSnapshot Build(ShiftState shiftState)
    {
        var currentTask = shiftState.Tasks.FirstOrDefault(task => task.TaskId == shiftState.CurrentTaskId);
        var summary = currentTask is null
            ? $"Shift {shiftState.ShiftId} is {shiftState.Status}."
            : $"Shift {shiftState.ShiftId} is {shiftState.Status}. Current task: {currentTask.TaskId} ({currentTask.Status}).";

        return new ShiftSnapshot(
            shiftState.ShiftId,
            summary,
            shiftState.AcceptedResults.TakeLast(3).ToArray(),
            shiftState.CurrentTaskId);
    }
}
