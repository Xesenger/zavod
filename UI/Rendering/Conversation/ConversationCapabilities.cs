namespace zavod.UI.Rendering.Conversation;

public sealed record ConversationCapabilities(
    bool CanSend = true,
    bool CanCancel = false,
    bool CanRetry = false,
    bool SupportsStreaming = true,
    bool ShowAvatars = false,
    bool ShowRoleBadges = true);
