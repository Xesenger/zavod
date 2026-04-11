using System;
using System.Collections.Generic;
using System.Linq;

namespace zavod.Acceptance;

public static class TouchedScopeBuilder
{
    public static TouchedScope Build(IEnumerable<string> relativePaths)
    {
        ArgumentNullException.ThrowIfNull(relativePaths);

        var normalized = relativePaths
            .Where(static path => !string.IsNullOrWhiteSpace(path))
            .Select(static path => path.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(static path => path, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return new TouchedScope(normalized);
    }
}
