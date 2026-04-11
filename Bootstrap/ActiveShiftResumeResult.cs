using zavod.Persistence;
using zavod.State;

namespace zavod.Bootstrap;

public sealed record ActiveShiftResumeResult(
    ProjectState ProjectState,
    ShiftState ShiftState,
    TaskState TaskState);
