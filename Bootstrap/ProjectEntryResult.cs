using zavod.Persistence;
using zavod.State;

namespace zavod.Bootstrap;

public sealed record ProjectEntryResult(
    ProjectEntryMode Mode,
    ProjectState ProjectState,
    ShiftState? ShiftState,
    TaskState? TaskState,
    bool IsBootstrapReady,
    ActiveShiftResumeResult? ResumeResult);
