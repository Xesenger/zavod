using System;
using System.Collections.Generic;
using System.Linq;
using zavod.Acceptance;
using zavod.Entry;
using zavod.Outcome;
using zavod.State;
using zavod.Tooling;

namespace zavod.Execution;

public static class ExecutionRuntimeController
{
    public static ExecutionRuntimeState Begin(TaskState task, ShiftState shift)
    {
        return Begin(task, shift, RuntimeSelectionRequestBuilder.BuildDefault());
    }

    public static ExecutionRuntimeState Begin(TaskState task, ShiftState shift, RuntimeSelectionRequest selectionRequest)
    {
        ArgumentNullException.ThrowIfNull(selectionRequest);

        var decision = RuntimeSelectionPolicy.Select(selectionRequest);
        if (!decision.IsAllowed)
        {
            throw new InvalidOperationException($"Execution runtime cannot begin: {decision.Reason}");
        }

        return Begin(task, shift, decision);
    }

    public static ExecutionRuntimeState Begin(TaskState task, ShiftState shift, RuntimeProfile runtimeProfile)
    {
        return Begin(
            task,
            shift,
            new RuntimeSelectionDecision(
                runtimeProfile.Normalize(),
                IsAllowed: true,
                "Runtime profile was provided explicitly to execution runtime."));
    }

    public static ExecutionRuntimeState Begin(TaskState task, ShiftState shift, RuntimeSelectionDecision runtimeSelection)
    {
        ArgumentNullException.ThrowIfNull(task);
        ArgumentNullException.ThrowIfNull(shift);
        ArgumentNullException.ThrowIfNull(runtimeSelection);

        if (!runtimeSelection.IsAllowed)
        {
            throw new InvalidOperationException($"Execution runtime cannot begin: {runtimeSelection.Reason}");
        }

        var runtimeProfile = runtimeSelection.Profile;
        runtimeProfile.Validate();
        runtimeProfile = runtimeProfile.Normalize();
        runtimeSelection = runtimeSelection with { Profile = runtimeProfile };

        if (task.Status != TaskStateStatus.Active)
        {
            throw new InvalidOperationException("Execution runtime can begin only for active task truth.");
        }

        if (shift.Status != ShiftStateStatus.Active)
        {
            throw new InvalidOperationException("Execution runtime can begin only for active shift truth.");
        }

        if (!string.Equals(shift.CurrentTaskId, task.TaskId, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Execution runtime requires task bound as current shift task.");
        }

        var session = new ExecutionSession(
            $"SESSION-{task.TaskId}",
            Prompting.PromptRole.Worker,
            task.TaskId,
            shift.ShiftId,
            ExecutionSessionState.InProgress);
        var watchdog = ExecutionWatchdog.Start();

        return BuildState(
            session: session,
            task: task,
            shift: shift,
            runtimeSelection: runtimeSelection,
            runtimeProfile: runtimeProfile,
            watchdog: watchdog,
            resultHistory: Array.Empty<WorkerExecutionResult>(),
            attempts: Array.Empty<ExecutionAttempt>(),
            currentAttemptIndex: 0,
            result: null,
            qcStatus: QCReviewStatus.NotStarted,
            review: null,
            lastQcRejectReason: null,
            target: ExecutionTarget.ActiveShiftSubsystem,
            status: ExecutionOutcomeStatus.Deferred,
            message: "Execution session is active. QC review is not ready yet.");
    }

    public static ExecutionRuntimeState ProduceResult(ExecutionRuntimeState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        if (state.Session.State != ExecutionSessionState.InProgress)
        {
            throw new InvalidOperationException("Producing execution result requires InProgress runtime state.");
        }

        var revisionNumber = state.ResultHistory.Count + 1;
        var result = new WorkerExecutionResult(
            $"RESULT-{state.Task.TaskId}-{revisionNumber:D3}",
            state.Task.TaskId,
            WorkerExecutionStatus.Success,
            $"Исполнение по задаче подготовлено: {state.Task.Description}",
            Array.Empty<IntakeArtifact>(),
            Array.Empty<WorkerExecutionModification>(),
            Array.Empty<ToolWarning>());

        return ProduceProvidedResult(state, result);
    }

    public static ExecutionRuntimeState ProduceProvidedResult(ExecutionRuntimeState state, WorkerExecutionResult result)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(result);

        if (state.Session.State != ExecutionSessionState.InProgress)
        {
            throw new InvalidOperationException("Producing execution result requires InProgress runtime state.");
        }

        if (!string.Equals(state.Task.TaskId, result.TaskId, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Provided execution result must stay bound to the active task.");
        }

        var revisionNumber = state.ResultHistory.Count + 1;
        var session = ExecutionCoordinator.SubmitResult(state.Session, result);
        var attempts = state.Attempts.Concat(new[]
        {
            new ExecutionAttempt(
                revisionNumber,
                ExecutionOutcomeStatus.Deferred,
                QCReviewStatus.NotStarted,
                RejectReason: null,
                CreatedAt: DateTime.UtcNow)
        }).ToArray();
        return BuildState(
            session: session,
            task: state.Task,
            shift: state.Shift,
            runtimeSelection: state.RuntimeSelection,
            runtimeProfile: state.RuntimeProfile,
            watchdog: ExecutionWatchdog.RecordProgress(
                ExecutionWatchdog.RecordHeartbeat(
                    state.Watchdog,
                    new RuntimeHeartbeat(DateTimeOffset.UtcNow, "worker_result", "Worker result submitted.")),
                new RuntimeProgressSignal(DateTimeOffset.UtcNow, "worker_result", "Worker produced execution result.")),
            resultHistory: state.ResultHistory.Concat(new[] { result }).ToArray(),
            attempts: attempts,
            currentAttemptIndex: revisionNumber,
            result: result,
            qcStatus: QCReviewStatus.NotStarted,
            review: null,
            lastQcRejectReason: null,
            target: ExecutionTarget.ActiveShiftSubsystem,
            status: ExecutionOutcomeStatus.Deferred,
            message: "Execution result produced. QC review is still required before closure.");
    }

    public static ExecutionRuntimeState RequestQcReview(ExecutionRuntimeState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        if (state.Result is null)
        {
            throw new InvalidOperationException("QC review requires produced execution result.");
        }

        var session = ExecutionCoordinator.RequestReview(state.Session, state.Result);
        return BuildState(
            session: session,
            task: state.Task,
            shift: state.Shift,
            runtimeSelection: state.RuntimeSelection,
            runtimeProfile: state.RuntimeProfile,
            watchdog: state.Watchdog,
            resultHistory: state.ResultHistory,
            attempts: UpdateCurrentAttempt(state, attempt => attempt with { QcStatus = QCReviewStatus.PendingReview }),
            currentAttemptIndex: state.CurrentAttemptIndex,
            result: state.Result,
            qcStatus: QCReviewStatus.PendingReview,
            review: null,
            lastQcRejectReason: state.LastQcRejectReason,
            target: ExecutionTarget.ActiveShiftSubsystem,
            status: ExecutionOutcomeStatus.Deferred,
            message: "QC review is in progress. Shift closure remains unavailable.");
    }

    public static ExecutionRuntimeState AcceptQcReview(ExecutionRuntimeState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        if (state.Result is null)
        {
            throw new InvalidOperationException("QC acceptance requires produced execution result.");
        }

        if (state.QcStatus is QCReviewStatus.Rejected or QCReviewStatus.NeedsRevision)
        {
            throw new InvalidOperationException("QC acceptance requires a new execution result after rejected review.");
        }

        var review = new QCReviewResult(
            $"REVIEW-{state.Result.ResultId}",
            state.Result.ResultId,
            QCReviewStatus.Accepted,
            new[] { "QC accepted the produced execution result." },
            new[] { "DECISION://qc/accepted" });

        var session = ExecutionCoordinator.ApplyDecision(state.Session, state.Result, review);
        return BuildState(
            session: session,
            task: state.Task,
            shift: state.Shift,
            runtimeSelection: state.RuntimeSelection,
            runtimeProfile: state.RuntimeProfile,
            watchdog: state.Watchdog,
            resultHistory: state.ResultHistory,
            attempts: UpdateCurrentAttempt(state, attempt => attempt with
            {
                OutcomeStatus = ExecutionOutcomeStatus.NoOp,
                QcStatus = QCReviewStatus.Accepted,
                RejectReason = null
            }),
            currentAttemptIndex: state.CurrentAttemptIndex,
            result: state.Result,
            qcStatus: QCReviewStatus.Accepted,
            review: review,
            lastQcRejectReason: null,
            target: ExecutionTarget.IdleSubsystem,
            status: ExecutionOutcomeStatus.NoOp,
            message: "QC accepted the execution result. Shift is ready for closure review.");
    }

    public static ExecutionRuntimeState RejectQcReview(ExecutionRuntimeState state, bool needsRevision, string reason)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);

        if (state.Result is null)
        {
            throw new InvalidOperationException("QC rejection requires produced execution result.");
        }

        var reviewStatus = needsRevision ? QCReviewStatus.NeedsRevision : QCReviewStatus.Rejected;
        var trimmedReason = reason.Trim();
        var review = new QCReviewResult(
            $"REVIEW-{state.Result.ResultId}",
            state.Result.ResultId,
            reviewStatus,
            new[] { needsRevision ? "QC requested revision." : "QC rejected the execution result.", trimmedReason },
            new[] { "DECISION://qc/rejected" },
            trimmedReason);

        var session = ExecutionCoordinator.ApplyDecision(state.Session, state.Result, review);
        return BuildState(
            session: session,
            task: state.Task,
            shift: state.Shift,
            runtimeSelection: state.RuntimeSelection,
            runtimeProfile: state.RuntimeProfile,
            watchdog: state.Watchdog,
            resultHistory: state.ResultHistory,
            attempts: UpdateCurrentAttempt(state, attempt => attempt with
            {
                OutcomeStatus = ExecutionOutcomeStatus.Deferred,
                QcStatus = reviewStatus,
                RejectReason = trimmedReason
            }),
            currentAttemptIndex: state.CurrentAttemptIndex,
            result: state.Result,
            qcStatus: reviewStatus,
            review: review,
            lastQcRejectReason: trimmedReason,
            target: ExecutionTarget.ActiveShiftSubsystem,
            status: ExecutionOutcomeStatus.Deferred,
            message: "QC rejected the result. A new revision is required before closure.");
    }

    public static ExecutionRuntimeState ObserveAcceptanceAfterQc(
        ExecutionRuntimeState state,
        string projectRoot,
        AcceptanceProcessEvidence processEvidence,
        string currentWorkspaceCheckResult,
        AcceptanceClassification classification = AcceptanceClassification.Unknown,
        ToolExecutionEnvelope? toolExecution = null)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentException.ThrowIfNullOrWhiteSpace(projectRoot);
        ArgumentNullException.ThrowIfNull(processEvidence);
        ArgumentException.ThrowIfNullOrWhiteSpace(currentWorkspaceCheckResult);

        if (state.QcStatus != QCReviewStatus.Accepted || state.Review is null)
        {
            throw new InvalidOperationException("Acceptance observation requires accepted QC state.");
        }

        var evaluation = ExecutionAcceptanceAdapter.Evaluate(
            projectRoot: projectRoot,
            runtimeState: state,
            processEvidence: processEvidence,
            currentWorkspaceCheckResult: currentWorkspaceCheckResult,
            classification: classification,
            toolExecution: toolExecution);

        return state with { AcceptanceEvaluation = evaluation };
    }

    public static ExecutionRuntimeState RestartRevision(ExecutionRuntimeState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        var session = ExecutionCoordinator.RestartAfterRevision(state.Session);
        return BuildState(
            session: session,
            task: state.Task,
            shift: state.Shift,
            runtimeSelection: state.RuntimeSelection,
            runtimeProfile: state.RuntimeProfile,
            watchdog: state.Watchdog,
            resultHistory: state.ResultHistory,
            attempts: state.Attempts,
            currentAttemptIndex: state.CurrentAttemptIndex,
            result: null,
            qcStatus: QCReviewStatus.NotStarted,
            review: null,
            lastQcRejectReason: state.LastQcRejectReason,
            target: ExecutionTarget.ActiveShiftSubsystem,
            status: ExecutionOutcomeStatus.Deferred,
            message: "Execution returned for revision. Produce a new result before QC can continue.");
    }

    public static ExecutionRuntimeState RestartCompletedResultForRevision(ExecutionRuntimeState state, string reason)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);

        if (state.Session.State != ExecutionSessionState.Completed)
        {
            throw new InvalidOperationException("Completed-result revision restart requires Completed runtime state.");
        }

        var trimmedReason = reason.Trim();
        var session = state.Session with
        {
            CurrentRole = Prompting.PromptRole.Worker,
            State = ExecutionSessionState.InProgress,
            ResultId = null,
            ReviewId = null,
            FailureReason = null
        };

        return BuildState(
            session: session,
            task: state.Task,
            shift: state.Shift,
            runtimeSelection: state.RuntimeSelection,
            runtimeProfile: state.RuntimeProfile,
            watchdog: state.Watchdog,
            resultHistory: state.ResultHistory,
            attempts: UpdateCurrentAttempt(state, attempt => attempt with
            {
                OutcomeStatus = ExecutionOutcomeStatus.Deferred,
                QcStatus = QCReviewStatus.NeedsRevision,
                RejectReason = trimmedReason
            }),
            currentAttemptIndex: state.CurrentAttemptIndex,
            result: null,
            qcStatus: QCReviewStatus.NotStarted,
            review: null,
            lastQcRejectReason: trimmedReason,
            target: ExecutionTarget.ActiveShiftSubsystem,
            status: ExecutionOutcomeStatus.Deferred,
            message: "Completed result returned to a new revision cycle.");
    }

    public static ExecutionRuntimeState CancelInProgress(ExecutionRuntimeState state, string reason)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);

        if (state.Session.State != ExecutionSessionState.InProgress)
        {
            throw new InvalidOperationException("In-progress cancel requires active execution runtime state.");
        }

        var trimmedReason = reason.Trim();
        var session = state.Session with
        {
            State = ExecutionSessionState.Failed,
            ResultId = null,
            ReviewId = null,
            FailureReason = trimmedReason
        };

        return BuildState(
            session: session,
            task: state.Task,
            shift: state.Shift,
            runtimeSelection: state.RuntimeSelection,
            runtimeProfile: state.RuntimeProfile,
            watchdog: ExecutionWatchdog.Interrupt(
                state.Watchdog,
                new RuntimeInterruptionRecord(
                    StopReason.UserCancel,
                    DateTimeOffset.UtcNow,
                    GracefulStopAttempted: true,
                    HardKillRequired: false,
                    $"Execution watchdog interruption: reason={StopReason.UserCancel}, detail={trimmedReason}.")),
            resultHistory: state.ResultHistory,
            attempts: state.Attempts,
            currentAttemptIndex: state.CurrentAttemptIndex,
            result: null,
            qcStatus: QCReviewStatus.NotStarted,
            review: null,
            lastQcRejectReason: state.LastQcRejectReason,
            target: ExecutionTarget.ActiveShiftSubsystem,
            status: ExecutionOutcomeStatus.Deferred,
            message: $"Execution was stopped by user before result production ({trimmedReason}).");
    }

    public static ExecutionRunResult BuildInterruptedRunResult(ExecutionRuntimeState state, string reason)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);

        var trimmedReason = reason.Trim();
        var message = $"Execution interrupted: {trimmedReason}. Active task remains open.";
        return new ExecutionRunResult(
            new ExecutionOutcome(ExecutionTarget.ActiveShiftSubsystem, ExecutionOutcomeStatus.Deferred, message),
            new ExecutionRecord(
                state.Shift.ShiftId,
                state.Task.TaskId,
                ExecutionTarget.ActiveShiftSubsystem,
                ExecutionOutcomeStatus.Deferred,
                message,
                state.RuntimeProfile,
                state.Watchdog.Interruption),
            state.RuntimeProfile);
    }

    public static ExecutionRuntimeState ObserveWatchdog(
        ExecutionRuntimeState state,
        DateTimeOffset now,
        bool policyViolationObserved = false)
    {
        ArgumentNullException.ThrowIfNull(state);

        var interruption = ExecutionWatchdog.Evaluate(state.Watchdog, now, policyViolationObserved);
        if (interruption is null)
        {
            return state;
        }

        return InterruptByWatchdog(state, interruption);
    }

    public static ExecutionRuntimeState InterruptByWatchdog(
        ExecutionRuntimeState state,
        RuntimeInterruptionRecord interruption)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(interruption);

        var session = state.Session with
        {
            State = ExecutionSessionState.Failed,
            ResultId = null,
            ReviewId = null,
            FailureReason = interruption.Summary
        };

        return BuildState(
            session: session,
            task: state.Task,
            shift: state.Shift,
            runtimeSelection: state.RuntimeSelection,
            runtimeProfile: state.RuntimeProfile,
            watchdog: ExecutionWatchdog.Interrupt(state.Watchdog, interruption),
            resultHistory: state.ResultHistory,
            attempts: state.Attempts,
            currentAttemptIndex: state.CurrentAttemptIndex,
            result: null,
            qcStatus: QCReviewStatus.NotStarted,
            review: null,
            lastQcRejectReason: state.LastQcRejectReason,
            target: ExecutionTarget.ActiveShiftSubsystem,
            status: ExecutionOutcomeStatus.Deferred,
            message: $"Execution interrupted by host runtime ({interruption.Reason}).");
    }

    private static ExecutionRuntimeState BuildState(
        ExecutionSession session,
        TaskState task,
        ShiftState shift,
        RuntimeSelectionDecision runtimeSelection,
        RuntimeProfile runtimeProfile,
        ExecutionWatchdogSnapshot watchdog,
        IReadOnlyList<WorkerExecutionResult> resultHistory,
        IReadOnlyList<ExecutionAttempt> attempts,
        int currentAttemptIndex,
        WorkerExecutionResult? result,
        QCReviewStatus qcStatus,
        QCReviewResult? review,
        string? lastQcRejectReason,
        ExecutionTarget target,
        ExecutionOutcomeStatus status,
        string message)
    {
        ArgumentNullException.ThrowIfNull(runtimeSelection);
        ArgumentNullException.ThrowIfNull(runtimeProfile);
        ArgumentNullException.ThrowIfNull(watchdog);

        runtimeProfile.Validate();
        runtimeProfile = runtimeProfile.Normalize();
        runtimeSelection = runtimeSelection with { Profile = runtimeProfile };
        watchdog.Validate();
        watchdog = watchdog.Normalize();

        var runResult = new ExecutionRunResult(
            new ExecutionOutcome(target, status, message),
            new ExecutionRecord(
                shift.ShiftId,
                task.TaskId,
                target,
                status,
                message,
                runtimeProfile,
                watchdog.Interruption),
            runtimeProfile);
        var closureCandidate = ExecutionClosureBuilder.Build(runResult);
        var closureProposal = ShiftClosureProposalBuilder.Build(closureCandidate);

        return new ExecutionRuntimeState(
            session,
            task,
            shift,
            runtimeSelection,
            runtimeProfile,
            watchdog,
            resultHistory,
            attempts,
            currentAttemptIndex,
            result,
            qcStatus,
            review,
            lastQcRejectReason,
            runResult,
            AcceptanceEvaluation: null,
            closureCandidate,
            closureProposal);
    }

    private static IReadOnlyList<ExecutionAttempt> UpdateCurrentAttempt(
        ExecutionRuntimeState state,
        Func<ExecutionAttempt, ExecutionAttempt> updater)
    {
        if (state.CurrentAttemptIndex <= 0)
        {
            return state.Attempts;
        }

        return state.Attempts
            .Select(attempt => attempt.AttemptIndex == state.CurrentAttemptIndex ? updater(attempt) : attempt)
            .ToArray();
    }
}
