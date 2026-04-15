using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using zavod.Bootstrap;
using zavod.Contexting;
using zavod.Execution;
using zavod.Flow;
using zavod.Persistence;
using zavod.UI.Modes.Projects.Projections;
using zavod.UI.Rendering.Conversation;
using zavod.UI.Text;
using zavod.State;

namespace zavod.UI.Modes.Projects.WorkCycle.Actions;

internal sealed class WorkCycleActionController
{

    private readonly string _projectRoot;
    private readonly Func<ProjectsAdapter> _getProjectsAdapter;
    private readonly Func<Task> _refreshShellAsync;
    private readonly Action _updateDiscussionPreview;
    private readonly ProjectSageService _sage = new();

    private sealed record ActiveExecutionContext(
        ProjectState ProjectState,
        ShiftState ShiftState,
        TaskState TaskState);

    private sealed record ProjectExecutionInput(
        string Text,
        IReadOnlyList<OpenRouterAttachment> Attachments)
    {
        public string PromptText => ConversationAttachmentPromptBuilder.BuildUserPrompt(Text, Attachments);
    }

    public WorkCycleActionController(
        string projectRoot,
        Func<ProjectsAdapter> getProjectsAdapter,
        Func<Task> refreshShellAsync,
        Action updateDiscussionPreview)
    {
        _projectRoot = projectRoot ?? throw new ArgumentNullException(nameof(projectRoot));
        _getProjectsAdapter = getProjectsAdapter ?? throw new ArgumentNullException(nameof(getProjectsAdapter));
        _refreshShellAsync = refreshShellAsync ?? throw new ArgumentNullException(nameof(refreshShellAsync));
        _updateDiscussionPreview = updateDiscussionPreview ?? throw new ArgumentNullException(nameof(updateDiscussionPreview));
    }

    private ProjectsAdapter ProjectsAdapter => _getProjectsAdapter();

    public async Task<bool> SendProjectsMessageAsync(string text)
    {
        return await SendProjectsMessageAsync(new ConversationComposerSubmission(
            string.Empty,
            text?.Trim() ?? string.Empty,
            Array.Empty<ConversationComposerDraftItem>()));
    }

    public async Task<bool> SendProjectsMessageAsync(ConversationComposerSubmission submission)
    {
        ArgumentNullException.ThrowIfNull(submission);

        var normalizedText = submission.Text?.Trim();
        if (string.IsNullOrWhiteSpace(normalizedText))
        {
            return false;
        }

        var executionInput = new ProjectExecutionInput(
            normalizedText,
            ConversationAttachmentPromptBuilder.Load(submission.Attachments));
        var context = BuildContext();
        if (context.WorkCycle.PhaseState.Phase == SurfacePhase.Execution
            && context.WorkCycle.PhaseState.ExecutionSubphase == ExecutionSubphase.Revision
            && context.WorkCycle.PhaseState.ResultSubphase == ResultSubphase.RevisionRequested
            && context.QueryState.ResumeSnapshot?.RuntimeState is not null)
        {
            await ProjectsAdapter.AddMessageAsync(
                ConversationItemKind.User,
                AppText.Current.Get("role.user"),
                normalizedText,
                metadata: BuildProjectConversationMetadata("execution", context.QueryState.ActiveTaskId));

            var workerAdvisory = _sage.BuildWorkerAdvisory(context.QueryState.ProjectRoot, context.QueryState.ProjectId, executionInput.PromptText);
            var runtime = context.QueryState.ResumeSnapshot.RuntimeState;
            runtime = ExecutionRuntimeController.ProduceResult(runtime);
            runtime = ExecutionRuntimeController.RequestQcReview(runtime);
            runtime = ExecutionRuntimeController.AcceptQcReview(runtime);

            await ProjectsAdapter.AddMessageAsync(
                ConversationItemKind.Status,
                AppText.Current.Get("role.worker"),
                AppText.Current.Get("projects.message.revision_input_received"),
                metadata: BuildProjectConversationMetadata("execution", context.QueryState.ActiveTaskId));
            await ProjectsAdapter.AddLogAsync(
                AppText.Current.Get("role.worker"),
                BuildWorkerLog(context.QueryState, runtime, workerAdvisory, executionInput.Attachments),
                preview: AppText.Current.Get("projects.message.revision_log_prepared"),
                metadata: BuildProjectConversationMetadata("execution", context.QueryState.ActiveTaskId));
            await ProjectsAdapter.AddArtifactAsync(
                AppText.Current.Get("role.worker"),
                AppText.Current.Get("conversation.execution_brief_label"),
                BuildWorkerArtifact(context.QueryState, runtime, workerAdvisory, executionInput.Attachments),
                "md",
                preview: AppText.Current.Get("projects.message.revision_execution_brief_prepared"),
                metadata: BuildProjectConversationMetadata("execution", context.QueryState.ActiveTaskId));
            await ProjectsAdapter.AddMessageAsync(
                ConversationItemKind.Status,
                AppText.Current.Get("role.qc"),
                AppText.Current.Get("projects.message.revision_accepted"),
                metadata: BuildProjectConversationMetadata("result", context.QueryState.ActiveTaskId));

            var restarted = StepPhaseMachine.StartRevisionCycle(context.WorkCycle.PhaseState);
            var qcState = StepPhaseMachine.MoveToQc(restarted);
            var resultReady = StepPhaseMachine.AcceptQc(qcState);
            SaveWorkCycleSnapshot(
                context.QueryState.ProjectRoot,
                resultReady,
                normalizedText,
                isClarificationActive: false,
                clarificationDraft: string.Empty,
                runtimeState: runtime);
            _updateDiscussionPreview();
            await _refreshShellAsync();
            return true;
        }

        if (context.WorkCycle.PhaseState.Phase != SurfacePhase.Discussion)
        {
            return false;
        }

            await ProjectsAdapter.AddMessageAsync(
                ConversationItemKind.User,
                AppText.Current.Get("role.user"),
                normalizedText,
                metadata: BuildProjectConversationMetadata("discussion", context.QueryState.ActiveTaskId));

        var baseState = context.WorkCycle.PhaseState;
        StepPhaseState nextState;
        string leadReply;
        string intentSummary = context.WorkCycle.IntentSummary;

        if (OrientationIntentDetector.ShouldHandleAsOrientation(normalizedText))
        {
            nextState = StepPhaseMachine.RecordIntent(baseState, ContextIntentState.Orientation);
            leadReply = OrientationIntentResponder.Respond(normalizedText);
        }
        else
        {
            var classification = ProductIntentClassifier.Classify(normalizedText);
            nextState = StepPhaseMachine.RecordIntent(baseState, classification.FinalState);
            intentSummary = classification.FinalState == ContextIntentState.ReadyForValidation
                ? BuildIntentSummary(normalizedText)
                : intentSummary;
            leadReply = classification.FinalState switch
            {
                ContextIntentState.ReadyForValidation => AppText.Current.Get("projects.message.agreement_ready"),
                ContextIntentState.Candidate or ContextIntentState.Refining => AppText.Current.Get("projects.message.agreement_refining"),
                _ => AppText.Current.Get("projects.message.agreement_continue")
            };
        }

        var leadAdvisory = _sage.BuildLeadAdvisory(context.QueryState.ProjectRoot, context.QueryState.ProjectId, executionInput.PromptText);
        await ProjectsAdapter.AddMessageAsync(
            ConversationItemKind.Lead,
            AppText.Current.Get("role.shift_lead"),
            leadAdvisory.HasNotes
                ? $"{leadReply}{Environment.NewLine}{Environment.NewLine}{leadAdvisory.BuildLeadContextBlock()}"
                : leadReply,
            metadata: BuildProjectConversationMetadata("discussion", context.QueryState.ActiveTaskId));

        SaveWorkCycleSnapshot(context.QueryState.ProjectRoot, nextState, intentSummary, isClarificationActive: false, clarificationDraft: string.Empty);
        _updateDiscussionPreview();
        await _refreshShellAsync();
        return true;
    }

    public async Task<bool> EnterWorkAsync()
    {
        var context = BuildContext();
        if (!context.WorkCycle.Projection.CanStartIntentValidation)
        {
            return false;
        }

        var nextState = context.WorkCycle.PhaseState.HasActiveTask
            ? StepPhaseMachine.EnterReopenedPreflight(context.WorkCycle.PhaseState)
            : context.WorkCycle.PhaseState.HasActiveShift
                ? StepPhaseMachine.EnterActiveShiftPreflight(context.WorkCycle.PhaseState)
                : StepPhaseMachine.EnterPreflight(context.WorkCycle.PhaseState);

        await ProjectsAdapter.AddMessageAsync(
            ConversationItemKind.Status,
            AppText.Current.Get("role.qc"),
            AppText.Current.Get("projects.message.preflight_opened"),
            metadata: BuildProjectConversationMetadata("execution", context.QueryState.ActiveTaskId));

        SaveWorkCycleSnapshot(context.QueryState.ProjectRoot, nextState, context.WorkCycle.IntentSummary, isClarificationActive: false, clarificationDraft: string.Empty);
        await _refreshShellAsync();
        return true;
    }

    public async Task<bool> ConfirmPreflightAsync()
    {
        var context = BuildContext();
        if (context.WorkCycle.PhaseState.Phase != SurfacePhase.Execution || context.WorkCycle.PhaseState.ExecutionSubphase != ExecutionSubphase.Preflight)
        {
            return false;
        }

        MaterializeValidatedIntentIfNeeded(context.QueryState, context.WorkCycle.IntentSummary);

        var refreshedContext = BuildContext();
        var executionContext = LoadActiveExecutionContext(refreshedContext.QueryState);
        var runtime = ExecutionRuntimeController.Begin(executionContext.TaskState, executionContext.ShiftState);
        runtime = ExecutionRuntimeController.ProduceResult(runtime);
        runtime = ExecutionRuntimeController.RequestQcReview(runtime);
        runtime = ExecutionRuntimeController.AcceptQcReview(runtime);

        var runningState = StepPhaseMachine.ConfirmPreflight(context.WorkCycle.PhaseState);
        var qcState = StepPhaseMachine.MoveToQc(runningState);
        var resultReadyState = StepPhaseMachine.AcceptQc(qcState);
        var executionMetadata = BuildProjectConversationMetadata("execution", refreshedContext.QueryState.ActiveTaskId);
        var workerAdvisory = _sage.BuildWorkerAdvisory(
            refreshedContext.QueryState.ProjectRoot,
            refreshedContext.QueryState.ProjectId,
            string.IsNullOrWhiteSpace(refreshedContext.WorkCycle.IntentSummary)
                ? executionContext.TaskState.Description
                : refreshedContext.WorkCycle.IntentSummary);

        await ProjectsAdapter.AddMessageAsync(
            ConversationItemKind.Status,
            AppText.Current.Get("role.qc"),
            AppText.Current.Get("projects.message.preflight_confirmed"),
            metadata: executionMetadata);
        if (workerAdvisory.BuildWorkerWarning() is string warning && !string.IsNullOrWhiteSpace(warning))
        {
            await ProjectsAdapter.AddMessageAsync(
                ConversationItemKind.Status,
                AppText.Current.Get("role.worker"),
                AppText.Current.Format("projects.message.execution_guard", warning),
                metadata: executionMetadata);
        }

        await ProjectsAdapter.AddMessageAsync(
            ConversationItemKind.Status,
            AppText.Current.Get("role.worker"),
            AppText.Current.Get("projects.message.worker_working"),
            metadata: executionMetadata);
        await ProjectsAdapter.AddLogAsync(
            AppText.Current.Get("role.worker"),
            BuildWorkerLog(refreshedContext.QueryState, runtime, workerAdvisory),
            preview: AppText.Current.Get("projects.message.worker_log_prepared"),
            metadata: executionMetadata);
        await ProjectsAdapter.AddArtifactAsync(
            AppText.Current.Get("role.worker"),
            AppText.Current.Get("conversation.execution_brief_label"),
            BuildWorkerArtifact(refreshedContext.QueryState, runtime, workerAdvisory),
            "md",
            preview: AppText.Current.Get("projects.message.execution_brief_prepared"),
            metadata: executionMetadata);
        await ProjectsAdapter.AddMessageAsync(
            ConversationItemKind.Status,
            AppText.Current.Get("role.qc"),
            AppText.Current.Get("projects.message.qc_accepted_produced_result"),
            metadata: BuildProjectConversationMetadata("result", refreshedContext.QueryState.ActiveTaskId));

        SaveWorkCycleSnapshot(
            refreshedContext.QueryState.ProjectRoot,
            resultReadyState,
            refreshedContext.WorkCycle.IntentSummary,
            isClarificationActive: false,
            clarificationDraft: string.Empty,
            runtimeState: runtime);
        await _refreshShellAsync();
        return true;
    }

    public async Task<bool> BeginClarificationAsync(string clarificationDraft)
    {
        var context = BuildContext();
        if (context.WorkCycle.PhaseState.Phase != SurfacePhase.Execution || context.WorkCycle.PhaseState.ExecutionSubphase != ExecutionSubphase.Preflight)
        {
            return false;
        }

        var clarified = StepPhaseMachine.ApplyClarification(context.WorkCycle.PhaseState);
        await ProjectsAdapter.AddMessageAsync(
            ConversationItemKind.Status,
            AppText.Current.Get("role.qc"),
            AppText.Current.Get("projects.message.clarification_requested"),
            metadata: BuildProjectConversationMetadata("execution", context.QueryState.ActiveTaskId));
        SaveWorkCycleSnapshot(
            context.QueryState.ProjectRoot,
            clarified,
            context.WorkCycle.IntentSummary,
            isClarificationActive: true,
            clarificationDraft: clarificationDraft ?? string.Empty,
            runtimeState: context.QueryState.ResumeSnapshot?.RuntimeState);
        await _refreshShellAsync();
        return true;
    }

    public async Task<bool> ApplyClarificationAsync(string text)
    {
        var normalizedText = text?.Trim();
        if (string.IsNullOrWhiteSpace(normalizedText))
        {
            return false;
        }

        var context = BuildContext();
        if (context.WorkCycle.PhaseState.ExecutionSubphase != ExecutionSubphase.Preflight)
        {
            return false;
        }

        await ProjectsAdapter.AddMessageAsync(
            ConversationItemKind.User,
            AppText.Current.Get("role.user"),
            normalizedText,
            metadata: BuildProjectConversationMetadata("discussion", context.QueryState.ActiveTaskId));

        var updatedSummary = BuildIntentSummary(normalizedText);
        await ProjectsAdapter.AddMessageAsync(
            ConversationItemKind.Lead,
            AppText.Current.Get("role.shift_lead"),
            AppText.Current.Format("projects.message.clarification_accepted", updatedSummary),
            metadata: BuildProjectConversationMetadata("discussion", context.QueryState.ActiveTaskId));

        var clarified = StepPhaseMachine.ApplyClarification(context.WorkCycle.PhaseState);
        SaveWorkCycleSnapshot(
            context.QueryState.ProjectRoot,
            clarified,
            updatedSummary,
            isClarificationActive: false,
            clarificationDraft: string.Empty,
            runtimeState: context.QueryState.ResumeSnapshot?.RuntimeState);
        _updateDiscussionPreview();
        await _refreshShellAsync();
        return true;
    }

    public async Task<bool> CancelClarificationAsync()
    {
        var context = BuildContext();
        SaveWorkCycleSnapshot(
            context.QueryState.ProjectRoot,
            context.WorkCycle.PhaseState,
            context.WorkCycle.IntentSummary,
            isClarificationActive: false,
            clarificationDraft: string.Empty,
            runtimeState: context.QueryState.ResumeSnapshot?.RuntimeState);
        await _refreshShellAsync();
        return true;
    }

    public async Task<bool> ReturnToChatAsync()
    {
        var context = BuildContext();
        if (context.WorkCycle.PhaseState.ExecutionSubphase != ExecutionSubphase.Preflight)
        {
            return false;
        }

        var returned = StepPhaseMachine.CancelPreflight(context.WorkCycle.PhaseState);
        await ProjectsAdapter.AddMessageAsync(
            ConversationItemKind.Status,
            AppText.Current.Get("role.qc"),
            AppText.Current.Get("projects.message.returned_to_chat"),
            metadata: BuildProjectConversationMetadata("discussion", context.QueryState.ActiveTaskId));
        SaveWorkCycleSnapshot(context.QueryState.ProjectRoot, returned, context.WorkCycle.IntentSummary, isClarificationActive: false, clarificationDraft: string.Empty);
        await _refreshShellAsync();
        return true;
    }

    public async Task<bool> AcceptResultAsync()
    {
        var context = BuildContext();
        if (context.WorkCycle.PhaseState.Phase != SurfacePhase.Result || context.QueryState.ResumeSnapshot?.RuntimeState is null)
        {
            return false;
        }

        var executionContext = LoadActiveExecutionContext(context.QueryState);
        var applied = AcceptedResultApplyProcessor.Apply(
            executionContext.ProjectState,
            executionContext.ShiftState,
            executionContext.TaskState,
            context.QueryState.ResumeSnapshot.RuntimeState,
            DateTimeOffset.Now);

        await ProjectsAdapter.AddMessageAsync(
            ConversationItemKind.Status,
            AppText.Current.Get("role.qc"),
            AppText.Current.Format("projects.message.accepted_result_applied", applied.CommitRecord.CommitId),
            metadata: BuildProjectConversationMetadata("discussion", applied.TaskState.TaskId));

        SaveWorkCycleSnapshot(
            context.QueryState.ProjectRoot,
            StepPhaseMachine.ResumeActiveShiftDiscussion(),
            string.Empty,
            isClarificationActive: false,
            clarificationDraft: string.Empty,
            runtimeState: null,
            isResultAccepted: true);
        await _refreshShellAsync();
        return true;
    }

    public async Task<bool> RequestRevisionAsync()
    {
        var context = BuildContext();
        if (context.WorkCycle.PhaseState.Phase != SurfacePhase.Result || context.QueryState.ResumeSnapshot?.RuntimeState is null)
        {
            return false;
        }

        var runtime = ExecutionRuntimeController.RestartCompletedResultForRevision(
            context.QueryState.ResumeSnapshot.RuntimeState,
            AppText.Current.Get("projects.message.default_revision_reason"));
        var revisionState = StepPhaseMachine.ReturnForRevision(context.WorkCycle.PhaseState);

        await ProjectsAdapter.AddMessageAsync(
            ConversationItemKind.Status,
            AppText.Current.Get("role.qc"),
            AppText.Current.Get("projects.message.revision_requested"),
            metadata: BuildProjectConversationMetadata("execution", context.QueryState.ActiveTaskId));

        SaveWorkCycleSnapshot(
            context.QueryState.ProjectRoot,
            revisionState,
            context.WorkCycle.IntentSummary,
            isClarificationActive: false,
            clarificationDraft: string.Empty,
            runtimeState: runtime,
            revisionIntakeText: AppText.Current.Get("projects.message.default_revision_reason"));
        await _refreshShellAsync();
        return true;
    }

    public async Task<bool> RejectResultAsync()
    {
        var context = BuildContext();
        if (context.WorkCycle.PhaseState.Phase != SurfacePhase.Result)
        {
            return false;
        }

        var executionContext = LoadActiveExecutionContext(context.QueryState);
        var abandoned = ResultAbandonProcessor.Abandon(
            executionContext.ProjectState,
            executionContext.ShiftState,
            executionContext.TaskState,
            DateTimeOffset.Now);

        await ProjectsAdapter.AddMessageAsync(
            ConversationItemKind.Status,
            AppText.Current.Get("role.qc"),
            AppText.Current.Format("projects.message.result_rejected", abandoned.TaskState.TaskId),
            metadata: BuildProjectConversationMetadata("discussion", abandoned.TaskState.TaskId));

        SaveWorkCycleSnapshot(
            context.QueryState.ProjectRoot,
            StepPhaseMachine.AbandonResult(context.WorkCycle.PhaseState),
            string.Empty,
            isClarificationActive: false,
            clarificationDraft: string.Empty,
            runtimeState: null);
        await _refreshShellAsync();
        return true;
    }

    internal static Dictionary<string, string> BuildProjectConversationMetadata(string phase, string? activeTaskId)
    {
        var metadata = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["mode"] = "project",
            ["phase"] = phase
        };

        if (!string.IsNullOrWhiteSpace(activeTaskId))
        {
            metadata["step-id"] = activeTaskId.Trim();
        }

        return metadata;
    }

    internal static string BuildIntentSummary(string text)
    {
        return string.IsNullOrWhiteSpace(text) ? string.Empty : text.Trim();
    }

    internal static string BuildAgreementItemsText(string summary)
    {
        if (string.IsNullOrWhiteSpace(summary))
        {
            return AppText.Current.Get("projects.message.agreement_not_formed");
        }

        var parts = summary
            .Split(new[] { '.', ';', '\n' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(part => part.Trim())
            .Where(part => part.Length > 0)
            .Take(3)
            .ToList();

        if (parts.Count == 0)
        {
            parts.Add(summary.Trim());
        }

        return string.Join(Environment.NewLine, parts.Select(part => $"- {part}"));
    }

    internal static void SaveWorkCycleSnapshot(
        string normalizedRoot,
        StepPhaseState phaseState,
        string intentSummary,
        bool isClarificationActive,
        string clarificationDraft,
        ExecutionRuntimeState? runtimeState = null,
        bool isResultAccepted = false,
        string revisionIntakeText = "")
    {
        ResumeStageStorage.Save(
            normalizedRoot,
            new ResumeStageSnapshot(
                Version: "1.0",
                PhaseState: phaseState,
                IntentState: phaseState.IntentState,
                IntentSummary: intentSummary,
                IsExecutionPreflightActive: phaseState.Phase == SurfacePhase.Execution && phaseState.ExecutionSubphase == ExecutionSubphase.Preflight,
                IsPreflightClarificationActive: isClarificationActive,
                IsResultAccepted: isResultAccepted,
                ExecutionRefinement: BuildAgreementItemsText(intentSummary),
                PreflightClarificationText: clarificationDraft,
                RevisionIntakeText: revisionIntakeText,
                RuntimeState: runtimeState,
                DemoState: null));
    }

    internal static bool IsPreflightClarificationActive(string normalizedRoot)
    {
        return ResumeStageStorage.Load(normalizedRoot)?.IsPreflightClarificationActive ?? false;
    }

    internal static void MaterializeValidatedIntentIfNeeded(ProjectWorkCycleQueryState queryState, string intentSummary)
    {
        var projectState = queryState.ProjectState;
        if (projectState.ActiveTaskId is not null)
        {
            return;
        }

        var intent = TaskIntentFactory
            .CreateCandidate(string.IsNullOrWhiteSpace(intentSummary) ? AppText.Current.Get("projects.message.recovered_task_from_validation") : intentSummary)
            .MarkReadyForValidation()
            .Validate();

        if (projectState.ActiveShiftId is null)
        {
            _ = ValidatedIntentShiftStarter.Start(
                projectState,
                intent,
                DateTimeOffset.Now,
                scope: BuildTaskScope(queryState),
                acceptanceCriteria: BuildTaskAcceptanceCriteria(queryState));
            return;
        }

        var shift = ShiftStateStorage.Load(queryState.ProjectRoot, projectState.ActiveShiftId);
        var nextTaskId = BuildNextTaskId(shift);
        var applied = ValidatedIntentTaskApplier.Apply(
            projectState,
            shift,
            intent,
            nextTaskId,
            DateTimeOffset.Now,
            scope: BuildTaskScope(queryState),
            acceptanceCriteria: BuildTaskAcceptanceCriteria(queryState));
        var persistedProjectState = ProjectStateStorage.Save(applied.ProjectState);
        _ = ShiftStateStorage.Save(persistedProjectState.Paths.ProjectRoot, applied.ShiftState);
    }

    internal static string BuildNextTaskId(ShiftState shiftState)
    {
        var nextNumber = shiftState.Tasks
            .Select(task => task.TaskId)
            .Where(taskId => taskId.StartsWith("TASK-", StringComparison.Ordinal))
            .Select(taskId => int.TryParse(taskId["TASK-".Length..], out var value) ? value : 0)
            .DefaultIfEmpty(0)
            .Max() + 1;
        return $"TASK-{nextNumber:D3}";
    }

    private (ProjectWorkCycleQueryState QueryState, ProjectsShellProjection ShellProjection, ProjectWorkCycleProjection WorkCycle) BuildContext()
    {
        var normalizedRoot = Path.GetFullPath(_projectRoot);
        var queryState = ProjectWorkCycleQueryStateBuilder.Build(normalizedRoot);
        var shellProjection = ProjectsShellProjection.Build(queryState);
        var workCycle = ProjectWorkCycleProjection.Build(queryState, shellProjection);
        return (queryState, shellProjection, workCycle);
    }

    private static ActiveExecutionContext LoadActiveExecutionContext(ProjectWorkCycleQueryState queryState)
    {
        var projectState = ProjectStateStorage.Load(queryState.ProjectRoot);
        var resume = ActiveShiftResume.Resume(projectState);
        return new ActiveExecutionContext(resume.ProjectState, resume.ShiftState, resume.TaskState);
    }

    private static IReadOnlyList<string> BuildTaskScope(ProjectWorkCycleQueryState queryState)
    {
        if (queryState.HasProjectDocument && !string.IsNullOrWhiteSpace(queryState.ProjectDocument.Path))
        {
            return new[] { queryState.ProjectDocument.Path };
        }

        return new[] { queryState.ProjectRoot };
    }

    private static IReadOnlyList<string> BuildTaskAcceptanceCriteria(ProjectWorkCycleQueryState queryState)
    {
        return new[]
        {
            AppText.Current.Format("projects.message.acceptance.project", queryState.ProjectName),
            AppText.Current.Get("projects.message.acceptance.qc"),
            AppText.Current.Format("projects.message.acceptance.document_stage", queryState.DocumentStage)
        };
    }

    private static string BuildWorkerLog(
        ProjectWorkCycleQueryState queryState,
        ExecutionRuntimeState runtime,
        ProjectSageAdvisory advisory,
        IReadOnlyList<OpenRouterAttachment>? attachments = null)
    {
        var lines = new List<string>
        {
            $"runtime.session={runtime.Session.SessionId}",
            $"runtime.state={runtime.Session.State}",
            $"project={queryState.ProjectName}",
            $"root={queryState.ProjectRoot}",
            $"task={runtime.Task.TaskId}",
            $"task.description={runtime.Task.Description}",
            $"result.id={runtime.Result?.ResultId ?? "none"}",
            $"result.summary={runtime.Result?.Summary ?? runtime.RunResult.Outcome.Message}",
            $"qc.status={runtime.QcStatus}",
            $"document.stage={queryState.DocumentStage}"
        };

        if (advisory.HasNotes)
        {
            lines.Add(advisory.BuildWorkerAppendix());
        }

        AppendAttachmentEvidence(lines, attachments);
        return string.Join(Environment.NewLine, lines.Where(static line => !string.IsNullOrWhiteSpace(line)));
    }

    private static string BuildWorkerArtifact(
        ProjectWorkCycleQueryState queryState,
        ExecutionRuntimeState runtime,
        ProjectSageAdvisory advisory,
        IReadOnlyList<OpenRouterAttachment>? attachments = null)
    {
        var lines = new List<string>
        {
            AppText.Current.Get("projects.message.worker_artifact.heading"),
            string.Empty,
            AppText.Current.Format("projects.message.worker_artifact.project", queryState.ProjectName),
            AppText.Current.Format("projects.message.worker_artifact.root", queryState.ProjectRoot),
            AppText.Current.Format("projects.message.worker_artifact.task", runtime.Task.TaskId),
            AppText.Current.Format("projects.message.worker_artifact.task_description", runtime.Task.Description),
            AppText.Current.Format("projects.message.worker_artifact.runtime_state", runtime.Session.State),
            AppText.Current.Format("projects.message.worker_artifact.qc_status", runtime.QcStatus),
            string.Empty,
            AppText.Current.Get("projects.message.worker_artifact.result"),
            runtime.Result?.Summary ?? runtime.RunResult.Outcome.Message ?? string.Empty
        };

        if (advisory.HasNotes)
        {
            lines.Add(string.Empty);
            lines.Add(AppText.Current.Get("projects.message.worker_artifact.advisory"));
            lines.AddRange(advisory.Notes.Select(static note => $"- {note}"));
        }

        AppendAttachmentEvidence(lines, attachments);
        return string.Join(Environment.NewLine, lines);
    }

    private static void AppendAttachmentEvidence(List<string> lines, IReadOnlyList<OpenRouterAttachment>? attachments)
    {
        if (attachments is null || attachments.Count == 0)
        {
            return;
        }

        lines.Add(string.Empty);
        lines.Add("attached.content");
        foreach (var attachment in attachments)
        {
            lines.Add($"- {attachment.Label} [{attachment.IntakeType}]");
            lines.Add(attachment.Content);
        }
    }
}
