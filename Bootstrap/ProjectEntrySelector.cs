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

        var resumeResult = ActiveShiftResume.Resume(projectState);
        return new ProjectEntrySelection(ProjectEntryMode.Resume, projectState, resumeResult);
    }
}
