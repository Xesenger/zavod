using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace zavod.Persistence;

public static class ProjectRootResolver
{
    public static string Resolve()
    {
        var seeds = new[]
        {
            Environment.CurrentDirectory,
            AppContext.BaseDirectory,
            Path.GetDirectoryName(Environment.ProcessPath ?? string.Empty) ?? string.Empty
        }
        .Where(static path => !string.IsNullOrWhiteSpace(path))
        .Select(Path.GetFullPath)
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .ToArray();

        foreach (var seed in seeds)
        {
            foreach (var candidate in EnumerateSelfAndParents(seed))
            {
                if (IsRepositoryRoot(candidate))
                {
                    return candidate;
                }
            }
        }

        throw new InvalidOperationException(
            $"Unable to resolve repository project root from runtime directories: {string.Join(", ", seeds)}");
    }

    private static bool IsRepositoryRoot(string path)
    {
        return File.Exists(Path.Combine(path, "zavod.csproj")) &&
               File.Exists(Path.Combine(path, "App.xaml")) &&
               Directory.Exists(Path.Combine(path, "docs", "zavod", "canon"));
    }

    private static IEnumerable<string> EnumerateSelfAndParents(string path)
    {
        var current = new DirectoryInfo(path);
        while (current is not null)
        {
            yield return current.FullName;
            current = current.Parent;
        }
    }
}
