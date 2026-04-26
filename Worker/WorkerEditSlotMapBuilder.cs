using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace zavod.Worker;

public static class WorkerEditSlotMapBuilder
{
    private const int DefaultMaxSlots = 80;
    private const int MaxFileBytes = 64000;
    private const int MaxFunctionsPerFile = 10;

    private static readonly HashSet<string> JsLikeExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".js", ".jsx", ".ts", ".tsx", ".mjs", ".cjs"
    };

    public static IReadOnlyList<WorkerEditSlot> Build(
        string projectRoot,
        IReadOnlyList<string>? anchorLines,
        int maxSlots = DefaultMaxSlots)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(projectRoot);
        if (anchorLines is null || anchorLines.Count == 0 || maxSlots <= 0)
        {
            return Array.Empty<WorkerEditSlot>();
        }

        var normalizedRoot = Path.GetFullPath(projectRoot);
        var paths = ExtractPaths(anchorLines);
        var slots = new List<WorkerEditSlot>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var relativePath in paths)
        {
            if (slots.Count >= maxSlots)
            {
                break;
            }

            if (!TryReadFile(normalizedRoot, relativePath, out var content))
            {
                continue;
            }

            AddSlot(slots, seen, relativePath, "file:top", "file", "start of existing file", maxSlots);
            AddSlot(slots, seen, relativePath, "file:end", "file", "end of existing file", maxSlots);

            var extension = Path.GetExtension(relativePath);
            if (string.Equals(extension, ".css", StringComparison.OrdinalIgnoreCase))
            {
                AddSlot(slots, seen, relativePath, "css:file:end", "css", "append CSS rule at stylesheet end", maxSlots);
                continue;
            }

            if (JsLikeExtensions.Contains(extension)
                && WorkerEditSlotResolver.TryResolveInsertionIndex(relativePath, content, "module:after_imports", out _, out _))
            {
                AddSlot(slots, seen, relativePath, "module:after_imports", "module", "after leading import block", maxSlots);
            }

            foreach (var name in WorkerEditSlotResolver.ExtractFunctionNames(relativePath, content).Take(MaxFunctionsPerFile))
            {
                AddSlot(slots, seen, relativePath, $"function:{name}:after_start", "function", $"after start of function {name}", maxSlots);
            }
        }

        return slots;
    }

    private static void AddSlot(
        List<WorkerEditSlot> slots,
        HashSet<string> seen,
        string path,
        string slotId,
        string kind,
        string reason,
        int maxSlots)
    {
        if (slots.Count >= maxSlots)
        {
            return;
        }

        var key = $"{path}\n{slotId}";
        if (seen.Add(key))
        {
            slots.Add(new WorkerEditSlot(ToPromptPath(path), slotId, kind, reason));
        }
    }

    private static IReadOnlyList<string> ExtractPaths(IReadOnlyList<string> anchorLines)
    {
        var paths = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var line in anchorLines)
        {
            AddExtractedPath(paths, seen, TryExtractSnippetPath(line));
        }

        foreach (var line in anchorLines)
        {
            AddExtractedPath(paths, seen, TryExtractListPath(line));
        }

        return paths;
    }

    private static void AddExtractedPath(List<string> paths, HashSet<string> seen, string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        path = path.Replace('/', Path.DirectorySeparatorChar);
        if (Path.IsPathRooted(path) || path.StartsWith("..", StringComparison.Ordinal))
        {
            return;
        }

        if (seen.Add(path))
        {
            paths.Add(path);
        }
    }

    private static string? TryExtractSnippetPath(string line)
    {
        var trimmed = line.Trim();
        var snippetMatch = Regex.Match(trimmed, @"^--- snippet path:\s+(?<path>.+?)\s+\(first\s+\d+\s+lines\)\s+---$", RegexOptions.IgnoreCase);
        if (snippetMatch.Success)
        {
            return snippetMatch.Groups["path"].Value.Trim();
        }

        return null;
    }

    private static string? TryExtractListPath(string line)
    {
        var trimmed = line.Trim();
        if (!trimmed.StartsWith("- ", StringComparison.Ordinal))
        {
            return null;
        }

        var candidate = trimmed[2..].Trim();
        candidate = Regex.Replace(candidate, @"\s+\(entry\)$", string.Empty, RegexOptions.IgnoreCase).Trim();
        if (candidate.Contains(':', StringComparison.Ordinal) || candidate.StartsWith("(", StringComparison.Ordinal))
        {
            return null;
        }

        var extension = Path.GetExtension(candidate);
        return string.IsNullOrWhiteSpace(extension) ? null : candidate;
    }

    private static bool TryReadFile(string projectRoot, string relativePath, out string content)
    {
        content = string.Empty;
        var absolutePath = Path.GetFullPath(Path.Combine(projectRoot, relativePath));
        if (!IsInsideDirectory(absolutePath, projectRoot) || !File.Exists(absolutePath))
        {
            return false;
        }

        try
        {
            var info = new FileInfo(absolutePath);
            if (info.Length > MaxFileBytes)
            {
                return false;
            }

            content = File.ReadAllText(absolutePath);
            return true;
        }
        catch (IOException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
    }

    private static bool IsInsideDirectory(string candidatePath, string rootPath)
    {
        var normalizedRoot = Path.GetFullPath(rootPath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            + Path.DirectorySeparatorChar;
        var normalizedCandidate = Path.GetFullPath(candidatePath);
        return normalizedCandidate.StartsWith(normalizedRoot, StringComparison.OrdinalIgnoreCase);
    }

    private static string ToPromptPath(string path)
    {
        return path
            .Replace(Path.DirectorySeparatorChar, '/')
            .Replace(Path.AltDirectorySeparatorChar, '/');
    }
}

internal static class WorkerEditSlotResolver
{
    private static readonly HashSet<string> JsLikeExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".js", ".jsx", ".ts", ".tsx", ".mjs", ".cjs"
    };

    public static bool TryResolveInsertionIndex(
        string relativePath,
        string content,
        string? slotId,
        out int insertionIndex,
        out string? reason)
    {
        insertionIndex = 0;
        reason = null;

        if (string.IsNullOrWhiteSpace(slotId))
        {
            reason = "insert_at_slot requires slotId";
            return false;
        }

        var normalizedSlot = slotId.Trim();
        if (string.Equals(normalizedSlot, "file:top", StringComparison.OrdinalIgnoreCase))
        {
            insertionIndex = 0;
            return true;
        }

        if (string.Equals(normalizedSlot, "file:end", StringComparison.OrdinalIgnoreCase))
        {
            insertionIndex = content.Length;
            return true;
        }

        if (string.Equals(normalizedSlot, "css:file:end", StringComparison.OrdinalIgnoreCase))
        {
            if (!string.Equals(Path.GetExtension(relativePath), ".css", StringComparison.OrdinalIgnoreCase))
            {
                reason = "slot not supported for file type";
                return false;
            }

            insertionIndex = content.Length;
            return true;
        }

        if (string.Equals(normalizedSlot, "module:after_imports", StringComparison.OrdinalIgnoreCase))
        {
            if (!JsLikeExtensions.Contains(Path.GetExtension(relativePath)))
            {
                reason = "slot not supported for file type";
                return false;
            }

            return TryResolveAfterImports(content, out insertionIndex, out reason);
        }

        if (normalizedSlot.StartsWith("function:", StringComparison.OrdinalIgnoreCase)
            && normalizedSlot.EndsWith(":after_start", StringComparison.OrdinalIgnoreCase))
        {
            var functionName = normalizedSlot["function:".Length..^":after_start".Length];
            return TryResolveAfterFunctionStart(relativePath, content, functionName, out insertionIndex, out reason);
        }

        reason = "slot not found";
        return false;
    }

    public static IReadOnlyList<string> ExtractFunctionNames(string relativePath, string content)
    {
        var names = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var pattern in BuildFunctionPatterns())
        {
            foreach (Match match in pattern.Matches(content))
            {
                var name = match.Groups["name"].Value;
                if (!string.IsNullOrWhiteSpace(name) && seen.Add(name))
                {
                    names.Add(name);
                }
            }
        }

        return names;
    }

    private static bool TryResolveAfterImports(string content, out int insertionIndex, out string? reason)
    {
        insertionIndex = 0;
        reason = null;
        var position = 0;
        var sawImport = false;

        foreach (var line in EnumerateLinesWithTerminators(content))
        {
            var trimmed = line.Text.Trim();
            if (IsJsImportLike(trimmed))
            {
                sawImport = true;
                position += line.Text.Length;
                insertionIndex = position;
                continue;
            }

            if (sawImport && string.IsNullOrWhiteSpace(trimmed))
            {
                position += line.Text.Length;
                insertionIndex = position;
                continue;
            }

            if (!sawImport && (string.IsNullOrWhiteSpace(trimmed) || trimmed.StartsWith("//", StringComparison.Ordinal)))
            {
                position += line.Text.Length;
                continue;
            }

            break;
        }

        if (!sawImport)
        {
            reason = "slot not found";
            return false;
        }

        return true;
    }

    private static bool TryResolveAfterFunctionStart(
        string relativePath,
        string content,
        string functionName,
        out int insertionIndex,
        out string? reason)
    {
        insertionIndex = 0;
        reason = null;

        if (string.IsNullOrWhiteSpace(functionName))
        {
            reason = "slot not found";
            return false;
        }

        var matches = new List<Match>();
        foreach (var pattern in BuildFunctionPatterns(functionName))
        {
            matches.AddRange(pattern.Matches(content).Cast<Match>());
        }

        if (matches.Count == 0)
        {
            reason = "slot not found";
            return false;
        }

        if (matches.Count > 1)
        {
            reason = "slot ambiguous";
            return false;
        }

        insertionIndex = matches[0].Index + matches[0].Length;
        return true;
    }

    private static bool IsJsImportLike(string trimmedLine)
    {
        return trimmedLine.StartsWith("import ", StringComparison.Ordinal)
            || trimmedLine.StartsWith("import{", StringComparison.Ordinal)
            || trimmedLine.StartsWith("export ", StringComparison.Ordinal)
            || trimmedLine.StartsWith("const ", StringComparison.Ordinal) && trimmedLine.Contains("require(", StringComparison.Ordinal)
            || trimmedLine.StartsWith("let ", StringComparison.Ordinal) && trimmedLine.Contains("require(", StringComparison.Ordinal)
            || trimmedLine.StartsWith("var ", StringComparison.Ordinal) && trimmedLine.Contains("require(", StringComparison.Ordinal)
            || trimmedLine.StartsWith("require(", StringComparison.Ordinal);
    }

    private static IEnumerable<Regex> BuildFunctionPatterns(string? exactName = null)
    {
        var name = exactName is null ? @"(?<name>[A-Za-z_][A-Za-z0-9_]*)" : Regex.Escape(exactName);
        var namedGroup = exactName is null ? name : $"(?<name>{name})";
        var options = RegexOptions.Multiline | RegexOptions.CultureInvariant;

        yield return new Regex($@"^\s*(?:export\s+)?(?:async\s+)?function\s+{namedGroup}\s*\([^)]*\)\s*\{{[^\r\n]*(?:\r?\n)?", options);
        yield return new Regex($@"^\s*(?:export\s+)?const\s+{namedGroup}\s*=\s*(?:async\s*)?(?:\([^)]*\)|[A-Za-z_][A-Za-z0-9_]*)\s*=>\s*\{{[^\r\n]*(?:\r?\n)?", options);
        yield return new Regex($@"^\s*def\s+{namedGroup}\s*\([^)]*\)\s*:[^\r\n]*(?:\r?\n)?", options);
        yield return new Regex($@"^\s*(?:(?:public|private|protected|internal|static|async|virtual|override|sealed|partial)\s+)+[A-Za-z_][A-Za-z0-9_<>,\s\?\[\]]*\s+{namedGroup}\s*\([^)]*\)\s*(?:\r?\n\s*)?\{{[^\r\n]*(?:\r?\n)?", options);
    }

    private static IEnumerable<(string Text, int Start)> EnumerateLinesWithTerminators(string content)
    {
        var index = 0;
        while (index < content.Length)
        {
            var start = index;
            while (index < content.Length && content[index] != '\n')
            {
                index++;
            }

            if (index < content.Length)
            {
                index++;
            }

            yield return (content[start..index], start);
        }
    }
}
