namespace zavod.Execution;

public enum ExecutionSessionState
{
    Initialized,
    TaskPrepared,
    InProgress,
    ResultProduced,
    UnderReview,
    ReturnedForRevision,
    Completed,
    Failed
}
