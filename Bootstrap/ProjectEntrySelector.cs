using System;
using zavod.Persistence;

namespace zavod.Bootstrap;

public static class ProjectEntrySelector
{
    public static ProjectEntrySelection Select(ProjectState projectState)
    {
        ArgumentNullException.ThrowIfNull(projectState);

        if (projectState.ActiveShiftId is null)
        {
            if (projectState.ActiveTaskId is not null)
            {
                throw new InvalidOperationException("Canonical project entry requires empty active task when no active shift exists.");
            }

            return new ProjectEntrySelection(ProjectEntryMode.Bootstrap, projectState, null);
        }

        // Shift is open but no active task — valid between-tasks state produced by
        // AcceptedResultApplyProcessor/ResultAbandonProcessor (task cleared, shift
        // kept open for the next intake). Resume requires both, so fall back to
        // Bootstrap semantics for entry resolution — downstream code that actually
        // needs shift+task will call ActiveShiftResume.Resume directly after
        // MaterializeValidatedIntent repopulates ActiveTaskId.
        if (projectState.ActiveTaskId is null)
        {
            return new ProjectEntrySelection(ProjectEntryMode.Bootstrap, projectState, null);
        }

        var resumeResult = ActiveShiftResume.Resume(projectState);
        return new ProjectEntrySelection(ProjectEntryMode.Resume, projectState, resumeResult);
    }
}
