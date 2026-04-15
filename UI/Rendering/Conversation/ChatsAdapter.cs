using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;
using zavod.Persistence;

namespace zavod.UI.Rendering.Conversation;

public sealed class ChatsAdapter : IConversationAdapter
{
    private readonly MessageRenderPipeline _pipeline;
    private readonly Func<string, CancellationToken, Task>? _sendHandler;
    private readonly ConversationLogStorage? _storage;
    private readonly ConversationArtifactStorage? _artifactStorage;

    public ChatsAdapter(
        MessageRenderPipeline? pipeline = null,
        Func<string, CancellationToken, Task>? sendHandler = null,
        ConversationLogStorage? storage = null,
        ConversationArtifactStorage? artifactStorage = null)
    {
        _pipeline = pipeline ?? new MessageRenderPipeline();
        _sendHandler = sendHandler;
        _storage = storage;
        _artifactStorage = artifactStorage;
    }

    public ObservableCollection<ConversationItemViewModel> Items { get; } = new();

    public bool PersistenceEnabled { get; set; } = true;

    public ConversationCapabilities Capabilities { get; } = new(
        CanSend: true,
        CanCancel: true,
        CanRetry: true,
        SupportsStreaming: true,
        ShowAvatars: false,
        ShowRoleBadges: true);

    public Task SendAsync(string text, CancellationToken cancellationToken = default)
    {
        return _sendHandler?.Invoke(text, cancellationToken) ?? Task.CompletedTask;
    }

    public Task CancelAsync(string itemId, CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task RetryAsync(string itemId, CancellationToken cancellationToken = default) => Task.CompletedTask;

    public bool HasPersistedItems()
    {
        return _storage?.HasEntries() ?? false;
    }

    public async Task<int> RestorePersistedAsync(bool replaceExisting = true)
    {
        if (_storage is null)
        {
            return 0;
        }

        var snapshots = _storage.LoadLatest();
        if (snapshots.Count == 0)
        {
            return 0;
        }

        if (replaceExisting)
        {
            Items.Clear();
        }

        foreach (var snapshot in snapshots)
        {
            if (!Enum.TryParse<ConversationItemKind>(snapshot.Kind, out var kind))
            {
                kind = ConversationItemKind.System;
            }

            var item = new ConversationItemViewModel(
                snapshot.MessageId,
                kind,
                snapshot.Role,
                snapshot.Text,
                snapshot.Timestamp,
                snapshot.IsStreaming,
                snapshot.IsStreaming ? MessageRenderState.Streaming : MessageRenderState.Final,
                snapshot.Metadata);
            Items.Add(item);
            await _pipeline.RenderAsync(item);
        }

        return snapshots.Count;
    }

    public async Task<int> LoadSnapshotsAsync(
        IReadOnlyList<ConversationLogSnapshot> snapshots,
        bool replaceExisting = true,
        bool prepend = false)
    {
        ArgumentNullException.ThrowIfNull(snapshots);

        if (replaceExisting)
        {
            Items.Clear();
        }

        var insertIndex = 0;
        foreach (var snapshot in snapshots)
        {
            if (!Enum.TryParse<ConversationItemKind>(snapshot.Kind, out var kind))
            {
                kind = ConversationItemKind.System;
            }

            var item = new ConversationItemViewModel(
                snapshot.MessageId,
                kind,
                snapshot.Role,
                snapshot.Text,
                snapshot.Timestamp,
                snapshot.IsStreaming,
                snapshot.IsStreaming ? MessageRenderState.Streaming : MessageRenderState.Final,
                snapshot.Metadata);
            if (prepend)
            {
                Items.Insert(insertIndex++, item);
            }
            else
            {
                Items.Add(item);
            }

            await _pipeline.RenderAsync(item);
        }

        return snapshots.Count;
    }

    public async Task<ConversationItemViewModel> AddMessageAsync(
        ConversationItemKind kind,
        string authorLabel,
        string text,
        bool isStreaming = false,
        IReadOnlyDictionary<string, string>? metadata = null)
    {
        return await AddMessageCoreAsync(kind, authorLabel, text, isStreaming, metadata);
    }

    public async Task<ConversationItemViewModel> AddLogAsync(
        string authorLabel,
        string fullText,
        string? preview = null,
        IReadOnlyDictionary<string, string>? metadata = null)
    {
        if (_artifactStorage is null)
        {
            throw new InvalidOperationException("Artifact storage is not configured for chat log persistence.");
        }

        var reference = _artifactStorage.SaveLog(fullText, preview);
        var itemMetadata = ConversationArtifactStorage.BuildReferenceMetadata("log", reference, metadata);
        return await AddMessageCoreAsync(ConversationItemKind.Log, authorLabel, reference.Preview, isStreaming: false, itemMetadata);
    }

    public async Task<ConversationItemViewModel> AddArtifactAsync(
        string authorLabel,
        string label,
        string fullText,
        string extension,
        string? preview = null,
        IReadOnlyDictionary<string, string>? metadata = null)
    {
        if (_artifactStorage is null)
        {
            throw new InvalidOperationException("Artifact storage is not configured for chat artifact persistence.");
        }

        var reference = _artifactStorage.SaveArtifact(label, fullText, extension, preview);
        var itemMetadata = ConversationArtifactStorage.BuildReferenceMetadata("artifact", reference, metadata);
        return await AddMessageCoreAsync(ConversationItemKind.Artifact, authorLabel, reference.Preview, isStreaming: false, itemMetadata);
    }

    public async Task<ConversationItemViewModel> AddArtifactReferenceAsync(
        string authorLabel,
        ConversationArtifactReference reference,
        IReadOnlyDictionary<string, string>? metadata = null)
    {
        ArgumentNullException.ThrowIfNull(reference);
        var itemMetadata = ConversationArtifactStorage.BuildReferenceMetadata("artifact", reference, metadata);
        return await AddMessageCoreAsync(ConversationItemKind.Artifact, authorLabel, reference.Preview, isStreaming: false, itemMetadata);
    }

    public Task AppendStreamingAsync(ConversationItemViewModel item, string chunk)
    {
        return PersistAfterAsync(item, () => _pipeline.AppendStreamingAsync(item, chunk), "update", advanceRevision: true);
    }

    public Task CompleteStreamingAsync(ConversationItemViewModel item, string? authoritativeText = null)
    {
        return PersistAfterAsync(item, () => _pipeline.CompleteStreamingAsync(item, authoritativeText), "final", advanceRevision: true);
    }

    private async Task PersistAfterAsync(ConversationItemViewModel item, Func<Task> renderAction, string eventType, bool advanceRevision = false)
    {
        if (advanceRevision)
        {
            item.AdvanceRevision();
        }

        await renderAction();
        PersistSnapshot(item, eventType);
    }

    private void PersistSnapshot(ConversationItemViewModel item, string eventType = "append")
    {
        if (_storage is null)
        {
            return;
        }

        if (!PersistenceEnabled)
        {
            return;
        }

        _storage.UpsertLatest(
            new ConversationLogSnapshot(
                item.Id,
                item.Timestamp,
                item.AuthorLabel,
                item.Kind.ToString(),
                item.Text,
                item.Text,
                StepId: null,
                Phase: null,
                Attachments: ConversationArtifactStorage.BuildAttachments(item.Metadata),
                Source: "chats",
                Adapter: "chats",
                item.IsStreaming,
                Metadata: item.Metadata),
            eventType);
    }

    private async Task<ConversationItemViewModel> AddMessageCoreAsync(
        ConversationItemKind kind,
        string authorLabel,
        string text,
        bool isStreaming,
        IReadOnlyDictionary<string, string>? metadata)
    {
        var item = new ConversationItemViewModel(
            Guid.NewGuid().ToString("N"),
            kind,
            authorLabel,
            text,
            DateTimeOffset.Now,
            isStreaming,
            isStreaming ? MessageRenderState.Streaming : MessageRenderState.Raw,
            metadata);
        Items.Add(item);
        await _pipeline.RenderAsync(item);
        PersistSnapshot(item);
        return item;
    }
}
