using System;
using System.Linq;
using zavod.Contexting;
using zavod.Flow;
using zavod.State;

namespace zavod.Persistence;

public sealed record LegacyResumeMigrationResult(
    ResumeStageSnapshot? Snapshot,
    bool SuppressDemoDrafts,
    bool WasMigrated);

public static class LegacyResumeStateMigrator
{
    public static LegacyResumeMigrationResult Normalize(
        ProjectState projectState,
        ShiftState? activeShift,
        ResumeStageSnapshot? snapshot)
    {
        ArgumentNullException.ThrowIfNull(projectState);

        if (snapshot is null
            || projectState.ActiveShiftId is null
            || projectState.ActiveTaskId is not null
            || activeShift is null
            || activeShift.CurrentTaskId is not null)
        {
            return new LegacyResumeMigrationResult(snapshot, SuppressDemoDrafts: false, WasMigrated: false);
        }

        var finalizedTask = activeShift.Tasks.LastOrDefault(static task => task.Status is TaskStateStatus.Completed or TaskStateStatus.Abandoned);
        if (finalizedTask?.Status != TaskStateStatus.Abandoned)
        {
            return new LegacyResumeMigrationResult(snapshot, SuppressDemoDrafts: false, WasMigrated: false);
        }

        var normalized = snapshot with
        {
            PhaseState = StepPhaseMachine.ResumeActiveShiftDiscussion(),
            IntentState = ContextIntentState.None,
            IntentSummary = string.Empty,
            IsExecutionPreflightActive = false,
            IsPreflightClarificationActive = false,
            IsResultAccepted = false,
            ExecutionRefinement = null,
            PreflightClarificationText = string.Empty,
            RevisionIntakeText = string.Empty,
            RuntimeState = null
        };

        return new LegacyResumeMigrationResult(
            normalized,
            SuppressDemoDrafts: true,
            WasMigrated: !Equals(normalized, snapshot));
    }
}
