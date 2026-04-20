using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.Web.WebView2.Core;
using System;
using System.Diagnostics;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Threading.Tasks;
using zavod.Diagnostics;
using zavod.UI.Modes.Chats;
using zavod.UI.Modes.Projects.Bridge;

namespace zavod.UI.Modes.Projects;

public sealed partial class ProjectsWebRendererView : UserControl
{
    private const string VirtualHostName = "appassets.zavod";
    private const string SelectedProjectVirtualHost = "projectfiles.zavod";
    private string? _currentSelectedProjectFolder;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    private bool _isInitialized;
    private bool _navigationCompleted;
    private string? _pendingSnapshotJson;
    private Task? _initializationTask;

    public ProjectsWebRendererView()
    {
        InitializeComponent();
        RootCauseTrace.Mark("projects_webview_construct");
        Loaded += ProjectsWebRendererView_Loaded;
    }

    public event EventHandler<ChatsWebIntentReceivedEventArgs>? IntentReceived;
    public event EventHandler? FirstFrameReady;

    public Task PreloadAsync()
    {
        RootCauseTrace.Mark("projects_webview_prewarm_start");
        return EnsureInitializedAsync();
    }

    /// <summary>
    /// Registers a virtual host mapping that exposes the selected project's folder to
    /// the embedded HTML so iframes / images / docs from <c>&lt;project&gt;/.zavod/</c>
    /// can load via <c>https://projectfiles.zavod/...</c>. Calling repeatedly with the
    /// same path is a no-op; switching to a different project replaces the mapping
    /// (last call wins per the WebView2 contract).
    /// </summary>
    public void SetSelectedProjectFolder(string? folderPath)
    {
        if (string.IsNullOrWhiteSpace(folderPath))
        {
            return;
        }

        var resolved = System.IO.Path.GetFullPath(folderPath);
        if (string.Equals(_currentSelectedProjectFolder, resolved, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var core = ProjectsWebView.CoreWebView2;
        if (core is null)
        {
            // Renderer not initialized yet — folder will be applied after EnsureInitializedCoreAsync.
            _currentSelectedProjectFolder = resolved;
            return;
        }

        core.SetVirtualHostNameToFolderMapping(
            SelectedProjectVirtualHost,
            resolved,
            CoreWebView2HostResourceAccessKind.Allow);
        _currentSelectedProjectFolder = resolved;
        RootCauseTrace.Mark("projects_selected_folder_mapped", resolved);
    }

    public async Task ApplySnapshotAsync(ProjectsWebStateSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        var envelope = new ChatsWebEnvelope<ProjectsWebStateSnapshot>("state_snapshot", snapshot);
        _pendingSnapshotJson = JsonSerializer.Serialize(envelope, JsonOptions);
        RootCauseTrace.Mark(
            "projects_state_snapshot_window",
            $"screen={snapshot.CurrentScreen}, hasOlder={snapshot.Conversation.HasOlder}, count={snapshot.Conversation.Messages.Count}");

        await EnsureInitializedAsync();
        await FlushSnapshotAsync();
    }

    private async void ProjectsWebRendererView_Loaded(object sender, RoutedEventArgs e)
    {
        RootCauseTrace.Mark("projects_webview_loaded");
        await EnsureInitializedAsync();
    }

    private async Task EnsureInitializedAsync()
    {
        if (_isInitialized)
        {
            return;
        }

        if (_initializationTask is not null)
        {
            await _initializationTask;
            return;
        }

        _initializationTask = EnsureInitializedCoreAsync();
        await _initializationTask;
    }

    private async Task EnsureInitializedCoreAsync()
    {
        RootCauseTrace.Mark("projects_webview_ensure_start");
        await ProjectsWebView.EnsureCoreWebView2Async();
        RootCauseTrace.Mark("projects_webview_ensure_end");

        var core = ProjectsWebView.CoreWebView2;
        if (core is null)
        {
            throw new InvalidOperationException("Projects WebView2 core was not initialized.");
        }

        core.Settings.AreDefaultContextMenusEnabled = false;
        core.Settings.AreDevToolsEnabled = false;
        core.Settings.IsStatusBarEnabled = false;
        core.Settings.IsZoomControlEnabled = false;
        core.Settings.AreDefaultScriptDialogsEnabled = true;
        core.Settings.IsWebMessageEnabled = true;
        core.SetVirtualHostNameToFolderMapping(
            VirtualHostName,
            AppContext.BaseDirectory,
            CoreWebView2HostResourceAccessKind.Allow);

        if (!string.IsNullOrWhiteSpace(_currentSelectedProjectFolder))
        {
            core.SetVirtualHostNameToFolderMapping(
                SelectedProjectVirtualHost,
                _currentSelectedProjectFolder,
                CoreWebView2HostResourceAccessKind.Allow);
            RootCauseTrace.Mark("projects_selected_folder_mapped_on_init", _currentSelectedProjectFolder);
        }

        core.WebMessageReceived += CoreWebView2_WebMessageReceived;
        core.NavigationStarting += CoreWebView2_NavigationStarting;
        core.NavigationCompleted += CoreWebView2_NavigationCompleted;
        core.NewWindowRequested += CoreWebView2_NewWindowRequested;

        ProjectsWebView.Source = new Uri($"https://{VirtualHostName}/UI/Web/Projects/projects.surface.html");
        _isInitialized = true;
        RootCauseTrace.Mark("projects_webview_source_set", ProjectsWebView.Source.ToString());
    }

    private async void CoreWebView2_NavigationCompleted(CoreWebView2 sender, CoreWebView2NavigationCompletedEventArgs args)
    {
        _navigationCompleted = args.IsSuccess;
        RootCauseTrace.Mark("projects_webview_navigation_completed", args.IsSuccess.ToString());
        await FlushSnapshotAsync();
    }

    private void CoreWebView2_NavigationStarting(CoreWebView2 sender, CoreWebView2NavigationStartingEventArgs args)
    {
        RootCauseTrace.Mark("projects_webview_navigation_starting", args.Uri);

        if (!Uri.TryCreate(args.Uri, UriKind.Absolute, out var uri))
        {
            args.Cancel = true;
            return;
        }

        if (uri.Scheme == Uri.UriSchemeHttps &&
            (string.Equals(uri.Host, VirtualHostName, StringComparison.OrdinalIgnoreCase) ||
             string.Equals(uri.Host, SelectedProjectVirtualHost, StringComparison.OrdinalIgnoreCase)))
        {
            return;
        }

        args.Cancel = true;
    }

    private void CoreWebView2_NewWindowRequested(CoreWebView2 sender, CoreWebView2NewWindowRequestedEventArgs args)
    {
        args.Handled = true;
    }

    private void CoreWebView2_WebMessageReceived(CoreWebView2 sender, CoreWebView2WebMessageReceivedEventArgs args)
    {
        var raw = args.WebMessageAsJson;
        if (string.IsNullOrWhiteSpace(raw))
        {
            return;
        }

        var message = JsonSerializer.Deserialize<ChatsWebIntentMessage>(raw, JsonOptions);
        if (message is null || string.IsNullOrWhiteSpace(message.Type))
        {
            return;
        }

        if (string.Equals(message.Type, "render_complete", StringComparison.Ordinal))
        {
            RootCauseTrace.Mark("projects_render_complete_received");
            FirstFrameReady?.Invoke(this, EventArgs.Empty);
            return;
        }

        // Pass-through to host (MainWindow) which will dispatch to WorkCycleActionController etc.
        // For now (Pass 1 scaffolding) host can subscribe to IntentReceived; if no subscriber
        // we just trace for diagnostics.
        Debug.WriteLine($"[ProjectsWeb] {message.Type}: {message.Payload.GetRawText()}");
        RootCauseTrace.Mark("projects_web_message_received", message.Type);
        IntentReceived?.Invoke(this, new ChatsWebIntentReceivedEventArgs(message));
    }

    private async Task FlushSnapshotAsync()
    {
        if (!_isInitialized || !_navigationCompleted || string.IsNullOrWhiteSpace(_pendingSnapshotJson))
        {
            return;
        }

        ProjectsWebView.CoreWebView2?.PostWebMessageAsJson(_pendingSnapshotJson);
        RootCauseTrace.Mark("projects_state_snapshot_posted");
        await Task.CompletedTask;
    }
}
