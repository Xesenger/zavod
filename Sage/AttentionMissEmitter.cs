using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace zavod.Sage;

// S5a emitter: attention_miss on BeforeExecution.
//
// Detects when the task description references a source file that
// does not appear anywhere in the assembled anchor pack or task scope.
// Worker will try to edit/read that file, but will not have its
// contents in context — likely a "where is the game loop?" failure.
//
// Design bias (per S3 directive, still applies):
//   - Prefer false negatives over false positives.
//   - Severity = Hint (never blocks, never must-show).
//   - Channel  = SageOnly (never injected into role prompts).
//   - Max ONE observation per call; the hook enforces max-1.
//
// Heuristic:
//   1. Scan TaskDescription for file-like tokens (regex).
//   2. Filter to known source extensions only. Unknown extensions
//      are treated as noise (e.g. "v2.1.md" → keep, "Hello.wor" →
//      drop).
//   3. For each valid file token, check if any anchor or scope entry
//      contains it as a case-insensitive substring.
//   4. Emit exactly once for the FIRST unmatched file token.
//
// The heuristic deliberately ignores non-file concepts (colors,
// positions, behaviors). Those are S3 semantic_gap territory.
internal static class AttentionMissEmitter
{
    // Anchored-style match: [path/][name].[ext] where the path portion
    // is optional and ext is alpha 2-5 chars. Dot separators inside
    // [name] are not captured (we match the rightmost name.ext).
    private static readonly Regex FileTokenPattern = new(
        @"(?:[A-Za-z0-9_\-]+[/\\])*[A-Za-z0-9_\-]+\.[A-Za-z]{2,5}",
        RegexOptions.Compiled);

    private static readonly HashSet<string> KnownExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        "cs", "js", "ts", "tsx", "jsx", "mjs", "cjs",
        "css", "scss", "sass", "less",
        "html", "htm", "json", "xml", "xaml", "yml", "yaml", "toml", "ini",
        "py", "go", "rs", "java", "kt", "swift", "rb", "php", "lua",
        "md", "txt", "sh", "bat", "ps1", "sql"
    };

    public static SageObservation? TryObserve(SageBeforeExecutionContext context)
    {
        if (context is null) return null;
        if (string.IsNullOrWhiteSpace(context.TaskDescription)) return null;
        if (context.Anchors.Count == 0 && context.Scope.Count == 0) return null;

        var matches = FileTokenPattern.Matches(context.TaskDescription);
        if (matches.Count == 0) return null;

        foreach (Match match in matches)
        {
            var token = match.Value;
            if (token.Length < 4) continue;

            var dotIndex = token.LastIndexOf('.');
            if (dotIndex < 0 || dotIndex >= token.Length - 1) continue;
            var ext = token[(dotIndex + 1)..];
            if (!KnownExtensions.Contains(ext)) continue;

            if (IsKnownInAnchorsOrScope(token, context.Anchors, context.Scope)) continue;

            return new SageObservation(
                Type: SageObservationType.AttentionMiss,
                Severity: SageSeverity.Hint,
                Message: $"Task references '{token}' but it is not present in the anchor pack or scope.",
                Stage: SageStage.BeforeExecution,
                Channel: SageChannel.SageOnly,
                ObservedAt: DateTimeOffset.UtcNow,
                ProjectId: context.ProjectId,
                TaskId: context.TaskId,
                AnchorRef: token);
        }

        return null;
    }

    private static bool IsKnownInAnchorsOrScope(
        string fileToken,
        IReadOnlyList<string> anchors,
        IReadOnlyList<string> scope)
    {
        foreach (var anchor in anchors)
        {
            if (!string.IsNullOrEmpty(anchor)
                && anchor.Contains(fileToken, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }
        foreach (var scopeEntry in scope)
        {
            if (!string.IsNullOrEmpty(scopeEntry)
                && scopeEntry.Contains(fileToken, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }
        return false;
    }
}
