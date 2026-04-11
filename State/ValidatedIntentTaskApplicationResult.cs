using zavod.Contexting;
using zavod.Persistence;

namespace zavod.State;

public sealed record ValidatedIntentTaskApplicationResult(
    ProjectState ProjectState,
    ShiftState ShiftState,
    TaskIntent Intent,
    TaskState Task);
