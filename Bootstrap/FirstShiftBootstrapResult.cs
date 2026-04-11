using zavod.Contexting;
using zavod.Persistence;
using zavod.State;

namespace zavod.Bootstrap;

public sealed record FirstShiftBootstrapResult(
    ProjectState ProjectState,
    ShiftState ShiftState,
    TaskIntent? Intent,
    TaskState? Task,
    string ShiftFilePath);
