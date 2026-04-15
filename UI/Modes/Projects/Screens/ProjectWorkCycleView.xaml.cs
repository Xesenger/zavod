using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using zavod.UI.Modes.Chats;
using zavod.UI.Modes.Projects.Projections;
using zavod.UI.Text;

namespace zavod.UI.Modes.Projects.Screens;

public sealed partial class ProjectWorkCycleView : UserControl
{
    public ProjectWorkCycleView()
    {
        InitializeComponent();
        ApplyLocalization();
        AutomationProperties.SetAutomationId(Root, "Projects.WorkCycle");
        AutomationProperties.SetAutomationId(ProjectInteractionSurface, "Projects.WorkCycle.Surface.Chat");
        AutomationProperties.SetAutomationId(ProjectExecutionSurface, "Projects.WorkCycle.Surface.Execution");
        AutomationProperties.SetAutomationId(ProjectResultSurface, "Projects.WorkCycle.Surface.Result");
        AutomationProperties.SetAutomationId(EnterWorkButton, "Projects.WorkCycle.Action.EnterWork");
        AutomationProperties.SetAutomationId(ExecutionConfirmButton, "Projects.WorkCycle.Action.Confirm");
        AutomationProperties.SetAutomationId(ExecutionClarifyButton, "Projects.WorkCycle.Action.Clarify");
        AutomationProperties.SetAutomationId(ExecutionReturnToChatButton, "Projects.WorkCycle.Action.ReturnToChat");
    }

    public Grid RootView => Root;
    public ColumnDefinition ChatColumn => WorkCycleChatColumn;
    public ColumnDefinition ExecutionColumn => WorkCycleExecutionColumn;
    public ColumnDefinition ResultColumn => WorkCycleResultColumn;
    public Border ChatSurface => ProjectInteractionSurface;
    public Border ExecutionSurface => ProjectExecutionSurface;
    public Border ResultSurface => ProjectResultSurface;
    public TextBlock ProjectContextBlock => WorkCycleProjectContextText;
    public TextBlock ChatSurfaceStateBlock => WorkCycleChatSurfaceStateText;
    public TextBlock PhaseBlock => WorkCyclePhaseText;
    public TextBlock SummaryBlock => WorkCycleSummaryText;
    public ChatsWebRendererView ConversationRenderer => ProjectsConversationRenderer;
    public StackPanel FocusBar => WorkCycleFocusBar;
    public Button FocusAutoButton => WorkCycleFocusAutoButton;
    public Button FocusChatButton => WorkCycleFocusChatButton;
    public Button FocusExecutionButton => WorkCycleFocusExecutionButton;
    public Button FocusResultButton => WorkCycleFocusResultButton;
    public Border ProjectCard => WorkCycleProjectCard;
    public TextBlock ProjectNameBlock => ProjectNameText;
    public TextBlock ProjectRootBlock => ProjectRootText;
    public Border StateCard => WorkCycleStateCard;
    public TextBlock EntryStateBlock => EntryStateText;
    public TextBlock StorageStateBlock => StorageStateText;
    public TextBlock ActiveShiftBlock => ActiveShiftText;
    public TextBlock ActiveTaskBlock => ActiveTaskText;
    public TextBlock ChatSummaryTitleBlock => WorkCycleChatSummaryTitleText;
    public TextBlock ChatSummaryBlock => WorkCycleChatSummaryText;
    public TextBlock ChatSummaryNoteBlock => WorkCycleChatSummaryNoteText;
    public Button EnterWorkAction => EnterWorkButton;
    public Grid ComposerGrid => ProjectsComposerGrid;
    public TextBox ComposerTextBox => ProjectsComposerTextBox;
    public Button ComposerSendAction => ProjectsComposerSendButton;
    public TextBlock ExecutionSurfaceStateBlock => WorkCycleExecutionSurfaceStateText;
    public TextBlock ExecutionSummaryBlock => WorkCycleExecutionSummaryText;
    public StackPanel ExecutionValidationPanelView => ExecutionValidationPanel;
    public StackPanel ExecutionRuntimePanelView => ExecutionRuntimePanel;
    public TextBlock ExecutionStateBlock => ExecutionStateText;
    public TextBlock ExecutionValidationReasonBlock => ExecutionValidationReasonText;
    public TextBlock ExecutionValidationSummaryBlock => ExecutionValidationSummaryText;
    public TextBlock ExecutionValidationItemsBlock => ExecutionValidationItemsText;
    public TextBlock ExecutionRuntimeStateBlock => ExecutionRuntimeStateText;
    public TextBlock ExecutionDetailBlock => ExecutionDetailText;
    public StackPanel ExecutionValidationActionsPanelView => ExecutionValidationActionsPanel;
    public Button ExecutionConfirmAction => ExecutionConfirmButton;
    public Button ExecutionClarifyAction => ExecutionClarifyButton;
    public Button ExecutionReturnToChatAction => ExecutionReturnToChatButton;
    public StackPanel ExecutionClarificationPanelView => ExecutionClarificationPanel;
    public TextBox ExecutionClarificationTextBoxView => ExecutionClarificationTextBox;
    public Button ExecutionApplyClarificationAction => ExecutionApplyClarificationButton;
    public Button ExecutionCancelClarificationAction => ExecutionCancelClarificationButton;
    public StackPanel ExecutionRuntimeDetailsPanelView => ExecutionRuntimeDetailsPanel;
    public TextBlock DocumentStageBlock => DocumentStageText;
    public TextBlock ProjectDocumentPathBlock => ProjectDocumentPathText;
    public Button OpenProjectDocumentAction => OpenProjectDocumentButton;
    public Button OpenCapsuleDocumentAction => OpenCapsuleDocumentButton;
    public StackPanel ExecutionActionStubBarView => ExecutionActionStubBar;
    public TextBlock ResultSurfaceStateBlock => WorkCycleResultSurfaceStateText;
    public TextBlock ResultSummaryBlock => WorkCycleResultSummaryText;
    public TextBlock ResultStateBlock => ResultStateText;
    public TextBlock ResultDetailBlock => ResultDetailText;
    public TextBlock ResultMetadataBlock => WorkCycleResultMetadataText;
    public StackPanel ResultActionStubBarView => ResultActionStubBar;
    public Button ResultAcceptAction => ResultAcceptStubButton;
    public Button ResultReviseAction => ResultReviseStubButton;
    public Button ResultRejectAction => ResultRejectStubButton;
    public TextBlock ResultNotesBlock => ResultNotesText;
    public Button ReturnToExecutionAction => ReturnToExecutionSurfaceButton;

    public void ApplyLocalization()
    {
        var text = AppText.Current;
        ChatTitleText.Text = text.Get("projects.work_cycle.chat.title");
        WorkCycleFocusAutoButton.Content = text.Get("projects.work_cycle.focus.auto");
        WorkCycleFocusChatButton.Content = text.Get("projects.work_cycle.focus.chat");
        WorkCycleFocusExecutionButton.Content = text.Get("projects.work_cycle.focus.execution");
        WorkCycleFocusResultButton.Content = text.Get("projects.work_cycle.focus.result");
        ProjectTitleText.Text = text.Get("projects.work_cycle.project.title");
        ProjectRootTitleText.Text = text.Get("projects.work_cycle.project.root");
        StateTitleText.Text = text.Get("projects.work_cycle.state.title");
        EnterWorkButton.Content = text.Get("projects.work_cycle.enter_work");
        ProjectsComposerTextBox.PlaceholderText = text.Get("projects.work_cycle.composer.placeholder");
        ProjectsComposerSendButton.Content = text.Get("projects.work_cycle.composer.send");
        ExecutionTitleText.Text = text.Get("projects.work_cycle.execution.title");
        ExecutionStateTitleText.Text = text.Get("projects.work_cycle.execution.state");
        ValidationTitleText.Text = text.Get("projects.work_cycle.validation.title");
        ExecutionRuntimeStateTitleText.Text = text.Get("projects.work_cycle.execution.state");
        AvailableEvidenceTitleText.Text = text.Get("projects.work_cycle.available_evidence");
        ExecutionConfirmButton.Content = text.Get("projects.work_cycle.execution.confirm");
        ExecutionClarifyButton.Content = text.Get("projects.work_cycle.execution.clarify");
        ExecutionReturnToChatButton.Content = text.Get("projects.work_cycle.execution.return_to_chat");
        ExecutionClarificationTextBox.PlaceholderText = text.Get("projects.work_cycle.execution.clarification_placeholder");
        ExecutionApplyClarificationButton.Content = text.Get("projects.work_cycle.execution.apply_clarification");
        ExecutionCancelClarificationButton.Content = text.Get("projects.work_cycle.execution.cancel_clarification");
        OpenProjectDocumentButton.Content = text.Get("projects.work_cycle.open_project_doc");
        OpenCapsuleDocumentButton.Content = text.Get("projects.work_cycle.open_capsule_doc");
        ExecutionConfirmStubButton.Content = text.Get("projects.work_cycle.execution.confirm_stub");
        ExecutionClarifyStubButton.Content = text.Get("projects.work_cycle.execution.clarify_stub");
        ExecutionCancelStubButton.Content = text.Get("projects.work_cycle.execution.cancel_stub");
        ResultTitleText.Text = text.Get("projects.work_cycle.result.title");
        ResultStateTitleText.Text = text.Get("projects.work_cycle.result.state");
        ResultAcceptStubButton.Content = text.Get("projects.work_cycle.result.accept");
        ResultReviseStubButton.Content = text.Get("projects.work_cycle.result.revise");
        ResultRejectStubButton.Content = text.Get("projects.work_cycle.result.reject");
        WithheldTitleText.Text = text.Get("projects.work_cycle.result.withheld");
        ReturnToExecutionSurfaceButton.Content = text.Get("projects.work_cycle.result.return_to_execution");
    }

    public void ApplyShellState(
        string projectName,
        string projectRoot,
        string entryState,
        string activeShift,
        string activeTask,
        string storageState,
        string documentStage,
        string projectDocumentPath,
        bool hasProjectDocument,
        string? projectDocumentTag,
        bool hasCapsuleDocument,
        string? capsuleDocumentTag,
        string executionState,
        string executionDetail,
        string resultState,
        string resultDetail)
    {
        ProjectNameText.Text = projectName;
        ProjectRootText.Text = projectRoot;
        EntryStateText.Text = entryState;
        ActiveShiftText.Text = activeShift;
        ActiveTaskText.Text = activeTask;
        StorageStateText.Text = storageState;
        DocumentStageText.Text = documentStage;
        ProjectDocumentPathText.Text = projectDocumentPath;
        OpenProjectDocumentButton.IsEnabled = hasProjectDocument;
        OpenCapsuleDocumentButton.IsEnabled = hasCapsuleDocument;
        OpenProjectDocumentButton.Tag = projectDocumentTag;
        OpenCapsuleDocumentButton.Tag = capsuleDocumentTag;
        ExecutionStateText.Text = executionState;
        ExecutionDetailText.Text = executionDetail;
        ResultStateText.Text = resultState;
        ResultDetailText.Text = resultDetail;
    }

    public void ApplyWorkCycleState(
        ProjectWorkCycleProjection workCycle,
        string projectContext,
        bool showEnterWork,
        bool composerEnabled,
        bool clarificationVisible,
        string validationReason,
        string validationSummary,
        string validationItems)
    {
        FocusBar.Visibility = workCycle.ShowSurfaceNavigation ? Visibility.Visible : Visibility.Collapsed;
        ProjectContextBlock.Text = projectContext;
        ChatSurfaceStateBlock.Text = workCycle.ChatSurfaceStateText;
        PhaseBlock.Text = $"{AppText.Current.Get("projects.work_cycle.phase_prefix")}{workCycle.PhaseLabel}";
        SummaryBlock.Text = workCycle.SummaryText;
        ChatSummaryTitleBlock.Text = workCycle.ChatSummaryTitle;
        ChatSummaryBlock.Text = workCycle.ChatSummaryText;
        ChatSummaryNoteBlock.Text = workCycle.ChatSummaryNote;
        ExecutionSurfaceStateBlock.Text = workCycle.ExecutionSurfaceStateText;
        ExecutionSummaryBlock.Text = workCycle.ExecutionSummaryText;
        ExecutionDetailBlock.Text = workCycle.ExecutionDetailText;
        DocumentStageBlock.Text = workCycle.ExecutionEvidenceText;
        ResultSurfaceStateBlock.Text = workCycle.ResultSurfaceStateText;
        ResultSummaryBlock.Text = workCycle.ResultSummaryText;
        ResultDetailBlock.Text = workCycle.ResultDetailText;
        ResultNotesBlock.Text = workCycle.ResultEvidenceText;
        ResultMetadataBlock.Text = workCycle.ResultMetadataText;
        ChatSummaryNoteBlock.Text = AppText.Current.Get("projects.work_cycle.chat.summary_note_active");
        ChatSurface.Visibility = workCycle.Projection.ShowChat ? Visibility.Visible : Visibility.Collapsed;
        ExecutionSurface.Visibility = workCycle.Projection.ShowExecution ? Visibility.Visible : Visibility.Collapsed;
        ResultSurface.Visibility = workCycle.Projection.ShowResult ? Visibility.Visible : Visibility.Collapsed;
        ProjectCard.Visibility = workCycle.PhaseState.Phase == Flow.SurfacePhase.Discussion ? Visibility.Collapsed : Visibility.Visible;
        StateCard.Visibility = workCycle.PhaseState.Phase == Flow.SurfacePhase.Discussion ? Visibility.Collapsed : Visibility.Visible;
        ChatSurfaceStateBlock.Visibility = workCycle.PhaseState.Phase == Flow.SurfacePhase.Discussion ? Visibility.Collapsed : Visibility.Visible;
        SummaryBlock.Visibility = workCycle.PhaseState.Phase == Flow.SurfacePhase.Discussion ? Visibility.Collapsed : Visibility.Visible;
        EnterWorkAction.Visibility = showEnterWork ? Visibility.Visible : Visibility.Collapsed;
        ComposerGrid.Visibility = Visibility.Collapsed;
        ComposerTextBox.IsEnabled = composerEnabled;
        ComposerSendAction.IsEnabled = composerEnabled;
        ExecutionValidationPanelView.Visibility = workCycle.PhaseState.Phase == Flow.SurfacePhase.Execution
            && workCycle.PhaseState.ExecutionSubphase == Flow.ExecutionSubphase.Preflight
            ? Visibility.Visible
            : Visibility.Collapsed;
        ExecutionRuntimePanelView.Visibility = ExecutionValidationPanelView.Visibility == Visibility.Visible
            ? Visibility.Collapsed
            : Visibility.Visible;
        ExecutionValidationActionsPanelView.Visibility = ExecutionValidationPanelView.Visibility;
        ExecutionRuntimeDetailsPanelView.Visibility = ExecutionValidationPanelView.Visibility == Visibility.Visible
            ? Visibility.Collapsed
            : Visibility.Visible;
        ExecutionClarificationPanelView.Visibility = clarificationVisible ? Visibility.Visible : Visibility.Collapsed;
        ExecutionValidationReasonBlock.Text = validationReason;
        ExecutionValidationSummaryBlock.Text = validationSummary;
        ExecutionValidationItemsBlock.Text = validationItems;
        ExecutionRuntimeStateBlock.Text = ExecutionStateBlock.Text;
        ExecutionActionStubBarView.Visibility = workCycle.Projection.ShowExecution ? Visibility.Visible : Visibility.Collapsed;
        ResultActionStubBarView.Visibility = workCycle.Projection.ShowResult ? Visibility.Visible : Visibility.Collapsed;
        var resultActionsEnabled = workCycle.PhaseState.Phase == Flow.SurfacePhase.Result
            && workCycle.PhaseState.ResultSubphase == Flow.ResultSubphase.Ready;
        ResultAcceptAction.IsEnabled = resultActionsEnabled;
        ResultReviseAction.IsEnabled = resultActionsEnabled;
        ResultRejectAction.IsEnabled = resultActionsEnabled;
    }

    public void SetDiscussionPreviewText(string text)
    {
        _ = text;
    }
}
