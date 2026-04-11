namespace zavod.Execution;

public sealed record EntryPack(
    RuntimeCapsule Capsule,
    RuntimeSnapshot Snapshot,
    bool HasExecutionContext,
    bool HasShiftActivity,
    string EntryLine);
