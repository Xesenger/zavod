using System;
using System.Collections.Generic;
using System.Linq;
using zavod.Boundary;
using zavod.Execution;
using zavod.Outcome;
using zavod.Persistence;
using zavod.Prompting;
using zavod.Traceing;
using TraceSnapshotBuilder = zavod.Traceing.SnapshotBuilder;

namespace zavod.State;

public static class ShiftClosureProcessor
{
    public static ShiftClosureResult Close(
        ProjectState projectState,
        ShiftState shiftState,
        ExecutionRunResult runResult,
        DateTimeOffset timestamp,
        bool isUserConfirmed,
        IReadOnlyList<CommitRecord>? commits = null)
    {
        ArgumentNullException.ThrowIfNull(projectState);
        ArgumentNullException.ThrowIfNull(shiftState);
        ArgumentNullException.ThrowIfNull(runResult);

        var task = ResolveTask(shiftState, runResult.Record.TaskId);

        if (!isUserConfirmed)
        {
            return new ShiftClosureResult(
                ShiftClosureStatus.Cancelled,
                task,
                shiftState,
                projectState,
                null,
                null,
                null,
                "Shift closure requires explicit user confirmation.");
        }

        if (!string.Equals(projectState.ActiveShiftId, shiftState.ShiftId, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("ProjectState active shift must match closure shift.");
        }

        if (!string.Equals(runResult.Record.ShiftId, shiftState.ShiftId, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Execution record shift binding must match closure shift.");
        }

        if (!string.Equals(shiftState.CurrentTaskId, task.TaskId, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Closure requires execution bound to the current shift task.");
        }

        if (runResult.Outcome.Status != ExecutionOutcomeStatus.NoOp)
        {
            return new ShiftClosureResult(
                ShiftClosureStatus.Cancelled,
                task,
                shiftState,
                projectState,
                null,
                null,
                null,
                "Only closable execution outcome may finalize canonical closure.");
        }

        var completedTask = CompleteForClosure(task, timestamp);
        var completedShift = shiftState.UpdateTask(completedTask) with { CurrentTaskId = null };
        completedShift = completedShift.Complete(PromptRole.ShiftLead);

        var updatedProjectState = projectState with
        {
            ActiveShiftId = null,
            ActiveTaskId = null
        };

        VerifyLifecycleForSnapshot(runResult, completedTask, completedShift);
        var executionReference = BuildExecutionReference(runResult);
        var taskReference = BuildTaskReference(completedTask);

        var canonicalSnapshot = TraceSnapshotBuilder.BuildSnapshot(
            completedShift,
            executionReference,
            taskReference,
            commits ?? Array.Empty<CommitRecord>(),
            completedShift.Tasks,
            timestamp,
            checkpointKind: "closure");

        var persistedProjectState = ProjectStateStorage.Save(updatedProjectState);
        var shiftPath = ShiftStateStorage.Save(persistedProjectState.Paths.ProjectRoot, completedShift);
        var snapshotPath = SnapshotStorage.Save(persistedProjectState.Paths.ProjectRoot, canonicalSnapshot);

        return new ShiftClosureResult(
            ShiftClosureStatus.Completed,
            completedTask,
            completedShift,
            persistedProjectState,
            canonicalSnapshot,
            shiftPath,
            snapshotPath,
            "Canonical closure finalized task, updated ProjectState, and created immutable snapshot.");
    }

    public static ShiftClosureResult CloseAcceptedShift(
        ProjectState projectState,
        ShiftState shiftState,
        DateTimeOffset timestamp,
        string summary)
    {
        ArgumentNullException.ThrowIfNull(projectState);
        ArgumentNullException.ThrowIfNull(shiftState);
        ArgumentException.ThrowIfNullOrWhiteSpace(summary);

        if (!string.Equals(projectState.ActiveShiftId, shiftState.ShiftId, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("ProjectState active shift must match closure shift.");
        }

        if (shiftState.Status != ShiftStateStatus.Active)
        {
            throw new InvalidOperationException("Accepted shift closure requires active shift truth.");
        }

        if (shiftState.CurrentTaskId is not null)
        {
            throw new InvalidOperationException("Accepted shift closure requires shift without active task.");
        }

        var finalizedTask = shiftState.Tasks
            .LastOrDefault(task => task.Status is TaskStateStatus.Completed or TaskStateStatus.Abandoned)
            ?? throw new InvalidOperationException("Accepted shift closure requires finalized task history.");

        var completedShift = shiftState.Complete(PromptRole.ShiftLead);
        var updatedProjectState = projectState with
        {
            ActiveShiftId = null,
            ActiveTaskId = null
        };

        var snapshot = TraceSnapshotBuilder.BuildSnapshot(
            completedShift,
            $"execution://shift/{completedShift.ShiftId}/accepted-summary/{timestamp:yyyyMMddHHmmss}",
            $"task://{finalizedTask.TaskId}",
            Array.Empty<CommitRecord>(),
            completedShift.Tasks,
            timestamp,
            checkpointKind: "closure");

        var persistedProjectState = ProjectStateStorage.Save(updatedProjectState);
        var shiftPath = ShiftStateStorage.Save(persistedProjectState.Paths.ProjectRoot, completedShift);
        var snapshotPath = SnapshotStorage.Save(persistedProjectState.Paths.ProjectRoot, snapshot);

        return new ShiftClosureResult(
            ShiftClosureStatus.Completed,
            finalizedTask,
            completedShift,
            persistedProjectState,
            snapshot,
            shiftPath,
            snapshotPath,
            summary.Trim());
    }

    private static TaskState ResolveTask(ShiftState shiftState, string taskId)
    {
        var task = shiftState.Tasks.FirstOrDefault(candidate => candidate.TaskId == taskId);
        if (task is null)
        {
            throw new InvalidOperationException("Execution record task binding must exist in shift truth.");
        }

        return task;
    }

    private static TaskState CompleteForClosure(TaskState task, DateTimeOffset timestamp)
    {
        return task.Status switch
        {
            TaskStateStatus.Active => task.Complete(PromptRole.ShiftLead, timestamp),
            TaskStateStatus.Completed => task,
            _ => throw new InvalidOperationException("Canonical closure can finalize only active or already completed task truth.")
        };
    }

    private static void VerifyLifecycleForSnapshot(ExecutionRunResult runResult, TaskState finalizedTask, ShiftState completedShift)
    {
        if (string.IsNullOrWhiteSpace(runResult.Record.ShiftId))
        {
            throw new InvalidOperationException("Canonical closure requires execution record shift binding before snapshot creation.");
        }

        if (string.IsNullOrWhiteSpace(runResult.Record.TaskId))
        {
            throw new InvalidOperationException("Canonical closure requires execution record task binding before snapshot creation.");
        }

        if (string.IsNullOrWhiteSpace(runResult.Outcome.Message) || string.IsNullOrWhiteSpace(runResult.Record.Message))
        {
            throw new InvalidOperationException("Canonical closure requires non-empty execution message before snapshot creation.");
        }

        if (!string.Equals(runResult.Outcome.Message, runResult.Record.Message, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Canonical closure requires consistent execution message across outcome and record.");
        }

        if (runResult.Outcome.Status != runResult.Record.OutcomeStatus)
        {
            throw new InvalidOperationException("Canonical closure requires consistent execution outcome status across outcome and record.");
        }

        if (runResult.Outcome.Status != ExecutionOutcomeStatus.NoOp)
        {
            throw new InvalidOperationException("Canonical closure can create snapshot only from closable execution outcome.");
        }

        if (finalizedTask.Status != TaskStateStatus.Completed)
        {
            throw new InvalidOperationException("Canonical snapshot creation requires finalized completed task truth.");
        }

        if (!string.Equals(runResult.Record.TaskId, finalizedTask.TaskId, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Canonical snapshot task truth must match execution task binding.");
        }

        if (completedShift.Status != ShiftStateStatus.Completed)
        {
            throw new InvalidOperationException("Canonical snapshot creation requires completed shift truth.");
        }

        if (completedShift.CurrentTaskId is not null)
        {
            throw new InvalidOperationException("Canonical snapshot creation requires shift without active task.");
        }

        var persistedTask = completedShift.Tasks.FirstOrDefault(task => task.TaskId == finalizedTask.TaskId);
        if (persistedTask is null || persistedTask.Status != TaskStateStatus.Completed)
        {
            throw new InvalidOperationException("Canonical snapshot creation requires completed shift to contain completed finalized task truth.");
        }
    }

    private static string BuildExecutionReference(ExecutionRunResult runResult)
    {
        return $"execution://shift/{runResult.Record.ShiftId}/task/{runResult.Record.TaskId}/target/{runResult.Record.Target}/outcome/{runResult.Record.OutcomeStatus}";
    }

    private static string BuildTaskReference(TaskState task)
    {
        return $"task://{task.TaskId}";
    }
}
