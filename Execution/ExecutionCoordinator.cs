using System;
using System.Collections.Generic;
using System.Linq;
using zavod.Contexting;
using zavod.Prompting;
using zavod.Retrieval;
using StateTaskState = zavod.State.TaskState;
using zavod.State;

namespace zavod.Execution;

public static class ExecutionCoordinator
{
    public static ExecutionPreparation PrepareTask(ExecutionSession session, StateTaskState taskState, ScopedContext scopedContext)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(taskState);
        ArgumentNullException.ThrowIfNull(scopedContext);

        Require(session.State == ExecutionSessionState.Initialized, "prepare task", "session state", "Only initialized session can prepare task.");
        Require(session.AssociatedTaskId == taskState.TaskId, "prepare task", "task identity", "Session task id must match TaskState.");
        Require(taskState.IntentState == ContextIntentState.Validated, "prepare task", "validated intent", "Execution requires validated intent.");
        Require(scopedContext.SelectedCandidates.Count > 0, "prepare task", "scoped context", "Execution requires scoped context with selected candidates.");

        var allowedRoles = new[] { PromptRole.Worker, PromptRole.Qc };
        var binding = new ExecutionTaskBinding(
            taskState.TaskId,
            $"validated://task/{taskState.TaskId}",
            BuildScopedContextReference(taskState.TaskId, scopedContext),
            scopedContext,
            allowedRoles);

        var updatedSession = session with
        {
            CurrentRole = PromptRole.ShiftLead,
            State = ExecutionSessionState.TaskPrepared,
            BindingReference = binding.ScopedContextReference,
            FailureReason = null
        };

        return new ExecutionPreparation(updatedSession, binding);
    }

    public static ExecutionSession StartExecution(ExecutionSession session, ExecutionTaskBinding binding)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(binding);

        Require(session.State == ExecutionSessionState.TaskPrepared, "start execution", "session state", "Execution can start only from TaskPrepared.");
        Require(session.AssociatedTaskId == binding.TaskId, "start execution", "task binding", "Binding task must match session task.");
        Require(binding.ScopedContext.SelectedCandidates.Count > 0, "start execution", "scoped context", "Execution cannot start without scoped context.");
        Require(binding.AllowedRoles.Contains(PromptRole.Worker), "start execution", "worker permission", "Binding must allow Worker execution.");

        return session with
        {
            CurrentRole = PromptRole.Worker,
            State = ExecutionSessionState.InProgress,
            FailureReason = null
        };
    }

    public static ExecutionSession SubmitResult(ExecutionSession session, WorkerExecutionResult result)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(result);

        Require(session.State == ExecutionSessionState.InProgress, "submit result", "session state", "Result can be submitted only from InProgress.");
        Require(session.AssociatedTaskId == result.TaskId, "submit result", "result task binding", "Result must belong to session task.");
        Require(!string.IsNullOrWhiteSpace(result.ResultId), "submit result", "result id", "Result id is required.");

        return result.Status == WorkerExecutionStatus.Failed
            ? session with
            {
                CurrentRole = PromptRole.Worker,
                State = ExecutionSessionState.Failed,
                ResultId = result.ResultId,
                FailureReason = result.Diagnostics?.Message ?? result.Summary
            }
            : session with
            {
                CurrentRole = PromptRole.Worker,
                State = ExecutionSessionState.ResultProduced,
                ResultId = result.ResultId,
                FailureReason = null
            };
    }

    public static ExecutionSession RequestReview(ExecutionSession session, WorkerExecutionResult result)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(result);

        Require(session.State == ExecutionSessionState.ResultProduced, "request review", "session state", "Review can be requested only from ResultProduced.");
        Require(session.ResultId == result.ResultId, "request review", "result identity", "Review requires the current produced result.");

        return session with
        {
            CurrentRole = PromptRole.Qc,
            State = ExecutionSessionState.UnderReview,
            FailureReason = null
        };
    }

    public static ExecutionSession ApplyDecision(ExecutionSession session, WorkerExecutionResult result, QCReviewResult review)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(review);

        Require(session.State == ExecutionSessionState.UnderReview, "apply decision", "session state", "Decision can be applied only from UnderReview.");
        Require(session.ResultId == result.ResultId, "apply decision", "result identity", "Decision must reference current result.");
        Require(review.ResultId == result.ResultId, "apply decision", "review result binding", "Review must reference the current result.");
        Require(review.DecisionAnchors.Count > 0, "apply decision", "decision anchors", "Review decision requires decision anchors.");
        Require(
            review.Status is QCReviewStatus.Accepted or QCReviewStatus.Rejected or QCReviewStatus.NeedsRevision,
            "apply decision",
            "qc status",
            "Decision requires terminal QC review status.");

        return review.Status == QCReviewStatus.Accepted
            ? session with
            {
                CurrentRole = PromptRole.Qc,
                State = ExecutionSessionState.Completed,
                ReviewId = review.ReviewId,
                FailureReason = null
            }
            : session with
            {
                CurrentRole = PromptRole.Qc,
                State = ExecutionSessionState.ReturnedForRevision,
                ReviewId = review.ReviewId,
                FailureReason = string.Join("; ", review.Comments.Where(static comment => !string.IsNullOrWhiteSpace(comment)))
            };
    }

    public static ExecutionSession RestartAfterRevision(ExecutionSession session)
    {
        ArgumentNullException.ThrowIfNull(session);

        Require(session.State == ExecutionSessionState.ReturnedForRevision, "restart after revision", "session state", "Revision restart requires ReturnedForRevision state.");

        return session with
        {
            CurrentRole = PromptRole.Worker,
            State = ExecutionSessionState.InProgress,
            ResultId = null,
            ReviewId = null,
            FailureReason = null
        };
    }

    private static string BuildScopedContextReference(string taskId, ScopedContext scopedContext)
    {
        var referenceTail = string.Join(
            "|",
            scopedContext.SourceReferences
                .Select(static reference => $"{reference.ArtifactId}:{reference.Reference}")
                .OrderBy(static value => value, StringComparer.Ordinal));

        return $"scoped://task/{taskId}/{referenceTail}";
    }

    private static void Require(bool condition, string area, string missingRequirement, string reason)
    {
        if (!condition)
        {
            throw new ExecutionCoordinatorException(area, missingRequirement, reason);
        }
    }
}
