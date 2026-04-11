using System;

namespace zavod.Contexting;

public static class TaskIntentFactory
{
    public static TaskIntent CreateCandidate(string description, string intentId = "INTENT-001")
    {
        if (string.IsNullOrWhiteSpace(description))
        {
            throw new InvalidOperationException("Task intent requires non-empty description.");
        }

        if (string.IsNullOrWhiteSpace(intentId))
        {
            throw new InvalidOperationException("Task intent requires non-empty id.");
        }

        return new TaskIntent(intentId.Trim(), description.Trim(), ContextIntentState.Candidate);
    }
}
