using System;
using zavod.Persistence;

namespace zavod.Execution;

public static class CoreCheckpointBundleBuilder
{
    public static CoreCheckpointBundle Build(
        ProjectState projectState,
        RuntimeSnapshot snapshot,
        RuntimeCapsule capsule,
        EntryPack entryPack,
        TaskProjectionBundle taskProjectionBundle)
    {
        ArgumentNullException.ThrowIfNull(projectState);
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(capsule);
        ArgumentNullException.ThrowIfNull(entryPack);
        ArgumentNullException.ThrowIfNull(taskProjectionBundle);

        if (entryPack.HasExecutionContext != taskProjectionBundle.HasExecutionContext)
        {
            throw new InvalidOperationException("Core checkpoint bundle requires consistent HasExecutionContext values.");
        }

        if (capsule.HasShiftActivity != entryPack.HasShiftActivity ||
            capsule.HasShiftActivity != taskProjectionBundle.HasShiftActivity)
        {
            throw new InvalidOperationException("Core checkpoint bundle requires consistent HasShiftActivity values.");
        }

        return new CoreCheckpointBundle(
            projectState,
            snapshot,
            capsule,
            entryPack,
            taskProjectionBundle,
            entryPack.HasExecutionContext,
            capsule.HasShiftActivity);
    }
}
