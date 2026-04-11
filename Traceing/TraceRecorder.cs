using System;
using System.Linq;
using zavod.Boundary;
using zavod.Execution;
using zavod.Prompting;

namespace zavod.Traceing;

public static class TraceRecorder
{
    public static ExecutionTrace Start(string traceId, string sessionId, string taskId)
    {
        Require(!string.IsNullOrWhiteSpace(traceId), "start trace", "trace id", "Trace id is required.");
        Require(!string.IsNullOrWhiteSpace(sessionId), "start trace", "session id", "Session id is required.");
        Require(!string.IsNullOrWhiteSpace(taskId), "start trace", "task id", "Task id is required.");

        return new ExecutionTrace(traceId.Trim(), sessionId.Trim(), taskId.Trim(), Array.Empty<ExecutionStep>());
    }

    public static ExecutionTrace AppendStep(
        ExecutionTrace trace,
        ExecutionStepType stepType,
        DateTimeOffset timestamp,
        PromptRole role,
        string referenceId,
        string summary)
    {
        ArgumentNullException.ThrowIfNull(trace);
        Require(!string.IsNullOrWhiteSpace(referenceId), "append step", "reference id", "Reference id is required.");
        Require(!string.IsNullOrWhiteSpace(summary), "append step", "summary", "Summary is required.");

        var steps = trace.Steps
            .Concat(new[]
            {
                new ExecutionStep(stepType, timestamp, role, referenceId.Trim(), summary.Trim())
            })
            .OrderBy(static step => step.Timestamp)
            .ThenBy(static step => GetStepOrder(step.StepType))
            .ThenBy(static step => step.ReferenceId, StringComparer.Ordinal)
            .ToArray();

        return trace with { Steps = steps };
    }

    public static ExecutionTrace RecordPreparation(ExecutionTrace trace, ExecutionPreparation preparation, DateTimeOffset timestamp)
    {
        ArgumentNullException.ThrowIfNull(preparation);
        Require(preparation.Session.AssociatedTaskId == trace.TaskId, "record preparation", "task id", "Preparation must match trace task.");
        return AppendStep(trace, ExecutionStepType.Prepare, timestamp, PromptRole.ShiftLead, preparation.Binding.TaskId, "Task prepared for execution.");
    }

    public static ExecutionTrace RecordExecutionStarted(ExecutionTrace trace, ExecutionSession session, DateTimeOffset timestamp)
    {
        ArgumentNullException.ThrowIfNull(session);
        Require(session.AssociatedTaskId == trace.TaskId, "record execution", "task id", "Execution session must match trace task.");
        return AppendStep(trace, ExecutionStepType.Execute, timestamp, PromptRole.Worker, session.SessionId, "Execution started.");
    }

    public static ExecutionTrace RecordResult(ExecutionTrace trace, WorkerExecutionResult result, DateTimeOffset timestamp)
    {
        ArgumentNullException.ThrowIfNull(result);
        Require(result.TaskId == trace.TaskId, "record result", "task id", "Worker result must match trace task.");
        return AppendStep(trace, ExecutionStepType.Result, timestamp, PromptRole.Worker, result.ResultId, result.Summary);
    }

    public static ExecutionTrace RecordReview(ExecutionTrace trace, QCReviewResult review, string taskId, DateTimeOffset timestamp)
    {
        ArgumentNullException.ThrowIfNull(review);
        Require(taskId == trace.TaskId, "record review", "task id", "QC review must match trace task.");
        return AppendStep(trace, ExecutionStepType.Review, timestamp, PromptRole.Qc, review.ReviewId, $"QC review: {review.Status}.");
    }

    public static ExecutionTrace RecordApply(ExecutionTrace trace, ApplyOperation applyOperation, string taskId, DateTimeOffset timestamp)
    {
        ArgumentNullException.ThrowIfNull(applyOperation);
        Require(taskId == trace.TaskId, "record apply", "task id", "Apply operation must match trace task.");
        return AppendStep(trace, ExecutionStepType.Apply, timestamp, PromptRole.ShiftLead, applyOperation.ApplyId, $"Apply operation: {applyOperation.ApplyStatus}.");
    }

    public static ExecutionTrace RecordCommit(ExecutionTrace trace, CommitRecord commitRecord, DateTimeOffset timestamp)
    {
        ArgumentNullException.ThrowIfNull(commitRecord);
        Require(commitRecord.LinkedTaskId == trace.TaskId, "record commit", "task id", "Commit must match trace task.");
        return AppendStep(trace, ExecutionStepType.Commit, timestamp, PromptRole.ShiftLead, commitRecord.CommitId, commitRecord.Summary);
    }

    private static int GetStepOrder(ExecutionStepType stepType)
    {
        return stepType switch
        {
            ExecutionStepType.Prepare => 0,
            ExecutionStepType.Execute => 1,
            ExecutionStepType.Result => 2,
            ExecutionStepType.Review => 3,
            ExecutionStepType.Apply => 4,
            ExecutionStepType.Commit => 5,
            _ => int.MaxValue
        };
    }

    private static void Require(bool condition, string area, string missingRequirement, string reason)
    {
        if (!condition)
        {
            throw new TraceingException(area, missingRequirement, reason);
        }
    }
}
