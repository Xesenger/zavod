using System;
using zavod.Outcome;
using zavod.Persistence;

namespace zavod.Execution;

public static class ProjectStateMutator
{
    public static ProjectStateMutationResult Mutate(ProjectState currentState, ShiftUpdateDecision decision)
    {
        ArgumentNullException.ThrowIfNull(currentState);
        ArgumentNullException.ThrowIfNull(decision);

        return new ProjectStateMutationResult(
            currentState,
            ProjectStateMutationStatus.Unchanged,
            "Execution layer cannot mutate canonical ProjectState directly. Closure-path is required.");
    }
}
