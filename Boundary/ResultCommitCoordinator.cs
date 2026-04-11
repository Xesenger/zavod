using System;
using System.Collections.Generic;
using System.Linq;
using zavod.Execution;
using zavod.Prompting;
using zavod.State;

namespace zavod.Boundary;

public static class ResultCommitCoordinator
{
    public static ResultLifecycle RegisterProducedResult(WorkerExecutionResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        Require(!string.IsNullOrWhiteSpace(result.ResultId), "register produced result", "result id", "Result id is required.");
        Require(!string.IsNullOrWhiteSpace(result.TaskId), "register produced result", "task id", "Task id is required.");

        return new ResultLifecycle(
            $"LIFECYCLE-{result.ResultId}",
            result.ResultId,
            result.TaskId,
            ResultLifecycleStatus.Produced);
    }

    public static AcceptResultOutcome AcceptResult(ResultLifecycle lifecycle, WorkerExecutionResult result, QCReviewResult review, DateTimeOffset acceptedAt)
    {
        ArgumentNullException.ThrowIfNull(lifecycle);
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(review);

        Require(lifecycle.Status == ResultLifecycleStatus.Produced, "accept result", "lifecycle state", "Only produced result can be accepted.");
        Require(lifecycle.SourceResultId == result.ResultId, "accept result", "source result binding", "Lifecycle must match source result.");
        Require(lifecycle.TaskId == result.TaskId, "accept result", "task binding", "Lifecycle task must match result task.");
        Require(review.ResultId == result.ResultId, "accept result", "review result binding", "Review must reference source result.");
        Require(review.Status == QCReviewStatus.Accepted, "accept result", "qc status", "AcceptResult requires accepted QC review.");
        Require(review.DecisionAnchors.Count > 0, "accept result", "decision anchors", "Accepted review requires decision anchors.");

        var reviewedLifecycle = lifecycle with
        {
            Status = ResultLifecycleStatus.Reviewed,
            ReviewId = review.ReviewId
        };

        var accepted = new AcceptedResult(
            $"ACCEPTED-{result.ResultId}",
            result.ResultId,
            result.TaskId,
            result.Summary,
            acceptedAt,
            review.ReviewId,
            result.ProducedArtifacts);

        var acceptedLifecycle = reviewedLifecycle with
        {
            Status = ResultLifecycleStatus.Accepted,
            AcceptedResultId = accepted.AcceptedResultId
        };

        return new AcceptResultOutcome(reviewedLifecycle, acceptedLifecycle, accepted);
    }

    public static ResultLifecycle RejectResult(ResultLifecycle lifecycle, WorkerExecutionResult result, QCReviewResult review)
    {
        ArgumentNullException.ThrowIfNull(lifecycle);
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(review);

        Require(lifecycle.Status == ResultLifecycleStatus.Produced, "reject result", "lifecycle state", "Only produced result can be rejected.");
        Require(lifecycle.SourceResultId == result.ResultId, "reject result", "source result binding", "Lifecycle must match source result.");
        Require(review.ResultId == result.ResultId, "reject result", "review result binding", "Review must reference source result.");
        Require(review.Status is QCReviewStatus.Rejected or QCReviewStatus.NeedsRevision, "reject result", "qc status", "RejectResult requires rejected or revision QC review.");

        return lifecycle with
        {
            Status = ResultLifecycleStatus.Rejected,
            ReviewId = review.ReviewId
        };
    }

    public static ApplyOperationOutcome ApplyAcceptedResult(ResultLifecycle lifecycle, AcceptedResult acceptedResult, ApplyTarget target, IReadOnlyList<ApplyChange> changeSet)
    {
        ArgumentNullException.ThrowIfNull(lifecycle);
        ArgumentNullException.ThrowIfNull(acceptedResult);
        ArgumentNullException.ThrowIfNull(changeSet);

        Require(lifecycle.Status == ResultLifecycleStatus.Accepted, "apply accepted result", "lifecycle state", "Only accepted result can be applied.");
        Require(lifecycle.AcceptedResultId == acceptedResult.AcceptedResultId, "apply accepted result", "accepted result binding", "Lifecycle must reference accepted result.");
        Require(changeSet.Count > 0, "apply accepted result", "change set", "Apply operation requires change set.");

        var applyOperation = new ApplyOperation(
            $"APPLY-{acceptedResult.AcceptedResultId}",
            acceptedResult.AcceptedResultId,
            target,
            changeSet.ToArray(),
            ApplyStatus.Applied);

        var appliedLifecycle = lifecycle with
        {
            Status = ResultLifecycleStatus.Applied,
            ApplyId = applyOperation.ApplyId
        };

        return new ApplyOperationOutcome(appliedLifecycle, applyOperation);
    }

    public static CommitOutcome Commit(ResultLifecycle lifecycle, ApplyOperation applyOperation, DateTimeOffset timestamp, string summary, string linkedTaskId, IReadOnlyList<string> linkedAnchors)
    {
        ArgumentNullException.ThrowIfNull(lifecycle);
        ArgumentNullException.ThrowIfNull(applyOperation);
        ArgumentNullException.ThrowIfNull(linkedAnchors);

        Require(lifecycle.Status == ResultLifecycleStatus.Applied, "commit", "lifecycle state", "Only applied result can be committed.");
        Require(lifecycle.ApplyId == applyOperation.ApplyId, "commit", "apply binding", "Lifecycle must reference apply operation.");
        Require(applyOperation.ApplyStatus == ApplyStatus.Applied, "commit", "apply status", "Commit requires applied operation.");
        Require(!string.IsNullOrWhiteSpace(linkedTaskId), "commit", "task id", "Commit requires linked task id.");
        Require(linkedAnchors.Count > 0, "commit", "linked anchors", "Commit requires linked anchors.");

        var commitRecord = new CommitRecord(
            $"COMMIT-{applyOperation.ApplyId}",
            applyOperation.ApplyId,
            timestamp,
            summary.Trim(),
            linkedTaskId.Trim(),
            linkedAnchors
                .Where(static anchor => !string.IsNullOrWhiteSpace(anchor))
                .Select(static anchor => anchor.Trim())
                .Distinct(StringComparer.Ordinal)
                .OrderBy(static anchor => anchor, StringComparer.Ordinal)
                .ToArray());

        var committedLifecycle = lifecycle with
        {
            Status = ResultLifecycleStatus.Committed,
            CommitId = commitRecord.CommitId
        };

        return new CommitOutcome(committedLifecycle, commitRecord);
    }

    public static ShiftState UpdateShiftState(ShiftState shiftState, CommitRecord commitRecord, PromptRole actor)
    {
        ArgumentNullException.ThrowIfNull(shiftState);
        ArgumentNullException.ThrowIfNull(commitRecord);

        var acceptedResultReference = $"{commitRecord.CommitId}|task:{commitRecord.LinkedTaskId}|{commitRecord.Summary}";
        return shiftState.RecordAcceptedResult(actor, acceptedResultReference);
    }

    private static void Require(bool condition, string area, string missingRequirement, string reason)
    {
        if (!condition)
        {
            throw new ResultCommitCoordinatorException(area, missingRequirement, reason);
        }
    }
}
