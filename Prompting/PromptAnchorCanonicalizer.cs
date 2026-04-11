using System;
using System.Collections.Generic;
using System.Linq;

namespace zavod.Prompting;

public static class PromptAnchorCanonicalizer
{
    public static IReadOnlyList<PromptAnchor> Order(IReadOnlyList<PromptAnchor> anchors)
    {
        ArgumentNullException.ThrowIfNull(anchors);

        return anchors
            .OrderBy(static anchor => GetAnchorTypeOrder(anchor.Type))
            .ThenBy(static anchor => anchor.Id, StringComparer.OrdinalIgnoreCase)
            .ThenBy(static anchor => anchor.Source, StringComparer.OrdinalIgnoreCase)
            .ThenBy(static anchor => anchor.Value, StringComparer.Ordinal)
            .ThenBy(static anchor => anchor.Scope, StringComparer.OrdinalIgnoreCase)
            .ThenBy(static anchor => anchor.Reference, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public static int GetAnchorTypeOrder(PromptAnchorType type)
    {
        return type switch
        {
            PromptAnchorType.Task => 0,
            PromptAnchorType.Truth => 1,
            PromptAnchorType.Decision => 2,
            PromptAnchorType.Code => 3,
            PromptAnchorType.State => 4,
            PromptAnchorType.Artifact => 5,
            PromptAnchorType.Constraint => 6,
            _ => int.MaxValue
        };
    }
}
