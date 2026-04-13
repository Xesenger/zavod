using System.Collections.Generic;
using System.Linq;

namespace zavod.UI.Rendering.Conversation;

public static class ConversationMetadataPresenter
{
    public static string BuildSummary(IReadOnlyDictionary<string, string>? metadata)
    {
        if (metadata is null || metadata.Count == 0)
        {
            return string.Empty;
        }

        return string.Join(" • ", metadata.Select(pair => $"{pair.Key}: {pair.Value}"));
    }
}
