namespace zavod.Execution;

public sealed record TaskProjectionBundle(
    RuntimeTaskState TaskState,
    TaskView TaskView,
    bool HasExecutionContext,
    bool HasShiftActivity);
