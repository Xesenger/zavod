using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using zavod.Diagnostics;
using zavod.Execution;
using zavod.Persistence;
using zavod.UI.Rendering.Conversation;
using zavod.UI.Text;

namespace zavod.UI.Modes.Chats;

internal sealed class ChatsRuntimeController
{
    private const int InitialWindowSize = 12;
    private const string ChatMode = "chat";

    private sealed class ChatSessionState
    {
        public ChatSessionState(
            string id,
            ChatsAdapter adapter,
            ConversationLogStorage storage,
            string title,
            bool isDraft,
            DateTimeOffset updatedAt)
        {
            Id = id;
            Adapter = adapter;
            Storage = storage;
            Title = title;
            IsDraft = isDraft;
            UpdatedAt = updatedAt;
            WindowStartSeq = 0;
            HasLoaded = false;
        }

        public string Id { get; }

        public ChatsAdapter Adapter { get; }

        public ConversationLogStorage Storage { get; }

        public string Title { get; set; }

        public bool IsDraft { get; set; }

        public DateTimeOffset UpdatedAt { get; set; }

        public int WindowStartSeq { get; set; }

        public bool HasLoaded { get; set; }
    }

    private readonly string _projectRoot;
    private readonly ConversationLogStorage _legacyChatStorage;
    private readonly ConversationArtifactStorage _artifactStorage;
    private readonly ConversationComposerDraftStore _composerDraftStore;
    private readonly IOpenRouterExecutionClient _openRouterClient;
    private readonly List<ChatSessionState> _chatSessions = new();
    private ChatsAdapter _activeAdapter;
    private ChatSessionState? _activeChatSession;
    private ChatSessionState? _draftChatSession;
    private bool _initialized;

    public ChatsRuntimeController(string projectRoot, IOpenRouterExecutionClient? openRouterClient = null)
    {
        _projectRoot = Path.GetFullPath(projectRoot);
        _legacyChatStorage = ConversationLogStorage.ForChats(_projectRoot);
        _artifactStorage = new ConversationArtifactStorage(_projectRoot);
        _composerDraftStore = new ConversationComposerDraftStore(_artifactStorage);
        _openRouterClient = openRouterClient ?? new OpenRouterExecutionClient();
        _activeAdapter = new ChatsAdapter(artifactStorage: _artifactStorage);
    }

    public async Task EnsureInitializedAsync()
    {
        if (_initialized)
        {
            return;
        }

        var indexEntries = ConversationIndexStorage.Load(_projectRoot)
            .Where(entry => string.Equals(entry.Mode, ChatMode, StringComparison.OrdinalIgnoreCase))
            .ToArray();
        if (indexEntries.Length > 0)
        {
            _chatSessions.Clear();
            foreach (var entry in indexEntries)
            {
                _chatSessions.Add(new ChatSessionState(
                    entry.ConversationId,
                    new ChatsAdapter(artifactStorage: _artifactStorage),
                    ConversationLogStorage.ForChatConversation(_projectRoot, entry.ConversationId),
                    entry.Title,
                    isDraft: false,
                    entry.UpdatedAt));
            }

            var latestSession = _chatSessions[0];
            await EnsureSessionLoadedAsync(latestSession);
            ActivateChatSession(latestSession);
            _initialized = true;
            return;
        }

        var restoredWindow = _legacyChatStorage.LoadLatestWindow(InitialWindowSize);
        RootCauseTrace.Mark(
            "storage_latest_window_loaded",
            $"start={restoredWindow.WindowStartSeq}, end={restoredWindow.WindowEndSeq}, total={restoredWindow.TotalCount}, loaded={restoredWindow.Snapshots.Count}");

        if (restoredWindow.Snapshots.Count == 0)
        {
            ActivateChatSession(null);
            _initialized = true;
            return;
        }

        var conversationId = ConversationRouting.CreateConversationId();
        var restoredAdapter = new ChatsAdapter(artifactStorage: _artifactStorage);
        await restoredAdapter.LoadSnapshotsAsync(restoredWindow.Snapshots);

        var updatedAt = restoredWindow.Snapshots[^1].Timestamp;
        var session = new ChatSessionState(
            id: conversationId,
            adapter: restoredAdapter,
            storage: ConversationLogStorage.ForChatConversation(_projectRoot, conversationId, fallbackFileName: "chats-active.jsonl"),
            title: BuildChatTitle(restoredAdapter),
            isDraft: false,
            updatedAt: updatedAt);
        session.WindowStartSeq = restoredWindow.WindowStartSeq;
        session.HasLoaded = true;
        _chatSessions.Clear();
        _chatSessions.Add(session);
        ActivateChatSession(session);
        _initialized = true;
    }

    public ChatsWebStateSnapshot BuildSnapshot()
    {
        var loadedCount = _activeAdapter.Items.Count;
        var windowStartSeq = ResolveWindowStartSeq(_activeChatSession, loadedCount);
        var windowEndSeq = loadedCount == 0 ? 0 : windowStartSeq + loadedCount - 1;
        var hasOlder = windowStartSeq > 1;
        var messages = ConversationWebProjection.BuildMessages(_activeAdapter.Items);
        var composer = BuildComposerState();
        var localizedComposer = new ChatsWebComposerState(
            AppText.Current.Get("chats.composer.placeholder"),
            composer.PendingAttachments);

        return new ChatsWebStateSnapshot(
            Mode: "chats",
            ActiveChatId: _activeChatSession is { IsDraft: false } ? _activeChatSession.Id : null,
            IsEmpty: messages.Length == 0 && (localizedComposer.PendingAttachments?.Count ?? 0) == 0,
            HasOlder: hasOlder,
            WindowStartSeq: windowStartSeq,
            WindowEndSeq: windowEndSeq,
            Chats: _chatSessions.Select(session => new ChatsWebChatSummary(session.Id, session.Title)).ToArray(),
            Messages: messages,
            EmptyState: new ChatsWebEmptyState(
                AppText.Current.Get("chats.empty.headline"),
                AppText.Current.Get("chats.empty.subtitle")),
            Composer: localizedComposer,
            Text: BuildLocalizedText());
    }

    public async Task<ConversationComposerSubmission> ConsumeComposerSubmissionAsync(string? text, string authorLabel = "User")
    {
        var conversationId = GetOrCreateActiveConversationId();
        var pendingDrafts = _composerDraftStore.ConsumeDrafts(conversationId);
        foreach (var draft in pendingDrafts)
        {
            await _activeAdapter.AddArtifactReferenceAsync(
                authorLabel,
                draft.Reference,
                ConversationArtifactStorage.BuildConversationInputMetadata(
                    draft.ConversationId,
                    draft.ProjectId,
                    draft.Origin,
                    draft.IntakeType,
                    draft.DisplayName,
                    draft.Reference));
        }

        return new ConversationComposerSubmission(
            conversationId,
            text?.Trim() ?? string.Empty,
            pendingDrafts);
    }

    public async Task<bool> SendMessageAsync(string text, Func<Task>? publishSnapshotAsync = null)
    {
        var submission = await ConsumeComposerSubmissionAsync(text);
        return await SendMessageAsync(submission, publishSnapshotAsync);
    }

    public async Task<bool> SendMessageAsync(ConversationComposerSubmission submission, Func<Task>? publishSnapshotAsync = null)
    {
        ArgumentNullException.ThrowIfNull(submission);

        var trimmed = submission.Text?.Trim();
        if (string.IsNullOrWhiteSpace(trimmed) && submission.Attachments.Count == 0)
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(trimmed))
        {
            await _activeAdapter.AddMessageAsync(ConversationItemKind.User, "User", trimmed);
            if (publishSnapshotAsync is not null)
            {
                await publishSnapshotAsync();
            }
        }

        if (submission.Attachments.Count > 0 && publishSnapshotAsync is not null)
        {
            await publishSnapshotAsync();
        }

        if (!string.IsNullOrWhiteSpace(trimmed))
        {
            var assistantItem = await _activeAdapter.AddMessageAsync(
                ConversationItemKind.Assistant,
                "Assistant",
                string.Empty,
                isStreaming: true);
            if (publishSnapshotAsync is not null)
            {
                await publishSnapshotAsync();
            }

            var attachments = ConversationAttachmentPromptBuilder.Load(submission.Attachments);
            var assistantText = await ExecuteAssistantReplyAsync(trimmed, attachments);
            await _activeAdapter.CompleteStreamingAsync(assistantItem, assistantText);
            if (publishSnapshotAsync is not null)
            {
                await publishSnapshotAsync();
            }
        }

        FinalizeActiveSession();
        return true;
    }

    public bool StageFiles(IReadOnlyList<string> filePaths)
    {
        ArgumentNullException.ThrowIfNull(filePaths);

        var conversationId = GetOrCreateActiveConversationId();
        var staged = _composerDraftStore.StageFiles(conversationId, projectId: null, filePaths);
        return staged.Count > 0;
    }

    public bool StageLongTextArtifact(string text)
    {
        var conversationId = GetOrCreateActiveConversationId();
        return _composerDraftStore.StageLongTextArtifact(conversationId, projectId: null, text) is not null;
    }

    public bool RemovePendingComposerInput(string draftId)
    {
        if (string.IsNullOrWhiteSpace(draftId))
        {
            return false;
        }

        var conversationId = _activeChatSession?.Id ?? _draftChatSession?.Id;
        return !string.IsNullOrWhiteSpace(conversationId)
            && _composerDraftStore.RemoveDraft(conversationId, draftId.Trim());
    }

    public void CreateOrActivateDraft()
    {
        if (_draftChatSession is not null)
        {
            ActivateChatSession(_draftChatSession);
            return;
        }

        var conversationId = ConversationRouting.CreateConversationId();
        _draftChatSession = new ChatSessionState(
            id: conversationId,
            adapter: new ChatsAdapter(artifactStorage: _artifactStorage),
            storage: ConversationLogStorage.ForChatConversation(_projectRoot, conversationId),
            title: AppText.Current.Get("chats.draft.title"),
            isDraft: true,
            updatedAt: DateTimeOffset.Now);
        _draftChatSession.HasLoaded = true;
        ActivateChatSession(_draftChatSession);
    }

    public async Task<bool> SelectChatAsync(string id)
    {
        var session = _chatSessions.FirstOrDefault(candidate => string.Equals(candidate.Id, id, StringComparison.Ordinal));
        if (session is null)
        {
            return false;
        }

        await EnsureSessionLoadedAsync(session);
        ActivateChatSession(session);
        return true;
    }

    public async Task<bool> TryLoadOlderAsync(int beforeSeq)
    {
        if (_activeChatSession is null)
        {
            return false;
        }

        var currentWindowStart = ResolveWindowStartSeq(_activeChatSession, _activeChatSession.Adapter.Items.Count);
        var effectiveBeforeSeq = beforeSeq <= 0 ? currentWindowStart : beforeSeq;
        if (effectiveBeforeSeq <= 1)
        {
            return false;
        }

        var olderWindow = _activeChatSession.Storage.LoadWindowBefore(effectiveBeforeSeq, InitialWindowSize);
        RootCauseTrace.Mark(
            "storage_older_window_loaded",
            $"before={effectiveBeforeSeq}, start={olderWindow.WindowStartSeq}, end={olderWindow.WindowEndSeq}, total={olderWindow.TotalCount}, loaded={olderWindow.Snapshots.Count}");

        if (olderWindow.Snapshots.Count == 0)
        {
            return false;
        }

        await _activeChatSession.Adapter.LoadSnapshotsAsync(olderWindow.Snapshots, replaceExisting: false, prepend: true);
        _activeChatSession.WindowStartSeq = olderWindow.WindowStartSeq;
        _activeChatSession.HasLoaded = true;
        return true;
    }

    private async Task EnsureSessionLoadedAsync(ChatSessionState session)
    {
        if (session.HasLoaded)
        {
            return;
        }

        var restoredWindow = session.Storage.LoadLatestWindow(InitialWindowSize);
        RootCauseTrace.Mark(
            "storage_latest_window_loaded",
            $"conversation={session.Id}, start={restoredWindow.WindowStartSeq}, end={restoredWindow.WindowEndSeq}, total={restoredWindow.TotalCount}, loaded={restoredWindow.Snapshots.Count}");

        await session.Adapter.LoadSnapshotsAsync(restoredWindow.Snapshots);
        session.WindowStartSeq = restoredWindow.WindowStartSeq;
        session.HasLoaded = true;
        if (restoredWindow.Snapshots.Count > 0)
        {
            session.UpdatedAt = restoredWindow.Snapshots[^1].Timestamp;
            session.Title = BuildChatTitle(session.Adapter);
        }
    }

    private void ActivateChatSession(ChatSessionState? session)
    {
        _activeChatSession = session;
        _activeAdapter = session?.Adapter ?? new ChatsAdapter(artifactStorage: _artifactStorage);

        if (session is not null)
        {
            session.WindowStartSeq = ResolveWindowStartSeq(session, session.Adapter.Items.Count);
        }
    }

    private void PersistActiveChatSession()
    {
        if (_activeChatSession is null || _activeChatSession.IsDraft)
        {
            return;
        }

        var olderSnapshots = _activeChatSession.WindowStartSeq > 1
            ? _activeChatSession.Storage.LoadWindowBefore(_activeChatSession.WindowStartSeq, _activeChatSession.WindowStartSeq - 1).Snapshots
            : Array.Empty<ConversationLogSnapshot>();
        var snapshots = olderSnapshots
            .Concat(BuildSnapshots(_activeChatSession.Adapter.Items))
            .ToArray();

        _activeChatSession.Storage.ReplaceAll(snapshots);
        ConversationIndexStorage.Upsert(
            _projectRoot,
            new ConversationIndexEntry(
                _activeChatSession.Id,
                ChatMode,
                ProjectId: null,
                _activeChatSession.Title,
                _activeChatSession.UpdatedAt));
        SortChatSessions();
    }

    private void SortChatSessions()
    {
        _chatSessions.Sort((left, right) =>
        {
            var updatedAt = right.UpdatedAt.CompareTo(left.UpdatedAt);
            return updatedAt != 0
                ? updatedAt
                : string.Compare(left.Id, right.Id, StringComparison.Ordinal);
        });
    }

    private ChatsWebComposerState BuildComposerState()
    {
        var conversationId = _activeChatSession?.Id ?? _draftChatSession?.Id;
        var pendingAttachments = string.IsNullOrWhiteSpace(conversationId)
            ? Array.Empty<ChatsWebComposerAttachment>()
            : _composerDraftStore.GetDrafts(conversationId)
                .Select(draft => new ChatsWebComposerAttachment(
                    draft.DraftId,
                    draft.DisplayName,
                    draft.IntakeType,
                    draft.Detail))
                .ToArray();

        return new ChatsWebComposerState(AppText.Current.Get("chats.composer.placeholder"), pendingAttachments);
    }

    private string GetOrCreateActiveConversationId()
    {
        if (_activeChatSession is not null)
        {
            return _activeChatSession.Id;
        }

        CreateOrActivateDraft();
        return _activeChatSession?.Id
            ?? throw new InvalidOperationException("Active chat conversation is unavailable.");
    }

    private void FinalizeActiveSession()
    {
        if (_activeChatSession is null)
        {
            return;
        }

        if (_activeChatSession.IsDraft)
        {
            _activeChatSession.IsDraft = false;
            _chatSessions.Add(_activeChatSession);
            _draftChatSession = null;
        }

        _activeChatSession.Title = BuildChatTitle(_activeChatSession.Adapter);
        _activeChatSession.UpdatedAt = ResolveUpdatedAt(_activeChatSession.Adapter);
        PersistActiveChatSession();
    }

    private async Task<string> ExecuteAssistantReplyAsync(string userText, IReadOnlyList<OpenRouterAttachment> attachments)
    {
        var request = new OpenRouterExecutionRequest(
            RouteId: "chats.web.runtime",
            SystemPrompt: "You are ZAVOD Chats. Reply helpfully and concisely. Use plain text or light markdown only when useful.",
            UserPrompt: userText,
            ModelId: null,
            Temperature: 0.2,
            Attachments: attachments);

        var response = await Task.Run(() => _openRouterClient.Execute(request));
        if (response.Success && !string.IsNullOrWhiteSpace(response.Content))
        {
            return response.Content.Trim();
        }

        if (response.Diagnostic is not null)
        {
            return AppText.Current.Format("chats.openrouter.failed", response.Diagnostic.Code, response.Diagnostic.Message);
        }

        return AppText.Current.Get("chats.openrouter.empty_failed");
    }

    private static string BuildChatTitle(ChatsAdapter adapter)
    {
        return ConversationWebProjection.BuildTitleFromFirstUserMessage(adapter.Items, AppText.Current.Get("chats.current.title"));
    }

    private static DateTimeOffset ResolveUpdatedAt(ChatsAdapter adapter)
    {
        return adapter.Items.Count == 0
            ? DateTimeOffset.Now
            : adapter.Items[^1].Timestamp;
    }

    private static IReadOnlyList<ConversationLogSnapshot> BuildSnapshots(IEnumerable<ConversationItemViewModel> items)
    {
        return items
            .Select(item => new ConversationLogSnapshot(
                item.Id,
                item.Timestamp,
                item.AuthorLabel,
                item.Kind.ToString(),
                item.Text,
                item.Text,
                StepId: item.Metadata is not null && item.Metadata.TryGetValue("step-id", out var stepId) ? stepId : null,
                Phase: item.Metadata is not null && item.Metadata.TryGetValue("phase", out var phase) ? phase : null,
                Attachments: ConversationArtifactStorage.BuildAttachments(item.Metadata),
                Source: "chats",
                Adapter: "chats",
                item.IsStreaming,
                Metadata: item.Metadata))
            .ToArray();
    }

    private static int ResolveWindowStartSeq(ChatSessionState? session, int loadedCount)
    {
        if (loadedCount <= 0)
        {
            if (session is not null)
            {
                session.WindowStartSeq = 0;
            }

            return 0;
        }

        if (session is null)
        {
            return 1;
        }

        if (session.WindowStartSeq <= 0)
        {
            session.WindowStartSeq = 1;
        }

        return session.WindowStartSeq;
    }

    private static ChatsWebLocalizedText BuildLocalizedText()
    {
        return new ChatsWebLocalizedText(
            AppText.Current.Get("chats.sidebar.show"),
            AppText.Current.Get("chats.sidebar.hide"),
            AppText.Current.Get("chats.new_chat"),
            AppText.Current.Get("chats.add_attachment.title"),
            AppText.Current.Get("chats.send.title"),
            AppText.Current.Get("chats.add_file"),
            AppText.Current.Get("chats.add_image"),
            AppText.Current.Get("chats.add_note"),
            AppText.Current.Get("chats.load_older"),
            AppText.Current.Get("chats.loading_older"),
            AppText.Current.Get("conversation.default_artifact_label"),
            AppText.Current.Get("conversation.default_log_label"));
    }
}

