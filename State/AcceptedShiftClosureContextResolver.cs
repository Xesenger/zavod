using System;
using System.Linq;
using zavod.Persistence;

namespace zavod.State;

public sealed record AcceptedShiftClosureContext(
    ProjectState ProjectState,
    ShiftState ShiftState,
    TaskState FinalizedTask);

public static class AcceptedShiftClosureContextResolver
{
    public static bool TryResolve(
        ProjectState projectState,
        ShiftState? liveShiftState,
        Func<string, ShiftState> shiftLoader,
        out AcceptedShiftClosureContext? context)
    {
        ArgumentNullException.ThrowIfNull(projectState);
        ArgumentNullException.ThrowIfNull(shiftLoader);

        context = null;

        if (projectState.ActiveShiftId is null || projectState.ActiveTaskId is not null)
        {
            return false;
        }

        var shiftState = ResolveShift(projectState.ActiveShiftId, liveShiftState, shiftLoader);
        if (shiftState is null
            || shiftState.Status != ShiftStateStatus.Active
            || shiftState.CurrentTaskId is not null)
        {
            return false;
        }

        var finalizedTask = shiftState.Tasks.LastOrDefault(static task => task.Status is TaskStateStatus.Completed or TaskStateStatus.Abandoned);
        if (finalizedTask is null)
        {
            return false;
        }

        context = new AcceptedShiftClosureContext(projectState, shiftState, finalizedTask);
        return true;
    }

    private static ShiftState? ResolveShift(
        string activeShiftId,
        ShiftState? liveShiftState,
        Func<string, ShiftState> shiftLoader)
    {
        if (liveShiftState is not null
            && string.Equals(liveShiftState.ShiftId, activeShiftId, StringComparison.Ordinal))
        {
            return liveShiftState;
        }

        return shiftLoader(activeShiftId);
    }
}
