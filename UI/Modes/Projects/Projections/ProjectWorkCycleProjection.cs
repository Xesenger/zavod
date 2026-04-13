using System;
using zavod.Flow;
using zavod.Persistence;

namespace zavod.UI.Modes.Projects.Projections;

public sealed record ProjectWorkCycleProjection(
    StepPhaseState PhaseState,
    StepPhaseProjection Projection,
    string IntentSummary,
    string PhaseLabel,
    string SummaryText,
    bool ShowSurfaceNavigation,
    string ChatSurfaceStateText,
    string ChatSummaryTitle,
    string ChatSummaryText,
    string ChatSummaryNote,
    string ExecutionSurfaceStateText,
    string ExecutionSummaryText,
    string ExecutionDetailText,
    string ExecutionEvidenceText,
    string ResultSurfaceStateText,
    string ResultSummaryText,
    string ResultDetailText,
    string ResultEvidenceText,
    string ResultMetadataText)
{
    public static ProjectWorkCycleProjection Build(string projectRoot, ProjectsShellProjection shellProjection)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(projectRoot);
        ArgumentNullException.ThrowIfNull(shellProjection);

        var normalizedRoot = System.IO.Path.GetFullPath(projectRoot);
        var hasActiveShift = !shellProjection.ActiveShiftText.EndsWith("none", StringComparison.Ordinal);
        var hasActiveTask = !shellProjection.ActiveTaskText.EndsWith("none", StringComparison.Ordinal);
        var normalizedResume = ResumeStageNormalizer.Normalize(
            ResumeStageStorage.Load(normalizedRoot),
            hasActiveWork: hasActiveTask,
            preserveLiveRuntimePhase: false,
            hasActiveShift: hasActiveShift);

        var phaseState = ResolvePhaseState(normalizedResume, hasActiveShift, hasActiveTask, shellProjection.HasCapsuleDocument);
        var projection = StepPhaseProjectionBuilder.Build(phaseState);
        var intentSummary = normalizedResume?.IntentSummary ?? string.Empty;

        return new ProjectWorkCycleProjection(
            phaseState,
            projection,
            intentSummary,
            BuildPhaseLabel(phaseState),
            $"Current phase: {BuildPhaseLabel(phaseState)}. Work Cycle follows phase-driven visibility, and the discussion/preflight owner path is partially reconnected.",
            BuildShowSurfaceNavigation(projection),
            BuildChatSurfaceStateText(phaseState),
            BuildChatSummaryTitle(phaseState),
            BuildChatSummaryText(phaseState),
            BuildChatSummaryNote(phaseState),
            BuildExecutionSurfaceStateText(phaseState, projection),
            BuildExecutionSummaryText(phaseState, projection),
            BuildExecutionDetailText(shellProjection, phaseState),
            BuildExecutionEvidenceText(shellProjection, phaseState),
            BuildResultSurfaceStateText(phaseState, projection),
            BuildResultSummaryText(phaseState, projection),
            BuildResultDetailText(shellProjection, phaseState),
            BuildResultEvidenceText(shellProjection, phaseState),
            BuildResultMetadataText(shellProjection, phaseState));
    }

    private static StepPhaseState ResolvePhaseState(
        ResumeStageSnapshot? normalizedResume,
        bool hasActiveShift,
        bool hasActiveTask,
        bool hasCapsule)
    {
        if (normalizedResume is not null)
        {
            return normalizedResume.PhaseState;
        }

        if (hasCapsule && hasActiveTask)
        {
            return new StepPhaseState(
                SurfacePhase.Execution,
                DiscussionSubphase.None,
                ExecutionSubphase.Revision,
                ResultSubphase.RevisionRequested,
                Contexting.ContextIntentState.Validated,
                HasActiveShift: true,
                HasActiveTask: true,
                HasClarification: false,
                HasReopenedContext: false);
        }

        if (hasCapsule)
        {
            return StepPhaseMachine.ResumeResult();
        }

        if (hasActiveTask)
        {
            return StepPhaseMachine.ResumeInterrupted();
        }

        if (hasActiveShift)
        {
            return StepPhaseMachine.ResumeActiveShiftDiscussion();
        }

        return StepPhaseMachine.ResumeDiscussion();
    }

    private static string BuildPhaseLabel(StepPhaseState phaseState)
    {
        return phaseState.Phase switch
        {
            SurfacePhase.Discussion when phaseState.DiscussionSubphase == DiscussionSubphase.Reopened => "Discussion / Reopened",
            SurfacePhase.Discussion when phaseState.DiscussionSubphase == DiscussionSubphase.Ready => "Discussion / Ready",
            SurfacePhase.Discussion when phaseState.DiscussionSubphase == DiscussionSubphase.Forming => "Discussion / Forming",
            SurfacePhase.Execution when phaseState.ExecutionSubphase == ExecutionSubphase.Preflight => "Execution / Preflight",
            SurfacePhase.Execution when phaseState.ExecutionSubphase == ExecutionSubphase.Running => "Execution / Running",
            SurfacePhase.Execution when phaseState.ExecutionSubphase == ExecutionSubphase.Qc => "Execution / QC",
            SurfacePhase.Execution when phaseState.ExecutionSubphase == ExecutionSubphase.Revision => "Execution / Revision",
            SurfacePhase.Execution when phaseState.ExecutionSubphase == ExecutionSubphase.Interrupted => "Execution / Interrupted",
            SurfacePhase.Result when phaseState.ResultSubphase == ResultSubphase.RevisionRequested => "Result / Revision Requested",
            SurfacePhase.Result => "Result / Ready",
            SurfacePhase.Completed => "Completed",
            _ => "Discussion / Idle"
        };
    }

    private static string BuildChatSummaryTitle(StepPhaseState phaseState)
    {
        return phaseState.Phase switch
        {
            SurfacePhase.Discussion => "Discussion",
            SurfacePhase.Execution => "Frozen Context",
            SurfacePhase.Result => "Decision Context",
            SurfacePhase.Completed => "Completed Context",
            _ => "Discussion"
        };
    }

    private static string BuildChatSurfaceStateText(StepPhaseState phaseState)
    {
        return phaseState.Phase switch
        {
            SurfacePhase.Discussion => "Primary surface",
            SurfacePhase.Execution => "Frozen context",
            SurfacePhase.Result => "Frozen context",
            SurfacePhase.Completed => "Historical context",
            _ => "Context"
        };
    }

    private static bool BuildShowSurfaceNavigation(StepPhaseProjection projection)
    {
        var visibleCount = 0;
        if (projection.ShowChat)
        {
            visibleCount++;
        }

        if (projection.ShowExecution)
        {
            visibleCount++;
        }

        if (projection.ShowResult)
        {
            visibleCount++;
        }

        return visibleCount > 1;
    }

    private static string BuildChatSummaryText(StepPhaseState phaseState)
    {
        return phaseState.Phase switch
        {
            SurfacePhase.Discussion => "Chat is the primary surface in this phase. It frames intent and keeps the project discussion visible without creating work truth on its own.",
            SurfacePhase.Execution => "Chat remains visible only as bounded context while execution becomes the active work surface.",
            SurfacePhase.Result => "Chat stays visible as frozen context while execution/result carry the active work and decision surfaces.",
            SurfacePhase.Completed => "The visible discussion context is now historical and no longer an active control surface.",
            _ => "Chat remains part of the current project context."
        };
    }

    private static string BuildChatSummaryNote(StepPhaseState phaseState)
    {
        return phaseState.Phase switch
        {
            SurfacePhase.Discussion => "Composer and agreement actions remain disconnected in this slice.",
            SurfacePhase.Execution => "Execution phase must not reintroduce free-form discussion UI as active control.",
            SurfacePhase.Result => "Result visibility must not imply lifecycle completion.",
            SurfacePhase.Completed => "Completed state must not silently erase step history.",
            _ => string.Empty
        };
    }

    private static string BuildExecutionSummaryText(StepPhaseState phaseState, StepPhaseProjection projection)
    {
        if (!projection.ShowExecution)
        {
            return "Execution surface is hidden until the phase engine reaches execution/result territory.";
        }

        return phaseState.ExecutionSubphase switch
        {
            ExecutionSubphase.Preflight => "Execution is present as a prepared shell before active work begins.",
            ExecutionSubphase.Running => "Execution is the active work surface in this phase.",
            ExecutionSubphase.Qc => "Execution remains visible while QC-related state is active.",
            ExecutionSubphase.Revision => "Execution is active again because the current task is in revision flow.",
            ExecutionSubphase.Interrupted => "Execution stays visible as an interrupted/degraded work surface.",
            _ => "Execution surface is visible because the current phase requires it."
        };
    }

    private static string BuildExecutionSurfaceStateText(StepPhaseState phaseState, StepPhaseProjection projection)
    {
        if (!projection.ShowExecution)
        {
            return "Hidden";
        }

        return phaseState.Phase switch
        {
            SurfacePhase.Execution => "Active surface",
            SurfacePhase.Result => "Reference surface",
            _ => "Visible surface"
        };
    }

    private static string BuildExecutionDetailText(ProjectsShellProjection shellProjection, StepPhaseState phaseState)
    {
        return phaseState.ExecutionSubphase switch
        {
            ExecutionSubphase.Preflight => "Preflight must not create active work truth on its own. It stays read-only in this slice.",
            ExecutionSubphase.Running => $"Task context: {shellProjection.ActiveTaskText}. Live controls remain disconnected.",
            ExecutionSubphase.Qc => $"QC-adjacent execution context remains projection-only. {shellProjection.ActiveTaskText}",
            ExecutionSubphase.Revision => $"Revision-capable execution context is visible. {shellProjection.ActiveTaskText}",
            ExecutionSubphase.Interrupted => "Interrupted execution must degrade honestly and must not pretend live running survived restart.",
            _ => "Execution details remain read-only until owner paths are re-proven."
        };
    }

    private static string BuildExecutionEvidenceText(ProjectsShellProjection shellProjection, StepPhaseState phaseState)
    {
        var phaseNote = phaseState.Phase == SurfacePhase.Result
            ? "Execution remains visible beside Result."
            : "Execution evidence remains available as context.";
        return $"{shellProjection.DocumentStageText}. {phaseNote}";
    }

    private static string BuildResultSummaryText(StepPhaseState phaseState, StepPhaseProjection projection)
    {
        if (!projection.ShowResult)
        {
            return "Result surface stays hidden until the phase engine reaches result/revision territory.";
        }

        return phaseState.ResultSubphase switch
        {
            ResultSubphase.Ready => "Result is now the active decision surface while execution remains visible as context.",
            ResultSubphase.RevisionRequested => "Result remains visible as reference because the current task is in revision flow.",
            _ => "Result surface is visible because the current phase requires it."
        };
    }

    private static string BuildResultSurfaceStateText(StepPhaseState phaseState, StepPhaseProjection projection)
    {
        if (!projection.ShowResult)
        {
            return "Hidden";
        }

        return phaseState.ResultSubphase switch
        {
            ResultSubphase.Ready => "Active decision surface",
            ResultSubphase.RevisionRequested => "Reference decision surface",
            _ => "Visible decision surface"
        };
    }

    private static string BuildResultDetailText(ProjectsShellProjection shellProjection, StepPhaseState phaseState)
    {
        return phaseState.ResultSubphase switch
        {
            ResultSubphase.Ready => $"Result remains read-only in this slice. {shellProjection.ActiveTaskText}",
            ResultSubphase.RevisionRequested => "Revision keeps the result visible, but decision hooks remain disconnected in this slice.",
            _ => "Result details remain read-only until owner paths are re-proven."
        };
    }

    private static string BuildResultEvidenceText(ProjectsShellProjection shellProjection, StepPhaseState phaseState)
    {
        var capsuleText = shellProjection.HasCapsuleDocument
            ? "Capsule/result evidence is present."
            : "Capsule/result evidence is not materialized yet.";
        var phaseText = phaseState.ResultSubphase == ResultSubphase.RevisionRequested
            ? "Result remains on screen as revision reference."
            : "Result stays a decision surface only when supported by phase truth.";
        return $"{capsuleText} {phaseText}";
    }

    private static string BuildResultMetadataText(ProjectsShellProjection shellProjection, StepPhaseState phaseState)
    {
        var phaseText = phaseState.ResultSubphase == ResultSubphase.RevisionRequested
            ? "Revision reference active."
            : phaseState.ResultSubphase == ResultSubphase.Ready
                ? "Decision review active."
                : "Decision review unavailable.";
        return $"{shellProjection.DocumentStageText} {phaseText}";
    }
}
