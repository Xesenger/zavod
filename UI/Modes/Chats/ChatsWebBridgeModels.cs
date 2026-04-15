using System;
using System.Collections.Generic;
using System.Text.Json;

namespace zavod.UI.Modes.Chats;

public sealed record ChatsWebEnvelope<TPayload>(string Type, TPayload Payload);

public sealed record ChatsWebIntentMessage(string Type, JsonElement Payload);

public sealed record ChatsWebStateSnapshot(
    string Mode,
    string? ActiveChatId,
    bool IsEmpty,
    bool HasOlder,
    int WindowStartSeq,
    int WindowEndSeq,
    IReadOnlyList<ChatsWebChatSummary> Chats,
    IReadOnlyList<ConversationWebItem> Messages,
    ChatsWebEmptyState EmptyState,
    ChatsWebComposerState Composer,
    ChatsWebLocalizedText Text);

public sealed record ChatsWebChatSummary(string Id, string Title);

public sealed record ConversationWebItem(
    string Id,
    int Revision,
    string Role,
    string Kind,
    string Format,
    string Text,
    string StreamState,
    string? Label,
    string? ReferenceId);

public sealed record ChatsWebEmptyState(string Headline, string Subtitle);

public sealed record ChatsWebComposerAttachment(
    string Id,
    string Label,
    string Kind,
    string Detail);

public sealed record ChatsWebComposerState(
    string Placeholder,
    IReadOnlyList<ChatsWebComposerAttachment>? PendingAttachments = null);

public sealed record ChatsWebLocalizedText(
    string SidebarShow,
    string SidebarHide,
    string NewChat,
    string AddAttachmentTitle,
    string SendTitle,
    string AddFile,
    string AddImage,
    string AddNote,
    string LoadOlder,
    string LoadingOlder,
    string DefaultArtifactLabel,
    string DefaultLogLabel);

public sealed class ChatsWebIntentReceivedEventArgs : EventArgs
{
    public ChatsWebIntentReceivedEventArgs(ChatsWebIntentMessage message)
    {
        Message = message ?? throw new ArgumentNullException(nameof(message));
    }

    public ChatsWebIntentMessage Message { get; }
}
