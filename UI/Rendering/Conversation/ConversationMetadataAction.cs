using System;
using System.Threading.Tasks;

namespace zavod.UI.Rendering.Conversation;

public sealed class ConversationMetadataAction
{
    private readonly Func<ConversationItemViewModel, Task>? _handler;

    public ConversationMetadataAction(
        string id,
        string type,
        string label,
        bool isPrimary = false,
        bool isEnabled = true,
        Func<ConversationItemViewModel, Task>? handler = null)
    {
        Id = id ?? throw new ArgumentNullException(nameof(id));
        Type = type ?? throw new ArgumentNullException(nameof(type));
        Label = label ?? throw new ArgumentNullException(nameof(label));
        IsPrimary = isPrimary;
        IsEnabled = isEnabled;
        _handler = handler;
    }

    public string Id { get; }

    public string Type { get; }

    public string Label { get; }

    public bool IsPrimary { get; }

    public bool IsEnabled { get; }

    public Task InvokeAsync(ConversationItemViewModel item)
    {
        return _handler?.Invoke(item) ?? Task.CompletedTask;
    }
}
