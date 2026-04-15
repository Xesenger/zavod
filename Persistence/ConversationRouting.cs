using System;

namespace zavod.Persistence;

public static class ConversationRouting
{
    public static string CreateConversationId() => Guid.NewGuid().ToString("N");

    public static string GetProjectConversationId(string projectId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(projectId);
        return $"project-{projectId.Trim()}";
    }
}
