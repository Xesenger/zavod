using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using zavod.Boundary;
using zavod.Persistence;
using zavod.Traceing;
using TraceSnapshotBuilder = zavod.Traceing.SnapshotBuilder;

namespace zavod.State;

public sealed record CheckpointSnapshotWriteResult(
    bool WasCreated,
    Snapshot Snapshot,
    string SnapshotFilePath);

public static class CheckpointSnapshotProcessor
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public static CheckpointSnapshotWriteResult WriteSoftCheckpoint(
        ProjectState projectState,
        ShiftState shiftState,
        AcceptedResult acceptedResult,
        CommitRecord commitRecord,
        int triggerScore,
        IReadOnlyList<string> triggerReasons)
    {
        ArgumentNullException.ThrowIfNull(projectState);
        ArgumentNullException.ThrowIfNull(shiftState);
        ArgumentNullException.ThrowIfNull(acceptedResult);
        ArgumentNullException.ThrowIfNull(commitRecord);
        ArgumentNullException.ThrowIfNull(triggerReasons);

        if (!string.Equals(projectState.ActiveShiftId, shiftState.ShiftId, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Soft checkpoint snapshot requires active project shift to match target shift.");
        }

        if (shiftState.Status != ShiftStateStatus.Active)
        {
            throw new InvalidOperationException("Soft checkpoint snapshot requires active shift truth.");
        }

        var finalizedTask = shiftState.Tasks
            .LastOrDefault(task => string.Equals(task.TaskId, acceptedResult.TaskId, StringComparison.Ordinal))
            ?? throw new InvalidOperationException("Soft checkpoint snapshot requires accepted task to remain present in shift truth.");

        if (finalizedTask.Status != TaskStateStatus.Completed)
        {
            throw new InvalidOperationException("Soft checkpoint snapshot requires completed accepted task truth.");
        }

        var dedupeKey = BuildDedupeKey(acceptedResult.AcceptedResultId);
        Console.WriteLine(
            $"[SOFT_ENTER] acceptedResultId={acceptedResult.AcceptedResultId}; triggerScore={triggerScore}; triggerReasons={string.Join(",", triggerReasons)}");
        var normalizedReasons = triggerReasons
            .Where(static reason => !string.IsNullOrWhiteSpace(reason))
            .Select(static reason => reason.Trim())
            .Distinct(StringComparer.Ordinal)
            .OrderBy(static reason => reason, StringComparer.Ordinal)
            .ToArray();
        var snapshotId = BuildSnapshotId(shiftState.ShiftId, acceptedResult.AcceptedResultId);
        var snapshotsRoot = Path.Combine(projectState.Paths.ZavodRoot, "snapshots");
        Directory.CreateDirectory(snapshotsRoot);
        var snapshotFilePath = Path.Combine(snapshotsRoot, $"{snapshotId}.json");

        if (File.Exists(snapshotFilePath))
        {
            Console.WriteLine($"[SOFT_DEDUPE] dedupeKey={dedupeKey}; isDuplicate=true");
            var persisted = ReadSnapshot(snapshotFilePath);
            if (!string.Equals(persisted.DedupeKey, dedupeKey, StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Existing soft checkpoint snapshot file must preserve dedupe key identity.");
            }

            return new CheckpointSnapshotWriteResult(false, persisted, snapshotFilePath);
        }

        Console.WriteLine($"[SOFT_DEDUPE] dedupeKey={dedupeKey}; isDuplicate=false");

        var snapshot = TraceSnapshotBuilder.BuildSnapshot(
            shiftState,
            $"execution://shift/{shiftState.ShiftId}/accepted-result/{acceptedResult.AcceptedResultId}/commit/{commitRecord.CommitId}",
            $"task://{finalizedTask.TaskId}",
            new[] { commitRecord },
            shiftState.Tasks,
            acceptedResult.AcceptedAt,
            checkpointKind: "soft",
            triggerScore: triggerScore,
            triggerReasons: normalizedReasons,
            dedupeKey: dedupeKey,
            snapshotId: snapshotId);

        var writtenPath = SnapshotStorage.Save(projectState.Paths.ProjectRoot, snapshot);
        Console.WriteLine(
            $"[SOFT_WRITE] checkpointKind={snapshot.CheckpointKind}; snapshotId={snapshot.SnapshotId}; dedupeKey={snapshot.DedupeKey}");
        return new CheckpointSnapshotWriteResult(true, snapshot, writtenPath);
    }

    public static string BuildDedupeKey(string acceptedResultId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(acceptedResultId);
        return $"soft-checkpoint:{acceptedResultId.Trim()}";
    }

    private static string BuildSnapshotId(string shiftId, string acceptedResultId)
    {
        var normalizedShiftId = shiftId.Trim();
        var suffix = acceptedResultId
            .Trim()
            .Replace(':', '-')
            .Replace('/', '-')
            .Replace('\\', '-');
        return $"SNAPSHOT-{normalizedShiftId}-SOFT-{suffix}";
    }

    private static Snapshot ReadSnapshot(string snapshotFilePath)
    {
        var serialized = File.ReadAllText(snapshotFilePath);
        return JsonSerializer.Deserialize<Snapshot>(serialized, JsonOptions)
            ?? throw new InvalidOperationException("Existing checkpoint snapshot file must deserialize correctly.");
    }
}
