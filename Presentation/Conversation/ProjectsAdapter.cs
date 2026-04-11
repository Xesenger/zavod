using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;
using zavod.Persistence;

namespace zavod.Presentation.Conversation;

public sealed class ProjectsAdapter : IConversationAdapter
{
    private readonly MessageRenderPipeline _pipeline;
    private readonly ConversationLogStorage? _storage;

    public ProjectsAdapter(MessageRenderPipeline? pipeline = null, ConversationLogStorage? storage = null)
    {
        _pipeline = pipeline ?? new MessageRenderPipeline();
        _storage = storage;
    }

    public ObservableCollection<ConversationItemViewModel> Items { get; } = new();

    public bool PersistenceEnabled { get; set; } = true;

    public ConversationCapabilities Capabilities { get; } = new(
        CanSend: false,
        CanCancel: false,
        CanRetry: false,
        SupportsStreaming: true,
        ShowAvatars: false,
        ShowRoleBadges: true);

    public Task SendAsync(string text, CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task CancelAsync(string itemId, CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task RetryAsync(string itemId, CancellationToken cancellationToken = default) => Task.CompletedTask;

    public void Clear()
    {
        Items.Clear();
    }

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

    public async Task<ConversationItemViewModel> AddMessageAsync(
        ConversationItemKind kind,
        string authorLabel,
        string text,
        bool isStreaming = false,
        IReadOnlyDictionary<string, string>? metadata = null,
        IReadOnlyList<ConversationMetadataAction>? metadataActions = null)
    {
        var item = new ConversationItemViewModel(
            Guid.NewGuid().ToString("N"),
            kind,
            authorLabel,
            text,
            DateTimeOffset.Now,
            isStreaming,
            isStreaming ? MessageRenderState.Streaming : MessageRenderState.Raw,
            metadata,
            metadataActions);
        Items.Add(item);
        await _pipeline.RenderAsync(item);
        PersistSnapshot(item);
        return item;
    }

    public Task AppendStreamingAsync(ConversationItemViewModel item, string chunk)
    {
        return PersistAfterAsync(item, () => _pipeline.AppendStreamingAsync(item, chunk), "update");
    }

    public Task CompleteStreamingAsync(ConversationItemViewModel item, string? authoritativeText = null)
    {
        return PersistAfterAsync(item, () => _pipeline.CompleteStreamingAsync(item, authoritativeText), "final");
    }

    private async Task PersistAfterAsync(ConversationItemViewModel item, Func<Task> renderAction, string eventType)
    {
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

        _storage.Append(BuildSnapshot(item), eventType);
    }

    private static ConversationLogSnapshot BuildSnapshot(ConversationItemViewModel item)
    {
        var metadata = item.Metadata is null
            ? null
            : new Dictionary<string, string>(item.Metadata, StringComparer.Ordinal);
        var phase = metadata is not null && metadata.TryGetValue("phase", out var phaseValue) ? phaseValue : null;
        var stepId = metadata is not null && metadata.TryGetValue("step-id", out var stepIdValue) ? stepIdValue : null;
        var attachments = metadata is not null && metadata.TryGetValue("file-path", out var filePath) && !string.IsNullOrWhiteSpace(filePath)
            ? new[] { filePath }
            : Array.Empty<string>();

        return new ConversationLogSnapshot(
            item.Id,
            item.Timestamp,
            item.AuthorLabel,
            item.Kind.ToString(),
            item.Text,
            item.Text,
            stepId,
            phase,
            attachments,
            Source: "projects",
            Adapter: "projects",
            item.IsStreaming,
            metadata);
    }
}
