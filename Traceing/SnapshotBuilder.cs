using System;
using System.Collections.Generic;
using System.Linq;
using zavod.Boundary;
using zavod.State;

namespace zavod.Traceing;

public static class SnapshotBuilder
{
    public static Snapshot BuildSnapshot(
        ShiftState shiftState,
        string executionReference,
        string taskReference,
        IReadOnlyList<CommitRecord> commits,
        IReadOnlyList<TaskState> tasks,
        DateTimeOffset timestamp,
        string checkpointKind = "closure",
        int triggerScore = 0,
        IReadOnlyList<string>? triggerReasons = null,
        string? dedupeKey = null,
        string? snapshotId = null)
    {
        ArgumentNullException.ThrowIfNull(shiftState);
        ArgumentException.ThrowIfNullOrWhiteSpace(executionReference);
        ArgumentException.ThrowIfNullOrWhiteSpace(taskReference);
        ArgumentNullException.ThrowIfNull(commits);
        ArgumentNullException.ThrowIfNull(tasks);
        ArgumentException.ThrowIfNullOrWhiteSpace(checkpointKind);

        var orderedCommits = commits
            .OrderBy(static commit => commit.Timestamp)
            .ThenBy(static commit => commit.CommitId, StringComparer.Ordinal)
            .ToArray();

        var openTasks = tasks
            .Where(static task => task.Status == TaskStateStatus.Active)
            .Select(static task => new SnapshotOpenTask(task.TaskId, task.IntentState, task.Status))
            .OrderBy(static task => task.TaskId, StringComparer.Ordinal)
            .ToArray();

        var constraints = shiftState.Constraints
            .Where(static constraint => !string.IsNullOrWhiteSpace(constraint))
            .Select(static constraint => constraint.Trim())
            .Distinct(StringComparer.Ordinal)
            .OrderBy(static constraint => constraint, StringComparer.Ordinal)
            .ToArray();
        var normalizedTriggerReasons = (triggerReasons ?? Array.Empty<string>())
            .Where(static reason => !string.IsNullOrWhiteSpace(reason))
            .Select(static reason => reason.Trim())
            .Distinct(StringComparer.Ordinal)
            .OrderBy(static reason => reason, StringComparer.Ordinal)
            .ToArray();
        var normalizedSnapshotId = string.IsNullOrWhiteSpace(snapshotId)
            ? $"SNAPSHOT-{shiftState.ShiftId}-{timestamp:yyyyMMddHHmmss}"
            : snapshotId.Trim();

        return new Snapshot(
            normalizedSnapshotId,
            $"shift://{shiftState.ShiftId}",
            executionReference.Trim(),
            taskReference.Trim(),
            orderedCommits,
            openTasks,
            constraints,
            timestamp,
            checkpointKind.Trim(),
            triggerScore,
            normalizedTriggerReasons,
            string.IsNullOrWhiteSpace(dedupeKey) ? null : dedupeKey.Trim());
    }
}
