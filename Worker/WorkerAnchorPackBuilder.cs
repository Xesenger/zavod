using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using zavod.Workspace;

namespace zavod.Worker;

/// <summary>
/// Builds a compact code-anchor pack for Worker grounding. Gives the Worker a
/// real file tree (relative paths + entry markers) instead of only the project
/// preview doc, which was the recurring root-cause of "refused: missing code
/// anchors" QC decisions.
/// </summary>
public static class WorkerAnchorPackBuilder
{
    private const int DefaultMaxEntries = 80;

    private static readonly HashSet<string> SourceExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".asm", ".astro", ".c", ".cjs", ".cc", ".cpp", ".cs", ".go", ".h", ".hpp",
        ".inc", ".java", ".js", ".jsx", ".kt", ".lua", ".mjs", ".php", ".ps1",
        ".py", ".rb", ".rs", ".s", ".sh", ".sql", ".svelte", ".swift", ".ts",
        ".tsx", ".vue", ".html", ".css"
    };

    private static readonly HashSet<string> BuildFileNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "package.json", "tsconfig.json", "Cargo.toml", "go.mod", "pom.xml",
        "pyproject.toml", "requirements.txt", "Gemfile", "composer.json",
        "Dockerfile", "Makefile", "vite.config.js", "webpack.config.js",
        "next.config.js", "CMakeLists.txt"
    };

    private const int DefaultSnippetFileCount = 6;
    private const int DefaultSnippetMaxLines = 80;
    private const int DefaultSnippetMaxBytes = 16384;

    // Semantic expansion: when the user says "FPS counter" they mean HUD/render
    // territory even though "hud" is not in their task phrase. Worker refuses
    // with "missing HUD anchors" because the scorer doesn't bridge the gap.
    // These seeds widen the match so hud.js/render-related files get picked
    // even when the task phrasing stays user-facing.
    private static readonly Dictionary<string, string[]> KeywordExpansions = new(StringComparer.OrdinalIgnoreCase)
    {
        ["fps"] = new[] { "hud", "frame", "loop", "timer", "stats", "debug" },
        ["counter"] = new[] { "hud", "stats", "display", "debug", "overlay" },
        ["display"] = new[] { "hud", "render", "overlay" },
        ["hud"] = new[] { "render", "ui", "overlay", "css", "style" },
        ["menu"] = new[] { "ui", "overlay", "css", "style" },
        ["score"] = new[] { "hud", "stats", "display" },
        ["timer"] = new[] { "hud", "stats", "display", "update" },
        ["ui"] = new[] { "hud", "overlay", "menu", "render", "css", "style" },
        ["game"] = new[] { "loop", "state", "main" },
        ["loop"] = new[] { "frame", "update", "tick" },
        // Positioning / visual keywords almost always imply CSS/style work.
        // Without expansion, a task phrased "top-right corner, don't change
        // layout" scored .css files at zero and missed them in snippets.
        ["top"] = new[] { "css", "style", "position" },
        ["right"] = new[] { "css", "style", "position" },
        ["left"] = new[] { "css", "style", "position" },
        ["corner"] = new[] { "css", "style", "position" },
        ["position"] = new[] { "css", "style" },
        ["layout"] = new[] { "css", "style" },
        ["color"] = new[] { "css", "style" },
        ["style"] = new[] { "css" },
        ["css"] = new[] { "style", "ui" }
    };

    public static IReadOnlyList<string> Build(
        WorkspaceScanResult scan,
        string projectRoot,
        int maxEntries = DefaultMaxEntries,
        string? taskDescription = null,
        int snippetFileCount = DefaultSnippetFileCount,
        int snippetMaxLines = DefaultSnippetMaxLines)
    {
        ArgumentNullException.ThrowIfNull(scan);
        ArgumentException.ThrowIfNullOrWhiteSpace(projectRoot);

        var normalizedRoot = Path.GetFullPath(projectRoot);
        var summary = scan.State.Summary;
        var lines = new List<string>();

        if (summary.SourceRoots is { Count: > 0 } sourceRoots)
        {
            lines.Add($"source roots: {string.Join(", ", sourceRoots)}");
        }

        if (summary.EntryCandidates is { Count: > 0 } entryCandidates)
        {
            lines.Add($"entry candidates: {string.Join(", ", entryCandidates.Take(6))}");
        }

        var entryPathSet = new HashSet<string>(summary.EntryCandidates ?? Array.Empty<string>(), StringComparer.OrdinalIgnoreCase);

        var relativePaths = (scan.RelevantFiles ?? Array.Empty<string>())
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Select(path => Path.GetRelativePath(normalizedRoot, path))
            .Where(relative => !string.IsNullOrWhiteSpace(relative)
                && !relative.StartsWith("..", StringComparison.Ordinal))
            .ToArray();

        var sources = relativePaths
            .Where(IsSourceLike)
            .OrderBy(PathDepth)
            .ThenBy(static p => p, StringComparer.OrdinalIgnoreCase)
            .Take(maxEntries)
            .ToArray();

        var builds = relativePaths
            .Where(IsBuildMarker)
            .OrderBy(PathDepth)
            .ThenBy(static p => p, StringComparer.OrdinalIgnoreCase)
            .Take(20)
            .ToArray();

        if (sources.Length == 0 && builds.Length == 0)
        {
            return lines.Count > 0 ? lines : Array.Empty<string>();
        }

        if (builds.Length > 0)
        {
            lines.Add($"build/config markers ({builds.Length}):");
            foreach (var build in builds)
            {
                lines.Add($"- {build}");
            }
        }

        if (sources.Length > 0)
        {
            var totalSourceCount = relativePaths.Count(IsSourceLike);
            var header = sources.Length < totalSourceCount
                ? $"source files (showing top {sources.Length} of {totalSourceCount}):"
                : $"source files ({sources.Length}):";
            lines.Add(header);
            foreach (var source in sources)
            {
                var entryTag = entryPathSet.Contains(source) ? "  (entry)" : string.Empty;
                lines.Add($"- {source}{entryTag}");
            }
        }

        if (snippetFileCount > 0 && sources.Length > 0)
        {
            var keywords = ExtractKeywords(taskDescription);
            var initialCandidates = PickSnippetCandidates(sources, entryPathSet, keywords, snippetFileCount);

            // Import-follow: after picking top-K by score, read those snippets
            // and harvest their local import targets. Lets Worker see hud.js
            // when index.js imports it, even if hud.js scored poorly on
            // task-description keywords alone.
            var sourceSet = new HashSet<string>(sources, StringComparer.OrdinalIgnoreCase);
            var finalOrder = new List<string>(initialCandidates);
            var finalSet = new HashSet<string>(initialCandidates, StringComparer.OrdinalIgnoreCase);
            var snippetBodies = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            foreach (var candidate in initialCandidates)
            {
                var absolute = Path.Combine(normalizedRoot, candidate);
                var snippet = ReadSnippet(absolute, snippetMaxLines, DefaultSnippetMaxBytes);
                if (string.IsNullOrEmpty(snippet))
                {
                    continue;
                }

                snippetBodies[candidate] = snippet;

                foreach (var imported in ExtractLocalImports(snippet, candidate, sourceSet))
                {
                    if (finalSet.Add(imported))
                    {
                        finalOrder.Add(imported);
                    }

                    if (finalOrder.Count >= snippetFileCount + 3)
                    {
                        break;
                    }
                }
            }

            foreach (var candidate in finalOrder)
            {
                if (!snippetBodies.TryGetValue(candidate, out var snippet))
                {
                    var absolute = Path.Combine(normalizedRoot, candidate);
                    snippet = ReadSnippet(absolute, snippetMaxLines, DefaultSnippetMaxBytes);
                    if (string.IsNullOrEmpty(snippet))
                    {
                        continue;
                    }
                }

                lines.Add(string.Empty);
                lines.Add($"--- snippet: {candidate} (first {snippetMaxLines} lines) ---");
                foreach (var snippetLine in snippet.Split('\n'))
                {
                    lines.Add(snippetLine.TrimEnd('\r'));
                }
            }
        }

        return lines;
    }

    private static IEnumerable<string> ExtractLocalImports(string snippet, string fromRelativePath, HashSet<string> knownSources)
    {
        // Surface-level regex: JS/TS static imports with a relative path. We
        // only follow targets that exist in the scanned source set — no
        // invention, no node_modules, no http.
        var fromDir = Path.GetDirectoryName(fromRelativePath) ?? string.Empty;
        var pattern = new System.Text.RegularExpressions.Regex(
            @"from\s+['""](?<path>\.[^'""]+)['""]",
            System.Text.RegularExpressions.RegexOptions.Compiled);
        foreach (System.Text.RegularExpressions.Match match in pattern.Matches(snippet))
        {
            var raw = match.Groups["path"].Value;
            var resolved = TryResolveImport(fromDir, raw, knownSources);
            if (resolved is not null)
            {
                yield return resolved;
            }
        }
    }

    private static string? TryResolveImport(string fromDir, string importSpec, HashSet<string> knownSources)
    {
        if (string.IsNullOrWhiteSpace(importSpec))
        {
            return null;
        }

        // Normalize the combined relative path. Path.GetFullPath relies on the
        // current directory which is meaningless here; compose manually.
        var combined = string.IsNullOrEmpty(fromDir)
            ? importSpec
            : fromDir + Path.DirectorySeparatorChar + importSpec;
        combined = combined.Replace('/', Path.DirectorySeparatorChar);

        var segments = new List<string>();
        foreach (var part in combined.Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries))
        {
            if (part == ".")
            {
                continue;
            }

            if (part == "..")
            {
                if (segments.Count == 0)
                {
                    return null;
                }

                segments.RemoveAt(segments.Count - 1);
                continue;
            }

            segments.Add(part);
        }

        var normalized = string.Join(Path.DirectorySeparatorChar, segments);

        if (knownSources.Contains(normalized))
        {
            return normalized;
        }

        foreach (var ext in new[] { ".js", ".ts", ".jsx", ".tsx", ".mjs", ".cjs" })
        {
            var withExt = normalized + ext;
            if (knownSources.Contains(withExt))
            {
                return withExt;
            }
        }

        foreach (var ext in new[] { ".js", ".ts" })
        {
            var indexPath = normalized + Path.DirectorySeparatorChar + "index" + ext;
            if (knownSources.Contains(indexPath))
            {
                return indexPath;
            }
        }

        return null;
    }

    private static IReadOnlyList<string> ExtractKeywords(string? taskDescription)
    {
        if (string.IsNullOrWhiteSpace(taskDescription))
        {
            return Array.Empty<string>();
        }

        var direct = taskDescription
            .ToLowerInvariant()
            .Split(new[] { ' ', '\t', '.', ',', ';', ':', '!', '?', '\n', '\r', '(', ')', '[', ']', '{', '}', '"', '\'' },
                StringSplitOptions.RemoveEmptyEntries)
            .Where(token => token.Length >= 3)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var expanded = new HashSet<string>(direct, StringComparer.OrdinalIgnoreCase);
        foreach (var token in direct)
        {
            if (KeywordExpansions.TryGetValue(token, out var siblings))
            {
                foreach (var sibling in siblings)
                {
                    expanded.Add(sibling);
                }
            }
        }

        return expanded.ToArray();
    }

    private static IReadOnlyList<string> PickSnippetCandidates(
        IReadOnlyList<string> sources,
        HashSet<string> entryPathSet,
        IReadOnlyList<string> keywords,
        int count)
    {
        var scored = sources
            .Select(path => new { path, score = ScoreSnippetCandidate(path, entryPathSet, keywords) })
            .OrderByDescending(item => item.score)
            .ThenBy(item => PathDepth(item.path))
            .ThenBy(item => item.path, StringComparer.OrdinalIgnoreCase)
            .Take(count)
            .Select(item => item.path)
            .ToArray();
        return scored;
    }

    private static int ScoreSnippetCandidate(string relativePath, HashSet<string> entryPathSet, IReadOnlyList<string> keywords)
    {
        var score = 0;
        if (entryPathSet.Contains(relativePath))
        {
            score += 100;
        }

        var fileName = Path.GetFileName(relativePath).ToLowerInvariant();
        if (fileName is "index.js" or "index.ts" or "main.js" or "main.ts" or "main.py" or "app.js")
        {
            score += 40;
        }

        var normalized = relativePath.Replace('\\', '/').ToLowerInvariant();
        foreach (var keyword in keywords)
        {
            if (normalized.Contains(keyword, StringComparison.OrdinalIgnoreCase))
            {
                score += 30;
            }
        }

        // prefer shallower paths
        score -= PathDepth(relativePath) * 2;
        return score;
    }

    private static string ReadSnippet(string absolutePath, int maxLines, int maxBytes)
    {
        try
        {
            if (!File.Exists(absolutePath))
            {
                return string.Empty;
            }

            var info = new FileInfo(absolutePath);
            if (info.Length > maxBytes * 4L)
            {
                return string.Empty;
            }

            using var reader = new StreamReader(absolutePath);
            var buffer = new System.Text.StringBuilder();
            var lineCount = 0;
            string? line;
            while ((line = reader.ReadLine()) is not null && lineCount < maxLines && buffer.Length < maxBytes)
            {
                buffer.AppendLine(line);
                lineCount++;
            }

            return buffer.ToString();
        }
        catch
        {
            return string.Empty;
        }
    }

    private static bool IsSourceLike(string relativePath)
    {
        var extension = Path.GetExtension(relativePath);
        return SourceExtensions.Contains(extension);
    }

    private static bool IsBuildMarker(string relativePath)
    {
        var fileName = Path.GetFileName(relativePath);
        return BuildFileNames.Contains(fileName);
    }

    private static int PathDepth(string relativePath)
    {
        return relativePath.Count(c => c == Path.DirectorySeparatorChar || c == Path.AltDirectorySeparatorChar);
    }
}
