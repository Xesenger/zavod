using zavod.State;
using zavod.Acceptance;

namespace zavod.Execution;

public sealed record ExecutionRuntimeState(
    ExecutionSession Session,
    TaskState Task,
    ShiftState Shift,
    RuntimeSelectionDecision RuntimeSelection,
    RuntimeProfile RuntimeProfile,
    ExecutionWatchdogSnapshot Watchdog,
    System.Collections.Generic.IReadOnlyList<WorkerExecutionResult> ResultHistory,
    System.Collections.Generic.IReadOnlyList<ExecutionAttempt> Attempts,
    int CurrentAttemptIndex,
    WorkerExecutionResult? Result,
    QCReviewStatus QcStatus,
    QCReviewResult? Review,
    string? LastQcRejectReason,
    ExecutionRunResult RunResult,
    AcceptanceEvaluation? AcceptanceEvaluation,
    ExecutionClosureCandidate ClosureCandidate,
    ShiftClosureProposal ClosureProposal);
