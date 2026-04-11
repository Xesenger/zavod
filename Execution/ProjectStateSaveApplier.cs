using System;
using zavod.Persistence;

namespace zavod.Execution;

public static class ProjectStateSaveApplier
{
    public static ProjectStateSaveResult Apply(ProjectStatePersistenceDecision decision)
    {
        ArgumentNullException.ThrowIfNull(decision);

        if (decision.ShouldPersist)
        {
            throw new InvalidOperationException(
                "Canonical ProjectState persistence is allowed only from ShiftClosureProcessor closure-path.");
        }

        if (!decision.ShouldPersist)
        {
            return new ProjectStateSaveResult(
                decision.State,
                ProjectStateSaveStatus.Skipped,
                WasPersisted: false,
                "Execution-side persistence seam is blocked. Canonical closure-path must decide and persist truth.");
        }

        throw new InvalidOperationException("Unreachable execution path in ProjectStateSaveApplier.");
    }
}
