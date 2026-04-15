using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using zavod.Contexting;
using zavod.Persistence;
using zavod.State;

namespace zavod.Bootstrap;

public static class ValidatedIntentShiftStarter
{
    public static FirstShiftBootstrapResult Start(
        ProjectState projectState,
        TaskIntent intent,
        DateTimeOffset timestamp,
        IReadOnlyList<string>? scope = null,
        IReadOnlyList<string>? acceptanceCriteria = null)
    {
        ArgumentNullException.ThrowIfNull(projectState);
        ArgumentNullException.ThrowIfNull(intent);

        if (intent.Status != ContextIntentState.Validated)
        {
            throw new InvalidOperationException("Shift start requires validated intent.");
        }

        if (projectState.ActiveShiftId is not null)
        {
            throw new InvalidOperationException("Shift start requires project without active shift.");
        }

        var shiftsRoot = Path.Combine(projectState.Paths.ZavodRoot, "shifts");
        var shiftFiles = Directory.GetFiles(shiftsRoot, "*.json");
        if (shiftFiles.Length == 0)
        {
            return FirstShiftBootstrap.Create(
                projectState,
                new FirstShiftBootstrapRequest(
                    intent.Description,
                    intent.Description,
                    timestamp,
                    scope,
                    acceptanceCriteria));
        }

        return StartNextShift(projectState, intent, timestamp, shiftFiles, scope, acceptanceCriteria);
    }

    private static FirstShiftBootstrapResult StartNextShift(
        ProjectState projectState,
        TaskIntent intent,
        DateTimeOffset timestamp,
        string[] shiftFiles,
        IReadOnlyList<string>? scope,
        IReadOnlyList<string>? acceptanceCriteria)
    {
        var shiftId = BuildNextShiftId(shiftFiles);
        var shift = new ShiftState(
            shiftId,
            intent.Description,
            null,
            ShiftStateStatus.Active,
            Array.Empty<TaskState>(),
            Array.Empty<string>(),
            Array.Empty<string>(),
            Array.Empty<string>());

        var applied = ValidatedIntentTaskApplier.Apply(
            projectState,
            shift,
            intent,
            "TASK-001",
            timestamp,
            scope: scope,
            acceptanceCriteria: acceptanceCriteria);

        var persistedProjectState = ProjectStateStorage.Save(applied.ProjectState);
        var shiftFilePath = ShiftStateStorage.Save(persistedProjectState.Paths.ProjectRoot, applied.ShiftState);

        return new FirstShiftBootstrapResult(
            persistedProjectState,
            applied.ShiftState,
            applied.Intent,
            applied.Task,
            shiftFilePath);
    }

    private static string BuildNextShiftId(string[] shiftFiles)
    {
        var nextNumber = shiftFiles
            .Select(Path.GetFileNameWithoutExtension)
            .Where(static name => name is not null && name.StartsWith("SHIFT-", StringComparison.Ordinal))
            .Select(static name =>
            {
                var suffix = name!["SHIFT-".Length..];
                return int.TryParse(suffix, out var number) ? number : 0;
            })
            .DefaultIfEmpty(0)
            .Max() + 1;

        return $"SHIFT-{nextNumber:D3}";
    }
}
