using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using System;
using System.Diagnostics;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Windows.UI;
using zavod.Bootstrap;
using zavod.Contexting;
using zavod.Flow;
using zavod.Persistence;
using zavod.UI.Rendering.Conversation;
using zavod.UI.Modes.Chats;
using zavod.UI.Modes.Projects.Projections;
using zavod.UI.Shell.Verification;
using zavod.State;
using zavod.UI.Modes.Projects.WorkCycle.Actions;

namespace zavod
{
    public sealed partial class MainWindow : Window
    {
        private sealed class ChatSessionState
        {
            public ChatSessionState(string id, ChatsAdapter adapter, string title, bool isDraft)
            {
                Id = id;
                Adapter = adapter;
                Title = title;
                IsDraft = isDraft;
            }

            public string Id { get; }

            public ChatsAdapter Adapter { get; }

            public string Title { get; set; }

            public bool IsDraft { get; set; }
        }

        private readonly string _projectRoot;
        private readonly bool _isDemoMode;
        private readonly ConversationLogStorage _chatStorage;
        private readonly ProjectsAdapter _projectsAdapter;
        private readonly WorkCycleActionController _workCycleActions;
        private readonly List<ChatSessionState> _chatSessions = new();
        private ChatsAdapter _chatsAdapter;
        private ChatSessionState? _activeChatSession;
        private ChatSessionState? _draftChatSession;
        private AppMode _selectedMode = AppMode.Projects;
        private ProjectsScreen _projectsScreen = ProjectsScreen.List;
        private WorkCycleFocus _workCycleFocus = WorkCycleFocus.Auto;
        private bool _initialRefreshRequested;
        private bool _chatsReady;
        private bool _projectsReady;
        public MainWindow(string projectRoot, bool isDemoMode = false)
        {
            _projectRoot = projectRoot ?? throw new ArgumentNullException(nameof(projectRoot));
            _isDemoMode = isDemoMode;

            InitializeComponent();
            _chatStorage = ConversationLogStorage.ForChats(Path.GetFullPath(_projectRoot));
            _chatsAdapter = new ChatsAdapter();
            _projectsAdapter = new ProjectsAdapter(storage: ConversationLogStorage.ForProjects(Path.GetFullPath(_projectRoot)));
            ChatsHostView.ConversationView.Adapter = _chatsAdapter;
            ModeSwitchView.ChatsClicked += ChatsModeButton_Click;
            ModeSwitchView.ProjectsClicked += ProjectsModeButton_Click;
            ChatsHostView.ComposerSendClicked += ChatsComposerSendButton_Click;
            ChatsHostView.NewChatClicked += ChatsNewChatButton_Click;
            ChatsHostView.ChatRowClicked += ChatsHostView_ChatRowClicked;
            ProjectsHostView.ListView.OpenCurrentProjectAction.Click += OpenCurrentProjectButton_Click;
            ProjectsHostView.HomeView.OpenProjectHtmlAction.Click += OpenProjectHtmlButton_Click;
            ProjectsHostView.HomeView.OpenProjectDocumentAction.Click += OpenProjectDocumentButton_Click;
            ProjectsHostView.HomeView.OpenWorkCycleAction.Click += OpenWorkCycleButton_Click;
            ProjectsHostView.WorkCycleView.FocusAutoButton.Click += WorkCycleFocusAutoButton_Click;
            ProjectsHostView.WorkCycleView.FocusChatButton.Click += WorkCycleFocusChatButton_Click;
            ProjectsHostView.WorkCycleView.FocusExecutionButton.Click += WorkCycleFocusExecutionButton_Click;
            ProjectsHostView.WorkCycleView.FocusResultButton.Click += WorkCycleFocusResultButton_Click;
            ProjectsHostView.WorkCycleView.EnterWorkAction.Click += EnterWorkButton_WrapperClick;
            ProjectsHostView.WorkCycleView.ComposerSendAction.Click += ProjectsComposerSendButton_WrapperClick;
            ProjectsHostView.WorkCycleView.ExecutionConfirmAction.Click += ExecutionConfirmButton_WrapperClick;
            ProjectsHostView.WorkCycleView.ExecutionClarifyAction.Click += ExecutionClarifyButton_WrapperClick;
            ProjectsHostView.WorkCycleView.ExecutionReturnToChatAction.Click += ExecutionReturnToChatButton_WrapperClick;
            ProjectsHostView.WorkCycleView.ExecutionApplyClarificationAction.Click += ExecutionApplyClarificationButton_WrapperClick;
            ProjectsHostView.WorkCycleView.ExecutionCancelClarificationAction.Click += ExecutionCancelClarificationButton_WrapperClick;
            ProjectsHostView.WorkCycleView.OpenProjectDocumentAction.Click += OpenProjectDocumentButton_Click;
            ProjectsHostView.WorkCycleView.OpenCapsuleDocumentAction.Click += OpenCapsuleDocumentButton_Click;
            ProjectsHostView.WorkCycleView.ReturnToExecutionAction.Click += ReturnToExecutionSurfaceButton_Click;

            _workCycleActions = new WorkCycleActionController(
                _projectRoot,
                _projectsAdapter,
                RefreshRecoveryShellAsync,
                UpdateProjectsDiscussionPreview);

            Title = GetWindowTitle();
            WindowTitleText.Text = Title;
            Activated += MainWindow_Activated;
        }

        private ColumnDefinition WorkCycleChatColumn => ProjectsHostView.WorkCycleView.ChatColumn;
        private ColumnDefinition WorkCycleExecutionColumn => ProjectsHostView.WorkCycleView.ExecutionColumn;
        private ColumnDefinition WorkCycleResultColumn => ProjectsHostView.WorkCycleView.ResultColumn;
        private Border ProjectInteractionSurface => ProjectsHostView.WorkCycleView.ChatSurface;
        private Border ProjectExecutionSurface => ProjectsHostView.WorkCycleView.ExecutionSurface;
        private Border ProjectResultSurface => ProjectsHostView.WorkCycleView.ResultSurface;
        private TextBlock WorkCycleProjectContextText => ProjectsHostView.WorkCycleView.ProjectContextBlock;
        private TextBlock WorkCycleChatSurfaceStateText => ProjectsHostView.WorkCycleView.ChatSurfaceStateBlock;
        private TextBlock WorkCyclePhaseText => ProjectsHostView.WorkCycleView.PhaseBlock;
        private TextBlock WorkCycleSummaryText => ProjectsHostView.WorkCycleView.SummaryBlock;
        private StackPanel WorkCycleFocusBar => ProjectsHostView.WorkCycleView.FocusBar;
        private Button WorkCycleFocusAutoButton => ProjectsHostView.WorkCycleView.FocusAutoButton;
        private Button WorkCycleFocusChatButton => ProjectsHostView.WorkCycleView.FocusChatButton;
        private Button WorkCycleFocusExecutionButton => ProjectsHostView.WorkCycleView.FocusExecutionButton;
        private Button WorkCycleFocusResultButton => ProjectsHostView.WorkCycleView.FocusResultButton;
        private Border WorkCycleProjectCard => ProjectsHostView.WorkCycleView.ProjectCard;
        private Button EnterWorkButton => ProjectsHostView.WorkCycleView.EnterWorkAction;
        private TextBox ProjectsComposerTextBox => ProjectsHostView.WorkCycleView.ComposerTextBox;
        private Button ProjectsComposerSendButton => ProjectsHostView.WorkCycleView.ComposerSendAction;
        private StackPanel ExecutionValidationPanel => ProjectsHostView.WorkCycleView.ExecutionValidationPanelView;
        private StackPanel ExecutionRuntimePanel => ProjectsHostView.WorkCycleView.ExecutionRuntimePanelView;
        private StackPanel ExecutionValidationActionsPanel => ProjectsHostView.WorkCycleView.ExecutionValidationActionsPanelView;
        private Button ExecutionConfirmButton => ProjectsHostView.WorkCycleView.ExecutionConfirmAction;
        private Button ExecutionClarifyButton => ProjectsHostView.WorkCycleView.ExecutionClarifyAction;
        private Button ExecutionReturnToChatButton => ProjectsHostView.WorkCycleView.ExecutionReturnToChatAction;
        private StackPanel ExecutionClarificationPanel => ProjectsHostView.WorkCycleView.ExecutionClarificationPanelView;
        private TextBox ExecutionClarificationTextBox => ProjectsHostView.WorkCycleView.ExecutionClarificationTextBoxView;
        private Button ExecutionApplyClarificationButton => ProjectsHostView.WorkCycleView.ExecutionApplyClarificationAction;
        private Button ExecutionCancelClarificationButton => ProjectsHostView.WorkCycleView.ExecutionCancelClarificationAction;
        private StackPanel ExecutionRuntimeDetailsPanel => ProjectsHostView.WorkCycleView.ExecutionRuntimeDetailsPanelView;
        private Button OpenProjectDocumentButton => ProjectsHostView.WorkCycleView.OpenProjectDocumentAction;
        private Button OpenCapsuleDocumentButton => ProjectsHostView.WorkCycleView.OpenCapsuleDocumentAction;
        private StackPanel ExecutionActionStubBar => ProjectsHostView.WorkCycleView.ExecutionActionStubBarView;
        private StackPanel ResultActionStubBar => ProjectsHostView.WorkCycleView.ResultActionStubBarView;
        private Button ReturnToExecutionSurfaceButton => ProjectsHostView.WorkCycleView.ReturnToExecutionAction;

        private async void MainWindow_Activated(object sender, WindowActivatedEventArgs args)
        {
            if (_initialRefreshRequested)
            {
                return;
            }

            _initialRefreshRequested = true;
            Activated -= MainWindow_Activated;
            await RefreshRecoveryShellAsync();
        }

        private async Task RefreshRecoveryShellAsync()
        {
            var normalizedRoot = Path.GetFullPath(_projectRoot);
            var projection = ProjectsShellProjection.Build(normalizedRoot);

            ProjectsHostView.WorkCycleView.ApplyShellState(
                projectName: projection.ProjectName,
                projectRoot: projection.ProjectRoot,
                entryState: projection.EntryStateText,
                activeShift: projection.ActiveShiftText,
                activeTask: projection.ActiveTaskText,
                storageState: projection.StorageStateText,
                documentStage: projection.DocumentStageText,
                projectDocumentPath: projection.ProjectDocumentPathText,
                hasProjectDocument: projection.HasProjectDocument,
                projectDocumentTag: projection.ProjectDocumentPath,
                hasCapsuleDocument: projection.HasCapsuleDocument,
                capsuleDocumentTag: projection.CapsuleDocumentPath,
                executionState: projection.ActiveTaskText.EndsWith("none", StringComparison.Ordinal) ? "Recovery hold" : "Task context detected",
                executionDetail: projection.ActiveTaskText.EndsWith("none", StringComparison.Ordinal)
                    ? "Execution wiring is still disabled. The center surface is back only as a verified shell."
                    : $"{projection.ActiveTaskText.Replace("Active task: ", "A live task id exists (", StringComparison.Ordinal)}) but execution controls remain intentionally disconnected during recovery.",
                resultState: projection.HasCapsuleDocument ? "Evidence available" : "No result loaded",
                resultDetail: projection.HasCapsuleDocument
                    ? "A capsule document exists and can be opened from the execution surface."
                    : "No capsule/result document is materialized yet.");

            await EnsureChatsConversationAsync(normalizedRoot);
            ApplyChatsLayout(normalizedRoot);

            ProjectsHostView.ListView.ApplyContent(
                summary: "Choose a project entry block. Import and New Project stay disconnected in this slice.",
                currentProject: projection.ProjectListCurrentProjectText,
                currentStage: projection.ProjectListCurrentStageText,
                currentDetails: "Current project entry remains read-only in this slice.",
                notes: "This list stays read-only for now.");

            ProjectsHostView.HomeView.ApplyContent(
                summary: "Open HTML, inspect materials, and read a few truthful status lines.",
                status: projection.ProjectHomeStatusText,
                stage: projection.ProjectHomeStageText,
                activity: projection.ProjectHomeActivityText,
                materials: projection.ProjectHomeMaterialsText,
                notes: "This surface stays read-only in the current slice.");

            ProjectsHostView.HomeView.SetActionState(
                hasProjectHtml: projection.HasProjectHtml,
                projectHtmlPath: projection.ProjectHtmlPath,
                hasProjectDocument: projection.HasProjectDocument,
                projectDocumentPath: projection.ProjectDocumentPath);
            await EnsureProjectsConversationAsync(normalizedRoot, projection);
            UpdateProjectsDiscussionPreview();

            var workCycle = ProjectWorkCycleProjection.Build(normalizedRoot, projection);
            ProjectsHostView.WorkCycleView.ApplyWorkCycleState(
                workCycle,
                projectContext: $"{projection.ProjectName}  |  {projection.ProjectRoot}",
                showEnterWork: _projectsScreen == ProjectsScreen.WorkCycle && workCycle.Projection.CanStartIntentValidation,
                composerEnabled: _projectsScreen == ProjectsScreen.WorkCycle && workCycle.PhaseState.Phase == Flow.SurfacePhase.Discussion,
                clarificationVisible: WorkCycleActionController.IsPreflightClarificationActive(normalizedRoot),
                validationReason: "РџСЂРѕРІРµСЂРєР° РґРѕРіРѕРІРѕСЂС‘РЅРЅРѕСЃС‚Рё РїРµСЂРµРґ Р·Р°РїСѓСЃРєРѕРј.",
                validationSummary: string.IsNullOrWhiteSpace(workCycle.IntentSummary)
                    ? "Р”РѕРіРѕРІРѕСЂС‘РЅРЅРѕСЃС‚СЊ РµС‰С‘ РЅРµ СЃС„РѕСЂРјРёСЂРѕРІР°РЅР°."
                    : workCycle.IntentSummary,
                validationItems: WorkCycleActionController.BuildAgreementItemsText(workCycle.IntentSummary));
            ApplyWorkCycleColumnLayout(workCycle);
            ApplyWorkCyclePhaseChrome(workCycle);

            ApplyModeChrome();
            ApplyProjectsScreenChrome();
        }

        private async Task EnsureChatsConversationAsync(string normalizedRoot)
        {
            if (_chatsReady)
            {
                ChatsHostView.ConversationView.RefreshItems();
                return;
            }

            var restoredAdapter = new ChatsAdapter(storage: _chatStorage)
            {
                PersistenceEnabled = false
            };

            if (await restoredAdapter.RestorePersistedAsync() == 0 && restoredAdapter.Items.Count == 0)
            {
                ActivateChatSession(null);
                ChatsHostView.ConversationView.RefreshItems();
                _chatsReady = true;
                return;
            }

            var session = new ChatSessionState(
                id: Guid.NewGuid().ToString("N"),
                adapter: restoredAdapter,
                title: BuildChatTitle(restoredAdapter),
                isDraft: false);
            _chatSessions.Clear();
            _chatSessions.Add(session);
            ActivateChatSession(session);
            ChatsHostView.ConversationView.RefreshItems();
            _chatsReady = true;
        }

        private void ApplyChatsLayout(string normalizedRoot)
        {
            var hasConversation = _chatsAdapter.Items.Count > 0;

            ChatsHostView.SetSummaryVisibility(Visibility.Collapsed);
            ChatsHostView.SetConversationVisible(hasConversation);
            ChatsHostView.SetSidebarState(
                visible: true,
                title: string.Empty,
                meta: string.Empty,
                note: string.Empty,
                width: 270);
            ChatsHostView.SetSidebarEntries(
                _chatSessions
                    .Select(session => new ChatsSidebarEntry(session.Id, session.Title))
                    .ToArray(),
                _activeChatSession is { IsDraft: false } ? _activeChatSession.Id : null);
            ChatsHostView.SetEmptyState(
                visible: !hasConversation,
                headline: "Quiet space for a thought",
                subtitle: "Start anywhere. Structure can arrive later.");
            ChatsHostView.SetPersistenceState(
                text: hasConversation
                    ? $"Saved locally. Messages in current chat: {_chatsAdapter.Items.Count}."
                    : string.Empty,
                visible: hasConversation);
            ChatsHostView.SetAttachVisible(hasConversation);
            ChatsHostView.SetComposerPlacement(hasConversation);
        }

        private static string BuildChatTitle(ChatsAdapter adapter)
        {
            foreach (var item in adapter.Items)
            {
                if (item.Kind != ConversationItemKind.User)
                {
                    continue;
                }

                var text = item.Text?.Trim();
                if (string.IsNullOrWhiteSpace(text))
                {
                    continue;
                }

                var singleLine = text.Replace("\r", " ").Replace("\n", " ").Trim();
                if (singleLine.Length <= 34)
                {
                    return singleLine;
                }

                return $"{singleLine[..31].TrimEnd()}...";
            }

            return "Current chat";
        }

        private void ActivateChatSession(ChatSessionState? session)
        {
            _activeChatSession = session;
            _chatsAdapter = session?.Adapter ?? new ChatsAdapter();
            ChatsHostView.ConversationView.Adapter = _chatsAdapter;
            ChatsHostView.ConversationView.RefreshItems();

            if (session is { IsDraft: false })
            {
                PersistActiveChatSession();
            }
        }

        private void PersistActiveChatSession()
        {
            if (_activeChatSession is null || _activeChatSession.IsDraft)
            {
                return;
            }

            var snapshots = _activeChatSession.Adapter.Items
                .Select(item => new ConversationLogSnapshot(
                    item.Id,
                    item.Timestamp,
                    item.AuthorLabel,
                    item.Kind.ToString(),
                    item.Text,
                    item.Text,
                    StepId: null,
                    Phase: null,
                    Attachments: Array.Empty<string>(),
                    Source: "chats",
                    Adapter: "chats",
                    item.IsStreaming,
                    Metadata: null))
                .ToArray();

            _chatStorage.ReplaceAll(snapshots);
        }

        private void ChatsNewChatButton_Click(object? sender, RoutedEventArgs e)
        {
            if (_draftChatSession is not null)
            {
                ActivateChatSession(_draftChatSession);
                ApplyChatsLayout(Path.GetFullPath(_projectRoot));
                return;
            }

            _draftChatSession = new ChatSessionState(
                id: Guid.NewGuid().ToString("N"),
                adapter: new ChatsAdapter(),
                title: "New chat",
                isDraft: true);
            ActivateChatSession(_draftChatSession);
            ApplyChatsLayout(Path.GetFullPath(_projectRoot));
        }

        private void ChatsHostView_ChatRowClicked(object? sender, string id)
        {
            var session = _chatSessions.FirstOrDefault(candidate => string.Equals(candidate.Id, id, StringComparison.Ordinal));
            if (session is null)
            {
                return;
            }

            ActivateChatSession(session);
            ApplyChatsLayout(Path.GetFullPath(_projectRoot));
        }

        private void UpdateProjectsDiscussionPreview()
        {
            var items = _projectsAdapter.Items;
            if (items.Count == 0)
            {
                ProjectsHostView.WorkCycleView.SetDiscussionPreviewText("Discussion preview will appear here as soon as the first project messages are available.");
                return;
            }

            var lines = items
                .TakeLast(6)
                .Select(item => $"{item.AuthorLabel}{Environment.NewLine}{item.Text}".Trim())
                .ToArray();
            ProjectsHostView.WorkCycleView.SetDiscussionPreviewText(string.Join($"{Environment.NewLine}{Environment.NewLine}", lines));
        }

        private async Task EnsureProjectsConversationAsync(string normalizedRoot, ProjectsShellProjection projection)
        {
            if (_projectsReady)
            {
                return;
            }

            if (await _projectsAdapter.RestorePersistedAsync() == 0 && _projectsAdapter.Items.Count == 0)
            {
                _projectsAdapter.PersistenceEnabled = false;
                try
                {
                    await _projectsAdapter.AddMessageAsync(
                        ConversationItemKind.Lead,
                        "Shift Lead",
                        $"РџСЂРёРІРµС‚. РњС‹ РІ РїСЂРѕРµРєС‚Рµ **{projection.ProjectName}**. РћРїРёС€Рё, С‡С‚Рѕ РёРјРµРЅРЅРѕ С…РѕС‡РµС€СЊ СЃРґРµР»Р°С‚СЊ, Рё СЏ РїРѕРјРѕРіСѓ РґРѕРІРµСЃС‚Рё Р·Р°РґР°С‡Сѓ РґРѕ СЃРѕСЃС‚РѕСЏРЅРёСЏ, РєРѕРіРґР° РµС‘ РјРѕР¶РЅРѕ РѕС‚РїСЂР°РІРёС‚СЊ РІ СЂР°Р±РѕС‚Сѓ.",
                        metadata: WorkCycleActionController.BuildProjectConversationMetadata("discussion", projection.ActiveTaskText));
                }
                finally
                {
                    _projectsAdapter.PersistenceEnabled = true;
                }
            }

            _projectsReady = true;
        }

        private void RefreshButton_Click(object sender, RoutedEventArgs e)
        {
            _ = RefreshRecoveryShellAsync();
        }

        private void ProjectsComposerSendButton_WrapperClick(object sender, RoutedEventArgs e)
        {
            _ = SendProjectsMessageThroughControllerAsync();
        }

        private async Task SendProjectsMessageThroughControllerAsync()
        {
            if (_selectedMode != AppMode.Projects || _projectsScreen != ProjectsScreen.WorkCycle)
            {
                return;
            }

            if (await _workCycleActions.SendProjectsMessageAsync(ProjectsComposerTextBox.Text ?? string.Empty))
            {
                ProjectsComposerTextBox.Text = string.Empty;
            }
        }

        private void EnterWorkButton_WrapperClick(object sender, RoutedEventArgs e)
        {
            _ = _workCycleActions.EnterWorkAsync();
        }

        private void ExecutionConfirmButton_WrapperClick(object sender, RoutedEventArgs e)
        {
            _ = _workCycleActions.ConfirmPreflightAsync();
        }

        private void ExecutionClarifyButton_WrapperClick(object sender, RoutedEventArgs e)
        {
            _ = _workCycleActions.BeginClarificationAsync(ExecutionClarificationTextBox.Text ?? string.Empty);
        }

        private async void ExecutionApplyClarificationButton_WrapperClick(object sender, RoutedEventArgs e)
        {
            if (await _workCycleActions.ApplyClarificationAsync(ExecutionClarificationTextBox.Text ?? string.Empty))
            {
                ExecutionClarificationTextBox.Text = string.Empty;
            }
        }

        private void ExecutionCancelClarificationButton_WrapperClick(object sender, RoutedEventArgs e)
        {
            ExecutionClarificationTextBox.Text = string.Empty;
            _ = _workCycleActions.CancelClarificationAsync();
        }

        private void ExecutionReturnToChatButton_WrapperClick(object sender, RoutedEventArgs e)
        {
            _ = _workCycleActions.ReturnToChatAsync();
        }

        private void ChatsModeButton_Click(object sender, RoutedEventArgs e)
        {
            _selectedMode = AppMode.Chats;
            ApplyModeChrome();
            ApplyProjectsScreenChrome();
        }

        private void ProjectsModeButton_Click(object sender, RoutedEventArgs e)
        {
            _selectedMode = AppMode.Projects;
            ApplyModeChrome();
            ApplyProjectsScreenChrome();
        }

        private void ChatsComposerSendButton_Click(object sender, RoutedEventArgs e)
        {
            _ = SendChatsMessageAsync();
        }

        private async Task SendChatsMessageAsync()
        {
            if (_selectedMode != AppMode.Chats)
            {
                return;
            }

            var text = ChatsHostView.ComposerTextBox.Text?.Trim();
            if (string.IsNullOrWhiteSpace(text))
            {
                return;
            }

            await _chatsAdapter.AddMessageAsync(ConversationItemKind.User, "User", text);
            await _chatsAdapter.AddMessageAsync(
                ConversationItemKind.Assistant,
                "Assistant",
                "Chats mode is restored as a safe local loop for now. This reply is local and independent from project core.");
            ChatsHostView.ComposerTextBox.Text = string.Empty;

            if (_activeChatSession is not null)
            {
                if (_activeChatSession.IsDraft)
                {
                    _activeChatSession.IsDraft = false;
                    _activeChatSession.Title = BuildChatTitle(_activeChatSession.Adapter);
                    _chatSessions.Insert(0, _activeChatSession);
                    _draftChatSession = null;
                }
                else
                {
                    _activeChatSession.Title = BuildChatTitle(_activeChatSession.Adapter);
                }

                PersistActiveChatSession();
            }

            ChatsHostView.ConversationView.RefreshItems();
            ApplyChatsLayout(Path.GetFullPath(_projectRoot));
        }

        private void OpenProjectHtmlButton_Click(object sender, RoutedEventArgs e)
        {
            OpenTaggedPath(sender as Button);
        }

        private void OpenWorkCycleButton_Click(object sender, RoutedEventArgs e)
        {
            _projectsScreen = ProjectsScreen.WorkCycle;
            ApplyProjectsScreenChrome();
        }

        private void OpenCurrentProjectButton_Click(object sender, RoutedEventArgs e)
        {
            _projectsScreen = ProjectsScreen.Home;
            ApplyProjectsScreenChrome();
        }

        private void ReturnToExecutionSurfaceButton_Click(object sender, RoutedEventArgs e)
        {
            _workCycleFocus = WorkCycleFocus.Execution;
            _ = RefreshRecoveryShellAsync();
        }

        private void WorkCycleFocusAutoButton_Click(object sender, RoutedEventArgs e)
        {
            _workCycleFocus = WorkCycleFocus.Auto;
            _ = RefreshRecoveryShellAsync();
        }

        private void WorkCycleFocusChatButton_Click(object sender, RoutedEventArgs e)
        {
            _workCycleFocus = WorkCycleFocus.Chat;
            _ = RefreshRecoveryShellAsync();
        }

        private void WorkCycleFocusExecutionButton_Click(object sender, RoutedEventArgs e)
        {
            _workCycleFocus = WorkCycleFocus.Execution;
            _ = RefreshRecoveryShellAsync();
        }

        private void WorkCycleFocusResultButton_Click(object sender, RoutedEventArgs e)
        {
            _workCycleFocus = WorkCycleFocus.Result;
            _ = RefreshRecoveryShellAsync();
        }

        private void ProjectsBackButton_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedMode != AppMode.Projects)
            {
                return;
            }

            _projectsScreen = _projectsScreen == ProjectsScreen.WorkCycle
                ? ProjectsScreen.Home
                : ProjectsScreen.List;
            ApplyProjectsScreenChrome();
        }

        private void ApplyModeChrome()
        {
            var chats = _selectedMode == AppMode.Chats;

            ChatsHostView.Visibility = chats ? Visibility.Visible : Visibility.Collapsed;
            ProjectsHostView.Visibility = chats ? Visibility.Collapsed : Visibility.Visible;

            WindowRoot.Background = chats
                ? Brush("Ui.Chat.BackgroundBrush")
                : new SolidColorBrush(ColorFromHex("#171717"));
            HeaderBorder.Background = chats
                ? new SolidColorBrush(Color.FromArgb(0, 0, 0, 0))
                : new SolidColorBrush(ColorFromHex("#1D1D1D"));
            HeaderBorder.BorderBrush = chats
                ? new SolidColorBrush(Color.FromArgb(0, 0, 0, 0))
                : new SolidColorBrush(ColorFromHex("#323232"));
            HeaderBorder.BorderThickness = chats
                ? new Thickness(0)
                : new Thickness(0, 0, 0, 1);
            HeaderBorder.Padding = chats
                ? new Thickness(0, 8, 0, 0)
                : new Thickness(14, 12, 14, 12);
            HeaderBorder.Margin = chats
                ? new Thickness(24, 16, 24, 0)
                : new Thickness(18, 18, 18, 0);
            TitleBarRoot.MinHeight = chats ? 40 : 64;
            ModeSwitchView.SwitchBorder.Background = chats
                ? Brush("Ui.Chat.ChromeSurfaceBrush")
                : new SolidColorBrush(ColorFromHex("#2A2A2A"));
            ModeSwitchView.SwitchBorder.BorderBrush = chats
                ? Brush("Ui.Chat.ChromeBorderBrush")
                : new SolidColorBrush(Color.FromArgb(0, 0, 0, 0));
            ModeSwitchView.SwitchBorder.BorderThickness = chats
                ? new Thickness(1)
                : new Thickness(1);

            WindowStatusText.Text = chats
                ? "Chats mode active. Free chat stays outside project lifecycle."
                : _isDemoMode
                    ? "Projects mode active. Demo runtime detected. List, Home, and Work Cycle hosts are restored; discussion/preflight path is partially reconnected."
                    : "Projects mode active. List, Home, and Work Cycle hosts are restored; discussion/preflight path is partially reconnected.";
            WindowStatusText.Foreground = chats
                ? Brush("Ui.Chat.TextSecondaryBrush")
                : new SolidColorBrush(ColorFromHex("#CFCFCF"));
            WindowTitleText.Foreground = chats
                ? Brush("Ui.Chat.TextPrimaryBrush")
                : new SolidColorBrush(ColorFromHex("#F2F2F2"));
            WindowTitleText.Visibility = chats ? Visibility.Collapsed : Visibility.Visible;
            WindowStatusText.Visibility = chats ? Visibility.Collapsed : Visibility.Visible;
            RefreshButton.Visibility = chats ? Visibility.Collapsed : Visibility.Visible;

            ApplyModeButtonState(ModeSwitchView.ChatsButton, chats, isChatsButton: true);
            ApplyModeButtonState(ModeSwitchView.ProjectsButton, !chats, isChatsButton: false);
        }

        private void ApplyProjectsScreenChrome()
        {
            var projection = ProjectsScreenProjection.Build(_projectsScreen switch
            {
                ProjectsScreen.Home => "home",
                ProjectsScreen.WorkCycle => "work_cycle",
                _ => "list"
            });

            ProjectsHostView.SetScreenVisibility(
                showList: projection.ShowList,
                showHome: projection.ShowHome,
                showWorkCycle: projection.ShowWorkCycle);
            ProjectsBackButton.Visibility = _selectedMode == AppMode.Projects && projection.ShowBack
                ? Visibility.Visible
                : Visibility.Collapsed;
            ProjectsHostView.ApplyChrome(projection.TitleText, projection.SummaryText);
            if (_projectsScreen != ProjectsScreen.WorkCycle)
            {
                ProjectsHostView.WorkCycleView.FocusBar.Visibility = Visibility.Collapsed;
            }
        }

        private void ApplyWorkCycleColumnLayout(ProjectWorkCycleProjection workCycle)
        {
            if (!workCycle.Projection.ShowExecution && !workCycle.Projection.ShowResult)
            {
                WorkCycleChatColumn.Width = new GridLength(1, GridUnitType.Star);
                WorkCycleExecutionColumn.Width = new GridLength(0);
                WorkCycleResultColumn.Width = new GridLength(0);
                return;
            }

            if (workCycle.Projection.ShowExecution && !workCycle.Projection.ShowResult)
            {
                WorkCycleChatColumn.Width = new GridLength(1.15, GridUnitType.Star);
                WorkCycleExecutionColumn.Width = new GridLength(1, GridUnitType.Star);
                WorkCycleResultColumn.Width = new GridLength(0);
                return;
            }

            WorkCycleChatColumn.Width = new GridLength(1.15, GridUnitType.Star);
            WorkCycleExecutionColumn.Width = new GridLength(1, GridUnitType.Star);
            WorkCycleResultColumn.Width = new GridLength(1, GridUnitType.Star);
        }

        private void ApplyWorkCyclePhaseChrome(ProjectWorkCycleProjection workCycle)
        {
            var phase = workCycle.PhaseState.Phase;

            ProjectInteractionSurface.Opacity = phase is Flow.SurfacePhase.Execution or Flow.SurfacePhase.Result ? 0.78 : 1.0;
            ProjectExecutionSurface.Opacity = phase == Flow.SurfacePhase.Result ? 0.84 : 1.0;
            ProjectResultSurface.Opacity = workCycle.PhaseState.ResultSubphase == Flow.ResultSubphase.RevisionRequested ? 0.92 : 1.0;

            ApplyFocusOverride(workCycle);

            ProjectsHostView.WorkCycleView.ChatSummaryNoteBlock.Text = phase switch
            {
                Flow.SurfacePhase.Discussion => "Discussion is active in this phase. Send stays discussion-only, and work entry remains a separate validated transition.",
                Flow.SurfacePhase.Execution when workCycle.PhaseState.ExecutionSubphase == Flow.ExecutionSubphase.Preflight => "Chat stays primary while the same persistent execution surface opens in validation/preflight mode.",
                Flow.SurfacePhase.Execution => "Execution is the active surface. Chat remains visible only as bounded frozen context in this slice.",
                Flow.SurfacePhase.Result => "Result is the active decision surface. Execution stays visible as context and chat remains frozen.",
                Flow.SurfacePhase.Completed => "Completed state is shown as historical context only in this slice.",
                _ => "Discussion and execution stay limited to the currently re-proven owner paths."
            };
        }

        private void ApplyFocusOverride(ProjectWorkCycleProjection workCycle)
        {
            ApplyWorkCycleFocusButtonState(WorkCycleFocusAutoButton, _workCycleFocus == WorkCycleFocus.Auto);
            ApplyWorkCycleFocusButtonState(WorkCycleFocusChatButton, _workCycleFocus == WorkCycleFocus.Chat);
            ApplyWorkCycleFocusButtonState(WorkCycleFocusExecutionButton, _workCycleFocus == WorkCycleFocus.Execution);
            ApplyWorkCycleFocusButtonState(WorkCycleFocusResultButton, _workCycleFocus == WorkCycleFocus.Result);

            WorkCycleFocusChatButton.IsEnabled = workCycle.Projection.ShowChat;
            WorkCycleFocusExecutionButton.IsEnabled = workCycle.Projection.ShowExecution;
            WorkCycleFocusResultButton.IsEnabled = workCycle.Projection.ShowResult;
            ReturnToExecutionSurfaceButton.Visibility = workCycle.Projection.ShowExecution && workCycle.Projection.ShowResult
                ? Visibility.Visible
                : Visibility.Collapsed;

            if (_workCycleFocus == WorkCycleFocus.Auto)
            {
                return;
            }

            if (_workCycleFocus == WorkCycleFocus.Chat)
            {
                ProjectInteractionSurface.Opacity = 1.0;
                if (workCycle.Projection.ShowExecution)
                {
                    ProjectExecutionSurface.Opacity = 0.58;
                }

                if (workCycle.Projection.ShowResult)
                {
                    ProjectResultSurface.Opacity = 0.52;
                }

                return;
            }

            if (_workCycleFocus == WorkCycleFocus.Execution && workCycle.Projection.ShowExecution)
            {
                ProjectInteractionSurface.Opacity = workCycle.Projection.ShowChat ? 0.58 : ProjectInteractionSurface.Opacity;
                ProjectExecutionSurface.Opacity = 1.0;
                if (workCycle.Projection.ShowResult)
                {
                    ProjectResultSurface.Opacity = 0.58;
                }

                return;
            }

            if (_workCycleFocus == WorkCycleFocus.Result && workCycle.Projection.ShowResult)
            {
                ProjectInteractionSurface.Opacity = workCycle.Projection.ShowChat ? 0.52 : ProjectInteractionSurface.Opacity;
                if (workCycle.Projection.ShowExecution)
                {
                    ProjectExecutionSurface.Opacity = 0.58;
                }

                ProjectResultSurface.Opacity = 1.0;
            }
        }

        private void ApplyWorkCycleFocusButtonState(Button button, bool selected)
        {
            button.Background = selected ? Brush("Ui.Project.AccentFillBrush") : new SolidColorBrush(ColorFromHex("#202020"));
            button.BorderBrush = selected ? Brush("Ui.Project.AccentLineBrush") : Brush("Ui.Project.BorderQuietBrush");
            button.Foreground = selected ? Brush("Ui.Project.AccentForegroundBrush") : Brush("Ui.Project.TextPrimaryBrush");
        }

        private void ApplyModeButtonState(Button button, bool selected, bool isChatsButton)
        {
            if (isChatsButton)
            {
                button.Background = selected ? new SolidColorBrush(ColorFromHex("#FFFDF8")) : new SolidColorBrush(Color.FromArgb(0, 0, 0, 0));
                button.BorderBrush = selected ? Brush("Ui.Chat.AccentLineBrush") : new SolidColorBrush(Color.FromArgb(0, 0, 0, 0));
                button.BorderThickness = selected ? new Thickness(1) : new Thickness(0);
                button.Foreground = selected ? Brush("Ui.Chat.AccentForegroundBrush") : new SolidColorBrush(ColorFromHex("#6B5C4F"));
                return;
            }

            button.Background = selected ? new SolidColorBrush(ColorFromHex("#F5F1EC")) : new SolidColorBrush(Color.FromArgb(0, 0, 0, 0));
            button.BorderBrush = selected ? Brush("Ui.Project.AccentLineBrush") : new SolidColorBrush(Color.FromArgb(0, 0, 0, 0));
            button.BorderThickness = selected ? new Thickness(1) : new Thickness(0);
            button.Foreground = selected ? new SolidColorBrush(ColorFromHex("#272727")) : new SolidColorBrush(ColorFromHex("#D8D2C8"));
        }

        private static Brush Brush(string key)
        {
            return (Brush)Application.Current.Resources[key];
        }

        private static Color ColorFromHex(string value)
        {
            return Color.FromArgb(
                255,
                Convert.ToByte(value[1..3], 16),
                Convert.ToByte(value[3..5], 16),
                Convert.ToByte(value[5..7], 16));
        }

        private static string GetWindowTitle()
        {
            return Application.Current.Resources["Ui.WindowTitle"] as string ?? "ZAVOD";
        }

        private void OpenProjectDocumentButton_Click(object sender, RoutedEventArgs e)
        {
            OpenTaggedPath(sender as Button);
        }

        private void OpenCapsuleDocumentButton_Click(object sender, RoutedEventArgs e)
        {
            OpenTaggedPath(sender as Button);
        }

        private static void OpenTaggedPath(Button? button)
        {
            if (button?.Tag is not string path || string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            {
                return;
            }

            Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
        }

        private enum AppMode
        {
            Chats,
            Projects
        }

        private enum ProjectsScreen
        {
            List,
            Home,
            WorkCycle
        }

        private enum WorkCycleFocus
        {
            Auto,
            Chat,
            Execution,
            Result
        }

        internal string GetUiVerificationSnapshotJson()
        {
            var projectsScreenProjection = ProjectsScreenProjection.Build(_projectsScreen switch
            {
                ProjectsScreen.Home => "home",
                ProjectsScreen.WorkCycle => "work_cycle",
                _ => "list"
            });

            var shellProjection = ProjectsShellProjection.Build(Path.GetFullPath(_projectRoot));
            var workCycleProjection = ProjectWorkCycleProjection.Build(Path.GetFullPath(_projectRoot), shellProjection);
            var appMode = _selectedMode == AppMode.Chats ? "Chats" : "Projects";

            return UiVerificationSnapshotBuilder.BuildJson(
                appMode,
                projectsScreenProjection,
                workCycleProjection);
        }
    }
}
