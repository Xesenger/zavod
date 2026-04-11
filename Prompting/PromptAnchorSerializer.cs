using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace zavod.Prompting;

public static class PromptAnchorSerializer
{
    public static IReadOnlyList<SerializedPromptAnchor> Serialize(IReadOnlyList<PromptAnchor> anchors)
    {
        ArgumentNullException.ThrowIfNull(anchors);

        return PromptAnchorCanonicalizer.Order(anchors)
            .Select(static anchor => new SerializedPromptAnchor(
                anchor.Id,
                anchor.Type.ToString().ToUpperInvariant(),
                anchor.Source,
                anchor.Value,
                anchor.Confidence,
                anchor.Scope,
                anchor.Reference))
            .ToArray();
    }

    public static string Render(IReadOnlyList<SerializedPromptAnchor> anchors)
    {
        ArgumentNullException.ThrowIfNull(anchors);

        var builder = new StringBuilder();

        foreach (var anchor in anchors)
        {
            builder.Append($"{anchor.Id}. [{anchor.Type} | {anchor.Source}] {anchor.Value}");

            if (!string.IsNullOrWhiteSpace(anchor.Scope))
            {
                builder.Append($" | scope={anchor.Scope}");
            }

            if (!string.IsNullOrWhiteSpace(anchor.Reference))
            {
                builder.Append($" | ref={anchor.Reference}");
            }

            if (anchor.Confidence is not null)
            {
                builder.Append($" | confidence={anchor.Confidence:0.###}");
            }

            builder.AppendLine();
        }

        return builder.ToString().TrimEnd();
    }
}
