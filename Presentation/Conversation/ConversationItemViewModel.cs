using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using zavod.Presentation.Conversation.Markdown;

namespace zavod.Presentation.Conversation;

public sealed class ConversationItemViewModel : INotifyPropertyChanged
{
    private string _text;
    private MessageRenderState _renderState;
    private bool _isStreaming;
    private IReadOnlyList<MarkdownBlock> _blocks;
    private IReadOnlyList<ConversationMetadataAction> _metadataActions;

    public ConversationItemViewModel(
        string id,
        ConversationItemKind kind,
        string authorLabel,
        string text,
        DateTimeOffset timestamp,
        bool isStreaming = false,
        MessageRenderState renderState = MessageRenderState.Raw,
        IReadOnlyDictionary<string, string>? metadata = null,
        IReadOnlyList<ConversationMetadataAction>? metadataActions = null)
    {
        Id = id ?? throw new ArgumentNullException(nameof(id));
        Kind = kind;
        AuthorLabel = authorLabel ?? string.Empty;
        _text = text ?? string.Empty;
        Timestamp = timestamp;
        _isStreaming = isStreaming;
        _renderState = renderState;
        Metadata = metadata;
        _blocks = Array.Empty<MarkdownBlock>();
        _metadataActions = metadataActions ?? Array.Empty<ConversationMetadataAction>();
    }

    public string Id { get; }

    public ConversationItemKind Kind { get; }

    public string AuthorLabel { get; }

    public DateTimeOffset Timestamp { get; }

    public IReadOnlyDictionary<string, string>? Metadata { get; }

    public IReadOnlyList<ConversationMetadataAction> MetadataActions
    {
        get => _metadataActions;
        set => SetField(ref _metadataActions, value);
    }

    public string Text
    {
        get => _text;
        set => SetField(ref _text, value);
    }

    public MessageRenderState RenderState
    {
        get => _renderState;
        set => SetField(ref _renderState, value);
    }

    public bool IsStreaming
    {
        get => _isStreaming;
        set => SetField(ref _isStreaming, value);
    }

    public IReadOnlyList<MarkdownBlock> Blocks
    {
        get => _blocks;
        set => SetField(ref _blocks, value);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return;
        }

        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
