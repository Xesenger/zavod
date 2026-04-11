using System;
using System.IO;

namespace zavod.Execution;

internal static class BundledToolLocator
{
    public static string ResolveOrFallback(string preferredRelativePath, string fallbackCommand)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(preferredRelativePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(fallbackCommand);

        var direct = Path.Combine(AppContext.BaseDirectory, preferredRelativePath);
        if (File.Exists(direct))
        {
            return direct;
        }

        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            var candidate = Path.Combine(current.FullName, preferredRelativePath);
            if (File.Exists(candidate))
            {
                return candidate;
            }

            current = current.Parent;
        }

        return fallbackCommand;
    }
}
