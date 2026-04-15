using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.Web.WebView2.Core;
using System;
using System.IO;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Threading.Tasks;
using zavod.Diagnostics;

namespace zavod.UI.Modes.Chats;

public sealed partial class ChatsWebRendererView : UserControl
{
    private const string VirtualHostName = "appassets.zavod";
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    private bool _isInitialized;
    private bool _navigationCompleted;
    private string? _pendingSnapshotJson;
    private Task? _initializationTask;

    public ChatsWebRendererView()
    {
        InitializeComponent();
        RootCauseTrace.Mark("loading_overlay_initially_visible");
        Loaded += ChatsWebRendererView_Loaded;
    }

    public event EventHandler<ChatsWebIntentReceivedEventArgs>? IntentReceived;
    public event EventHandler? FirstFrameReady;

    public Task PreloadAsync()
    {
        RootCauseTrace.Mark("webview_prewarm_start");
        return EnsureInitializedAsync();
    }

    public async Task ApplySnapshotAsync(ChatsWebStateSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        var envelope = new ChatsWebEnvelope<ChatsWebStateSnapshot>("state_snapshot", snapshot);
        _pendingSnapshotJson = JsonSerializer.Serialize(envelope, JsonOptions);
        RootCauseTrace.Mark(
            "state_snapshot_window",
            $"start={snapshot.WindowStartSeq}, end={snapshot.WindowEndSeq}, hasOlder={snapshot.HasOlder}, count={snapshot.Messages.Count}");

        await EnsureInitializedAsync();
        await FlushSnapshotAsync();
    }

    private async void ChatsWebRendererView_Loaded(object sender, RoutedEventArgs e)
    {
        RootCauseTrace.Mark("webview_loaded");
        await EnsureInitializedAsync();
    }

    private async Task EnsureInitializedAsync()
    {
        if (_isInitialized)
        {
            RootCauseTrace.Mark("webview_ensure_skipped_initialized");
            return;
        }

        if (_initializationTask is not null)
        {
            RootCauseTrace.Mark("webview_ensure_join_existing");
            await _initializationTask;
            return;
        }

        _initializationTask = EnsureInitializedCoreAsync();
        await _initializationTask;
    }

    private async Task EnsureInitializedCoreAsync()
    {
        RootCauseTrace.Mark("webview_ensure_start");
        await ChatsWebView.EnsureCoreWebView2Async();
        RootCauseTrace.Mark("webview_ensure_end");

        var core = ChatsWebView.CoreWebView2;
        if (core is null)
        {
            throw new InvalidOperationException("WebView2 core was not initialized.");
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

        core.WebMessageReceived += CoreWebView2_WebMessageReceived;
        core.NavigationStarting += CoreWebView2_NavigationStarting;
        core.NavigationCompleted += CoreWebView2_NavigationCompleted;
        core.NewWindowRequested += CoreWebView2_NewWindowRequested;

        ChatsWebView.Source = new Uri($"https://{VirtualHostName}/UI/Web/Chats/chats.surface.html?v=top-layer-v2");
        _isInitialized = true;
        RootCauseTrace.Mark("webview_source_set", ChatsWebView.Source.ToString());
    }

    private async void CoreWebView2_NavigationCompleted(CoreWebView2 sender, CoreWebView2NavigationCompletedEventArgs args)
    {
        _navigationCompleted = args.IsSuccess;
        RootCauseTrace.Mark("webview_navigation_completed", args.IsSuccess.ToString());
        await FlushSnapshotAsync();
    }

    private void CoreWebView2_NavigationStarting(CoreWebView2 sender, CoreWebView2NavigationStartingEventArgs args)
    {
        LoadingOverlay.Visibility = Visibility.Visible;
        RootCauseTrace.Mark("webview_navigation_starting", args.Uri);

        if (!Uri.TryCreate(args.Uri, UriKind.Absolute, out var uri))
        {
            args.Cancel = true;
            return;
        }

        if (uri.Scheme == Uri.UriSchemeHttps && string.Equals(uri.Host, VirtualHostName, StringComparison.OrdinalIgnoreCase))
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
            LoadingOverlay.Visibility = Visibility.Collapsed;
            RootCauseTrace.Mark("render_complete_received");
            RootCauseTrace.Mark("loading_overlay_hidden");
            FirstFrameReady?.Invoke(this, EventArgs.Empty);
            return;
        }

        if (string.Equals(message.Type, "conversation_patch_stats", StringComparison.Ordinal))
        {
            RootCauseTrace.Mark("conversation_patch_stats", message.Payload.GetRawText());
        }

        if (string.Equals(message.Type, "windowing_stats", StringComparison.Ordinal))
        {
            RootCauseTrace.Mark("windowing_stats", message.Payload.GetRawText());
        }

        if (string.Equals(message.Type, "request_older", StringComparison.Ordinal))
        {
            RootCauseTrace.Mark("request_older", message.Payload.GetRawText());
        }

        RootCauseTrace.Mark("web_message_received", message.Type);
        IntentReceived?.Invoke(this, new ChatsWebIntentReceivedEventArgs(message));
    }

    private async Task FlushSnapshotAsync()
    {
        if (!_isInitialized || !_navigationCompleted || string.IsNullOrWhiteSpace(_pendingSnapshotJson))
        {
            return;
        }

        ChatsWebView.CoreWebView2?.PostWebMessageAsJson(_pendingSnapshotJson);
        RootCauseTrace.Mark("state_snapshot_posted");
        await Task.CompletedTask;
    }
}
