using System;
using System.Collections.Generic;
using System.Linq;
using zavod.UI.Modes.Chats;
using zavod.UI.Text;

namespace zavod.UI.Rendering.Conversation;

internal static class ConversationWebProjection
{
    public static ConversationWebItem[] BuildMessages(IEnumerable<ConversationItemViewModel> items)
    {
        ArgumentNullException.ThrowIfNull(items);

        return items
            .Select(item => new ConversationWebItem(
                item.Id,
                item.Revision,
                MapRole(item.Kind),
                MapKind(item.Kind),
                MapFormat(item.Kind),
                item.Text,
                item.IsStreaming ? "streaming" : "final",
                ResolveReferenceLabel(item),
                ResolveReferenceId(item)))
            .ToArray();
    }

    public static string BuildTitleFromFirstUserMessage(IEnumerable<ConversationItemViewModel> items, string fallbackTitle)
    {
        ArgumentNullException.ThrowIfNull(items);
        ArgumentException.ThrowIfNullOrWhiteSpace(fallbackTitle);

        foreach (var item in items)
        {
            if (item.Kind != ConversationItemKind.User)
            {
                continue;
            }

            var text = item.Text?.Trim();
            if (string.IsNullOrWhiteSpace(text))
            {
                continue;
            }

            var singleLine = text.Replace("\r", " ").Replace("\n", " ").Trim();
            if (singleLine.Length <= 34)
            {
                return singleLine;
            }

            return $"{singleLine[..31].TrimEnd()}...";
        }

        return fallbackTitle.Trim();
    }

    private static string MapRole(ConversationItemKind kind)
    {
        return kind switch
        {
            ConversationItemKind.User => "user",
            ConversationItemKind.System or ConversationItemKind.Status or ConversationItemKind.Log or ConversationItemKind.Artifact => "system",
            _ => "assistant"
        };
    }

    private static string MapFormat(ConversationItemKind kind)
    {
        return kind switch
        {
            ConversationItemKind.User => "plain",
            ConversationItemKind.Status or ConversationItemKind.Log or ConversationItemKind.Artifact => "plain",
            _ => "markdown"
        };
    }

    private static string MapKind(ConversationItemKind kind)
    {
        return kind switch
        {
            ConversationItemKind.Status => "status",
            ConversationItemKind.Log => "log",
            ConversationItemKind.Artifact => "artifact",
            _ => "message"
        };
    }

    private static string? ResolveReferenceLabel(ConversationItemViewModel item)
    {
        return item.Metadata is not null && item.Metadata.TryGetValue("reference-label", out var label) && !string.IsNullOrWhiteSpace(label)
            ? label
            : item.Kind switch
            {
                ConversationItemKind.Log => AppText.Current.Get("conversation.default_log_label"),
                ConversationItemKind.Artifact => AppText.Current.Get("conversation.default_artifact_label"),
                _ => null
            };
    }

    private static string? ResolveReferenceId(ConversationItemViewModel item)
    {
        return item.Metadata is not null && item.Metadata.TryGetValue("reference-id", out var referenceId) && !string.IsNullOrWhiteSpace(referenceId)
            ? referenceId
            : null;
    }
}
