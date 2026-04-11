using System;
using zavod.Contexting;
using zavod.Prompting;

namespace zavod.State;

public static class TaskStateFactory
{
    public static TaskState CreateFromValidatedIntent(
        TaskIntent intent,
        string taskId,
        PromptRole createdByRole,
        PromptRole assignedRole,
        DateTimeOffset timestamp)
    {
        ArgumentNullException.ThrowIfNull(intent);

        if (intent.Status != ContextIntentState.Validated)
        {
            throw new InvalidOperationException("Canonical task creation requires validated intent.");
        }

        if (string.IsNullOrWhiteSpace(taskId))
        {
            throw new InvalidOperationException("Canonical task creation requires non-empty task id.");
        }

        return new TaskState(
            taskId.Trim(),
            intent.Status,
            TaskStateStatus.Active,
            intent.Description,
            Array.Empty<string>(),
            Array.Empty<string>(),
            createdByRole,
            assignedRole,
            timestamp);
    }
}
