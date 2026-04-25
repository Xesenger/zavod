using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using zavod.Bootstrap;
using zavod.Contexting;
using zavod.Execution;
using zavod.Flow;
using zavod.Lead;
using zavod.Orchestration;
using zavod.Qc;
using zavod.Worker;
using zavod.Tooling;
using zavod.Persistence;
using zavod.Sage;
using zavod.UI.Modes.Projects.Projections;
using zavod.UI.Rendering.Conversation;
using zavod.UI.Text;
using zavod.State;

namespace zavod.UI.Modes.Projects.WorkCycle.Actions;

internal sealed class WorkCycleActionController
{

    private string _projectRoot;
    private readonly Func<ProjectsAdapter> _getProjectsAdapter;
    private readonly Func<Task> _refreshShellAsync;
    private readonly Action _updateDiscussionPreview;
    private Func<Task>? _onProgressAsync;
    private readonly ProjectSageService _sage = new();
    private readonly SageHookRunner _sageHooks = new();
    private readonly LeadAgentRuntime _leadAgentRuntime;
    private readonly WorkerAgentRuntime _workerAgentRuntime;
    private readonly QcAgentRuntime _qcAgentRuntime;

    private sealed record ActiveExecutionContext(
        ProjectState ProjectState,
        ShiftState ShiftState,
        TaskState TaskState);

    private sealed record WorkerQcCycleOutcome(
        StepPhaseState FinalPhase,
        ExecutionRuntimeState? FinalRuntime);

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
        Action updateDiscussionPreview,
        LeadAgentRuntime? leadAgentRuntime = null,
        WorkerAgentRuntime? workerAgentRuntime = null,
        QcAgentRuntime? qcAgentRuntime = null)
    {
        _projectRoot = projectRoot ?? throw new ArgumentNullException(nameof(projectRoot));
        _getProjectsAdapter = getProjectsAdapter ?? throw new ArgumentNullException(nameof(getProjectsAdapter));
        _refreshShellAsync = refreshShellAsync ?? throw new ArgumentNullException(nameof(refreshShellAsync));
        _updateDiscussionPreview = updateDiscussionPreview ?? throw new ArgumentNullException(nameof(updateDiscussionPreview));
        _leadAgentRuntime = leadAgentRuntime ?? new LeadAgentRuntime();
        _workerAgentRuntime = workerAgentRuntime ?? new WorkerAgentRuntime();
        _qcAgentRuntime = qcAgentRuntime ?? new QcAgentRuntime();
    }

    private ProjectsAdapter ProjectsAdapter => _getProjectsAdapter();

    public void SetProjectRoot(string projectRoot)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(projectRoot);
        _projectRoot = projectRoot;
    }

    public void SetProgressCallback(Func<Task>? onProgressAsync)
    {
        _onProgressAsync = onProgressAsync;
    }

    private async Task InvokeProgressAsync()
    {
        if (_onProgressAsync is null)
        {
            return;
        }

        try
        {
            await _onProgressAsync();
        }
        catch
        {
            // Best-effort progress push — do not break the send flow if UI push fails.
        }
    }

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
            // User submitted revision intake after QC REVISE. Route through the
            // same authoritative Worker+QC cycle as ConfirmPreflightAsync instead
            // of the legacy synthetic-accept shortcut (which phantom-committed
            // unreviewed results whenever user pressed Accept on the auto-landed
            // Result/Ready surface).
            await ProjectsAdapter.AddMessageAsync(
                ConversationItemKind.User,
                AppText.Current.Get("role.user"),
                normalizedText,
                metadata: BuildProjectConversationMetadata("execution", context.QueryState.ActiveTaskId));

            var executionContext = LoadActiveExecutionContext(context.QueryState);
            var runtime = context.QueryState.ResumeSnapshot.RuntimeState;
            var runningState = StepPhaseMachine.StartRevisionCycle(context.WorkCycle.PhaseState);
            var workerAdvisory = _sage.BuildWorkerAdvisory(
                context.QueryState.ProjectRoot,
                context.QueryState.ProjectId,
                executionInput.PromptText);

            await ProjectsAdapter.AddMessageAsync(
                ConversationItemKind.Status,
                AppText.Current.Get("role.worker"),
                AppText.Current.Get("projects.message.revision_input_received"),
                metadata: BuildProjectConversationMetadata("execution", context.QueryState.ActiveTaskId));
            await InvokeProgressAsync();

            var outcome = await ExecuteWorkerQcCycleAsync(
                runtime,
                executionContext,
                context.QueryState,
                runningState,
                workerAdvisory,
                attachments: executionInput.Attachments,
                isRevision: true,
                revisionIntakeText: normalizedText);

            SaveWorkCycleSnapshot(
                context.QueryState.ProjectRoot,
                outcome.FinalPhase,
                normalizedText,
                isClarificationActive: false,
                clarificationDraft: string.Empty,
                runtimeState: outcome.FinalRuntime,
                revisionIntakeText: normalizedText);
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
        await InvokeProgressAsync();

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
        var recentTurns = BuildLeadRecentTurns(ProjectsAdapter);
        var isOrientationRequest = OrientationIntentDetector.IsOrientationRequest(normalizedText);
        var leadCanonicalDocsStatus = WorkPacketBuilder.BuildCanonicalDocsStatus(context.QueryState.DocumentSelection);
        var leadAgentInput = new LeadAgentInput(
            ProjectName: context.QueryState.ProjectName,
            ProjectRoot: context.QueryState.ProjectRoot,
            ProjectKind: ReadProjectKind(context.QueryState.ProjectRoot),
            UserMessage: normalizedText,
            PreClassifierIntentState: nextState.IntentState.ToString(),
            CurrentIntentSummary: intentSummary,
            AdvisoryNotes: leadAdvisory.HasNotes ? leadAdvisory.Notes : Array.Empty<string>(),
            RecentTurns: recentTurns,
            IsOrientationRequest: isOrientationRequest,
            ProjectStackSummary: BuildProjectStackSummary(context.QueryState.ProjectRoot),
            CanonicalDocsStatus: leadCanonicalDocsStatus,
            PreviewStatus: WorkPacketBuilder.BuildPreviewStatus(context.QueryState.DocumentSelection),
            MissingTruthWarnings: WorkPacketBuilder.BuildMissingTruthWarnings(leadCanonicalDocsStatus),
            IsFirstCycle: !context.QueryState.HasActiveShift);
        var leadAgentResult = await Task.Run(() => _leadAgentRuntime.Run(leadAgentInput));

        string leadMessageContent;
        if (leadAgentResult.Success)
        {
            leadMessageContent = leadAgentResult.Reply;
            if (leadAgentResult.Parsed?.Warnings is { Count: > 0 } leadWarnings)
            {
                leadMessageContent = leadMessageContent
                    + Environment.NewLine + Environment.NewLine
                    + "Warnings: " + string.Join("; ", leadWarnings);
            }
        }
        else
        {
            var fallbackBody = leadAdvisory.HasNotes
                ? $"{leadReply}{Environment.NewLine}{Environment.NewLine}{leadAdvisory.BuildLeadContextBlock()}"
                : leadReply;
            var diagnosticCode = leadAgentResult.DiagnosticCode ?? "LEAD_UNAVAILABLE";
            leadMessageContent = $"{fallbackBody}{Environment.NewLine}{Environment.NewLine}[lead.fallback={diagnosticCode}]";
        }

        // Lead is the authority on intent state per canon — override classifier pre-pass when Lead is confident.
        if (leadAgentResult.Success
            && leadAgentResult.Parsed is not null
            && TryMapLeadIntentState(leadAgentResult.Parsed.IntentState, out var leadDecidedIntent)
            && leadDecidedIntent != nextState.IntentState)
        {
            nextState = StepPhaseMachine.RecordIntent(baseState, leadDecidedIntent);
            if (leadDecidedIntent == ContextIntentState.ReadyForValidation)
            {
                var taskBrief = leadAgentResult.Parsed?.TaskBrief;
                intentSummary = !string.IsNullOrWhiteSpace(taskBrief)
                    ? taskBrief!.Trim()
                    : BuildIntentSummary(normalizedText);
            }
        }

        var leadMetadata = BuildProjectConversationMetadata("discussion", context.QueryState.ActiveTaskId);
        leadMetadata["lab.lead.model"] = leadAgentResult.ModelId ?? string.Empty;
        leadMetadata["lab.lead.latency_ms"] = leadAgentResult.LatencyMs.ToString(System.Globalization.CultureInfo.InvariantCulture);
        leadMetadata["lab.lead.success"] = leadAgentResult.Success ? "true" : "false";
        if (!string.IsNullOrEmpty(leadAgentResult.Parsed?.IntentState))
        {
            leadMetadata["lab.lead.intent_state"] = leadAgentResult.Parsed.IntentState;
        }
        if (!string.IsNullOrEmpty(leadAgentResult.TelemetryDirectory))
        {
            leadMetadata["lab.lead.telemetry_dir"] = leadAgentResult.TelemetryDirectory!;
        }
        if (!leadAgentResult.Success && !string.IsNullOrEmpty(leadAgentResult.DiagnosticCode))
        {
            leadMetadata["lab.lead.diagnostic"] = leadAgentResult.DiagnosticCode!;
        }

        _sageHooks.OnAfterIntent(new SageAfterIntentContext(
            ProjectId: context.QueryState.ProjectId,
            ProjectRoot: context.QueryState.ProjectRoot,
            ActiveTaskId: context.QueryState.ActiveTaskId,
            UserMessage: normalizedText,
            FinalIntentState: nextState.IntentState.ToString(),
            LeadSuccess: leadAgentResult.Success,
            TaskBrief: leadAgentResult.Parsed?.TaskBrief,
            LeadReply: leadAgentResult.Success ? leadAgentResult.Reply : null));

        await ProjectsAdapter.AddMessageAsync(
            ConversationItemKind.Lead,
            AppText.Current.Get("role.shift_lead"),
            leadMessageContent,
            metadata: leadMetadata);

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
        var runningState = StepPhaseMachine.ConfirmPreflight(context.WorkCycle.PhaseState);
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
        await InvokeProgressAsync();

        var outcome = await ExecuteWorkerQcCycleAsync(
            runtime,
            executionContext,
            refreshedContext.QueryState,
            runningState,
            workerAdvisory,
            attachments: null,
            isRevision: false);

        SaveWorkCycleSnapshot(
            refreshedContext.QueryState.ProjectRoot,
            outcome.FinalPhase,
            refreshedContext.WorkCycle.IntentSummary,
            isClarificationActive: false,
            clarificationDraft: string.Empty,
            runtimeState: outcome.FinalRuntime);
        await _refreshShellAsync();
        return true;
    }

    private async Task<WorkerQcCycleOutcome> ExecuteWorkerQcCycleAsync(
        ExecutionRuntimeState runtime,
        ActiveExecutionContext executionContext,
        ProjectWorkCycleQueryState queryState,
        StepPhaseState runningState,
        ProjectSageAdvisory workerAdvisory,
        IReadOnlyList<OpenRouterAttachment>? attachments,
        bool isRevision,
        string? revisionIntakeText = null)
    {
        var executionMetadata = BuildProjectConversationMetadata("execution", queryState.ActiveTaskId);

        var anchorPack = WorkerAnchorPackBuilder.Build(
            queryState.Scan,
            queryState.ProjectRoot,
            taskDescription: executionContext.TaskState.Description);

        // In revision cycles, feed back: user's revision text + prior QC
        // rationale + prior staging skip reasons. Without this, Worker keeps
        // re-emitting the same rejected pattern because it can't see why the
        // previous attempt failed.
        IReadOnlyList<string>? revisionNotes = null;
        if (isRevision)
        {
            var notes = new List<string>();
            if (!string.IsNullOrWhiteSpace(revisionIntakeText))
            {
                notes.Add($"User feedback for this revision: {revisionIntakeText.Trim()}");
            }
            if (!string.IsNullOrWhiteSpace(runtime.LastQcRejectReason))
            {
                notes.Add($"Previous QC rationale for rejection: {runtime.LastQcRejectReason.Trim()}");
            }
            if (notes.Count > 0)
            {
                revisionNotes = notes;
            }
        }

        var workerCanonicalDocsStatus = WorkPacketBuilder.BuildCanonicalDocsStatus(queryState.DocumentSelection);
        var workerInput = new WorkerAgentInput(
            ProjectName: queryState.ProjectName,
            ProjectRoot: queryState.ProjectRoot,
            ProjectKind: ReadProjectKind(queryState.ProjectRoot),
            TaskId: executionContext.TaskState.TaskId,
            TaskDescription: executionContext.TaskState.Description,
            Scope: executionContext.TaskState.Scope,
            AcceptanceCriteria: executionContext.TaskState.AcceptanceCriteria,
            AdvisoryNotes: workerAdvisory.HasNotes ? workerAdvisory.Notes : Array.Empty<string>(),
            Anchors: anchorPack,
            RevisionNotes: revisionNotes,
            CanonicalDocsStatus: workerCanonicalDocsStatus,
            PreviewStatus: WorkPacketBuilder.BuildPreviewStatus(queryState.DocumentSelection),
            MissingTruthWarnings: WorkPacketBuilder.BuildMissingTruthWarnings(workerCanonicalDocsStatus),
            IsFirstCycle: executionContext.ShiftState.Tasks.Count <= 1);

        _sageHooks.OnBeforeExecution(new SageBeforeExecutionContext(
            ProjectId: queryState.ProjectId,
            ProjectRoot: queryState.ProjectRoot,
            TaskId: executionContext.TaskState.TaskId,
            TaskDescription: executionContext.TaskState.Description,
            Scope: executionContext.TaskState.Scope,
            AnchorCount: anchorPack.Count,
            AdvisoryNoteCount: workerAdvisory.HasNotes ? workerAdvisory.Notes.Count : 0,
            IsRevision: isRevision,
            RevisionNoteCount: revisionNotes?.Count ?? 0,
            Anchors: anchorPack));

        var workerLlmResult = await Task.Run(() => _workerAgentRuntime.Run(workerInput));

        // ─── Worker response + authoritative QC routing ──────────────────────
        // Three branches drive phase and runtime to a final state:
        //   D. LLM infrastructure failure     → do not synthesize, do not run QC,
        //                                       do not cleanup staging — preserve
        //                                       for user retry. Phase rolls back
        //                                       to Revision (revision) or task
        //                                       is abandoned but staging kept
        //                                       (fresh).
        //   B. Worker refused/failed          → skip QC, abandon task (REJECT-equivalent)
        //   C. Worker success/partial         → run QC, route by decision:
        //                                         ACCEPT → Result/Ready
        //                                         REVISE → Execution/Revision/RevisionRequested
        //                                         REJECT → Discussion (task abandoned)
        var workerLlmAvailable = workerLlmResult.Success && workerLlmResult.Parsed is not null;

        if (!workerLlmAvailable)
        {
            // BRANCH D — Worker LLM timed out or parsing failed. Previously we
            // synthesized a result via ProduceResult and ran QC, which then
            // mechanically REJECTed the fake result and abandoned the task.
            // That destroyed real staging progress on transient upstream
            // failures. Now: preserve staging, skip QC, exit early.
            var diagnostic = workerLlmResult.DiagnosticCode ?? "WORKER_UNAVAILABLE";
            StepPhaseState fallbackPhase;
            ExecutionRuntimeState? fallbackRuntime;
            string fallbackMessageKey;
            string fallbackMessageArg;

            if (isRevision)
            {
                // Keep the revision surface alive. Runtime is still InProgress
                // (it was before the call). User can resend the revision text.
                fallbackPhase = new StepPhaseState(
                    SurfacePhase.Execution,
                    DiscussionSubphase.None,
                    ExecutionSubphase.Revision,
                    ResultSubphase.RevisionRequested,
                    ContextIntentState.Validated,
                    HasActiveShift: true,
                    HasActiveTask: true,
                    HasClarification: runningState.HasClarification,
                    HasReopenedContext: false);
                fallbackRuntime = runtime;
                fallbackMessageKey = "projects.message.worker_llm_unavailable_revision";
                fallbackMessageArg = diagnostic;
            }
            else
            {
                // Fresh cycle: abandon the task. Worker never produced anything
                // this cycle, but a prior failed attempt under the same taskId
                // could have left staging behind — quarantine it rather than
                // leave an orphan under the live namespace. No-op if absent.
                var abandoned = ResultAbandonProcessor.Abandon(
                    executionContext.ProjectState,
                    executionContext.ShiftState,
                    executionContext.TaskState,
                    DateTimeOffset.Now);
                StagingWriter.Quarantine(queryState.ProjectRoot, executionContext.TaskState.TaskId);
                fallbackPhase = StepPhaseMachine.ResumeActiveShiftDiscussion();
                fallbackRuntime = null;
                fallbackMessageKey = "projects.message.worker_llm_unavailable_fresh";
                fallbackMessageArg = abandoned.TaskState.TaskId;
            }

            var unavailableMetadata = BuildProjectConversationMetadata("execution", queryState.ActiveTaskId);
            unavailableMetadata["worker.diagnostic"] = diagnostic;
            await ProjectsAdapter.AddMessageAsync(
                ConversationItemKind.Status,
                AppText.Current.Get("role.worker"),
                AppText.Current.Format(fallbackMessageKey, diagnostic, fallbackMessageArg),
                metadata: unavailableMetadata);

            return new WorkerQcCycleOutcome(fallbackPhase, fallbackRuntime);
        }

        var workerStatusReviewable = IsReviewableWorkerStatus(workerLlmResult.Parsed!.Status);

        WorkerExecutionResult? executionResultForMessage = null;
        StagingManifest? stagingManifest = null;
        if (workerLlmAvailable && workerStatusReviewable)
        {
            var parsedWorker = workerLlmResult.Parsed!;
            var modifications = parsedWorker.Modifications
                .Select(m => new WorkerExecutionModification(m.Path, m.Kind, m.Summary))
                .ToArray();
            var warnings = parsedWorker.Warnings
                .Select(w => new ToolWarning("WORKER_WARNING", w))
                .ToArray();
            executionResultForMessage = new WorkerExecutionResult(
                $"RESULT-{executionContext.TaskState.TaskId}-001",
                executionContext.TaskState.TaskId,
                MapWorkerStatus(parsedWorker.Status),
                parsedWorker.Summary,
                Array.Empty<IntakeArtifact>(),
                modifications,
                warnings);
            runtime = ExecutionRuntimeController.ProduceProvidedResult(runtime, executionResultForMessage);

            // Stage real edits onto disk sandbox (.zavod.local/staging/<taskId>/attempt-<N>/).
            // Never touches the project tree. Apply happens only on user Accept.
            if (parsedWorker.Edits is { Count: > 0 } parsedEdits)
            {
                var attemptNumber = Math.Max(1, runtime.Attempts.Count);
                try
                {
                    stagingManifest = StagingWriter.Stage(
                        queryState.ProjectRoot,
                        executionContext.TaskState.TaskId,
                        attemptNumber,
                        executionContext.TaskState.Description,
                        parsedEdits);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[staging] stage failed: {ex.GetType().Name}: {ex.Message}");
                }
            }
        }
        // If !workerStatusReviewable (Worker refused/failed with valid parsed
        // output) → runtime stays at Begin() state; we go through the
        // REJECT-equivalent abandon path below and discard it.

        QcAgentResult? qcLlmResult = null;
        StepPhaseState finalPhaseState;
        ExecutionRuntimeState? finalRuntimeState;
        string qcFollowUpMessageKey;
        string? qcFollowUpMessageArg = null;
        ConversationItemKind qcMessageKind = ConversationItemKind.Qc;

        if (workerLlmAvailable && !workerStatusReviewable)
        {
            // BRANCH B — Worker refused/failed. No reviewable result → abandon.
            var abandoned = ResultAbandonProcessor.Abandon(
                executionContext.ProjectState,
                executionContext.ShiftState,
                executionContext.TaskState,
                DateTimeOffset.Now);
            // Defensive: a refused attempt after a prior reviewable one would
            // leave a stale sandbox from the previous attempt. Quarantine
            // instead of Cleanup — preserve forensics under _abandoned/ rather
            // than deleting what the Worker produced before it refused.
            StagingWriter.Quarantine(queryState.ProjectRoot, executionContext.TaskState.TaskId);
            finalPhaseState = StepPhaseMachine.ResumeActiveShiftDiscussion();
            finalRuntimeState = null;
            qcFollowUpMessageKey = "projects.message.worker_refused_abandoned";
            qcFollowUpMessageArg = abandoned.TaskState.TaskId;

            _sageHooks.OnAfterResult(new SageAfterResultContext(
                ProjectId: queryState.ProjectId,
                ProjectRoot: queryState.ProjectRoot,
                TaskId: executionContext.TaskState.TaskId,
                Outcome: SageResultOutcome.WorkerRefused,
                Rationale: workerLlmResult.Parsed?.Summary));
        }
        else
        {
            // BRANCHES A and C — run QC and route by decision.
            var stagedArtifactDescriptors = stagingManifest is null
                ? Array.Empty<string>()
                : stagingManifest.Results
                    .Select(r => r.Applied
                        ? $"{r.Operation}: {r.Path} (origin={r.OriginalBytes}B → staged={r.StagedBytes}B, sha256={r.OriginalSha256 ?? "new-file"})"
                        : $"SKIPPED {r.Operation}: {r.Path} — {r.SkipReason}")
                    .ToArray();

            var qcAgentInput = new QcAgentInput(
                ProjectName: queryState.ProjectName,
                ProjectRoot: queryState.ProjectRoot,
                ProjectKind: ReadProjectKind(queryState.ProjectRoot),
                TaskId: executionContext.TaskState.TaskId,
                TaskDescription: executionContext.TaskState.Description,
                AcceptanceCriteria: executionContext.TaskState.AcceptanceCriteria,
                WorkerStatus: workerLlmResult.Parsed?.Status ?? (workerLlmResult.Success ? "unknown" : "unavailable"),
                WorkerSummary: workerLlmResult.Parsed?.Summary ?? runtime.Result?.Summary ?? string.Empty,
                WorkerBlockers: workerLlmResult.Parsed?.Blockers ?? Array.Empty<string>(),
                WorkerWarnings: workerLlmResult.Parsed?.Warnings ?? Array.Empty<string>(),
                WorkerModifications: workerLlmResult.Parsed?.Modifications?
                    .Select(m => $"{m.Kind}: {m.Path} — {m.Summary}")
                    .ToArray() ?? Array.Empty<string>(),
                StagedArtifacts: stagedArtifactDescriptors);

            _sageHooks.OnBeforeResult(new SageBeforeResultContext(
                ProjectId: queryState.ProjectId,
                ProjectRoot: queryState.ProjectRoot,
                TaskId: executionContext.TaskState.TaskId,
                WorkerStatus: qcAgentInput.WorkerStatus,
                StagedArtifactCount: stagedArtifactDescriptors.Length,
                WorkerBlockerCount: qcAgentInput.WorkerBlockers.Count,
                WorkerWarningCount: qcAgentInput.WorkerWarnings.Count));

            qcLlmResult = await Task.Run(() => _qcAgentRuntime.Run(qcAgentInput));

            runtime = ExecutionRuntimeController.RequestQcReview(runtime);
            var qcPhaseState = StepPhaseMachine.MoveToQc(runningState);

            var parsedQcReply = qcLlmResult.Success ? qcLlmResult.Parsed : null;
            var qcUnavailable = parsedQcReply is null;
            var decision = "REVISE";
            if (parsedQcReply is not null)
            {
                decision = parsedQcReply.Decision;
            }

            switch (decision)
            {
                case "REVISE":
                    {
                        var rationale = qcUnavailable
                            ? BuildQcUnavailableRevisionRationale(qcLlmResult)
                            : parsedQcReply?.Rationale ?? "QC requested revision.";
                        runtime = ExecutionRuntimeController.RejectQcReview(runtime, needsRevision: true, rationale);
                        runtime = ExecutionRuntimeController.RestartRevision(runtime);
                        // Phase chains through AcceptQc only to satisfy ReturnForRevision
                        // precondition; the intermediate Result/Ready never persists.
                        var chainedAccept = StepPhaseMachine.AcceptQc(qcPhaseState);
                        finalPhaseState = StepPhaseMachine.ReturnForRevision(chainedAccept);
                        finalRuntimeState = runtime;
                        qcFollowUpMessageKey = "projects.message.qc_revise_revision_cycle_opened";
                        _sageHooks.OnAfterResult(new SageAfterResultContext(
                            ProjectId: queryState.ProjectId,
                            ProjectRoot: queryState.ProjectRoot,
                            TaskId: executionContext.TaskState.TaskId,
                            Outcome: SageResultOutcome.QcRevise,
                            Rationale: rationale));
                        break;
                    }
                case "REJECT":
                    {
                        var rationale = qcLlmResult.Parsed?.Rationale ?? "QC rejected the produced result.";
                        runtime = ExecutionRuntimeController.RejectQcReview(runtime, needsRevision: false, rationale);
                        var abandoned = ResultAbandonProcessor.Abandon(
                            executionContext.ProjectState,
                            executionContext.ShiftState,
                            executionContext.TaskState,
                            DateTimeOffset.Now);
                        StagingWriter.Quarantine(queryState.ProjectRoot, executionContext.TaskState.TaskId);
                        finalPhaseState = StepPhaseMachine.ResumeActiveShiftDiscussion();
                        finalRuntimeState = null;
                        qcFollowUpMessageKey = "projects.message.qc_reject_task_abandoned";
                        qcFollowUpMessageArg = abandoned.TaskState.TaskId;
                        _sageHooks.OnAfterResult(new SageAfterResultContext(
                            ProjectId: queryState.ProjectId,
                            ProjectRoot: queryState.ProjectRoot,
                            TaskId: executionContext.TaskState.TaskId,
                            Outcome: SageResultOutcome.QcReject,
                            Rationale: rationale));
                        break;
                    }
                case "ACCEPT":
                default:
                    {
                        runtime = ExecutionRuntimeController.AcceptQcReview(runtime);
                        finalPhaseState = StepPhaseMachine.AcceptQc(qcPhaseState);
                        finalRuntimeState = runtime;
                        qcFollowUpMessageKey = "projects.message.qc_accepted_produced_result";
                        _sageHooks.OnAfterResult(new SageAfterResultContext(
                            ProjectId: queryState.ProjectId,
                            ProjectRoot: queryState.ProjectRoot,
                            TaskId: executionContext.TaskState.TaskId,
                            Outcome: SageResultOutcome.QcAccepted,
                            Rationale: qcLlmResult.Parsed?.Rationale));
                        break;
                    }
            }
        }

        // ─── Emit Worker message + log + artifact (all branches) ─────────────
        var workerMessageContent = workerLlmResult.Success && workerLlmResult.Parsed is { } parsedReply
            ? parsedReply.Summary
            : $"{runtime.Result?.Summary ?? string.Empty}{Environment.NewLine}{Environment.NewLine}[worker.fallback={workerLlmResult.DiagnosticCode ?? "WORKER_UNAVAILABLE"}]";

        var workerMetadata = BuildProjectConversationMetadata("execution", queryState.ActiveTaskId);
        workerMetadata["lab.worker.model"] = workerLlmResult.ModelId ?? string.Empty;
        workerMetadata["lab.worker.latency_ms"] = workerLlmResult.LatencyMs.ToString(System.Globalization.CultureInfo.InvariantCulture);
        workerMetadata["lab.worker.success"] = workerLlmResult.Success ? "true" : "false";
        if (!string.IsNullOrEmpty(workerLlmResult.Parsed?.Status))
        {
            workerMetadata["lab.worker.status"] = workerLlmResult.Parsed.Status;
        }
        if (!string.IsNullOrEmpty(workerLlmResult.TelemetryDirectory))
        {
            workerMetadata["lab.worker.telemetry_dir"] = workerLlmResult.TelemetryDirectory!;
        }
        if (!workerLlmResult.Success && !string.IsNullOrEmpty(workerLlmResult.DiagnosticCode))
        {
            workerMetadata["lab.worker.diagnostic"] = workerLlmResult.DiagnosticCode!;
        }

        var logPreviewKey = isRevision ? "projects.message.revision_log_prepared" : "projects.message.worker_log_prepared";
        var artifactPreviewKey = isRevision ? "projects.message.revision_execution_brief_prepared" : "projects.message.execution_brief_prepared";

        await ProjectsAdapter.AddMessageAsync(
            ConversationItemKind.Worker,
            AppText.Current.Get("role.worker"),
            workerMessageContent,
            metadata: workerMetadata);
        await ProjectsAdapter.AddLogAsync(
            AppText.Current.Get("role.worker"),
            BuildWorkerLog(queryState, runtime, workerAdvisory, attachments),
            preview: AppText.Current.Get(logPreviewKey),
            metadata: executionMetadata);
        await ProjectsAdapter.AddArtifactAsync(
            AppText.Current.Get("role.worker"),
            AppText.Current.Get("conversation.execution_brief_label"),
            BuildWorkerArtifact(queryState, runtime, workerAdvisory, attachments),
            "md",
            preview: AppText.Current.Get(artifactPreviewKey),
            metadata: executionMetadata);

        // ─── Emit staging summary (when Worker produced stageable edits) ─────
        if (stagingManifest is not null)
        {
            var stagingSummaryText = AppText.Current.Format(
                "projects.message.staged_summary",
                stagingManifest.Results.Count,
                stagingManifest.AppliedCount,
                stagingManifest.SkippedCount,
                stagingManifest.StagingRoot);
            var stagingMetadata = BuildProjectConversationMetadata("execution", queryState.ActiveTaskId);
            stagingMetadata["staging.root"] = stagingManifest.StagingRoot;
            stagingMetadata["staging.applied"] = stagingManifest.AppliedCount.ToString(System.Globalization.CultureInfo.InvariantCulture);
            stagingMetadata["staging.skipped"] = stagingManifest.SkippedCount.ToString(System.Globalization.CultureInfo.InvariantCulture);
            await ProjectsAdapter.AddMessageAsync(
                ConversationItemKind.Status,
                AppText.Current.Get("role.worker"),
                stagingSummaryText,
                metadata: stagingMetadata);
        }

        // ─── Emit QC message (when QC ran) + follow-up status ────────────────
        if (qcLlmResult is not null)
        {
            var qcResultMetadata = BuildProjectConversationMetadata("result", queryState.ActiveTaskId);
            qcResultMetadata["lab.qc.model"] = qcLlmResult.ModelId ?? string.Empty;
            qcResultMetadata["lab.qc.latency_ms"] = qcLlmResult.LatencyMs.ToString(System.Globalization.CultureInfo.InvariantCulture);
            qcResultMetadata["lab.qc.success"] = qcLlmResult.Success ? "true" : "false";
            if (!string.IsNullOrEmpty(qcLlmResult.Parsed?.Decision))
            {
                qcResultMetadata["lab.qc.decision"] = qcLlmResult.Parsed.Decision;
            }
            if (!string.IsNullOrEmpty(qcLlmResult.TelemetryDirectory))
            {
                qcResultMetadata["lab.qc.telemetry_dir"] = qcLlmResult.TelemetryDirectory!;
            }
            if (!qcLlmResult.Success && !string.IsNullOrEmpty(qcLlmResult.DiagnosticCode))
            {
                qcResultMetadata["lab.qc.diagnostic"] = qcLlmResult.DiagnosticCode!;
            }

            var qcMessageContent = BuildQcDecisionMessage(qcLlmResult);
            await ProjectsAdapter.AddMessageAsync(
                qcMessageKind,
                AppText.Current.Get("role.qc"),
                qcMessageContent,
                metadata: qcResultMetadata);
        }

        var followUpMetadata = BuildProjectConversationMetadata(
            finalPhaseState.Phase switch
            {
                SurfacePhase.Result => "result",
                SurfacePhase.Execution => "execution",
                _ => "discussion"
            },
            queryState.ActiveTaskId);
        var followUpText = qcFollowUpMessageArg is null
            ? AppText.Current.Get(qcFollowUpMessageKey)
            : AppText.Current.Format(qcFollowUpMessageKey, qcFollowUpMessageArg);
        await ProjectsAdapter.AddMessageAsync(
            ConversationItemKind.Status,
            AppText.Current.Get("role.qc"),
            followUpText,
            metadata: followUpMetadata);

        return new WorkerQcCycleOutcome(finalPhaseState, finalRuntimeState);
    }

    private static bool IsReviewableWorkerStatus(string status)
    {
        // Worker vocabulary drifted between worker.system.md (Partial/Complete)
        // and the strict BuildUserPrompt schema (success/partial/failed/refused).
        // Accept all reasonable "something was produced" variants so a confident
        // Worker using the verb "complete" isn't dropped into the refused path
        // — that lost a perfect deliverable on TASK-011.
        return status?.Trim().ToLowerInvariant() switch
        {
            "success" => true,
            "partial" => true,
            "complete" => true,
            "completed" => true,
            "done" => true,
            _ => false
        };
    }

    private static bool TryMapLeadIntentState(string? raw, out ContextIntentState state)
    {
        switch (raw?.Trim().ToLowerInvariant())
        {
            case "orientation":
                state = ContextIntentState.Orientation;
                return true;
            case "candidate":
                state = ContextIntentState.Candidate;
                return true;
            case "refining":
                state = ContextIntentState.Refining;
                return true;
            case "ready_for_validation":
            case "readyforvalidation":
            case "ready":
                state = ContextIntentState.ReadyForValidation;
                return true;
            case "rejected":
                state = ContextIntentState.Refining;
                return true;
            default:
                state = ContextIntentState.None;
                return false;
        }
    }

    private static IReadOnlyList<LeadAgentTurn> BuildLeadRecentTurns(ProjectsAdapter adapter)
    {
        if (adapter?.Items is not { Count: > 0 } items)
        {
            return Array.Empty<LeadAgentTurn>();
        }

        const int MaxTurns = 8;
        var selected = new List<LeadAgentTurn>(MaxTurns);
        for (var index = items.Count - 1; index >= 0 && selected.Count < MaxTurns; index--)
        {
            var item = items[index];
            if (item is null)
            {
                continue;
            }

            var role = MapLeadTurnRole(item.Kind);
            if (role is null)
            {
                continue;
            }

            var text = item.Text?.Trim();
            if (string.IsNullOrWhiteSpace(text))
            {
                continue;
            }

            selected.Add(new LeadAgentTurn(role, text));
        }

        selected.Reverse();

        if (selected.Count > 0 && string.Equals(selected[^1].Role, "user", StringComparison.Ordinal))
        {
            selected.RemoveAt(selected.Count - 1);
        }

        return selected;
    }

    private static string? MapLeadTurnRole(ConversationItemKind kind)
    {
        // Lead framing context must not carry Worker/Qc turns back into the
        // Lead prompt. Otherwise the next Lead turn parrots the prior QC REVISE
        // rationale ("where is the game loop / HUD module?") at the user,
        // forcing a clarification loop on questions that belong to Worker/QC.
        // Keep only User and Lead turns — the framing dialogue proper.
        return kind switch
        {
            ConversationItemKind.User => "user",
            ConversationItemKind.Lead => "lead",
            _ => null
        };
    }

    private static WorkerExecutionStatus MapWorkerStatus(string status)
    {
        // Only called for reviewable statuses under A.2 routing. Worker
        // vocabulary is loose ("complete"/"completed"/"done" all mean "fully
        // delivered", "partial" means "delivered with self-noted gaps").
        // Map confident variants to Success, uncertain to Partial.
        return status?.Trim().ToLowerInvariant() switch
        {
            "success" => WorkerExecutionStatus.Success,
            "complete" => WorkerExecutionStatus.Success,
            "completed" => WorkerExecutionStatus.Success,
            "done" => WorkerExecutionStatus.Success,
            "partial" => WorkerExecutionStatus.Partial,
            _ => WorkerExecutionStatus.Failed
        };
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
        try
        {
            AcceptedResultApplyProcessor.ValidateCanApply(
                executionContext.ProjectState,
                executionContext.ShiftState,
                executionContext.TaskState,
                context.QueryState.ResumeSnapshot.RuntimeState);
        }
        catch (InvalidOperationException ex)
        {
            await ProjectsAdapter.AddMessageAsync(
                ConversationItemKind.Status,
                AppText.Current.Get("role.qc"),
                AppText.Current.Format("projects.message.accepted_result_apply_blocked", ex.Message),
                metadata: BuildProjectConversationMetadata("result", executionContext.TaskState.TaskId));
            return false;
        }

        // Apply staged files from .zavod.local/staging/<taskId>/attempt-<latest>/
        // into the project tree only after read-only acceptance validation.
        var stagingOutcome = StagingApplier.Apply(context.QueryState.ProjectRoot, executionContext.TaskState.TaskId);
        if (stagingOutcome.AppliedFiles.Count > 0)
        {
            await ProjectsAdapter.AddMessageAsync(
                ConversationItemKind.Status,
                AppText.Current.Get("role.worker"),
                AppText.Current.Format(
                    "projects.message.staging_applied",
                    stagingOutcome.AppliedFiles.Count,
                    string.Join(", ", stagingOutcome.AppliedFiles)),
                metadata: BuildProjectConversationMetadata("result", executionContext.TaskState.TaskId));
        }
        if (stagingOutcome.HashMismatchWarnings.Count > 0)
        {
            await ProjectsAdapter.AddMessageAsync(
                ConversationItemKind.Status,
                AppText.Current.Get("role.worker"),
                AppText.Current.Format(
                    "projects.message.staging_hash_drift",
                    string.Join("; ", stagingOutcome.HashMismatchWarnings)),
                metadata: BuildProjectConversationMetadata("result", executionContext.TaskState.TaskId));
        }

        if (stagingOutcome.HashMismatchWarnings.Count > 0)
        {
            return false;
        }

        if (stagingOutcome.SkippedFiles.Count > 0)
        {
            await ProjectsAdapter.AddMessageAsync(
                ConversationItemKind.Status,
                AppText.Current.Get("role.worker"),
                AppText.Current.Format(
                    "projects.message.accepted_result_apply_blocked",
                    $"Staging apply did not apply all files: {string.Join("; ", stagingOutcome.SkippedFiles)}"),
                metadata: BuildProjectConversationMetadata("result", executionContext.TaskState.TaskId));
            return false;
        }

        var applied = AcceptedResultApplyProcessor.Apply(
            executionContext.ProjectState,
            executionContext.ShiftState,
            executionContext.TaskState,
            context.QueryState.ResumeSnapshot.RuntimeState,
            DateTimeOffset.Now);

        // Aggressive cleanup policy: staged tree is disposable once state
        // bookkeeping committed. Diagnostic trace lives in .zavod.local/lab/.
        StagingWriter.Cleanup(context.QueryState.ProjectRoot, executionContext.TaskState.TaskId);

        _sageHooks.OnAfterResult(new SageAfterResultContext(
            ProjectId: context.QueryState.ProjectId,
            ProjectRoot: context.QueryState.ProjectRoot,
            TaskId: executionContext.TaskState.TaskId,
            Outcome: SageResultOutcome.Applied,
            Rationale: null));

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

        // User rejected the staged result — quarantine instead of delete so
        // the staged content is still inspectable under .zavod.local/staging/
        // _abandoned/<taskId>-<utc>/ if the user changes their mind about
        // what Worker actually produced.
        StagingWriter.Quarantine(context.QueryState.ProjectRoot, executionContext.TaskState.TaskId);

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

    internal static IReadOnlyList<string> BuildProjectStackSummary(string projectRoot)
    {
        if (string.IsNullOrWhiteSpace(projectRoot))
        {
            return Array.Empty<string>();
        }

        var lines = new List<string>();

        try
        {
            var passportPath = Path.Combine(projectRoot, ".zavod", "import_evidence_bundle", "technical_passport.json");
            if (File.Exists(passportPath))
            {
                using var document = System.Text.Json.JsonDocument.Parse(File.ReadAllText(passportPath));
                var root = document.RootElement;
                AppendArrayLine(lines, root, "ObservedLanguages", "languages");
                AppendArrayLine(lines, root, "Frameworks", "frameworks");
                AppendArrayLine(lines, root, "BuildSystems", "build_systems");
                AppendArrayLine(lines, root, "Toolchains", "toolchains");
                AppendArrayLine(lines, root, "RuntimeSurfaces", "runtime_surfaces");
                AppendArrayLine(lines, root, "TargetPlatforms", "target_platforms");
                AppendArrayLine(lines, root, "ConfigMarkers", "config_markers");
            }
        }
        catch
        {
            // best effort
        }

        try
        {
            var profilePath = Path.Combine(projectRoot, ".zavod", "import_evidence_bundle", "project_profile.json");
            if (File.Exists(profilePath))
            {
                using var document = System.Text.Json.JsonDocument.Parse(File.ReadAllText(profilePath));
                var root = document.RootElement;
                AppendArrayLine(lines, root, "SourceRoots", "source_roots");
                AppendIntLine(lines, root, "SourceFileCount", "source_files");
                AppendIntLine(lines, root, "AssetFileCount", "asset_files");
                AppendIntLine(lines, root, "ConfigFileCount", "config_files");
                AppendIntLine(lines, root, "RelevantFileCount", "relevant_files");
            }
        }
        catch
        {
            // best effort
        }

        return lines;
    }

    private static void AppendArrayLine(List<string> lines, System.Text.Json.JsonElement root, string property, string label)
    {
        if (!root.TryGetProperty(property, out var node) || node.ValueKind != System.Text.Json.JsonValueKind.Array)
        {
            return;
        }

        var values = new List<string>();
        foreach (var item in node.EnumerateArray())
        {
            if (item.ValueKind == System.Text.Json.JsonValueKind.String)
            {
                var value = item.GetString();
                if (!string.IsNullOrWhiteSpace(value))
                {
                    values.Add(value.Trim());
                }
            }
        }

        if (values.Count == 0)
        {
            return;
        }

        const int Max = 12;
        if (values.Count > Max)
        {
            lines.Add($"{label}: {string.Join(", ", values.Take(Max))} (+{values.Count - Max} more)");
        }
        else
        {
            lines.Add($"{label}: {string.Join(", ", values)}");
        }
    }

    private static void AppendIntLine(List<string> lines, System.Text.Json.JsonElement root, string property, string label)
    {
        if (root.TryGetProperty(property, out var node) && node.TryGetInt32(out var value))
        {
            lines.Add($"{label}: {value}");
        }
    }

    internal static string ReadProjectKind(string projectRoot)
    {
        if (string.IsNullOrWhiteSpace(projectRoot))
        {
            return "unknown";
        }

        try
        {
            var path = Path.Combine(projectRoot, ".zavod", "meta", "project_kind.txt");
            if (!File.Exists(path))
            {
                return "unknown";
            }

            var value = File.ReadAllText(path).Trim();
            return string.IsNullOrWhiteSpace(value) ? "unknown" : value;
        }
        catch
        {
            return "unknown";
        }
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
        // Worker interprets Scope as "allowed paths" for execution. The preview doc
        // was never a valid edit target — scoping Worker to it told the model
        // "you may only touch preview_project.md", which is the opposite of what
        // is needed. Scope the whole project root; the CODE ANCHORS block narrows
        // attention to real source files within it.
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

    private static string BuildQcDecisionMessage(QcAgentResult qcResult)
    {
        if (!qcResult.Success || qcResult.Parsed is null)
        {
            var diag = qcResult.DiagnosticCode ?? "QC_UNAVAILABLE";
            return AppText.Current.Format("projects.message.qc_decision_unavailable", diag);
        }

        var key = qcResult.Parsed.Decision switch
        {
            "ACCEPT" => "projects.message.qc_decision_accept",
            "REVISE" => "projects.message.qc_decision_revise",
            "REJECT" => "projects.message.qc_decision_reject",
            _ => "projects.message.qc_decision_accept"
        };
        var body = string.IsNullOrWhiteSpace(qcResult.Parsed.NextAction)
            ? qcResult.Parsed.Rationale
            : $"{qcResult.Parsed.Rationale}{Environment.NewLine}{Environment.NewLine}Next: {qcResult.Parsed.NextAction}";

        if (qcResult.Parsed.Issues is { Count: > 0 } issues)
        {
            var issuesBlock = string.Join(Environment.NewLine, issues.Select(i => $"- {i}"));
            body = $"{body}{Environment.NewLine}{Environment.NewLine}Issues:{Environment.NewLine}{issuesBlock}";
        }

        return AppText.Current.Format(key, body);
    }

    private static string BuildQcUnavailableRevisionRationale(QcAgentResult qcResult)
    {
        var diagnostic = qcResult.DiagnosticCode ?? "QC_UNAVAILABLE";
        return $"QC was unavailable or unparseable ({diagnostic}); the result was not accepted and requires a revision or retry.";
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
