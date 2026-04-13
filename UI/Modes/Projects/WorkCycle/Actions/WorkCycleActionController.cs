using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using zavod.Bootstrap;
using zavod.Contexting;
using zavod.Flow;
using zavod.Persistence;
using zavod.UI.Rendering.Conversation;
using zavod.UI.Modes.Projects.Projections;
using zavod.State;

namespace zavod.UI.Modes.Projects.WorkCycle.Actions;

internal sealed class WorkCycleActionController
{
    private readonly string _projectRoot;
    private readonly ProjectsAdapter _projectsAdapter;
    private readonly Func<Task> _refreshShellAsync;
    private readonly Action _updateDiscussionPreview;

    public WorkCycleActionController(
        string projectRoot,
        ProjectsAdapter projectsAdapter,
        Func<Task> refreshShellAsync,
        Action updateDiscussionPreview)
    {
        _projectRoot = projectRoot ?? throw new ArgumentNullException(nameof(projectRoot));
        _projectsAdapter = projectsAdapter ?? throw new ArgumentNullException(nameof(projectsAdapter));
        _refreshShellAsync = refreshShellAsync ?? throw new ArgumentNullException(nameof(refreshShellAsync));
        _updateDiscussionPreview = updateDiscussionPreview ?? throw new ArgumentNullException(nameof(updateDiscussionPreview));
    }

    public async Task<bool> SendProjectsMessageAsync(string text)
    {
        var normalizedText = text?.Trim();
        if (string.IsNullOrWhiteSpace(normalizedText))
        {
            return false;
        }

        var normalizedRoot = Path.GetFullPath(_projectRoot);
        var shellProjection = ProjectsShellProjection.Build(normalizedRoot);
        var workCycle = ProjectWorkCycleProjection.Build(normalizedRoot, shellProjection);
        if (workCycle.PhaseState.Phase != SurfacePhase.Discussion)
        {
            return false;
        }

        await _projectsAdapter.AddMessageAsync(
            ConversationItemKind.User,
            "User",
            normalizedText,
            metadata: BuildProjectConversationMetadata("discussion", shellProjection.ActiveTaskText));

        var baseState = workCycle.PhaseState;
        StepPhaseState nextState;
        string leadReply;
        string intentSummary = workCycle.IntentSummary;

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
                ContextIntentState.ReadyForValidation => "РџРѕС…РѕР¶Рµ, РґРѕРіРѕРІРѕСЂС‘РЅРЅРѕСЃС‚СЊ СЃС„РѕСЂРјРёСЂРѕРІР°РЅР° РґРѕСЃС‚Р°С‚РѕС‡РЅРѕ С‡С‘С‚РєРѕ. Р•СЃР»Рё РІСЃС‘ РІРµСЂРЅРѕ, РјРѕР¶РЅРѕ РѕС‚РїСЂР°РІР»СЏС‚СЊ Р·Р°РґР°С‡Сѓ РІ СЂР°Р±РѕС‚Сѓ.",
                ContextIntentState.Candidate or ContextIntentState.Refining => "РџРѕРЅСЏР». РџСЂРѕРґРѕР»Р¶Р°Р№ СѓС‚РѕС‡РЅСЏС‚СЊ Р·Р°РґР°С‡Сѓ, С‡С‚РѕР±С‹ СЏ СЃРѕР±СЂР°Р» РєРѕСЂСЂРµРєС‚РЅСѓСЋ РґРѕРіРѕРІРѕСЂС‘РЅРЅРѕСЃС‚СЊ РїРµСЂРµРґ Р·Р°РїСѓСЃРєРѕРј.",
                _ => "РџСЂРѕРґРѕР»Р¶РёРј СЂР°Р·Р±РёСЂР°С‚СЊ Р·Р°РґР°С‡Сѓ РґР°Р»СЊС€Рµ."
            };
        }

        await _projectsAdapter.AddMessageAsync(
            ConversationItemKind.Lead,
            "Shift Lead",
            leadReply,
            metadata: BuildProjectConversationMetadata("discussion", shellProjection.ActiveTaskText));

        SaveWorkCycleSnapshot(normalizedRoot, nextState, intentSummary, isClarificationActive: false, clarificationDraft: string.Empty);
        _updateDiscussionPreview();
        await _refreshShellAsync();
        return true;
    }

    public async Task<bool> EnterWorkAsync()
    {
        var normalizedRoot = Path.GetFullPath(_projectRoot);
        var shellProjection = ProjectsShellProjection.Build(normalizedRoot);
        var workCycle = ProjectWorkCycleProjection.Build(normalizedRoot, shellProjection);
        if (!workCycle.Projection.CanStartIntentValidation)
        {
            return false;
        }

        var nextState = workCycle.PhaseState.HasActiveTask
            ? StepPhaseMachine.EnterReopenedPreflight(workCycle.PhaseState)
            : workCycle.PhaseState.HasActiveShift
                ? StepPhaseMachine.EnterActiveShiftPreflight(workCycle.PhaseState)
                : StepPhaseMachine.EnterPreflight(workCycle.PhaseState);
        SaveWorkCycleSnapshot(normalizedRoot, nextState, workCycle.IntentSummary, isClarificationActive: false, clarificationDraft: string.Empty);
        await _refreshShellAsync();
        return true;
    }

    public async Task<bool> ConfirmPreflightAsync()
    {
        var normalizedRoot = Path.GetFullPath(_projectRoot);
        var shellProjection = ProjectsShellProjection.Build(normalizedRoot);
        var workCycle = ProjectWorkCycleProjection.Build(normalizedRoot, shellProjection);
        if (workCycle.PhaseState.Phase != SurfacePhase.Execution || workCycle.PhaseState.ExecutionSubphase != ExecutionSubphase.Preflight)
        {
            return false;
        }

        MaterializeValidatedIntentIfNeeded(normalizedRoot, shellProjection, workCycle.IntentSummary);
        var refreshedShell = ProjectsShellProjection.Build(normalizedRoot);
        var refreshedWorkCycle = ProjectWorkCycleProjection.Build(normalizedRoot, refreshedShell);
        var nextState = StepPhaseMachine.ConfirmPreflight(refreshedWorkCycle.PhaseState);
        SaveWorkCycleSnapshot(normalizedRoot, nextState, workCycle.IntentSummary, isClarificationActive: false, clarificationDraft: string.Empty);
        await _refreshShellAsync();
        return true;
    }

    public async Task<bool> BeginClarificationAsync(string clarificationDraft)
    {
        var normalizedRoot = Path.GetFullPath(_projectRoot);
        var shellProjection = ProjectsShellProjection.Build(normalizedRoot);
        var workCycle = ProjectWorkCycleProjection.Build(normalizedRoot, shellProjection);
        if (workCycle.PhaseState.Phase != SurfacePhase.Execution || workCycle.PhaseState.ExecutionSubphase != ExecutionSubphase.Preflight)
        {
            return false;
        }

        var clarified = StepPhaseMachine.ApplyClarification(workCycle.PhaseState);
        SaveWorkCycleSnapshot(normalizedRoot, clarified, workCycle.IntentSummary, isClarificationActive: true, clarificationDraft: clarificationDraft ?? string.Empty);
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

        var normalizedRoot = Path.GetFullPath(_projectRoot);
        var shellProjection = ProjectsShellProjection.Build(normalizedRoot);
        var workCycle = ProjectWorkCycleProjection.Build(normalizedRoot, shellProjection);
        if (workCycle.PhaseState.ExecutionSubphase != ExecutionSubphase.Preflight)
        {
            return false;
        }

        await _projectsAdapter.AddMessageAsync(
            ConversationItemKind.User,
            "User",
            normalizedText,
            metadata: BuildProjectConversationMetadata("discussion", shellProjection.ActiveTaskText));

        var updatedSummary = BuildIntentSummary(normalizedText);
        await _projectsAdapter.AddMessageAsync(
            ConversationItemKind.Lead,
            "Shift Lead",
            $"РЈС‚РѕС‡РЅРµРЅРёРµ РїСЂРёРЅСЏС‚Рѕ. РћР±РЅРѕРІРёР» РґРѕРіРѕРІРѕСЂС‘РЅРЅРѕСЃС‚СЊ: {updatedSummary}",
            metadata: BuildProjectConversationMetadata("discussion", shellProjection.ActiveTaskText));

        var clarified = StepPhaseMachine.ApplyClarification(workCycle.PhaseState);
        SaveWorkCycleSnapshot(normalizedRoot, clarified, updatedSummary, isClarificationActive: false, clarificationDraft: string.Empty);
        _updateDiscussionPreview();
        await _refreshShellAsync();
        return true;
    }

    public async Task<bool> CancelClarificationAsync()
    {
        var normalizedRoot = Path.GetFullPath(_projectRoot);
        var shellProjection = ProjectsShellProjection.Build(normalizedRoot);
        var workCycle = ProjectWorkCycleProjection.Build(normalizedRoot, shellProjection);
        SaveWorkCycleSnapshot(normalizedRoot, workCycle.PhaseState, workCycle.IntentSummary, isClarificationActive: false, clarificationDraft: string.Empty);
        await _refreshShellAsync();
        return true;
    }

    public async Task<bool> ReturnToChatAsync()
    {
        var normalizedRoot = Path.GetFullPath(_projectRoot);
        var shellProjection = ProjectsShellProjection.Build(normalizedRoot);
        var workCycle = ProjectWorkCycleProjection.Build(normalizedRoot, shellProjection);
        if (workCycle.PhaseState.ExecutionSubphase != ExecutionSubphase.Preflight)
        {
            return false;
        }

        var returned = StepPhaseMachine.CancelPreflight(workCycle.PhaseState);
        SaveWorkCycleSnapshot(normalizedRoot, returned, workCycle.IntentSummary, isClarificationActive: false, clarificationDraft: string.Empty);
        await _refreshShellAsync();
        return true;
    }

    internal static System.Collections.Generic.Dictionary<string, string> BuildProjectConversationMetadata(string phase, string activeTaskText)
    {
        var taskId = activeTaskText.Replace("Active task: ", string.Empty, StringComparison.Ordinal);
        return new System.Collections.Generic.Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["mode"] = "project",
            ["phase"] = phase,
            ["step-id"] = taskId
        };
    }

    internal static string BuildIntentSummary(string text)
    {
        return string.IsNullOrWhiteSpace(text) ? string.Empty : text.Trim();
    }

    internal static string BuildAgreementItemsText(string summary)
    {
        if (string.IsNullOrWhiteSpace(summary))
        {
            return "вЂ” Р”РѕРіРѕРІРѕСЂС‘РЅРЅРѕСЃС‚СЊ РµС‰С‘ РЅРµ СЃС„РѕСЂРјРёСЂРѕРІР°РЅР°.";
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

        return string.Join(Environment.NewLine, parts.Select(part => $"вЂ” {part}"));
    }

    internal static void SaveWorkCycleSnapshot(string normalizedRoot, StepPhaseState phaseState, string intentSummary, bool isClarificationActive, string clarificationDraft)
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
                IsResultAccepted: false,
                ExecutionRefinement: BuildAgreementItemsText(intentSummary),
                PreflightClarificationText: clarificationDraft,
                RevisionIntakeText: string.Empty,
                RuntimeState: null,
                DemoState: null));
    }

    internal static bool IsPreflightClarificationActive(string normalizedRoot)
    {
        return ResumeStageStorage.Load(normalizedRoot)?.IsPreflightClarificationActive ?? false;
    }

    internal static void MaterializeValidatedIntentIfNeeded(string normalizedRoot, ProjectsShellProjection shellProjection, string intentSummary)
    {
        var projectState = ProjectStateStorage.Load(normalizedRoot);
        if (projectState.ActiveTaskId is not null)
        {
            return;
        }

        var intent = TaskIntentFactory
            .CreateCandidate(string.IsNullOrWhiteSpace(intentSummary) ? "Recovered task from UI validation." : intentSummary)
            .MarkReadyForValidation()
            .Validate();

        if (projectState.ActiveShiftId is null)
        {
            _ = ValidatedIntentShiftStarter.Start(projectState, intent, DateTimeOffset.Now);
            return;
        }

        var shift = ShiftStateStorage.Load(normalizedRoot, projectState.ActiveShiftId);
        var nextTaskId = BuildNextTaskId(shift);
        var applied = ValidatedIntentTaskApplier.Apply(projectState, shift, intent, nextTaskId, DateTimeOffset.Now);
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
}
