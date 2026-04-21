using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using zavod.Execution;
using zavod.State;

namespace zavod.Persistence;

public sealed record ProjectSageAdvisory(IReadOnlyList<string> Notes)
{
    public bool HasNotes => Notes.Count > 0;

    public string BuildLeadContextBlock()
    {
        if (!HasNotes)
        {
            return string.Empty;
        }

        return "Keep in mind:" + Environment.NewLine + string.Join(Environment.NewLine, Notes.Select(static note => $"- {note}"));
    }

    public string? BuildWorkerWarning()
    {
        return HasNotes ? Notes[0] : null;
    }

    public string BuildWorkerAppendix()
    {
        if (!HasNotes)
        {
            return string.Empty;
        }

        return string.Join(Environment.NewLine, Notes.Select(static note => $"advisory: {note}"));
    }
}

public sealed class ProjectSageService
{
    private const int MaxNotes = 2;

    private static readonly HashSet<string> StopWords = new(StringComparer.Ordinal)
    {
        "this", "that", "with", "from", "into", "your", "have", "will", "would", "should",
        "task", "chat", "work", "user", "need", "needs", "make", "made", "using", "same",
        "это", "этот", "эта", "эти", "проект", "задача", "нужно", "надо", "можно", "если",
        "чтобы", "есть", "будет", "сделать", "делать", "через", "после", "перед"
    };

    private readonly ProjectDocumentRuntimeService _projectDocumentRuntime = new();

    public ProjectSageAdvisory BuildLeadAdvisory(string projectRootPath, string projectId, string queryText)
    {
        return BuildAdvisory(projectRootPath, projectId, queryText);
    }

    public ProjectSageAdvisory BuildWorkerAdvisory(string projectRootPath, string projectId, string queryText)
    {
        return BuildAdvisory(projectRootPath, projectId, queryText);
    }

    private ProjectSageAdvisory BuildAdvisory(string projectRootPath, string projectId, string queryText)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(projectRootPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(projectId);

        var normalizedQuery = NormalizeText(queryText);
        var keywords = ExtractKeywords(normalizedQuery);
        if (keywords.Count == 0)
        {
            return new ProjectSageAdvisory(Array.Empty<string>());
        }

        var candidates = EnumerateCandidates(Path.GetFullPath(projectRootPath), projectId.Trim(), normalizedQuery)
            .Select(candidate => candidate with { Score = Score(candidate.SearchText, keywords, candidate.BaseWeight) })
            .Where(candidate => candidate.Score > 0)
            .OrderByDescending(candidate => candidate.Score)
            .ThenBy(candidate => candidate.SortKey, StringComparer.Ordinal)
            .ToArray();

        if (candidates.Length == 0)
        {
            return new ProjectSageAdvisory(Array.Empty<string>());
        }

        var notes = new List<string>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var candidate in candidates)
        {
            var note = candidate.Note.Trim();
            if (note.Length == 0 || !seen.Add(note))
            {
                continue;
            }

            notes.Add(note);
            if (notes.Count >= MaxNotes)
            {
                break;
            }
        }

        return new ProjectSageAdvisory(notes);
    }

    private IEnumerable<SageCandidate> EnumerateCandidates(string projectRootPath, string projectId, string normalizedQuery)
    {
        foreach (var candidate in EnumerateConversationCandidates(projectRootPath, projectId, normalizedQuery))
        {
            yield return candidate;
        }

        foreach (var candidate in EnumerateShiftCandidates(projectRootPath))
        {
            yield return candidate;
        }

        foreach (var candidate in EnumerateDocumentCandidates(projectRootPath))
        {
            yield return candidate;
        }
    }

    private IEnumerable<SageCandidate> EnumerateConversationCandidates(string projectRootPath, string projectId, string normalizedQuery)
    {
        foreach (var entry in ConversationIndexStorage.Load(projectRootPath)
                     .Where(entry => string.Equals(entry.Mode, "project", StringComparison.OrdinalIgnoreCase)
                         && string.Equals(entry.ProjectId, projectId, StringComparison.Ordinal)))
        {
            var storage = ConversationLogStorage.ForProjectConversation(projectRootPath, entry.ConversationId);
            foreach (var snapshot in storage.LoadLatest().TakeLast(24))
            {
                // Conversation-advisory must not leak downstream role chatter back
                // into Lead/Worker prompts. A prior Worker refusal or QC REVISE
                // rationale, scored by keyword overlap, ends up coaching the new
                // Lead turn to parrot "where is the game loop?" at the user,
                // forcing another clarification loop. Framing advisory should
                // come only from User intents (and optionally Lead turns) — the
                // rest (Worker/QC/Log/Artifact/Status) is execution telemetry,
                // not intent framing.
                if (!IsFramingAdvisoryRole(snapshot.Role, snapshot.Kind))
                {
                    continue;
                }

                var normalized = NormalizeText(snapshot.Text);
                if (normalized.Length == 0 || string.Equals(normalized, normalizedQuery, StringComparison.Ordinal))
                {
                    continue;
                }

                yield return new SageCandidate(
                    BuildConversationNote(snapshot),
                    normalized,
                    BaseWeight: 4,
                    SortKey: $"conversation:{entry.ConversationId}:{snapshot.MessageId}");
            }
        }
    }

    private static bool IsFramingAdvisoryRole(string? role, string? kind)
    {
        if (string.Equals(kind, "Log", StringComparison.OrdinalIgnoreCase)
            || string.Equals(kind, "Artifact", StringComparison.OrdinalIgnoreCase)
            || string.Equals(kind, "Status", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return string.Equals(role, "User", StringComparison.OrdinalIgnoreCase)
            || string.Equals(role, "Lead", StringComparison.OrdinalIgnoreCase)
            || string.Equals(role, "ShiftLead", StringComparison.OrdinalIgnoreCase);
    }

    private IEnumerable<SageCandidate> EnumerateShiftCandidates(string projectRootPath)
    {
        var shiftsRoot = Path.Combine(Path.GetFullPath(projectRootPath), ".zavod", "shifts");
        if (!Directory.Exists(shiftsRoot))
        {
            yield break;
        }

        foreach (var path in Directory.EnumerateFiles(shiftsRoot, "*.json", SearchOption.TopDirectoryOnly))
        {
            ShiftState shift;
            try
            {
                shift = ShiftStateStorage.Load(projectRootPath, Path.GetFileNameWithoutExtension(path));
            }
            catch (ZavodPersistenceException)
            {
                continue;
            }

            foreach (var accepted in shift.AcceptedResults.Where(static value => !string.IsNullOrWhiteSpace(value)))
            {
                yield return new SageCandidate(
                    BuildNote("Accepted before", accepted),
                    NormalizeText(accepted),
                    BaseWeight: 24,
                    SortKey: $"accepted:{accepted}");
            }

            foreach (var issue in shift.OpenIssues.Where(static value => !string.IsNullOrWhiteSpace(value)))
            {
                yield return new SageCandidate(
                    BuildNote("Open issue", issue),
                    NormalizeText(issue),
                    BaseWeight: 20,
                    SortKey: $"issue:{issue}");
            }

            foreach (var constraint in shift.Constraints.Where(static value => !string.IsNullOrWhiteSpace(value)))
            {
                yield return new SageCandidate(
                    BuildNote("Constraint", constraint),
                    NormalizeText(constraint),
                    BaseWeight: 30,
                    SortKey: $"constraint:{constraint}");
            }

            foreach (var task in shift.Tasks.Where(static task => task.Status == TaskStateStatus.Abandoned))
            {
                yield return new SageCandidate(
                    BuildNote("Abandoned earlier", task.Description),
                    NormalizeText(task.Description),
                    BaseWeight: 18,
                    SortKey: $"abandoned:{task.TaskId}:{task.Description}");
            }
        }
    }

    private IEnumerable<SageCandidate> EnumerateDocumentCandidates(string projectRootPath)
    {
        foreach (var kind in Enum.GetValues<ProjectDocumentKind>())
        {
            var document = _projectDocumentRuntime.Read(projectRootPath, kind);
            if (!document.Exists || string.IsNullOrWhiteSpace(document.Markdown))
            {
                continue;
            }

            foreach (var line in ExtractDocumentLines(document.Markdown))
            {
                yield return new SageCandidate(
                    BuildNote("Project doc", line),
                    NormalizeText(line),
                    BaseWeight: 5,
                    SortKey: $"document:{kind}:{line}");
            }
        }
    }

    private static IEnumerable<string> ExtractDocumentLines(string markdown)
    {
        return markdown
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Split('\n')
            .Select(static line => line.Trim())
            .Where(static line => line.Length >= 12 && !line.StartsWith("#", StringComparison.Ordinal))
            .Select(static line => line.StartsWith("- ", StringComparison.Ordinal) ? line[2..].Trim() : line)
            .Where(static line => line.Length >= 12)
            .Take(48);
    }

    private static string BuildConversationNote(ConversationLogSnapshot snapshot)
    {
        var prefix = snapshot.Kind switch
        {
            "Log" => "Earlier log",
            "Artifact" => "Earlier artifact",
            "Status" => "Earlier status",
            _ => "Earlier conversation"
        };

        return BuildNote(prefix, snapshot.Text);
    }

    private static string BuildNote(string prefix, string value)
    {
        return $"{prefix}: {TrimSnippet(value)}";
    }

    private static int Score(string text, IReadOnlyCollection<string> keywords, int baseWeight)
    {
        if (keywords.Count == 0 || text.Length == 0)
        {
            return 0;
        }

        var matches = 0;
        foreach (var keyword in keywords)
        {
            if (text.Contains(keyword, StringComparison.Ordinal))
            {
                matches++;
            }
        }

        return matches == 0 ? 0 : baseWeight + matches * 10;
    }

    private static HashSet<string> ExtractKeywords(string text)
    {
        var keywords = new HashSet<string>(StringComparer.Ordinal);
        if (string.IsNullOrWhiteSpace(text))
        {
            return keywords;
        }

        var buffer = new List<char>(text.Length);
        void Flush()
        {
            if (buffer.Count == 0)
            {
                return;
            }

            var token = new string(buffer.ToArray());
            buffer.Clear();
            if (token.Length < 4 || StopWords.Contains(token))
            {
                return;
            }

            keywords.Add(token);
        }

        foreach (var ch in text)
        {
            if (char.IsLetterOrDigit(ch))
            {
                buffer.Add(char.ToLowerInvariant(ch));
            }
            else
            {
                Flush();
            }
        }

        Flush();
        return keywords;
    }

    private static string NormalizeText(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : value.Trim().ToLowerInvariant();
    }

    private static string TrimSnippet(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var normalized = value.Replace("\r", " ", StringComparison.Ordinal).Replace("\n", " ", StringComparison.Ordinal).Trim();
        return normalized.Length <= 140
            ? normalized
            : $"{normalized[..137].TrimEnd()}...";
    }

    private sealed record SageCandidate(
        string Note,
        string SearchText,
        int BaseWeight,
        string SortKey,
        int Score = 0);
}
