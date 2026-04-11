using System;
using System.IO;

namespace zavod.Prompting;

internal static class PromptSystemCatalog
{
    public static string GetImportSystemPrompt()
    {
        var path = ResolvePromptPath("import.system.md");
        return File.ReadAllText(path).Trim();
    }

    internal static string ResolvePromptPath(string fileName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);

        var directPath = Path.Combine(AppContext.BaseDirectory, "app", "prompts", fileName);
        if (File.Exists(directPath))
        {
            return directPath;
        }

        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            var candidate = Path.Combine(current.FullName, "app", "prompts", fileName);
            if (File.Exists(candidate))
            {
                return candidate;
            }

            current = current.Parent;
        }

        throw new InvalidOperationException($"Prompt system file '{fileName}' was not found under 'app/prompts'.");
    }
}
