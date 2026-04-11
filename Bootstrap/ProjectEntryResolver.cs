using System;

namespace zavod.Bootstrap;

public static class ProjectEntryResolver
{
    public static ProjectEntryResult Resolve(ProjectEntrySelection selection)
    {
        ArgumentNullException.ThrowIfNull(selection);

        return selection.Mode switch
        {
            ProjectEntryMode.Bootstrap => new ProjectEntryResult(
                selection.Mode,
                selection.ProjectState,
                null,
                null,
                IsBootstrapReady: true,
                ResumeResult: null),
            ProjectEntryMode.Resume => new ProjectEntryResult(
                selection.Mode,
                selection.ProjectState,
                selection.ResumeResult!.ShiftState,
                selection.ResumeResult.TaskState,
                IsBootstrapReady: false,
                selection.ResumeResult),
            _ => throw new InvalidOperationException("Unsupported canonical project entry mode.")
        };
    }

    public static ProjectEntryResult Resolve(zavod.Persistence.ProjectState projectState)
    {
        return Resolve(ProjectEntrySelector.Select(projectState));
    }
}
