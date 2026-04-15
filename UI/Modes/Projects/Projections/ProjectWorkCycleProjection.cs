using System;
using zavod.Flow;
using zavod.Persistence;
using zavod.UI.Text;

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
    public static ProjectWorkCycleProjection Build(ProjectWorkCycleQueryState queryState, ProjectsShellProjection shellProjection)
    {
        ArgumentNullException.ThrowIfNull(queryState);
        ArgumentNullException.ThrowIfNull(shellProjection);

        var phaseState = ResolvePhaseState(queryState.ResumeSnapshot, queryState.HasActiveShift, queryState.HasActiveTask, queryState.HasCapsuleDocument, queryState.RuntimeStatePresent);
        var projection = StepPhaseProjectionBuilder.Build(phaseState);
        var intentSummary = queryState.ResumeSnapshot?.IntentSummary ?? string.Empty;

        return new ProjectWorkCycleProjection(
            phaseState,
            projection,
            intentSummary,
            BuildPhaseLabel(phaseState),
            AppText.Current.Format("projects.work_cycle.summary", BuildPhaseLabel(phaseState)),
            BuildShowSurfaceNavigation(projection),
            BuildChatSurfaceStateText(phaseState),
            BuildChatSummaryTitle(phaseState),
            BuildChatSummaryText(phaseState),
            BuildChatSummaryNote(phaseState),
            BuildExecutionSurfaceStateText(phaseState, projection),
            BuildExecutionSummaryText(phaseState, projection),
            BuildExecutionDetailText(queryState, phaseState),
            BuildExecutionEvidenceText(queryState, phaseState),
            BuildResultSurfaceStateText(phaseState, projection),
            BuildResultSummaryText(phaseState, projection),
            BuildResultDetailText(queryState, phaseState),
            BuildResultEvidenceText(queryState, phaseState),
            BuildResultMetadataText(queryState, phaseState));
    }

    private static StepPhaseState ResolvePhaseState(
        ResumeStageSnapshot? normalizedResume,
        bool hasActiveShift,
        bool hasActiveTask,
        bool hasCapsule,
        bool runtimeStatePresent)
    {
        if (normalizedResume is not null)
        {
            return normalizedResume.PhaseState;
        }

        _ = hasCapsule;

        if (runtimeStatePresent && hasActiveTask)
        {
            return StepPhaseMachine.ResumeInterrupted();
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
            SurfacePhase.Discussion when phaseState.DiscussionSubphase == DiscussionSubphase.Reopened => AppText.Current.Get("projects.work_cycle.phase.discussion_reopened"),
            SurfacePhase.Discussion when phaseState.DiscussionSubphase == DiscussionSubphase.Ready => AppText.Current.Get("projects.work_cycle.phase.discussion_ready"),
            SurfacePhase.Discussion when phaseState.DiscussionSubphase == DiscussionSubphase.Forming => AppText.Current.Get("projects.work_cycle.phase.discussion_forming"),
            SurfacePhase.Execution when phaseState.ExecutionSubphase == ExecutionSubphase.Preflight => AppText.Current.Get("projects.work_cycle.phase.execution_preflight"),
            SurfacePhase.Execution when phaseState.ExecutionSubphase == ExecutionSubphase.Running => AppText.Current.Get("projects.work_cycle.phase.execution_running"),
            SurfacePhase.Execution when phaseState.ExecutionSubphase == ExecutionSubphase.Qc => AppText.Current.Get("projects.work_cycle.phase.execution_qc"),
            SurfacePhase.Execution when phaseState.ExecutionSubphase == ExecutionSubphase.Revision => AppText.Current.Get("projects.work_cycle.phase.execution_revision"),
            SurfacePhase.Execution when phaseState.ExecutionSubphase == ExecutionSubphase.Interrupted => AppText.Current.Get("projects.work_cycle.phase.execution_interrupted"),
            SurfacePhase.Result when phaseState.ResultSubphase == ResultSubphase.RevisionRequested => AppText.Current.Get("projects.work_cycle.phase.result_revision_requested"),
            SurfacePhase.Result => AppText.Current.Get("projects.work_cycle.phase.result_ready"),
            SurfacePhase.Completed => AppText.Current.Get("projects.work_cycle.phase.completed"),
            _ => AppText.Current.Get("projects.work_cycle.phase.discussion_idle")
        };
    }

    private static string BuildChatSummaryTitle(StepPhaseState phaseState)
    {
        return phaseState.Phase switch
        {
            SurfacePhase.Discussion => AppText.Current.Get("projects.work_cycle.chat_title.discussion"),
            SurfacePhase.Execution => AppText.Current.Get("projects.work_cycle.chat_title.execution"),
            SurfacePhase.Result => AppText.Current.Get("projects.work_cycle.chat_title.result"),
            SurfacePhase.Completed => AppText.Current.Get("projects.work_cycle.chat_title.completed"),
            _ => AppText.Current.Get("projects.work_cycle.chat_title.discussion")
        };
    }

    private static string BuildChatSurfaceStateText(StepPhaseState phaseState)
    {
        return phaseState.Phase switch
        {
            SurfacePhase.Discussion => AppText.Current.Get("projects.work_cycle.surface.primary"),
            SurfacePhase.Execution => AppText.Current.Get("projects.work_cycle.surface.frozen"),
            SurfacePhase.Result => AppText.Current.Get("projects.work_cycle.surface.frozen"),
            SurfacePhase.Completed => AppText.Current.Get("projects.work_cycle.surface.historical"),
            _ => AppText.Current.Get("projects.work_cycle.surface.context")
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
            SurfacePhase.Discussion => AppText.Current.Get("projects.work_cycle.chat_summary.discussion"),
            SurfacePhase.Execution => AppText.Current.Get("projects.work_cycle.chat_summary.execution"),
            SurfacePhase.Result => AppText.Current.Get("projects.work_cycle.chat_summary.result"),
            SurfacePhase.Completed => AppText.Current.Get("projects.work_cycle.chat_summary.completed"),
            _ => AppText.Current.Get("projects.work_cycle.chat_summary.fallback")
        };
    }

    private static string BuildChatSummaryNote(StepPhaseState phaseState)
    {
        return phaseState.Phase switch
        {
            SurfacePhase.Discussion => AppText.Current.Get("projects.work_cycle.chat_summary_note.discussion"),
            SurfacePhase.Execution => AppText.Current.Get("projects.work_cycle.chat_summary_note.execution"),
            SurfacePhase.Result => AppText.Current.Get("projects.work_cycle.chat_summary_note.result"),
            SurfacePhase.Completed => AppText.Current.Get("projects.work_cycle.chat_summary_note.completed"),
            _ => string.Empty
        };
    }

    private static string BuildExecutionSummaryText(StepPhaseState phaseState, StepPhaseProjection projection)
    {
        if (!projection.ShowExecution)
        {
            return AppText.Current.Get("projects.work_cycle.execution_summary.hidden");
        }

        return phaseState.ExecutionSubphase switch
        {
            ExecutionSubphase.Preflight => AppText.Current.Get("projects.work_cycle.execution_summary.preflight"),
            ExecutionSubphase.Running => AppText.Current.Get("projects.work_cycle.execution_summary.running"),
            ExecutionSubphase.Qc => AppText.Current.Get("projects.work_cycle.execution_summary.qc"),
            ExecutionSubphase.Revision => AppText.Current.Get("projects.work_cycle.execution_summary.revision"),
            ExecutionSubphase.Interrupted => AppText.Current.Get("projects.work_cycle.execution_summary.interrupted"),
            _ => AppText.Current.Get("projects.work_cycle.execution_summary.visible")
        };
    }

    private static string BuildExecutionSurfaceStateText(StepPhaseState phaseState, StepPhaseProjection projection)
    {
        if (!projection.ShowExecution)
        {
            return AppText.Current.Get("projects.work_cycle.surface.hidden");
        }

        return phaseState.Phase switch
        {
            SurfacePhase.Execution => AppText.Current.Get("projects.work_cycle.surface.active"),
            SurfacePhase.Result => AppText.Current.Get("projects.work_cycle.surface.reference"),
            _ => AppText.Current.Get("projects.work_cycle.surface.visible")
        };
    }

    private static string BuildExecutionDetailText(ProjectWorkCycleQueryState queryState, StepPhaseState phaseState)
    {
        var taskText = queryState.ActiveTaskId is null
            ? AppText.Current.Format("projects.shell.active_task", AppText.Current.Get("projects.token.none"))
            : AppText.Current.Format("projects.shell.active_task", queryState.ActiveTaskId);
        return phaseState.ExecutionSubphase switch
        {
            ExecutionSubphase.Preflight => AppText.Current.Get("projects.work_cycle.execution_detail.preflight"),
            ExecutionSubphase.Running => AppText.Current.Format("projects.work_cycle.execution_detail.running", taskText),
            ExecutionSubphase.Qc => AppText.Current.Format("projects.work_cycle.execution_detail.qc", taskText),
            ExecutionSubphase.Revision => AppText.Current.Format("projects.work_cycle.execution_detail.revision", taskText),
            ExecutionSubphase.Interrupted => AppText.Current.Get("projects.work_cycle.execution_detail.interrupted"),
            _ => AppText.Current.Get("projects.work_cycle.execution_detail.fallback")
        };
    }

    private static string BuildExecutionEvidenceText(ProjectWorkCycleQueryState queryState, StepPhaseState phaseState)
    {
        var phaseNote = phaseState.Phase == SurfacePhase.Result
            ? AppText.Current.Get("projects.work_cycle.execution_evidence.result_visible")
            : AppText.Current.Get("projects.work_cycle.execution_evidence.context");
        return AppText.Current.Format(
            "projects.work_cycle.execution_evidence.format",
            queryState.ProjectDocument.Exists ? DisplayDocumentStage(queryState.ProjectDocument.Stage) : AppText.Current.Get("projects.token.missing"),
            queryState.CapsuleDocument.Exists ? DisplayDocumentStage(queryState.CapsuleDocument.Stage) : AppText.Current.Get("projects.token.missing"),
            phaseNote);
    }

    private static string BuildResultSummaryText(StepPhaseState phaseState, StepPhaseProjection projection)
    {
        if (!projection.ShowResult)
        {
            return AppText.Current.Get("projects.work_cycle.result_summary.hidden");
        }

        return phaseState.ResultSubphase switch
        {
            ResultSubphase.Ready => AppText.Current.Get("projects.work_cycle.result_summary.ready"),
            ResultSubphase.RevisionRequested => AppText.Current.Get("projects.work_cycle.result_summary.revision"),
            _ => AppText.Current.Get("projects.work_cycle.result_summary.visible")
        };
    }

    private static string BuildResultSurfaceStateText(StepPhaseState phaseState, StepPhaseProjection projection)
    {
        if (!projection.ShowResult)
        {
            return AppText.Current.Get("projects.work_cycle.surface.hidden");
        }

        return phaseState.ResultSubphase switch
        {
            ResultSubphase.Ready => AppText.Current.Get("projects.work_cycle.result_surface.active"),
            ResultSubphase.RevisionRequested => AppText.Current.Get("projects.work_cycle.result_surface.reference"),
            _ => AppText.Current.Get("projects.work_cycle.result_surface.visible")
        };
    }

    private static string BuildResultDetailText(ProjectWorkCycleQueryState queryState, StepPhaseState phaseState)
    {
        var taskText = queryState.ActiveTaskId is null
            ? AppText.Current.Format("projects.shell.active_task", AppText.Current.Get("projects.token.none"))
            : AppText.Current.Format("projects.shell.active_task", queryState.ActiveTaskId);
        return phaseState.ResultSubphase switch
        {
            ResultSubphase.Ready => AppText.Current.Format("projects.work_cycle.result_detail.ready", taskText),
            ResultSubphase.RevisionRequested => AppText.Current.Get("projects.work_cycle.result_detail.revision"),
            _ => AppText.Current.Get("projects.work_cycle.result_detail.fallback")
        };
    }

    private static string BuildResultEvidenceText(ProjectWorkCycleQueryState queryState, StepPhaseState phaseState)
    {
        var capsuleText = queryState.HasCapsuleDocument
            ? AppText.Current.Get("projects.work_cycle.result_evidence.capsule_present")
            : AppText.Current.Get("projects.work_cycle.result_evidence.capsule_missing");
        var phaseText = phaseState.ResultSubphase == ResultSubphase.RevisionRequested
            ? AppText.Current.Get("projects.work_cycle.result_evidence.revision_reference")
            : AppText.Current.Get("projects.work_cycle.result_evidence.decision_surface");
        return $"{capsuleText} {phaseText}";
    }

    private static string BuildResultMetadataText(ProjectWorkCycleQueryState queryState, StepPhaseState phaseState)
    {
        var phaseText = phaseState.ResultSubphase == ResultSubphase.RevisionRequested
            ? AppText.Current.Get("projects.work_cycle.result_metadata.revision")
            : phaseState.ResultSubphase == ResultSubphase.Ready
                ? AppText.Current.Get("projects.work_cycle.result_metadata.ready")
                : AppText.Current.Get("projects.work_cycle.result_metadata.unavailable");
        return AppText.Current.Format("projects.work_cycle.result_metadata.format", DisplayDocumentStage(queryState.DocumentStage), phaseText);
    }

    private static string DisplayDocumentStage(ProjectDocumentStage stage)
    {
        return stage switch
        {
            ProjectDocumentStage.ImportPreview => AppText.Current.Get("projects.enum.document_stage.import_preview"),
            ProjectDocumentStage.PreviewDocs => AppText.Current.Get("projects.enum.document_stage.preview_docs"),
            ProjectDocumentStage.CanonicalDocs => AppText.Current.Get("projects.enum.document_stage.canonical_docs"),
            _ => stage.ToString()
        };
    }
}
