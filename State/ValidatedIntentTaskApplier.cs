using System;
using System.Collections.Generic;
using System.Linq;
using zavod.Contexting;
using zavod.Persistence;
using zavod.Prompting;

namespace zavod.State;

public static class ValidatedIntentTaskApplier
{
    public static ValidatedIntentTaskApplicationResult Apply(
        ProjectState projectState,
        ShiftState shiftState,
        TaskIntent intent,
        string taskId,
        DateTimeOffset timestamp,
        PromptRole createdByRole = PromptRole.ShiftLead,
        PromptRole assignedRole = PromptRole.Worker,
        IReadOnlyList<string>? scope = null,
        IReadOnlyList<string>? acceptanceCriteria = null)
    {
        ArgumentNullException.ThrowIfNull(projectState);
        ArgumentNullException.ThrowIfNull(shiftState);
        ArgumentNullException.ThrowIfNull(intent);

        if (intent.Status != ContextIntentState.Validated)
        {
            throw new InvalidOperationException("Canonical task application requires validated intent.");
        }

        if (projectState.ActiveShiftId is not null &&
            !string.Equals(projectState.ActiveShiftId, shiftState.ShiftId, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("ProjectState active shift must match target shift for canonical task application.");
        }

        if (projectState.ActiveTaskId is not null)
        {
            throw new InvalidOperationException("Canonical task application requires empty project task slot.");
        }

        if (shiftState.CurrentTaskId is not null)
        {
            throw new InvalidOperationException("Canonical task application requires empty shift task slot.");
        }

        if (shiftState.Tasks.Any(task => task.TaskId == taskId))
        {
            throw new InvalidOperationException("Canonical task application cannot reuse existing task id in shift truth.");
        }

        var task = TaskStateFactory.CreateFromValidatedIntent(intent, taskId, createdByRole, assignedRole, timestamp, scope, acceptanceCriteria);
        var shiftWithTask = shiftState with
        {
            Tasks = shiftState.Tasks.Concat(new[] { task }).ToArray()
        };
        shiftWithTask = shiftWithTask.AssignTask(PromptRole.ShiftLead, task.TaskId);

        var updatedProjectState = projectState with
        {
            ActiveShiftId = shiftState.ShiftId,
            ActiveTaskId = task.TaskId
        };

        return new ValidatedIntentTaskApplicationResult(updatedProjectState, shiftWithTask, intent, task);
    }
}
