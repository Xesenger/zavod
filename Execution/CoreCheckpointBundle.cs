using zavod.Persistence;

namespace zavod.Execution;

public sealed record CoreCheckpointBundle(
    ProjectState ProjectState,
    RuntimeSnapshot Snapshot,
    RuntimeCapsule Capsule,
    EntryPack EntryPack,
    TaskProjectionBundle TaskProjectionBundle,
    bool HasExecutionContext,
    bool HasShiftActivity);
