using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using zavod.Diagnostics;
using zavod.Persistence;
using zavod.UI.Modes.Chats;
using zavod.UI.Rendering.Conversation;
using zavod.UI.Text;

namespace zavod.UI.Modes.Projects;

internal sealed class ProjectsRuntimeController
{
    private const int InitialWindowSize = 12;
    private const string ProjectMode = "project";

    private sealed class ProjectConversationSessionState
    {
        public ProjectConversationSessionState(
            string id,
            ProjectsAdapter adapter,
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

        public ProjectsAdapter Adapter { get; }

        public ConversationLogStorage Storage { get; }

        public string Title { get; set; }

        public bool IsDraft { get; set; }

        public DateTimeOffset UpdatedAt { get; set; }

        public int WindowStartSeq { get; set; }

        public bool HasLoaded { get; set; }
    }

    private readonly string _projectRoot;
    private readonly ConversationLogStorage _legacyProjectStorage;
    private readonly ConversationArtifactStorage _artifactStorage;
    private readonly ConversationComposerDraftStore _composerDraftStore;
    private readonly List<ProjectConversationSessionState> _projectConversations = new();
    private ProjectsAdapter _activeAdapter;
    private ProjectConversationSessionState? _activeConversation;
    private ProjectConversationSessionState? _draftConversation;
    private string? _projectId;
    private string _projectName = "Project";
    private bool _initialized;

    public ProjectsRuntimeController(string projectRoot)
    {
        _projectRoot = Path.GetFullPath(projectRoot);
        _legacyProjectStorage = ConversationLogStorage.ForProjects(_projectRoot);
        _artifactStorage = new ConversationArtifactStorage(_projectRoot);
        _composerDraftStore = new ConversationComposerDraftStore(_artifactStorage);
        _activeAdapter = new ProjectsAdapter(artifactStorage: _artifactStorage);
    }

    public ProjectsAdapter ActiveAdapter => EnsureActiveAdapter();

    public async Task EnsureInitializedAsync(string projectId, string projectName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(projectId);
        ArgumentException.ThrowIfNullOrWhiteSpace(projectName);

        var normalizedProjectId = projectId.Trim();
        var normalizedProjectName = projectName.Trim();
        if (_initialized && string.Equals(_projectId, normalizedProjectId, StringComparison.Ordinal))
        {
            _projectName = normalizedProjectName;
            return;
        }

        ResetState(normalizedProjectId, normalizedProjectName);

        var indexEntries = ConversationIndexStorage.Load(_projectRoot)
            .Where(entry => string.Equals(entry.Mode, ProjectMode, StringComparison.OrdinalIgnoreCase)
                && string.Equals(entry.ProjectId, normalizedProjectId, StringComparison.Ordinal))
            .ToArray();
        if (indexEntries.Length > 0)
        {
            foreach (var entry in indexEntries)
            {
                _projectConversations.Add(new ProjectConversationSessionState(
                    entry.ConversationId,
                    CreateAdapter(ConversationLogStorage.ForProjectConversation(_projectRoot, entry.ConversationId)),
                    ConversationLogStorage.ForProjectConversation(_projectRoot, entry.ConversationId),
                    entry.Title,
                    isDraft: false,
                    entry.UpdatedAt));
            }

            var latestSession = _projectConversations[0];
            await EnsureSessionLoadedAsync(latestSession);
            ActivateConversation(latestSession);
            _initialized = true;
            return;
        }

        var restoredWindow = _legacyProjectStorage.LoadLatestWindow(InitialWindowSize);
        RootCauseTrace.Mark(
            "projects_storage_latest_window_loaded",
            $"start={restoredWindow.WindowStartSeq}, end={restoredWindow.WindowEndSeq}, total={restoredWindow.TotalCount}, loaded={restoredWindow.Snapshots.Count}");

        if (restoredWindow.Snapshots.Count == 0)
        {
            ActivateConversation(null);
            _initialized = true;
            return;
        }

        var conversationId = ConversationRouting.CreateConversationId();
        var storage = ConversationLogStorage.ForProjectConversation(_projectRoot, conversationId, fallbackFileName: "projects-active.jsonl");
        var restoredAdapter = CreateAdapter(storage);
        await restoredAdapter.LoadSnapshotsAsync(restoredWindow.Snapshots);

        var updatedAt = restoredWindow.Snapshots[^1].Timestamp;
        var session = new ProjectConversationSessionState(
            id: conversationId,
            adapter: restoredAdapter,
            storage: storage,
            title: ConversationWebProjection.BuildTitleFromFirstUserMessage(restoredAdapter.Items, normalizedProjectName),
            isDraft: false,
            updatedAt: updatedAt);
        session.WindowStartSeq = restoredWindow.WindowStartSeq;
        session.HasLoaded = true;
        _projectConversations.Add(session);
        ActivateConversation(session);
        _initialized = true;
    }

    public ChatsWebStateSnapshot BuildSnapshot()
    {
        var loadedCount = _activeAdapter.Items.Count;
        var windowStartSeq = ResolveWindowStartSeq(_activeConversation, loadedCount);
        var windowEndSeq = loadedCount == 0 ? 0 : windowStartSeq + loadedCount - 1;
        var hasOlder = windowStartSeq > 1;
        var messages = ConversationWebProjection.BuildMessages(_activeAdapter.Items);
        var composer = BuildComposerState();

        return new ChatsWebStateSnapshot(
            Mode: "projects",
            ActiveChatId: _activeConversation is { IsDraft: false } ? _activeConversation.Id : null,
            IsEmpty: messages.Length == 0 && (composer.PendingAttachments?.Count ?? 0) == 0,
            HasOlder: hasOlder,
            WindowStartSeq: windowStartSeq,
            WindowEndSeq: windowEndSeq,
            Chats: _projectConversations.Select(session => new ChatsWebChatSummary(session.Id, session.Title)).ToArray(),
            Messages: messages,
            EmptyState: new ChatsWebEmptyState(
                AppText.Current.Format("projects.empty.headline", _projectName),
                AppText.Current.Get("projects.empty.subtitle")),
            Composer: new ChatsWebComposerState(
                AppText.Current.Get("projects.work_cycle.composer.placeholder"),
                composer.PendingAttachments),
            Text: BuildLocalizedText());
    }

    public ProjectsAdapter EnsureActiveAdapter()
    {
        if (_activeConversation is null)
        {
            CreateOrActivateDraft();
        }

        return _activeAdapter;
    }

    public bool HasPendingComposerInputs()
    {
        var conversationId = _activeConversation?.Id ?? _draftConversation?.Id;
        return !string.IsNullOrWhiteSpace(conversationId)
            && _composerDraftStore.GetDrafts(conversationId).Count > 0;
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
            pendingDrafts,
            _projectId);
    }

    public async Task FlushPendingComposerInputsAsync(string authorLabel = "User")
    {
        _ = await ConsumeComposerSubmissionAsync(text: null, authorLabel);
    }

    public bool StageFiles(IReadOnlyList<string> filePaths)
    {
        ArgumentNullException.ThrowIfNull(filePaths);

        var conversationId = GetOrCreateActiveConversationId();
        var staged = _composerDraftStore.StageFiles(conversationId, _projectId, filePaths);
        return staged.Count > 0;
    }

    public bool StageLongTextArtifact(string text)
    {
        var conversationId = GetOrCreateActiveConversationId();
        return _composerDraftStore.StageLongTextArtifact(conversationId, _projectId, text) is not null;
    }

    public bool RemovePendingComposerInput(string draftId)
    {
        if (string.IsNullOrWhiteSpace(draftId))
        {
            return false;
        }

        var conversationId = _activeConversation?.Id ?? _draftConversation?.Id;
        return !string.IsNullOrWhiteSpace(conversationId)
            && _composerDraftStore.RemoveDraft(conversationId, draftId.Trim());
    }

    public void CreateOrActivateDraft()
    {
        if (_draftConversation is not null)
        {
            ActivateConversation(_draftConversation);
            return;
        }

        var conversationId = ConversationRouting.CreateConversationId();
        var storage = ConversationLogStorage.ForProjectConversation(_projectRoot, conversationId);
        _draftConversation = new ProjectConversationSessionState(
            id: conversationId,
            adapter: CreateAdapter(storage),
            storage: storage,
            title: AppText.Current.Get("projects.draft.title"),
            isDraft: true,
            updatedAt: DateTimeOffset.Now);
        _draftConversation.HasLoaded = true;
        ActivateConversation(_draftConversation);
    }

    public async Task<bool> SelectConversationAsync(string id)
    {
        var session = _projectConversations.FirstOrDefault(candidate => string.Equals(candidate.Id, id, StringComparison.Ordinal));
        if (session is null)
        {
            return false;
        }

        await EnsureSessionLoadedAsync(session);
        ActivateConversation(session);
        return true;
    }

    public async Task<bool> TryLoadOlderAsync(int beforeSeq)
    {
        if (_activeConversation is null)
        {
            return false;
        }

        var currentWindowStart = ResolveWindowStartSeq(_activeConversation, _activeConversation.Adapter.Items.Count);
        var effectiveBeforeSeq = beforeSeq <= 0 ? currentWindowStart : beforeSeq;
        if (effectiveBeforeSeq <= 1)
        {
            return false;
        }

        var olderWindow = _activeConversation.Storage.LoadWindowBefore(effectiveBeforeSeq, InitialWindowSize);
        RootCauseTrace.Mark(
            "projects_storage_older_window_loaded",
            $"before={effectiveBeforeSeq}, start={olderWindow.WindowStartSeq}, end={olderWindow.WindowEndSeq}, total={olderWindow.TotalCount}, loaded={olderWindow.Snapshots.Count}");

        if (olderWindow.Snapshots.Count == 0)
        {
            return false;
        }

        await _activeConversation.Adapter.LoadSnapshotsAsync(olderWindow.Snapshots, replaceExisting: false, prepend: true);
        _activeConversation.WindowStartSeq = olderWindow.WindowStartSeq;
        _activeConversation.HasLoaded = true;
        return true;
    }

    public void CommitActiveConversation()
    {
        if (_activeConversation is null || string.IsNullOrWhiteSpace(_projectId))
        {
            return;
        }

        if (_activeConversation.Adapter.Items.Count == 0)
        {
            return;
        }

        if (_activeConversation.IsDraft)
        {
            _activeConversation.IsDraft = false;
            _projectConversations.Add(_activeConversation);
            _draftConversation = null;
        }

        _activeConversation.Title = ConversationWebProjection.BuildTitleFromFirstUserMessage(_activeConversation.Adapter.Items, _projectName);
        _activeConversation.UpdatedAt = ResolveUpdatedAt(_activeConversation.Adapter);

        var olderSnapshots = _activeConversation.WindowStartSeq > 1
            ? _activeConversation.Storage.LoadWindowBefore(_activeConversation.WindowStartSeq, _activeConversation.WindowStartSeq - 1).Snapshots
            : Array.Empty<ConversationLogSnapshot>();
        var snapshots = olderSnapshots
            .Concat(BuildSnapshots(_activeConversation.Adapter.Items))
            .ToArray();

        _activeConversation.Storage.ReplaceAll(snapshots);
        ConversationIndexStorage.Upsert(
            _projectRoot,
            new ConversationIndexEntry(
                _activeConversation.Id,
                ProjectMode,
                _projectId,
                _activeConversation.Title,
                _activeConversation.UpdatedAt));
        SortConversations();
    }

    private void ResetState(string projectId, string projectName)
    {
        _projectConversations.Clear();
        _activeConversation = null;
        _draftConversation = null;
        _projectId = projectId;
        _projectName = projectName;
        _activeAdapter = new ProjectsAdapter(artifactStorage: _artifactStorage);
        _initialized = false;
    }

    private ProjectsAdapter CreateAdapter(ConversationLogStorage storage)
    {
        return new ProjectsAdapter(storage: storage, artifactStorage: _artifactStorage);
    }

    private async Task EnsureSessionLoadedAsync(ProjectConversationSessionState session)
    {
        if (session.HasLoaded)
        {
            return;
        }

        var restoredWindow = session.Storage.LoadLatestWindow(InitialWindowSize);
        RootCauseTrace.Mark(
            "projects_storage_latest_window_loaded",
            $"conversation={session.Id}, start={restoredWindow.WindowStartSeq}, end={restoredWindow.WindowEndSeq}, total={restoredWindow.TotalCount}, loaded={restoredWindow.Snapshots.Count}");

        await session.Adapter.LoadSnapshotsAsync(restoredWindow.Snapshots);
        session.WindowStartSeq = restoredWindow.WindowStartSeq;
        session.HasLoaded = true;
        if (restoredWindow.Snapshots.Count > 0)
        {
            session.UpdatedAt = restoredWindow.Snapshots[^1].Timestamp;
            session.Title = ConversationWebProjection.BuildTitleFromFirstUserMessage(session.Adapter.Items, _projectName);
        }
    }

    private void ActivateConversation(ProjectConversationSessionState? session)
    {
        _activeConversation = session;
        _activeAdapter = session?.Adapter ?? new ProjectsAdapter(artifactStorage: _artifactStorage);

        if (session is not null)
        {
            session.WindowStartSeq = ResolveWindowStartSeq(session, session.Adapter.Items.Count);
        }
    }

    private void SortConversations()
    {
        _projectConversations.Sort((left, right) =>
        {
            var updatedAt = right.UpdatedAt.CompareTo(left.UpdatedAt);
            return updatedAt != 0
                ? updatedAt
                : string.Compare(left.Id, right.Id, StringComparison.Ordinal);
        });
    }

    private ChatsWebComposerState BuildComposerState()
    {
        var conversationId = _activeConversation?.Id ?? _draftConversation?.Id;
        var pendingAttachments = string.IsNullOrWhiteSpace(conversationId)
            ? Array.Empty<ChatsWebComposerAttachment>()
            : _composerDraftStore.GetDrafts(conversationId)
                .Select(draft => new ChatsWebComposerAttachment(
                    draft.DraftId,
                    draft.DisplayName,
                    draft.IntakeType,
                    draft.Detail))
                .ToArray();

        return new ChatsWebComposerState(AppText.Current.Get("projects.work_cycle.composer.placeholder"), pendingAttachments);
    }

    private string GetOrCreateActiveConversationId()
    {
        if (_activeConversation is not null)
        {
            return _activeConversation.Id;
        }

        CreateOrActivateDraft();
        return _activeConversation?.Id
            ?? throw new InvalidOperationException("Active project conversation is unavailable.");
    }

    private static DateTimeOffset ResolveUpdatedAt(ProjectsAdapter adapter)
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
                Source: "projects",
                Adapter: "projects",
                item.IsStreaming,
                item.Metadata))
            .ToArray();
    }

    private static int ResolveWindowStartSeq(ProjectConversationSessionState? session, int loadedCount)
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
