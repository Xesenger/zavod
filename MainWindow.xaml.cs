using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using Windows.UI;
using zavod.Persistence;
using zavod.Presentation.Projects;

namespace zavod
{
    public sealed partial class MainWindow : Window
    {
        private readonly string _projectRoot;
        private readonly bool _isDemoMode;
        private AppMode _selectedMode = AppMode.Projects;
        private ProjectsScreen _projectsScreen = ProjectsScreen.List;
        private WorkCycleFocus _workCycleFocus = WorkCycleFocus.Auto;
        private bool _initialRefreshRequested;
        public MainWindow(string projectRoot, bool isDemoMode = false)
        {
            _projectRoot = projectRoot ?? throw new ArgumentNullException(nameof(projectRoot));
            _isDemoMode = isDemoMode;

            InitializeComponent();

            Title = GetWindowTitle();
            WindowTitleText.Text = Title;
            ExtendsContentIntoTitleBar = true;
            SetTitleBar(TitleBarRoot);
            Activated += MainWindow_Activated;
        }

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

            ProjectNameText.Text = projection.ProjectName;
            ProjectRootText.Text = projection.ProjectRoot;
            EntryStateText.Text = projection.EntryStateText;
            ActiveShiftText.Text = projection.ActiveShiftText;
            ActiveTaskText.Text = projection.ActiveTaskText;
            StorageStateText.Text = projection.StorageStateText;

            DocumentStageText.Text = projection.DocumentStageText;
            ProjectDocumentPathText.Text = projection.ProjectDocumentPathText;

            OpenProjectDocumentButton.IsEnabled = projection.HasProjectDocument;
            OpenCapsuleDocumentButton.IsEnabled = projection.HasCapsuleDocument;
            OpenProjectDocumentButton.Tag = projection.ProjectDocumentPath;
            OpenCapsuleDocumentButton.Tag = projection.CapsuleDocumentPath;

            ExecutionStateText.Text = projection.ActiveTaskText.EndsWith("none", StringComparison.Ordinal) ? "Recovery hold" : "Task context detected";
            ExecutionDetailText.Text = projection.ActiveTaskText.EndsWith("none", StringComparison.Ordinal)
                ? "Execution wiring is still disabled. The center surface is back only as a verified shell."
                : $"{projection.ActiveTaskText.Replace("Active task: ", "A live task id exists (", StringComparison.Ordinal)}) but execution controls remain intentionally disconnected during recovery.";

            ResultStateText.Text = projection.HasCapsuleDocument ? "Evidence available" : "No result loaded";
            ResultDetailText.Text = projection.HasCapsuleDocument
                ? "A capsule document exists and can be opened from the execution surface."
                : "No capsule/result document is materialized yet.";

            ChatsSummaryText.Text = "Free chats stay outside the bounded ZAVOD project lifecycle and remain a separate top-level mode.";
            ChatsPersistenceText.Text = $"Local chat history root: {Path.Combine(ZavodLocalStorageLayout.GetConversationsRoot(normalizedRoot), "chats-active.jsonl")}";
            ChatsNotesText.Text = "This is only the app-level shell return. Prompt assembly, anchors, execution streaming, and project workflow stay inside Projects mode only.";

            ProjectListSummaryText.Text = "Choose a project entry block. Import and New Project stay disconnected in this slice.";
            ProjectListCurrentProjectText.Text = projection.ProjectListCurrentProjectText;
            ProjectListCurrentStageText.Text = projection.ProjectListCurrentStageText;
            ProjectListCurrentDetailsText.Text = "Current project entry remains read-only in this slice.";
            ProjectListNotesText.Text = "This list stays read-only for now.";

            ProjectHomeSummaryText.Text = "Open HTML, inspect materials, and read a few truthful status lines.";
            ProjectHomeStatusText.Text = projection.ProjectHomeStatusText;
            ProjectHomeStageText.Text = projection.ProjectHomeStageText;
            ProjectHomeActivityText.Text = projection.ProjectHomeActivityText;
            ProjectHomeMaterialsText.Text = projection.ProjectHomeMaterialsText;
            ProjectHomeNotesText.Text = "This surface stays read-only in the current slice.";

            OpenProjectHtmlButton.IsEnabled = projection.HasProjectHtml;
            OpenProjectHtmlButton.Tag = projection.ProjectHtmlPath;
            OpenProjectHomeDocumentButton.IsEnabled = projection.HasProjectDocument;
            OpenProjectHomeDocumentButton.Tag = projection.ProjectDocumentPath;

            var workCycle = ProjectWorkCycleProjection.Build(normalizedRoot, projection);
            WorkCycleChatSurfaceStateText.Text = workCycle.ChatSurfaceStateText;
            WorkCyclePhaseText.Text = $"Phase: {workCycle.PhaseLabel}";
            WorkCycleSummaryText.Text = workCycle.SummaryText;
            WorkCycleChatSummaryTitleText.Text = workCycle.ChatSummaryTitle;
            WorkCycleChatSummaryText.Text = workCycle.ChatSummaryText;
            WorkCycleChatSummaryNoteText.Text = workCycle.ChatSummaryNote;
            WorkCycleExecutionSurfaceStateText.Text = workCycle.ExecutionSurfaceStateText;
            WorkCycleExecutionSummaryText.Text = workCycle.ExecutionSummaryText;
            ExecutionDetailText.Text = workCycle.ExecutionDetailText;
            DocumentStageText.Text = workCycle.ExecutionEvidenceText;
            WorkCycleResultSurfaceStateText.Text = workCycle.ResultSurfaceStateText;
            WorkCycleResultSummaryText.Text = workCycle.ResultSummaryText;
            ResultDetailText.Text = workCycle.ResultDetailText;
            ResultNotesText.Text = workCycle.ResultEvidenceText;
            InteractionNotesText.Text = "Discussion UI and composer remain intentionally disconnected until the owner path is re-proven.";
            ProjectInteractionSurface.Visibility = workCycle.Projection.ShowChat ? Visibility.Visible : Visibility.Collapsed;
            ProjectExecutionSurface.Visibility = workCycle.Projection.ShowExecution ? Visibility.Visible : Visibility.Collapsed;
            ProjectResultSurface.Visibility = workCycle.Projection.ShowResult ? Visibility.Visible : Visibility.Collapsed;
            ApplyWorkCycleColumnLayout(workCycle);
            ApplyWorkCyclePhaseChrome(workCycle);

            ApplyModeChrome();
            ApplyProjectsScreenChrome();
        }

        private void RefreshButton_Click(object sender, RoutedEventArgs e)
        {
            _ = RefreshRecoveryShellAsync();
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

            ChatsModeRoot.Visibility = chats ? Visibility.Visible : Visibility.Collapsed;
            ProjectsModeRoot.Visibility = chats ? Visibility.Collapsed : Visibility.Visible;

            WindowRoot.Background = chats
                ? new SolidColorBrush(ColorFromHex("#F2ECE4"))
                : new SolidColorBrush(ColorFromHex("#171717"));
            HeaderBorder.Background = chats
                ? new SolidColorBrush(ColorFromHex("#F7F1E8"))
                : new SolidColorBrush(ColorFromHex("#1D1D1D"));
            HeaderBorder.BorderBrush = chats
                ? new SolidColorBrush(ColorFromHex("#D8CEC1"))
                : new SolidColorBrush(ColorFromHex("#323232"));

            WindowStatusText.Text = chats
                ? "Chats mode active. Free chat stays outside project lifecycle."
                : _isDemoMode
                    ? "Projects mode active. Demo runtime detected. Project List recovery slice remains in safe incremental mode."
                    : "Projects mode active. Project List recovery slice remains in safe incremental mode.";
            WindowStatusText.Foreground = chats
                ? Brush("Ui.Chat.TextSecondaryBrush")
                : new SolidColorBrush(ColorFromHex("#CFCFCF"));
            WindowTitleText.Foreground = chats
                ? Brush("Ui.Chat.TextPrimaryBrush")
                : new SolidColorBrush(ColorFromHex("#F2F2F2"));

            ApplyModeButtonState(ChatsModeButton, chats, isChatsButton: true);
            ApplyModeButtonState(ProjectsModeButton, !chats, isChatsButton: false);
        }

        private void ApplyProjectsScreenChrome()
        {
            ProjectListRoot.Visibility = _projectsScreen == ProjectsScreen.List ? Visibility.Visible : Visibility.Collapsed;
            ProjectHomeRoot.Visibility = _projectsScreen == ProjectsScreen.Home ? Visibility.Visible : Visibility.Collapsed;
            ProjectWorkCycleRoot.Visibility = _projectsScreen == ProjectsScreen.WorkCycle ? Visibility.Visible : Visibility.Collapsed;
            ProjectsBackButton.Visibility = _selectedMode == AppMode.Projects && _projectsScreen != ProjectsScreen.List
                ? Visibility.Visible
                : Visibility.Collapsed;
            ProjectsScreenTitleText.Text = _projectsScreen switch
            {
                ProjectsScreen.List => "Project List",
                ProjectsScreen.Home => "Project Home",
                ProjectsScreen.WorkCycle => "Project Work Cycle",
                _ => "Projects"
            };
            ProjectsModeSummaryText.Text = _projectsScreen switch
            {
                ProjectsScreen.List => "Project navigation currently stays read-only.",
                ProjectsScreen.Home => "Project navigation currently stays read-only.",
                ProjectsScreen.WorkCycle => "Project Work Cycle is back as a phase-driven read-only shell in this slice.",
                _ => "Projects mode active."
            };
            WorkCycleFocusBar.Visibility = _projectsScreen == ProjectsScreen.WorkCycle
                ? Visibility.Visible
                : Visibility.Collapsed;
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

            InteractionNotesText.Text = phase switch
            {
                Flow.SurfacePhase.Discussion => "Discussion stays visible and read-only in this slice. Composer remains intentionally disconnected.",
                Flow.SurfacePhase.Execution when workCycle.PhaseState.ExecutionSubphase == Flow.ExecutionSubphase.Preflight => "Chat stays primary while execution appears only as a prepared shell.",
                Flow.SurfacePhase.Execution => "Execution is the active surface. Chat remains visible only as bounded frozen context in this slice.",
                Flow.SurfacePhase.Result => "Result is the active decision surface. Execution stays visible as context and chat remains frozen.",
                Flow.SurfacePhase.Completed => "Completed state is shown as historical context only in this slice.",
                _ => "Discussion UI and composer remain intentionally disconnected until the owner path is re-proven."
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
                button.Background = selected ? Brush("Ui.Chat.AccentFillBrush") : new SolidColorBrush(ColorFromHex("#FFFDF8"));
                button.BorderBrush = selected ? Brush("Ui.Chat.AccentLineBrush") : Brush("Ui.Chat.BorderQuietBrush");
                button.Foreground = selected ? Brush("Ui.Chat.AccentForegroundBrush") : Brush("Ui.Chat.TextPrimaryBrush");
                return;
            }

            button.Background = selected ? Brush("Ui.Project.AccentFillBrush") : new SolidColorBrush(ColorFromHex("#202020"));
            button.BorderBrush = selected ? Brush("Ui.Project.AccentLineBrush") : Brush("Ui.Project.BorderQuietBrush");
            button.Foreground = selected ? Brush("Ui.Project.AccentForegroundBrush") : Brush("Ui.Project.TextPrimaryBrush");
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
    }
}
