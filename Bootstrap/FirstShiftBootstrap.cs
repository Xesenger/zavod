using System;
using System.IO;
using zavod.Contexting;
using zavod.Persistence;
using zavod.Prompting;
using zavod.State;

namespace zavod.Bootstrap;

public static class FirstShiftBootstrap
{
    public static FirstShiftBootstrapResult Create(ProjectState projectState, FirstShiftBootstrapRequest request)
    {
        ArgumentNullException.ThrowIfNull(projectState);
        ArgumentNullException.ThrowIfNull(request);

        if (string.IsNullOrWhiteSpace(request.ShiftGoal))
        {
            throw new InvalidOperationException("First shift bootstrap requires non-empty shift goal.");
        }

        if (projectState.ActiveShiftId is not null)
        {
            throw new InvalidOperationException("First shift bootstrap requires project without active shift.");
        }

        var existingShiftFiles = Directory.GetFiles(Path.Combine(projectState.Paths.ZavodRoot, "shifts"), "*.json");
        if (existingShiftFiles.Length > 0)
        {
            throw new InvalidOperationException("First shift bootstrap requires empty shift history.");
        }

        var shift = new ShiftState(
            "SHIFT-001",
            request.ShiftGoal.Trim(),
            null,
            ShiftStateStatus.Active,
            Array.Empty<TaskState>(),
            Array.Empty<string>(),
            Array.Empty<string>(),
            Array.Empty<string>());

        var intent = CreateInitialIntent(request);
        var applied = ApplyInitialTask(projectState, shift, intent, request.Timestamp);
        var task = applied?.Task;
        shift = applied?.ShiftState ?? shift;

        var updatedProjectState = ProjectStateStorage.Save(applied?.ProjectState ?? (projectState with
        {
            ActiveShiftId = shift.ShiftId,
            ActiveTaskId = null
        }));

        var shiftFilePath = ShiftStateStorage.Save(updatedProjectState.Paths.ProjectRoot, shift);
        return new FirstShiftBootstrapResult(updatedProjectState, shift, intent, task, shiftFilePath);
    }

    private static TaskIntent? CreateInitialIntent(FirstShiftBootstrapRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.InitialTaskDescription))
        {
            return null;
        }

        return TaskIntentFactory
            .CreateCandidate(request.InitialTaskDescription)
            .MarkReadyForValidation()
            .Validate();
    }

    private static ValidatedIntentTaskApplicationResult? ApplyInitialTask(
        ProjectState projectState,
        ShiftState shiftState,
        TaskIntent? intent,
        DateTimeOffset timestamp)
    {
        if (intent is null)
        {
            return null;
        }

        return ValidatedIntentTaskApplier.Apply(
            projectState,
            shiftState,
            intent,
            "TASK-001",
            timestamp);
    }
}
