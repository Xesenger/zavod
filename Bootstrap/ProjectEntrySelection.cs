using zavod.Persistence;

namespace zavod.Bootstrap;

public sealed record ProjectEntrySelection(
    ProjectEntryMode Mode,
    ProjectState ProjectState,
    ActiveShiftResumeResult? ResumeResult);
