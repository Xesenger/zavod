using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace zavod.Prompting;

public static class PromptRoleCoreCatalog
{
    private static readonly Lazy<IReadOnlyDictionary<PromptRole, PromptRoleCore>> Cores = new(LoadCores);

    public static PromptRoleCore Get(PromptRole role)
    {
        if (!Cores.Value.TryGetValue(role, out var core))
        {
            throw new ArgumentOutOfRangeException(nameof(role), role, "Unknown prompt role.");
        }

        return core;
    }

    private static IReadOnlyDictionary<PromptRole, PromptRoleCore> LoadCores()
    {
        return new Dictionary<PromptRole, PromptRoleCore>
        {
            [PromptRole.Worker] = ParseRoleCore("worker.system.md"),
            [PromptRole.ShiftLead] = ParseRoleCore("lead.system.md"),
            [PromptRole.Qc] = ParseRoleCore("qc.system.md"),
            [PromptRole.SeniorSpecialist] = ParseRoleCore("senior.system.md")
        };
    }

    private static PromptRoleCore ParseRoleCore(string fileName)
    {
        var path = PromptSystemCatalog.ResolvePromptPath(fileName);
        var lines = File.ReadAllLines(path);
        var role = ReadScalar(lines, "Role:", path);
        var stack = ReadScalar(lines, "Stack:", path);
        var style = ReadScalar(lines, "Style:", path);
        var rules = ReadSection(lines, "[Rules]", path);
        var responseContract = ReadSection(lines, "[Response Contract]", path);
        var constraints = ReadSection(lines, "[Constraints]", path);

        return new PromptRoleCore(role, stack, style, rules, responseContract, constraints);
    }

    private static string ReadScalar(IReadOnlyList<string> lines, string prefix, string path)
    {
        var line = lines.FirstOrDefault(candidate => candidate.StartsWith(prefix, StringComparison.Ordinal));
        if (string.IsNullOrWhiteSpace(line))
        {
            throw new InvalidOperationException($"Prompt system file '{path}' must contain '{prefix}'.");
        }

        var value = line[prefix.Length..].Trim();
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException($"Prompt system file '{path}' must contain non-empty value for '{prefix}'.");
        }

        return value;
    }

    private static IReadOnlyList<string> ReadSection(IReadOnlyList<string> lines, string sectionHeader, string path)
    {
        var startIndex = lines
            .Select((line, index) => new { line, index })
            .FirstOrDefault(entry => string.Equals(entry.line.Trim(), sectionHeader, StringComparison.Ordinal))
            ?.index ?? -1;
        if (startIndex < 0)
        {
            throw new InvalidOperationException($"Prompt system file '{path}' must contain section '{sectionHeader}'.");
        }

        var items = new List<string>();
        for (var index = startIndex + 1; index < lines.Count; index++)
        {
            var line = lines[index].Trim();
            if (line.Length == 0)
            {
                continue;
            }

            if (line.StartsWith("[", StringComparison.Ordinal) && line.EndsWith("]", StringComparison.Ordinal))
            {
                break;
            }

            if (!line.StartsWith("- ", StringComparison.Ordinal))
            {
                throw new InvalidOperationException($"Prompt system file '{path}' has invalid item in section '{sectionHeader}': '{lines[index]}'.");
            }

            items.Add(line[2..].Trim());
        }

        if (items.Count == 0)
        {
            throw new InvalidOperationException($"Prompt system file '{path}' must contain at least one item in section '{sectionHeader}'.");
        }

        return items;
    }
}
