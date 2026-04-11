using System;

namespace zavod.Presentation.Conversation;

public static class ConversationActionVisibility
{
    public static bool ShouldShow(ConversationItemViewModel item)
    {
        ArgumentNullException.ThrowIfNull(item);

        return item.Kind != ConversationItemKind.User
            && item.Metadata is not null
            && item.Metadata.TryGetValue("mode", out var mode)
            && string.Equals(mode, "project", StringComparison.OrdinalIgnoreCase)
            && item.MetadataActions.Count > 0;
    }
}
