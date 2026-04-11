using zavod.Persistence;
using zavod.Traceing;

namespace zavod.State;

public sealed record ShiftClosureResult(
    ShiftClosureStatus Status,
    TaskState Task,
    ShiftState Shift,
    ProjectState ProjectState,
    Snapshot? Snapshot,
    string? ShiftFilePath,
    string? SnapshotFilePath,
    string Reason);
