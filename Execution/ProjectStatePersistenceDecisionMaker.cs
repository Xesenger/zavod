using System;

namespace zavod.Execution;

public static class ProjectStatePersistenceDecisionMaker
{
    public static ProjectStatePersistenceDecision Decide(ProjectStateMutationResult mutationResult)
    {
        ArgumentNullException.ThrowIfNull(mutationResult);

        if (mutationResult.Status == ProjectStateMutationStatus.Mutated)
        {
            return new ProjectStatePersistenceDecision(
                mutationResult.State,
                mutationResult.Status,
                ProjectStatePersistenceDecisionStatus.Persist,
                ShouldPersist: true,
                "In-memory mutation changed project state and may proceed to persistence.");
        }

        return new ProjectStatePersistenceDecision(
            mutationResult.State,
            mutationResult.Status,
            ProjectStatePersistenceDecisionStatus.SkipPersist,
            ShouldPersist: false,
            "Project state did not change in memory, so persistence should be skipped.");
    }
}
