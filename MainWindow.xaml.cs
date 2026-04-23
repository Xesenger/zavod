using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using System;
using System.Diagnostics;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.Graphics.Imaging;
using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.Storage.Streams;
using Windows.UI;
using WinRT.Interop;
using zavod.Bootstrap;
using zavod.Contexting;
using zavod.Execution;
using zavod.Workspace;
using zavod.Flow;
using zavod.Persistence;
using zavod.UI.Rendering.Conversation;
using zavod.UI.Modes.Chats;
using zavod.UI.Modes.Projects;
using zavod.UI.Modes.Projects.Bridge;
using zavod.UI.Modes.Projects.Projections;
using zavod.UI.Shell.Verification;
using zavod.UI.Shell.Windowing;
using zavod.UI.Text;
using zavod.State;
using zavod.UI.Modes.Projects.WorkCycle.Actions;

namespace zavod
{
    public sealed partial class MainWindow : Window
    {
        private readonly string _projectRoot;
        private readonly bool _isDemoMode;
        private readonly ChatsRuntimeController _chatsController;
        private readonly ProjectsRuntimeController _projectsController;
        private readonly WorkCycleActionController _workCycleActions;
        private AppMode _selectedMode = AppMode.Projects;
        private ProjectsScreen _projectsScreen = ProjectsScreen.List;
        private WorkCycleFocus _workCycleFocus = WorkCycleFocus.Auto;
        private bool _initialRefreshRequested;
        private const int ShellMinimumWidth = 960;
        private const int ShellMinimumHeight = 640;
        private readonly bool _shellPassEnabled = true;
        private bool _shellPassWebRefreshRequested;

        // Pass 1 step 3a: feature flag for the new Projects WebView2 renderer.
        // When false, the legacy XAML ProjectsHostView is used (current production path).
        // When true, ProjectsWebRendererView is shown instead and consumes
        // ProjectsWebSnapshotBuilder output. Step 3b wires navigation intents
        // (select_project, navigate_screen) so screens flip on click; no core
        // mutations and no real project payloads yet.
        private const bool UseProjectsWebRenderer = true;
        private bool _projectsWebInitialSnapshotPushed;
        private readonly ProjectsWebSnapshotBuilder _projectsWebSnapshotBuilder;
        private readonly string? _verificationCaptureMode;
        private readonly string? _verificationCapturePath;
        private readonly string? _verificationProofTextPath;
        private readonly string? _verificationProofPrompt;
        private readonly string? _verificationProofResultPath;
        private readonly bool _verificationCaptureExit;
        private bool _verificationCaptureCompleted;
        private bool _verificationLiveProofStarted;
        private WindowShellController? _windowShell;
        public MainWindow(string projectRoot, bool isDemoMode = false)
        {
            _projectRoot = projectRoot ?? throw new ArgumentNullException(nameof(projectRoot));
            _isDemoMode = isDemoMode;

            InitializeComponent();
            _chatsController = new ChatsRuntimeController(Path.GetFullPath(_projectRoot));
            _projectsController = new ProjectsRuntimeController(Path.GetFullPath(_projectRoot));
            _projectsWebSnapshotBuilder = new ProjectsWebSnapshotBuilder(_projectsController);

            _workCycleActions = new WorkCycleActionController(
                _projectRoot,
                () => _projectsController.EnsureActiveAdapter(),
                async () =>
                {
                    await RefreshRecoveryShellAsync();
                    PushProjectsWebSnapshot();
                },
                UpdateProjectsDiscussionPreview);
            _workCycleActions.SetProgressCallback(() =>
            {
                PushProjectsWebSnapshot();
                return Task.CompletedTask;
            });
            _verificationCaptureMode = Environment.GetEnvironmentVariable("ZAVOD_UI_CAPTURE_MODE")?.Trim();
            _verificationCapturePath = Environment.GetEnvironmentVariable("ZAVOD_UI_CAPTURE_PATH")?.Trim();
            _verificationProofTextPath = Environment.GetEnvironmentVariable("ZAVOD_UI_PROOF_TEXT_PATH")?.Trim();
            _verificationProofPrompt = Environment.GetEnvironmentVariable("ZAVOD_UI_PROOF_PROMPT")?.Trim();
            _verificationProofResultPath = Environment.GetEnvironmentVariable("ZAVOD_UI_PROOF_RESULT_PATH")?.Trim();
            _verificationCaptureExit = string.Equals(Environment.GetEnvironmentVariable("ZAVOD_UI_CAPTURE_EXIT"), "1", StringComparison.Ordinal);

            Title = GetWindowTitle();
            WindowTitleText.Text = Title;
            ShellBrandText.Text = Title;
            WindowStatusText.Text = AppText.Current.Get("shell.recovery_mode_active");
            ProjectsBackButton.Content = AppText.Current.Get("shell.back");
            RefreshButton.Content = AppText.Current.Get("shell.refresh");

            _windowShell = new WindowShellController(this, ShellMinimumWidth, ShellMinimumHeight);
            ModeSwitchView.ChatsClicked += ChatsModeButton_Click;
            ModeSwitchView.ProjectsClicked += ProjectsModeButton_Click;
            WireWindowButtonChrome(MinimizeWindowButton, destructive: false);
            WireWindowButtonChrome(MaximizeWindowButton, destructive: false);
            WireWindowButtonChrome(CloseWindowButton, destructive: true);
            ApplyWindowButtonsReveal(revealed: false);
            UpdateMaximizeButtonGlyph();
            WindowRoot.Loaded += WindowRoot_Loaded;
            SizeChanged += MainWindow_SizeChanged;
            Closed += MainWindow_Closed;
            Activated += MainWindow_Activated;

            ChatsWebRendererView.IntentReceived += ChatsWebRendererView_IntentReceived;
            ChatsWebRendererView.FirstFrameReady += ChatsWebRendererView_FirstFrameReady;
            ProjectsWebRendererView.IntentReceived += ProjectsWebRendererView_IntentReceived;
            ProjectsHostView.WorkCycleView.ConversationRenderer.IntentReceived += ProjectsConversationRenderer_IntentReceived;

            if (_shellPassEnabled)
            {
                _selectedMode = AppMode.Chats;
                WindowStatusText.Text = AppText.Current.Format("shell.shell_pass_active", ShellMinimumWidth, ShellMinimumHeight);
                ApplyVerificationOverride();
            }

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
            ProjectsHostView.WorkCycleView.ResultAcceptAction.Click += ResultAcceptButton_WrapperClick;
            ProjectsHostView.WorkCycleView.ResultReviseAction.Click += ResultReviseButton_WrapperClick;
            ProjectsHostView.WorkCycleView.ResultRejectAction.Click += ResultRejectButton_WrapperClick;

            if (_shellPassEnabled)
            {
                ApplyModeChrome();
                return;
            }

            ApplyVerificationOverride();
        }

        private void MainWindow_Closed(object sender, WindowEventArgs args)
        {
            _windowShell?.Dispose();
        }

        private void MainWindow_SizeChanged(object sender, WindowSizeChangedEventArgs args)
        {
            ApplyShellCaptionRegion();
            UpdateMaximizeButtonGlyph();
        }

        private void WindowRoot_Loaded(object sender, RoutedEventArgs e)
        {
            ApplyShellCaptionRegion();
        }

        private void WindowButtonsHitArea_PointerEntered(object sender, PointerRoutedEventArgs e)
        {
            ApplyWindowButtonsReveal(revealed: true);
        }

        private void WindowButtonsHitArea_PointerExited(object sender, PointerRoutedEventArgs e)
        {
            ApplyWindowButtonsReveal(revealed: false);
        }

        private void MinimizeWindowButton_Click(object sender, RoutedEventArgs e)
        {
            if (!DispatcherQueue.TryEnqueue(() => _windowShell?.Minimize()))
            {
                _windowShell?.Minimize();
            }
        }

        private void MaximizeWindowButton_Click(object sender, RoutedEventArgs e)
        {
            _windowShell?.ToggleMaximize();
            UpdateMaximizeButtonGlyph();
        }

        private void CloseWindowButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void ApplyWindowButtonsReveal(bool revealed)
        {
            WindowButtonsHost.Opacity = revealed ? 1.0 : 0.05;

            if (!revealed)
            {
                ApplyWindowButtonChrome(MinimizeWindowButton, destructive: false, hovered: false, pressed: false);
                ApplyWindowButtonChrome(MaximizeWindowButton, destructive: false, hovered: false, pressed: false);
                ApplyWindowButtonChrome(CloseWindowButton, destructive: true, hovered: false, pressed: false);
                return;
            }

            MinimizeWindowButton.Background = new SolidColorBrush(Color.FromArgb(0, 255, 255, 255));
            MinimizeWindowButton.Foreground = new SolidColorBrush(ColorFromHex("#3B3B3B"));
            MaximizeWindowButton.Background = new SolidColorBrush(Color.FromArgb(0, 255, 255, 255));
            MaximizeWindowButton.Foreground = new SolidColorBrush(ColorFromHex("#3B3B3B"));
            CloseWindowButton.Background = new SolidColorBrush(Color.FromArgb(0, 255, 255, 255));
            CloseWindowButton.Foreground = new SolidColorBrush(ColorFromHex("#666666"));
        }

        private void WireWindowButtonChrome(Button button, bool destructive)
        {
            ApplyWindowButtonChrome(button, destructive, hovered: false, pressed: false);
            button.PointerEntered += (_, _) => ApplyWindowButtonChrome(button, destructive, hovered: true, pressed: false);
            button.PointerExited += (_, _) => ApplyWindowButtonChrome(button, destructive, hovered: false, pressed: false);
            button.PointerPressed += (_, _) => ApplyWindowButtonChrome(button, destructive, hovered: true, pressed: true);
            button.PointerReleased += (_, _) => ApplyWindowButtonChrome(button, destructive, hovered: true, pressed: false);
        }

        private static void ApplyWindowButtonChrome(Button button, bool destructive, bool hovered, bool pressed)
        {
            if (!hovered)
            {
                button.Background = new SolidColorBrush(Color.FromArgb(0, 255, 255, 255));
                button.Foreground = new SolidColorBrush(ColorFromHex("#5F5F5F"));
                return;
            }

            if (destructive)
            {
                button.Background = new SolidColorBrush(pressed ? ColorFromHex("#D74444") : ColorFromHex("#E65454"));
                button.Foreground = new SolidColorBrush(ColorFromHex("#FFFFFF"));
                return;
            }

            button.Background = new SolidColorBrush(pressed ? ColorFromHex("#E5E5E5") : ColorFromHex("#F1F1F1"));
            button.Foreground = new SolidColorBrush(ColorFromHex("#1E1E1E"));
        }

        private void UpdateMaximizeButtonGlyph()
        {
            MaximizeWindowButton.Content = _windowShell?.IsMaximized == true ? "\uE923" : "\uE922";
        }

        private void ApplyShellCaptionRegion()
        {
            if (!_shellPassEnabled || _windowShell is null || WindowRoot.XamlRoot is null)
            {
                return;
            }

            var scale = WindowRoot.XamlRoot.RasterizationScale;
            var captionRects = new List<Windows.Graphics.RectInt32>();
            var passthroughRects = new List<Windows.Graphics.RectInt32>();

            AppendCaptionRect(captionRects, LeftShellDragRegion, scale);
            AppendCaptionRect(captionRects, RightShellDragRegion, scale);
            AppendCaptionRect(passthroughRects, ModeSwitchView, scale);
            AppendCaptionRect(passthroughRects, WindowButtonsHitArea, scale);

            if (captionRects.Count == 0)
            {
                return;
            }

            _windowShell.SetCaptionRegions(captionRects);
            if (passthroughRects.Count > 0)
            {
                _windowShell.SetPassthroughRegions(passthroughRects);
            }
        }

        private void AppendCaptionRect(List<Windows.Graphics.RectInt32> rects, FrameworkElement element, double scale)
        {
            if (element.ActualWidth <= 0 || element.ActualHeight <= 0)
            {
                return;
            }

            var transform = element.TransformToVisual(WindowRoot);
            var origin = transform.TransformPoint(new Point(0, 0));
            rects.Add(new Windows.Graphics.RectInt32(
                Math.Max(0, (int)Math.Round(origin.X * scale)),
                Math.Max(0, (int)Math.Round(origin.Y * scale)),
                Math.Max(1, (int)Math.Round(element.ActualWidth * scale)),
                Math.Max(1, (int)Math.Round(element.ActualHeight * scale))));
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

            if (_shellPassEnabled)
            {
                if (_selectedMode == AppMode.Chats)
                {
                    await RefreshShellPassChatsWebAsync();
                    return;
                }
            }

            await RefreshRecoveryShellAsync();
        }

        private void ChatsWebRendererView_FirstFrameReady(object? sender, EventArgs e)
        {
            WindowStatusText.Text = AppText.Current.Get("shell.first_frame_ready");
            _ = TryRunChatsLiveProofAsync();
        }

        private void ApplyVerificationOverride()
        {
            switch (_verificationCaptureMode)
            {
                case "projects-home":
                    _selectedMode = AppMode.Projects;
                    _projectsScreen = ProjectsScreen.Home;
                    break;
                case "projects-work-cycle":
                    _selectedMode = AppMode.Projects;
                    _projectsScreen = ProjectsScreen.WorkCycle;
                    break;
                case "projects-list":
                    _selectedMode = AppMode.Projects;
                    _projectsScreen = ProjectsScreen.List;
                    break;
                case "chats":
                case "chats-live-proof":
                    _selectedMode = AppMode.Chats;
                    break;
            }
        }

        private async void ChatsWebRendererView_IntentReceived(object? sender, ChatsWebIntentReceivedEventArgs e)
        {
            switch (e.Message.Type?.Trim())
            {
                case "renderer_ready":
                case "dom_ready":
                    await ApplyShellPassChatsSnapshotAsync();
                    break;
                case "send_message":
                    if (TryGetMessageText(e.Message.Payload, out var text))
                    {
                        await SendChatsMessageAsync(text);
                    }
                    break;
                case "new_chat":
                    _chatsController.CreateOrActivateDraft();
                    await ApplyShellPassChatsSnapshotAsync();
                    break;
                case "select_chat":
                    if (TryGetChatId(e.Message.Payload, out var chatId))
                    {
                        await _chatsController.SelectChatAsync(chatId);
                        await ApplyShellPassChatsSnapshotAsync();
                    }
                    break;
                case "request_older":
                    if (TryGetBeforeSeq(e.Message.Payload, out var beforeSeq)
                        && await _chatsController.TryLoadOlderAsync(beforeSeq))
                    {
                        await ApplyShellPassChatsSnapshotAsync();
                    }
                    break;
                case "request_attach_files":
                    if (await StageComposerFilesAsync(projectsMode: false))
                    {
                        await ApplyShellPassChatsSnapshotAsync();
                    }
                    break;
                case "remove_attachment":
                    if (TryGetDraftId(e.Message.Payload, out var chatDraftId)
                        && _chatsController.RemovePendingComposerInput(chatDraftId))
                    {
                        await ApplyShellPassChatsSnapshotAsync();
                    }
                    break;
                case "stage_text_artifact":
                    if (TryGetComposerText(e.Message.Payload, out var longChatText)
                        && _chatsController.StageLongTextArtifact(longChatText))
                    {
                        await ApplyShellPassChatsSnapshotAsync();
                    }
                    break;
                default:
                    break;
            }
        }

        private async void ProjectsConversationRenderer_IntentReceived(object? sender, ChatsWebIntentReceivedEventArgs e)
        {
            switch (e.Message.Type?.Trim())
            {
                case "renderer_ready":
                case "dom_ready":
                    await ApplyProjectsConversationSnapshotAsync();
                    break;
                case "send_message":
                    if (TryGetMessageText(e.Message.Payload, out var text))
                    {
                        await SendProjectsMessageThroughControllerAsync(text);
                    }
                    break;
                case "new_chat":
                    _projectsController.CreateOrActivateDraft();
                    await ApplyProjectsConversationSnapshotAsync();
                    break;
                case "select_chat":
                    if (TryGetChatId(e.Message.Payload, out var chatId)
                        && await _projectsController.SelectConversationAsync(chatId))
                    {
                        await ApplyProjectsConversationSnapshotAsync();
                    }
                    break;
                case "request_older":
                    if (TryGetBeforeSeq(e.Message.Payload, out var beforeSeq)
                        && await _projectsController.TryLoadOlderAsync(beforeSeq))
                    {
                        await ApplyProjectsConversationSnapshotAsync();
                    }
                    break;
                case "request_attach_files":
                    if (await StageComposerFilesAsync(projectsMode: true))
                    {
                        await ApplyProjectsConversationSnapshotAsync();
                    }
                    break;
                case "remove_attachment":
                    if (TryGetDraftId(e.Message.Payload, out var projectDraftId)
                        && _projectsController.RemovePendingComposerInput(projectDraftId))
                    {
                        await ApplyProjectsConversationSnapshotAsync();
                    }
                    break;
                case "stage_text_artifact":
                    if (TryGetComposerText(e.Message.Payload, out var longProjectText)
                        && _projectsController.StageLongTextArtifact(longProjectText))
                    {
                        await ApplyProjectsConversationSnapshotAsync();
                    }
                    break;
                default:
                    break;
            }
        }

        private async Task RefreshShellPassChatsWebAsync()
        {
            if (_shellPassWebRefreshRequested)
            {
                return;
            }

            _shellPassWebRefreshRequested = true;
            await _chatsController.EnsureInitializedAsync();
            await ChatsWebRendererView.PreloadAsync();
            await ApplyShellPassChatsSnapshotAsync();
            ChatsWebRendererView.Visibility = Visibility.Visible;
            ProjectsHostView.Visibility = Visibility.Collapsed;
        }

        private async Task ApplyShellPassChatsSnapshotAsync()
        {
            var snapshot = _chatsController.BuildSnapshot();
            await ChatsWebRendererView.ApplySnapshotAsync(snapshot);
        }

        private async Task ApplyProjectsConversationSnapshotAsync()
        {
            if (_projectsScreen != ProjectsScreen.WorkCycle)
            {
                return;
            }

            await ProjectsHostView.WorkCycleView.ConversationRenderer.PreloadAsync();
            var snapshot = _projectsController.BuildSnapshot();
            await ProjectsHostView.WorkCycleView.ConversationRenderer.ApplySnapshotAsync(snapshot);
        }

        private static bool TryGetMessageText(System.Text.Json.JsonElement payload, out string text)
        {
            text = string.Empty;
            if (payload.ValueKind != System.Text.Json.JsonValueKind.Object
                || !payload.TryGetProperty("text", out var textProperty)
                || textProperty.ValueKind != System.Text.Json.JsonValueKind.String)
            {
                return false;
            }

            text = textProperty.GetString()?.Trim() ?? string.Empty;
            return !string.IsNullOrWhiteSpace(text);
        }

        private static bool TryGetChatId(System.Text.Json.JsonElement payload, out string chatId)
        {
            chatId = string.Empty;
            if (payload.ValueKind != System.Text.Json.JsonValueKind.Object
                || !payload.TryGetProperty("chatId", out var chatIdProperty)
                || chatIdProperty.ValueKind != System.Text.Json.JsonValueKind.String)
            {
                return false;
            }

            chatId = chatIdProperty.GetString()?.Trim() ?? string.Empty;
            return !string.IsNullOrWhiteSpace(chatId);
        }

        private static bool TryGetBeforeSeq(System.Text.Json.JsonElement payload, out int beforeSeq)
        {
            beforeSeq = 0;
            if (payload.ValueKind != System.Text.Json.JsonValueKind.Object
                || !payload.TryGetProperty("beforeSeq", out var beforeSeqProperty))
            {
                return false;
            }

            if (beforeSeqProperty.ValueKind == System.Text.Json.JsonValueKind.Number
                && beforeSeqProperty.TryGetInt32(out beforeSeq))
            {
                return beforeSeq > 0;
            }

            return false;
        }

        private static bool TryGetDraftId(System.Text.Json.JsonElement payload, out string draftId)
        {
            draftId = string.Empty;
            if (payload.ValueKind != System.Text.Json.JsonValueKind.Object
                || !payload.TryGetProperty("draftId", out var draftIdProperty)
                || draftIdProperty.ValueKind != System.Text.Json.JsonValueKind.String)
            {
                return false;
            }

            draftId = draftIdProperty.GetString()?.Trim() ?? string.Empty;
            return !string.IsNullOrWhiteSpace(draftId);
        }

        private static bool TryGetComposerText(System.Text.Json.JsonElement payload, out string text)
        {
            text = string.Empty;
            if (payload.ValueKind != System.Text.Json.JsonValueKind.Object
                || !payload.TryGetProperty("text", out var textProperty)
                || textProperty.ValueKind != System.Text.Json.JsonValueKind.String)
            {
                return false;
            }

            text = textProperty.GetString() ?? string.Empty;
            return !string.IsNullOrWhiteSpace(text);
        }

        private async Task RefreshRecoveryShellAsync()
        {
            var normalizedRoot = Path.GetFullPath(_projectRoot);
            var queryState = ProjectWorkCycleQueryStateBuilder.Build(normalizedRoot);
            var projection = ProjectsShellProjection.Build(queryState);
            var activeTaskDisplay = AppText.Current.Format(
                "projects.shell.active_task",
                queryState.ActiveTaskId ?? AppText.Current.Get("projects.token.none"));

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
                executionState: queryState.ActiveTaskId is null
                    ? AppText.Current.Get("projects.recovery.execution_state.recovery_hold")
                    : AppText.Current.Get("projects.recovery.execution_state.task_detected"),
                executionDetail: queryState.ActiveTaskId is null
                    ? AppText.Current.Get("projects.recovery.execution_detail.disabled")
                    : AppText.Current.Format("projects.recovery.execution_detail.task_detected", activeTaskDisplay),
                resultState: projection.HasCapsuleDocument
                    ? AppText.Current.Get("projects.recovery.result_state.evidence_available")
                    : AppText.Current.Get("projects.recovery.result_state.none"),
                resultDetail: projection.HasCapsuleDocument
                    ? AppText.Current.Get("projects.recovery.result_detail.capsule_exists")
                    : AppText.Current.Get("projects.recovery.result_detail.none"));

            ProjectsHostView.ListView.ApplyContent(
                summary: AppText.Current.Get("projects.list.summary"),
                currentProject: projection.ProjectListCurrentProjectText,
                currentStage: projection.ProjectListCurrentStageText,
                currentDetails: AppText.Current.Get("projects.list.current_project.details"),
                notes: AppText.Current.Get("projects.list.notes"));

            ProjectsHostView.HomeView.ApplyContent(
                summary: AppText.Current.Get("projects.home.summary"),
                status: projection.ProjectHomeStatusText,
                stage: projection.ProjectHomeStageText,
                activity: projection.ProjectHomeActivityText,
                materials: projection.ProjectHomeMaterialsText,
                notes: AppText.Current.Get("projects.home.notes"));

            ProjectsHostView.HomeView.SetActionState(
                hasProjectHtml: projection.HasProjectHtml,
                projectHtmlPath: projection.ProjectHtmlPath,
                hasProjectDocument: projection.HasProjectDocument,
                projectDocumentPath: projection.ProjectDocumentPath);
            await EnsureProjectsConversationAsync(normalizedRoot, projection);
            await ApplyProjectsConversationSnapshotAsync();

            var workCycle = ProjectWorkCycleProjection.Build(queryState, projection);
            ProjectsHostView.WorkCycleView.ApplyWorkCycleState(
                workCycle,
                projectContext: $"{projection.ProjectName}  |  {projection.ProjectRoot}",
                showEnterWork: _projectsScreen == ProjectsScreen.WorkCycle && workCycle.Projection.CanStartIntentValidation,
                composerEnabled: _projectsScreen == ProjectsScreen.WorkCycle && workCycle.PhaseState.Phase == Flow.SurfacePhase.Discussion,
                clarificationVisible: WorkCycleActionController.IsPreflightClarificationActive(normalizedRoot),
                validationReason: AppText.Current.Get("projects.work_cycle.validation.reason"),
                validationSummary: string.IsNullOrWhiteSpace(workCycle.IntentSummary)
                    ? AppText.Current.Get("projects.work_cycle.validation.empty")
                    : workCycle.IntentSummary,
                validationItems: WorkCycleActionController.BuildAgreementItemsText(workCycle.IntentSummary));
            ApplyWorkCycleColumnLayout(workCycle);
            ApplyWorkCyclePhaseChrome(workCycle);

            ApplyModeChrome();
            ApplyProjectsScreenChrome();
            await TryCaptureVerificationAsync();
        }

        private void UpdateProjectsDiscussionPreview()
        {
            if (_selectedMode != AppMode.Projects || _projectsScreen != ProjectsScreen.WorkCycle)
            {
                return;
            }

            _ = ApplyProjectsConversationSnapshotAsync();
        }

        private async Task EnsureProjectsConversationAsync(string normalizedRoot, ProjectsShellProjection projection)
        {
            if (!_projectsController.IsInitialized) await _projectsController.EnsureInitializedAsync(projection.ProjectId, projection.ProjectName);
        }

        private void RefreshButton_Click(object sender, RoutedEventArgs e)
        {
            _ = RefreshRecoveryShellAsync();
        }

        private void ProjectsComposerSendButton_WrapperClick(object sender, RoutedEventArgs e)
        {
            _ = SendProjectsMessageThroughControllerAsync();
        }

        private async Task SendProjectsMessageThroughControllerAsync(string? textOverride = null)
        {
            if (_selectedMode != AppMode.Projects || _projectsScreen != ProjectsScreen.WorkCycle)
            {
                return;
            }

            _projectsController.EnsureActiveAdapter();
            var submission = await _projectsController.ConsumeComposerSubmissionAsync(textOverride ?? ProjectsComposerTextBox.Text ?? string.Empty);
            if (submission.IsEmpty)
            {
                return;
            }

            if (submission.HasText && await _workCycleActions.SendProjectsMessageAsync(submission))
            {
                // Message path already emitted user/lead items via the action controller.
            }

            ProjectsComposerTextBox.Text = string.Empty;
            _projectsController.CommitActiveConversation();
            await ApplyProjectsConversationSnapshotAsync();
        }

        private async Task EnterWorkAsync()
        {
            if (_selectedMode != AppMode.Projects || _projectsScreen != ProjectsScreen.WorkCycle)
            {
                return;
            }

            _projectsController.EnsureActiveAdapter();
            if (await _workCycleActions.EnterWorkAsync())
            {
                _projectsController.CommitActiveConversation();
                await ApplyProjectsConversationSnapshotAsync();
            }
        }

        private async Task ConfirmPreflightAsync()
        {
            if (_selectedMode != AppMode.Projects || _projectsScreen != ProjectsScreen.WorkCycle)
            {
                return;
            }

            _projectsController.EnsureActiveAdapter();
            if (await _workCycleActions.ConfirmPreflightAsync())
            {
                _projectsController.CommitActiveConversation();
                await ApplyProjectsConversationSnapshotAsync();
            }
        }

        private async Task BeginClarificationAsync()
        {
            if (_selectedMode != AppMode.Projects || _projectsScreen != ProjectsScreen.WorkCycle)
            {
                return;
            }

            _projectsController.EnsureActiveAdapter();
            if (await _workCycleActions.BeginClarificationAsync(ExecutionClarificationTextBox.Text ?? string.Empty))
            {
                _projectsController.CommitActiveConversation();
                await ApplyProjectsConversationSnapshotAsync();
            }
        }

        private async Task<bool> ApplyClarificationAsync()
        {
            if (_selectedMode != AppMode.Projects || _projectsScreen != ProjectsScreen.WorkCycle)
            {
                return false;
            }

            _projectsController.EnsureActiveAdapter();
            if (!await _workCycleActions.ApplyClarificationAsync(ExecutionClarificationTextBox.Text ?? string.Empty))
            {
                return false;
            }

            _projectsController.CommitActiveConversation();
            await ApplyProjectsConversationSnapshotAsync();
            return true;
        }

        private async Task CancelClarificationAsync()
        {
            if (_selectedMode != AppMode.Projects || _projectsScreen != ProjectsScreen.WorkCycle)
            {
                return;
            }

            _projectsController.EnsureActiveAdapter();
            if (await _workCycleActions.CancelClarificationAsync())
            {
                _projectsController.CommitActiveConversation();
                await ApplyProjectsConversationSnapshotAsync();
            }
        }

        private async Task ReturnToChatAsync()
        {
            if (_selectedMode != AppMode.Projects || _projectsScreen != ProjectsScreen.WorkCycle)
            {
                return;
            }

            _projectsController.EnsureActiveAdapter();
            if (await _workCycleActions.ReturnToChatAsync())
            {
                _projectsController.CommitActiveConversation();
                await ApplyProjectsConversationSnapshotAsync();
            }
        }

        private async Task AcceptResultAsync()
        {
            if (_selectedMode != AppMode.Projects || _projectsScreen != ProjectsScreen.WorkCycle)
            {
                return;
            }

            _projectsController.EnsureActiveAdapter();
            if (await _workCycleActions.AcceptResultAsync())
            {
                _projectsController.CommitActiveConversation();
                await ApplyProjectsConversationSnapshotAsync();
            }
        }

        private async Task RequestRevisionAsync()
        {
            if (_selectedMode != AppMode.Projects || _projectsScreen != ProjectsScreen.WorkCycle)
            {
                return;
            }

            _projectsController.EnsureActiveAdapter();
            if (await _workCycleActions.RequestRevisionAsync())
            {
                _projectsController.CommitActiveConversation();
                await ApplyProjectsConversationSnapshotAsync();
            }
        }

        private async Task RejectResultAsync()
        {
            if (_selectedMode != AppMode.Projects || _projectsScreen != ProjectsScreen.WorkCycle)
            {
                return;
            }

            _projectsController.EnsureActiveAdapter();
            if (await _workCycleActions.RejectResultAsync())
            {
                _projectsController.CommitActiveConversation();
                await ApplyProjectsConversationSnapshotAsync();
            }
        }

        private async Task<bool> StageComposerFilesAsync(bool projectsMode)
        {
            var pickedFiles = await PickFilesAsync();
            if (pickedFiles.Count == 0)
            {
                return false;
            }

            return projectsMode
                ? _projectsController.StageFiles(pickedFiles)
                : _chatsController.StageFiles(pickedFiles);
        }

        private async Task<IReadOnlyList<string>> PickFilesAsync()
        {
            var picker = new FileOpenPicker();
            InitializeWithWindow.Initialize(picker, WindowNative.GetWindowHandle(this));
            picker.SuggestedStartLocation = PickerLocationId.DocumentsLibrary;
            picker.FileTypeFilter.Add("*");

            var files = await picker.PickMultipleFilesAsync();
            if (files is null || files.Count == 0)
            {
                return Array.Empty<string>();
            }

            return files
                .Where(static file => file is not null && !string.IsNullOrWhiteSpace(file.Path))
                .Select(static file => file.Path)
                .ToArray();
        }

        private void EnterWorkButton_WrapperClick(object sender, RoutedEventArgs e)
        {
            _ = EnterWorkAsync();
        }

        private void ExecutionConfirmButton_WrapperClick(object sender, RoutedEventArgs e)
        {
            _ = ConfirmPreflightAsync();
        }

        private void ExecutionClarifyButton_WrapperClick(object sender, RoutedEventArgs e)
        {
            _ = BeginClarificationAsync();
        }

        private async void ExecutionApplyClarificationButton_WrapperClick(object sender, RoutedEventArgs e)
        {
            if (await ApplyClarificationAsync())
            {
                ExecutionClarificationTextBox.Text = string.Empty;
            }
        }

        private void ExecutionCancelClarificationButton_WrapperClick(object sender, RoutedEventArgs e)
        {
            ExecutionClarificationTextBox.Text = string.Empty;
            _ = CancelClarificationAsync();
        }

        private void ExecutionReturnToChatButton_WrapperClick(object sender, RoutedEventArgs e)
        {
            _ = ReturnToChatAsync();
        }

        private void ResultAcceptButton_WrapperClick(object sender, RoutedEventArgs e)
        {
            _ = AcceptResultAsync();
        }

        private void ResultReviseButton_WrapperClick(object sender, RoutedEventArgs e)
        {
            _ = RequestRevisionAsync();
        }

        private void ResultRejectButton_WrapperClick(object sender, RoutedEventArgs e)
        {
            _ = RejectResultAsync();
        }

        private async void ChatsModeButton_Click(object sender, RoutedEventArgs e)
        {
            _selectedMode = AppMode.Chats;
            if (_shellPassEnabled)
            {
                await RefreshShellPassChatsWebAsync();
            }
            ApplyModeChrome();
            ApplyProjectsScreenChrome();
        }

        private async void ProjectsModeButton_Click(object sender, RoutedEventArgs e)
        {
            _selectedMode = AppMode.Projects;
            if (_shellPassEnabled)
            {
                await RefreshRecoveryShellAsync();
            }
            ApplyModeChrome();
            ApplyProjectsScreenChrome();
        }

        private void ChatsComposerSendButton_Click(object sender, RoutedEventArgs e)
        {
            _ = SendChatsMessageAsync();
        }

        private async Task SendChatsMessageAsync(string? shellPassText = null)
        {
            if (_selectedMode != AppMode.Chats)
            {
                return;
            }

            var submission = await _chatsController.ConsumeComposerSubmissionAsync(shellPassText ?? string.Empty);
            if (!await _chatsController.SendMessageAsync(
                    submission,
                    _shellPassEnabled ? ApplyShellPassChatsSnapshotAsync : null))
            {
                return;
            }
            if (_shellPassEnabled)
            {
                await ApplyShellPassChatsSnapshotAsync();
            }
        }

        private void OpenProjectHtmlButton_Click(object sender, RoutedEventArgs e)
        {
            OpenTaggedPath(sender as Button);
        }

        private void OpenWorkCycleButton_Click(object sender, RoutedEventArgs e)
        {
            _projectsScreen = ProjectsScreen.WorkCycle;
            ApplyProjectsScreenChrome();
            _ = RefreshRecoveryShellAsync();
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
            if (_shellPassEnabled)
            {
                var chats = _selectedMode == AppMode.Chats;
                var projectsWebActive = !chats && UseProjectsWebRenderer;

                ChatsWebRendererView.Visibility = chats ? Visibility.Visible : Visibility.Collapsed;
                ProjectsHostView.Visibility = chats || projectsWebActive ? Visibility.Collapsed : Visibility.Visible;
                ProjectsWebRendererView.Visibility = projectsWebActive ? Visibility.Visible : Visibility.Collapsed;
                TestSurface.Visibility = Visibility.Collapsed;

                if (projectsWebActive)
                {
                    PushProjectsWebInitialSnapshotIfNeeded();
                }

                WindowRoot.Background = chats
                    ? Brush("Ui.Chat.BackgroundBrush")
                    : new SolidColorBrush(ColorFromHex("#171717"));
                HeaderBorder.Background = WindowRoot.Background;
                ShellBrandText.Foreground = chats
                    ? new SolidColorBrush(ColorFromHex("#646464"))
                    : new SolidColorBrush(ColorFromHex("#B8B8B8"));
                ShellBrandText.Opacity = chats ? 0.62 : 0.72;

                ModeSwitchView.SwitchBorder.Background = chats
                    ? new SolidColorBrush(ColorFromHex("#F7F4EE"))
                    : new SolidColorBrush(ColorFromHex("#232323"));
                ModeSwitchView.SwitchBorder.BorderBrush = chats
                    ? new SolidColorBrush(ColorFromHex("#E7DED3"))
                    : new SolidColorBrush(ColorFromHex("#343434"));
                ModeSwitchView.SwitchBorder.BorderThickness = new Thickness(1);

                ApplyModeButtonState(ModeSwitchView.ChatsButton, chats, isChatsButton: true);
                ApplyModeButtonState(ModeSwitchView.ProjectsButton, !chats, isChatsButton: false);
                return;
            }

            var chatsMode = _selectedMode == AppMode.Chats;
            var projectsWebActiveFallback = !chatsMode && UseProjectsWebRenderer;

            ChatsWebRendererView.Visibility = chatsMode ? Visibility.Visible : Visibility.Collapsed;
            ProjectsHostView.Visibility = chatsMode || projectsWebActiveFallback ? Visibility.Collapsed : Visibility.Visible;
            ProjectsWebRendererView.Visibility = projectsWebActiveFallback ? Visibility.Visible : Visibility.Collapsed;

            if (projectsWebActiveFallback)
            {
                PushProjectsWebInitialSnapshotIfNeeded();
            }

            WindowRoot.Background = chatsMode
                ? Brush("Ui.Chat.BackgroundBrush")
                : new SolidColorBrush(ColorFromHex("#171717"));
            HeaderBorder.Background = chatsMode
                ? new SolidColorBrush(Color.FromArgb(0, 0, 0, 0))
                : new SolidColorBrush(ColorFromHex("#1D1D1D"));
            HeaderBorder.BorderBrush = chatsMode
                ? new SolidColorBrush(Color.FromArgb(0, 0, 0, 0))
                : new SolidColorBrush(ColorFromHex("#323232"));
            HeaderBorder.BorderThickness = chatsMode
                ? new Thickness(0)
                : new Thickness(0, 0, 0, 1);
            HeaderBorder.Padding = chatsMode
                ? new Thickness(0, 8, 0, 0)
                : new Thickness(14, 12, 14, 12);
            HeaderBorder.Margin = chatsMode
                ? new Thickness(24, 16, 24, 0)
                : new Thickness(18, 18, 18, 0);
            TitleBarRoot.MinHeight = chatsMode ? 40 : 64;
            ModeSwitchView.SwitchBorder.Background = chatsMode
                ? Brush("Ui.Chat.ChromeSurfaceBrush")
                : new SolidColorBrush(ColorFromHex("#2A2A2A"));
            ModeSwitchView.SwitchBorder.BorderBrush = chatsMode
                ? Brush("Ui.Chat.ChromeBorderBrush")
                : new SolidColorBrush(Color.FromArgb(0, 0, 0, 0));
            ModeSwitchView.SwitchBorder.BorderThickness = chatsMode
                ? new Thickness(1)
                : new Thickness(1);

            WindowStatusText.Text = chatsMode
                ? AppText.Current.Get("shell.chats_mode_active")
                : _isDemoMode
                    ? AppText.Current.Get("shell.projects_mode_demo_active")
                    : AppText.Current.Get("shell.projects_mode_active");
            WindowStatusText.Foreground = chatsMode
                ? Brush("Ui.Chat.TextSecondaryBrush")
                : new SolidColorBrush(ColorFromHex("#CFCFCF"));
            WindowTitleText.Foreground = chatsMode
                ? Brush("Ui.Chat.TextPrimaryBrush")
                : new SolidColorBrush(ColorFromHex("#F2F2F2"));
            WindowTitleText.Visibility = chatsMode ? Visibility.Collapsed : Visibility.Visible;
            WindowStatusText.Visibility = chatsMode ? Visibility.Collapsed : Visibility.Visible;
            RefreshButton.Visibility = chatsMode ? Visibility.Collapsed : Visibility.Visible;

            ApplyModeButtonState(ModeSwitchView.ChatsButton, chatsMode, isChatsButton: true);
            ApplyModeButtonState(ModeSwitchView.ProjectsButton, !chatsMode, isChatsButton: false);
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
                Flow.SurfacePhase.Discussion => AppText.Current.Get("projects.chrome.note.discussion"),
                Flow.SurfacePhase.Execution when workCycle.PhaseState.ExecutionSubphase == Flow.ExecutionSubphase.Preflight => AppText.Current.Get("projects.chrome.note.execution_preflight"),
                Flow.SurfacePhase.Execution => AppText.Current.Get("projects.chrome.note.execution"),
                Flow.SurfacePhase.Result => AppText.Current.Get("projects.chrome.note.result"),
                Flow.SurfacePhase.Completed => AppText.Current.Get("projects.chrome.note.completed"),
                _ => AppText.Current.Get("projects.chrome.note.fallback")
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
            if (_shellPassEnabled)
            {
                if (isChatsButton)
                {
                    button.Background = selected ? new SolidColorBrush(ColorFromHex("#FFFFFF")) : new SolidColorBrush(Color.FromArgb(0, 0, 0, 0));
                    button.BorderBrush = new SolidColorBrush(Color.FromArgb(0, 0, 0, 0));
                    button.BorderThickness = new Thickness(0);
                    button.Foreground = selected ? new SolidColorBrush(ColorFromHex("#2F2924")) : new SolidColorBrush(ColorFromHex("#8B8278"));
                    return;
                }

                button.Background = selected ? new SolidColorBrush(ColorFromHex("#2F2F2F")) : new SolidColorBrush(Color.FromArgb(0, 0, 0, 0));
                button.BorderBrush = new SolidColorBrush(Color.FromArgb(0, 0, 0, 0));
                button.BorderThickness = new Thickness(0);
                button.Foreground = selected ? new SolidColorBrush(ColorFromHex("#F2F2F2")) : new SolidColorBrush(ColorFromHex("#8F8F8F"));
                return;
            }

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

        // Pass 1 step 3a: pushes the initial Projects Web snapshot the first time the
        // Web renderer becomes visible. Step 3c: also ensures the runtime controller is
        // initialized so that the snapshot's Conversation portion is valid.
        private void PushProjectsWebInitialSnapshotIfNeeded()
        {
            if (_projectsWebInitialSnapshotPushed)
            {
                return;
            }

            _projectsWebInitialSnapshotPushed = true;
            _ = EnsureProjectsWebReadyAndPushAsync();
        }

        private async Task EnsureProjectsWebReadyAndPushAsync()
        {
            var projection = ProjectsShellProjection.Build(_projectRoot);
            if (!_projectsController.IsInitialized) await _projectsController.EnsureInitializedAsync(projection.ProjectId, projection.ProjectName);
            PushProjectsWebSnapshot();
        }

        private void PushProjectsWebSnapshot()
        {
            var snapshot = _projectsWebSnapshotBuilder.Build();
            _ = ProjectsWebRendererView.ApplySnapshotAsync(snapshot);
        }

        // Pass 1 step 3b: navigation intents (navigate_screen, select_project).
        // Pass 1 step 3c: composer intents (send_message, request_attach_files,
        // stage_text_artifact) routed to the shared ProjectsRuntimeController so
        // attachments / long text / atomic submission all flow through the engine
        // that already powers Chats. Snapshot pushed after each state-changing intent.
        private void ProjectsWebRendererView_IntentReceived(object? sender, ChatsWebIntentReceivedEventArgs e)
        {
            var type = e.Message.Type;
            var payload = e.Message.Payload;
            Debug.WriteLine($"[ProjectsWeb intent] type={type} payload={payload.GetRawText()}");

            switch (type)
            {
                case "navigate_screen":
                    if (payload.ValueKind == JsonValueKind.Object &&
                        payload.TryGetProperty("screen", out var screenProp) &&
                        screenProp.ValueKind == JsonValueKind.String)
                    {
                        var screen = screenProp.GetString();
                        if (!string.IsNullOrWhiteSpace(screen) && _projectsWebSnapshotBuilder.NavigateTo(screen))
                        {
                            PushProjectsWebSnapshot();
                        }
                    }
                    break;

                case "select_project":
                    if (payload.ValueKind == JsonValueKind.Object &&
                        payload.TryGetProperty("projectId", out var projectIdProp) &&
                        projectIdProp.ValueKind == JsonValueKind.String)
                    {
                        var projectId = projectIdProp.GetString();
                        if (!string.IsNullOrWhiteSpace(projectId))
                        {
                            var entry = ProjectRegistryStorage.Load().Projects
                                .FirstOrDefault(p => string.Equals(p.Id, projectId, StringComparison.Ordinal));
                            if (entry is not null)
                            {
                                ProjectsWebRendererView.SetSelectedProjectFolder(entry.RootPath);
                                ProjectRegistryStorage.Touch(projectId);
                                _ = HandleProjectsWebSelectProjectAsync(entry, projectId);
                                break;
                            }
                            if (_projectsWebSnapshotBuilder.SelectProject(projectId))
                            {
                                PushProjectsWebSnapshot();
                            }
                        }
                    }
                    break;

                case "enter_work":
                    _ = HandleProjectsWebWorkCycleActionAsync("enter_work", payload);
                    break;

                case "confirm_preflight":
                    _ = HandleProjectsWebWorkCycleActionAsync("confirm_preflight", payload);
                    break;

                case "accept_result":
                    _ = HandleProjectsWebWorkCycleActionAsync("accept_result", payload);
                    break;

                case "reject_result":
                    _ = HandleProjectsWebWorkCycleActionAsync("reject_result", payload);
                    break;

                case "request_revision":
                    _ = HandleProjectsWebWorkCycleActionAsync("request_revision", payload);
                    break;

                case "apply_clarification":
                    _ = HandleProjectsWebWorkCycleActionAsync("apply_clarification", payload);
                    break;

                case "return_to_chat":
                    _ = HandleProjectsWebWorkCycleActionAsync("return_to_chat", payload);
                    break;

                case "send_message":
                    if (TryGetMessageText(payload, out var messageText))
                    {
                        _ = SendProjectsWebMessageAsync(messageText);
                    }
                    break;

                case "request_attach_files":
                    _ = HandleProjectsWebAttachFilesAsync();
                    break;

                case "stage_text_artifact":
                    if (payload.ValueKind == JsonValueKind.Object &&
                        payload.TryGetProperty("text", out var stagedTextProp) &&
                        stagedTextProp.ValueKind == JsonValueKind.String)
                    {
                        var stagedText = stagedTextProp.GetString();
                        if (!string.IsNullOrWhiteSpace(stagedText) &&
                            _projectsController.StageLongTextArtifact(stagedText))
                        {
                            PushProjectsWebSnapshot();
                        }
                    }
                    break;

                case "remove_attachment":
                    if (TryGetDraftId(payload, out var removeDraftId) &&
                        _projectsController.RemovePendingComposerInput(removeDraftId))
                    {
                        PushProjectsWebSnapshot();
                    }
                    break;

                case "promote_preview_doc":
                    _ = HandleProjectsWebPromotePreviewDocAsync(payload);
                    break;

                case "reject_preview_doc":
                    _ = HandleProjectsWebRejectPreviewDocAsync(payload);
                    break;

                case "import_project":
                    _ = HandleProjectsWebImportAsync();
                    break;

                case "create_project":
                    {
                        var newName = string.Empty;
                        var newKind = "generic";
                        if (payload.ValueKind == JsonValueKind.Object)
                        {
                            if (payload.TryGetProperty("name", out var newNameProp) &&
                                newNameProp.ValueKind == JsonValueKind.String)
                            {
                                newName = newNameProp.GetString() ?? string.Empty;
                            }
                            if (payload.TryGetProperty("kind", out var newKindProp) &&
                                newKindProp.ValueKind == JsonValueKind.String)
                            {
                                newKind = newKindProp.GetString() ?? "generic";
                            }
                        }
                        _ = HandleProjectsWebCreateAsync(newName, newKind);
                    }
                    break;
            }
        }

        // Pass 1 step 4b: "new project" via in-app modal — receives {name, kind} from JS,
        // resolves a unique folder under ~/Documents/ZAVOD/, runs Bootstrap with the
        // explicit name (decoupled from folder), persists kind hint as plain text for
        // future Lead orientation, adds to registry. No FilePicker — folder is implicit.
        private async Task HandleProjectsWebCreateAsync(string name, string kind)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                Debug.WriteLine("[ProjectsWeb create] empty name, cancelled");
                return;
            }

            var trimmedName = name.Trim();
            var trimmedKind = string.IsNullOrWhiteSpace(kind) ? "generic" : kind.Trim();

            string resolvedFolder;
            try
            {
                resolvedFolder = ResolveUniqueProjectFolder(trimmedName);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ProjectsWeb create] folder resolution failed: {ex.Message}");
                return;
            }

            Debug.WriteLine($"[ProjectsWeb create] start name={trimmedName} kind={trimmedKind} path={resolvedFolder}");

            try
            {
                await Task.Run(() =>
                {
                    Directory.CreateDirectory(resolvedFolder);
                    ProjectBootstrap.Initialize(resolvedFolder, trimmedName);
                    WriteProjectKindHint(resolvedFolder, trimmedKind);
                });

                var entry = ProjectRegistryStorage.Add(trimmedName, resolvedFolder);
                Debug.WriteLine($"[ProjectsWeb create] registry entry id={entry.Id} name={entry.Name}");
                PushProjectsWebSnapshot();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ProjectsWeb create] FAILED: {ex}");
            }
        }

        private static string ResolveUniqueProjectFolder(string projectName)
        {
            var hub = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                "ZAVOD");
            Directory.CreateDirectory(hub);

            var baseSlug = SlugifyForFolder(projectName);
            if (string.IsNullOrWhiteSpace(baseSlug))
            {
                baseSlug = "project";
            }

            var candidate = Path.Combine(hub, baseSlug);
            var suffix = 2;
            while (Directory.Exists(candidate))
            {
                candidate = Path.Combine(hub, $"{baseSlug}-{suffix}");
                suffix++;
            }

            return candidate;
        }

        private static string SlugifyForFolder(string value)
        {
            var builder = new StringBuilder(value.Length);
            var previousWasSeparator = false;
            foreach (var character in value)
            {
                if (char.IsLetterOrDigit(character))
                {
                    builder.Append(char.ToLowerInvariant(character));
                    previousWasSeparator = false;
                    continue;
                }
                if (previousWasSeparator || builder.Length == 0)
                {
                    continue;
                }
                builder.Append('-');
                previousWasSeparator = true;
            }
            return builder.ToString().Trim('-');
        }

        private static void WriteProjectKindHint(string projectRoot, string kind)
        {
            try
            {
                var metaDir = Path.Combine(projectRoot, ".zavod", "meta");
                Directory.CreateDirectory(metaDir);
                File.WriteAllText(Path.Combine(metaDir, "project_kind.txt"), kind, Encoding.UTF8);
            }
            catch (Exception ex)
            {
                // Kind is a soft hint — failing to persist must not abort project creation.
                Debug.WriteLine($"[ProjectsWeb create] kind hint write failed: {ex.Message}");
            }
        }

        // Pass 1 step 4a: minimal end-to-end import roundtrip.
        // Picks a folder, runs Bootstrap + Scanner + Importer (gpt-4.1-nano) off the UI
        // thread, opens the generated preview.html on success. No registry persistence yet
        // — the project sits on disk with its own .zavod/, user can re-open via OS.
        private async Task HandleProjectsWebImportAsync()
        {
            string? folderPath;
            try
            {
                folderPath = await PickFolderAsync();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ProjectsWeb import] folder picker failed: {ex.Message}");
                return;
            }

            if (string.IsNullOrWhiteSpace(folderPath))
            {
                Debug.WriteLine("[ProjectsWeb import] cancelled");
                return;
            }

            var resolvedFolder = Path.GetFullPath(folderPath);
            Debug.WriteLine($"[ProjectsWeb import] start path={resolvedFolder}");

            try
            {
                var runResult = await Task.Run(() =>
                {
                    ProjectBootstrap.Initialize(resolvedFolder);
                    var scanResult = WorkspaceScanner.Scan(new WorkspaceScanRequest(resolvedFolder));
                    var runtime = new WorkspaceImportMaterialInterpreterRuntime();
                    return runtime.Interpret(scanResult);
                });

                Debug.WriteLine($"[ProjectsWeb import] done: {runResult.SummaryLine}");

                var entry = ProjectRegistryStorage.Add(new DirectoryInfo(resolvedFolder).Name, resolvedFolder);
                Debug.WriteLine($"[ProjectsWeb import] registry entry id={entry.Id} name={entry.Name}");
                PushProjectsWebSnapshot();

                var previewPath = Path.Combine(resolvedFolder, ".zavod", "preview.html");
                if (File.Exists(previewPath))
                {
                    Debug.WriteLine($"[ProjectsWeb import] opening preview at {previewPath}");
                    Process.Start(new ProcessStartInfo(previewPath) { UseShellExecute = true });
                }
                else
                {
                    Debug.WriteLine($"[ProjectsWeb import] preview.html not found at {previewPath} (interpreter may have degraded honestly)");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ProjectsWeb import] FAILED: {ex}");
            }
        }

        private async Task<string?> PickFolderAsync()
        {
            var picker = new FolderPicker();
            InitializeWithWindow.Initialize(picker, WindowNative.GetWindowHandle(this));
            picker.SuggestedStartLocation = PickerLocationId.DocumentsLibrary;
            picker.FileTypeFilter.Add("*");

            var folder = await picker.PickSingleFolderAsync();
            return folder?.Path;
        }

        private async Task HandleProjectsWebWorkCycleActionAsync(string action, JsonElement payload)
        {
            try
            {
                switch (action)
                {
                    case "enter_work":
                        await _workCycleActions.EnterWorkAsync();
                        break;
                    case "confirm_preflight":
                        await _workCycleActions.ConfirmPreflightAsync();
                        break;
                    case "accept_result":
                        await _workCycleActions.AcceptResultAsync();
                        break;
                    case "reject_result":
                        await _workCycleActions.RejectResultAsync();
                        break;
                    case "request_revision":
                        await _workCycleActions.RequestRevisionAsync();
                        break;
                    case "apply_clarification":
                        {
                            var text = payload.ValueKind == JsonValueKind.Object
                                && payload.TryGetProperty("text", out var textProp)
                                && textProp.ValueKind == JsonValueKind.String
                                ? textProp.GetString() ?? string.Empty
                                : string.Empty;
                            await _workCycleActions.ApplyClarificationAsync(text);
                        }
                        break;
                    case "return_to_chat":
                        await _workCycleActions.ReturnToChatAsync();
                        break;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ProjectsWeb] work-cycle action '{action}' failed: {ex.Message}");
            }

            PushProjectsWebSnapshot();
        }

        private async Task SendProjectsWebMessageAsync(string text)
        {
            try
            {
                _projectsController.EnsureActiveAdapter();
                var submission = await _projectsController.ConsumeComposerSubmissionAsync(text);
                if (submission.IsEmpty)
                {
                    return;
                }
                if (submission.HasText)
                {
                    var handled = await _workCycleActions.SendProjectsMessageAsync(submission);
                    if (!handled)
                    {
                        await _projectsController.ActiveAdapter.AddMessageAsync(
                            ConversationItemKind.Status,
                            AppText.Current.Get("role.shift_lead"),
                            "Сообщение не обработано: текущая фаза не принимает ввод. Используйте кнопки действий в правой панели.");
                    }
                }

                _projectsController.CommitActiveConversation();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ProjectsWeb] send_message failed: {ex.GetType().Name}: {ex.Message}");
            }

            PushProjectsWebSnapshot();
        }


        private async Task HandleProjectsWebSelectProjectAsync(ProjectRegistryEntry entry, string projectId)
        {
            _workCycleActions.SetProjectRoot(entry.RootPath);
            try
            {
                await _projectsController.ReanchorToAsync(entry.RootPath, projectId, entry.Name);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ProjectsWeb] reanchor failed: {ex.Message}");
            }

            _projectsWebSnapshotBuilder.SelectProject(projectId);
            PushProjectsWebSnapshot();
        }

        private async Task HandleProjectsWebAttachFilesAsync()
        {
            if (await StageComposerFilesAsync(projectsMode: true))
            {
                PushProjectsWebSnapshot();
            }
        }

        private async Task HandleProjectsWebPromotePreviewDocAsync(JsonElement payload)
        {
            if (payload.ValueKind != JsonValueKind.Object ||
                !payload.TryGetProperty("kind", out var kindProp) ||
                kindProp.ValueKind != JsonValueKind.String ||
                !TryParseProjectDocumentKind(kindProp.GetString(), out var kind))
            {
                Debug.WriteLine("[ProjectsWeb promote] missing or invalid document kind");
                return;
            }

            var selectedProjectId = _projectsWebSnapshotBuilder.SelectedProjectId;
            if (string.IsNullOrWhiteSpace(selectedProjectId))
            {
                Debug.WriteLine("[ProjectsWeb promote] no selected project");
                return;
            }

            var entry = ProjectRegistryStorage.Load().Projects
                .FirstOrDefault(p => string.Equals(p.Id, selectedProjectId, StringComparison.Ordinal));
            if (entry is null || !Directory.Exists(entry.RootPath))
            {
                Debug.WriteLine("[ProjectsWeb promote] selected project root unavailable");
                return;
            }

            try
            {
                var result = await Task.Run(() =>
                {
                    var documentRuntime = new ProjectDocumentRuntimeService();
                    return documentRuntime.PromotePreviewDoc(entry.RootPath, kind);
                });
                Debug.WriteLine($"[ProjectsWeb promote] {result.Kind} -> {result.CanonicalDocumentPath}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ProjectsWeb promote] FAILED: {ex.GetType().Name}: {ex.Message}");
            }

            PushProjectsWebSnapshot();
        }

        private async Task HandleProjectsWebRejectPreviewDocAsync(JsonElement payload)
        {
            if (payload.ValueKind != JsonValueKind.Object ||
                !payload.TryGetProperty("kind", out var kindProp) ||
                kindProp.ValueKind != JsonValueKind.String ||
                !TryParseProjectDocumentKind(kindProp.GetString(), out var kind))
            {
                Debug.WriteLine("[ProjectsWeb reject preview] missing or invalid document kind");
                return;
            }

            var selectedProjectId = _projectsWebSnapshotBuilder.SelectedProjectId;
            if (string.IsNullOrWhiteSpace(selectedProjectId))
            {
                Debug.WriteLine("[ProjectsWeb reject preview] no selected project");
                return;
            }

            var entry = ProjectRegistryStorage.Load().Projects
                .FirstOrDefault(p => string.Equals(p.Id, selectedProjectId, StringComparison.Ordinal));
            if (entry is null || !Directory.Exists(entry.RootPath))
            {
                Debug.WriteLine("[ProjectsWeb reject preview] selected project root unavailable");
                return;
            }

            try
            {
                var result = await Task.Run(() =>
                {
                    var documentRuntime = new ProjectDocumentRuntimeService();
                    return documentRuntime.RejectPreviewDoc(entry.RootPath, kind);
                });
                Debug.WriteLine($"[ProjectsWeb reject preview] {result.Kind} -> {result.PreviewRejectedEventId}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ProjectsWeb reject preview] FAILED: {ex.GetType().Name}: {ex.Message}");
            }

            PushProjectsWebSnapshot();
        }

        private static bool TryParseProjectDocumentKind(string? value, out ProjectDocumentKind kind)
        {
            switch (value?.Trim().ToLowerInvariant())
            {
                case "project":
                    kind = ProjectDocumentKind.Project;
                    return true;
                case "direction":
                    kind = ProjectDocumentKind.Direction;
                    return true;
                case "roadmap":
                    kind = ProjectDocumentKind.Roadmap;
                    return true;
                case "canon":
                    kind = ProjectDocumentKind.Canon;
                    return true;
                case "capsule":
                    kind = ProjectDocumentKind.Capsule;
                    return true;
                default:
                    kind = default;
                    return false;
            }
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

        private async Task TryCaptureVerificationAsync()
        {
            if (_verificationCaptureCompleted
                || string.IsNullOrWhiteSpace(_verificationCapturePath)
                || !string.Equals(_verificationCaptureMode, "projects-home", StringComparison.Ordinal))
            {
                return;
            }

            _verificationCaptureCompleted = true;
            await Task.Delay(250);
            await SaveElementScreenshotAsync(WindowRoot, _verificationCapturePath);
            if (_verificationCaptureExit)
            {
                Close();
            }
        }

        private async Task TryRunChatsLiveProofAsync()
        {
            if (_verificationLiveProofStarted
                || !string.Equals(_verificationCaptureMode, "chats-live-proof", StringComparison.Ordinal))
            {
                return;
            }

            _verificationLiveProofStarted = true;
            var result = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["mode"] = "chats-live-proof",
                ["startedAt"] = DateTimeOffset.Now
            };

            try
            {
                if (string.IsNullOrWhiteSpace(_verificationProofTextPath) || !File.Exists(_verificationProofTextPath))
                {
                    throw new InvalidOperationException("Live proof text file is missing.");
                }

                var proofText = File.ReadAllText(_verificationProofTextPath, Encoding.UTF8);
                var proofPrompt = string.IsNullOrWhiteSpace(_verificationProofPrompt)
                    ? "О чем этот текст?"
                    : _verificationProofPrompt.Trim();

                await _chatsController.EnsureInitializedAsync();
                _chatsController.CreateOrActivateDraft();
                if (!_chatsController.StageLongTextArtifact(proofText))
                {
                    throw new InvalidOperationException("Live proof text did not cross the artifact staging threshold.");
                }

                await ApplyShellPassChatsSnapshotAsync();
                var submission = await _chatsController.ConsumeComposerSubmissionAsync(proofPrompt);
                result["conversationId"] = submission.NormalizedConversationId;
                result["submissionText"] = submission.Text;
                result["attachmentCount"] = submission.Attachments.Count;
                result["attachmentLabels"] = submission.Attachments.Select(static item => item.DisplayName).ToArray();
                result["attachmentTypes"] = submission.Attachments.Select(static item => item.IntakeType).ToArray();

                var sendSucceeded = await _chatsController.SendMessageAsync(
                    submission,
                    _shellPassEnabled ? ApplyShellPassChatsSnapshotAsync : null);
                result["sendSucceeded"] = sendSucceeded;
                if (_shellPassEnabled)
                {
                    await ApplyShellPassChatsSnapshotAsync();
                }

                var snapshot = _chatsController.BuildSnapshot();
                result["messageCount"] = snapshot.Messages.Count;
                result["artifactVisible"] = snapshot.Messages.Any(static message => string.Equals(message.Kind, "artifact", StringComparison.Ordinal));
                result["cyrillicVisible"] = snapshot.Messages.Any(message => ContainsCyrillic(message.Text));
                result["assistantText"] = snapshot.Messages
                    .LastOrDefault(message => string.Equals(message.Role, "assistant", StringComparison.Ordinal))?.Text ?? string.Empty;

                await Task.Delay(500);
                if (!string.IsNullOrWhiteSpace(_verificationCapturePath))
                {
                    await SaveElementScreenshotAsync(WindowRoot, _verificationCapturePath);
                    result["screenshotPath"] = _verificationCapturePath;
                }

                result["success"] = true;
            }
            catch (Exception ex)
            {
                result["success"] = false;
                result["error"] = ex.ToString();
            }
            finally
            {
                if (!string.IsNullOrWhiteSpace(_verificationProofResultPath))
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(_verificationProofResultPath)!);
                    var options = new JsonSerializerOptions
                    {
                        WriteIndented = true,
                        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
                    };
                    await File.WriteAllTextAsync(
                        _verificationProofResultPath,
                        JsonSerializer.Serialize(result, options),
                        Encoding.UTF8);
                }

                if (_verificationCaptureExit)
                {
                    Close();
                }
            }
        }

        private static async Task SaveElementScreenshotAsync(FrameworkElement element, string outputPath)
        {
            var bitmap = new RenderTargetBitmap();
            await bitmap.RenderAsync(element);
            var pixels = await bitmap.GetPixelsAsync();

            Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);

            using var stream = new InMemoryRandomAccessStream();
            var encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.PngEncoderId, stream);
            encoder.SetPixelData(
                BitmapPixelFormat.Bgra8,
                BitmapAlphaMode.Premultiplied,
                (uint)bitmap.PixelWidth,
                (uint)bitmap.PixelHeight,
                element.XamlRoot?.RasterizationScale ?? 1,
                element.XamlRoot?.RasterizationScale ?? 1,
                pixels.ToArray());
            await encoder.FlushAsync();
            stream.Seek(0);

            var buffer = new byte[stream.Size];
            await stream.ReadAsync(buffer.AsBuffer(), (uint)stream.Size, InputStreamOptions.None);
            await File.WriteAllBytesAsync(outputPath, buffer);
        }

        private static bool ContainsCyrillic(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            return value.Any(static ch => ch is >= '\u0400' and <= '\u04FF');
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

            var queryState = ProjectWorkCycleQueryStateBuilder.Build(Path.GetFullPath(_projectRoot));
            var shellProjection = ProjectsShellProjection.Build(queryState);
            var workCycleProjection = ProjectWorkCycleProjection.Build(queryState, shellProjection);
            var appMode = _selectedMode == AppMode.Chats ? "Chats" : "Projects";

            return UiVerificationSnapshotBuilder.BuildJson(
                appMode,
                projectsScreenProjection,
                workCycleProjection);
        }
    }
}
