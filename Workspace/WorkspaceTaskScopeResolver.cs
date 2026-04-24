using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace zavod.Workspace;

public sealed record WorkspaceTaskScope(
    string TaskText,
    IReadOnlyList<WorkspaceTaskScopeFile> PrimaryFiles,
    IReadOnlyList<WorkspaceTaskScopeFile> RelatedFiles,
    IReadOnlyList<WorkspaceTaskScopeFile> SoftExcludedFiles,
    IReadOnlyList<string> Uncertainty,
    IReadOnlyList<string> Evidence);

public sealed record WorkspaceTaskScopeFile(
    string RelativePath,
    int Score,
    IReadOnlyList<string> Evidence);

public static class WorkspaceTaskScopeResolver
{
    public static WorkspaceTaskScope Resolve(
        WorkspaceEvidencePack pack,
        string taskText,
        IReadOnlyList<string>? allowedPaths = null,
        int maxPrimaryFiles = 6,
        int maxRelatedFiles = 12)
    {
        ArgumentNullException.ThrowIfNull(pack);
        ArgumentException.ThrowIfNullOrWhiteSpace(taskText);

        var terms = ExtractTerms(taskText);
        var allowed = NormalizeAllowedPaths(allowedPaths);
        var candidates = BuildCandidateMap(pack);
        foreach (var candidate in candidates.Values)
        {
            ScoreCandidate(candidate, terms, allowed);
        }

        var primary = candidates.Values
            .Where(static candidate => candidate.Score > 0)
            .OrderByDescending(static candidate => candidate.Score)
            .ThenBy(static candidate => candidate.RelativePath, StringComparer.OrdinalIgnoreCase)
            .Take(Math.Max(1, maxPrimaryFiles))
            .Select(static candidate => candidate.ToScopeFile())
            .ToArray();

        var primaryPaths = primary.Select(static file => file.RelativePath).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var related = BuildRelatedFiles(pack, candidates, primaryPaths, terms, allowed, maxRelatedFiles);
        var relatedPaths = related.Select(static file => file.RelativePath).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var softExcluded = candidates.Values
            .Where(candidate => !primaryPaths.Contains(candidate.RelativePath) &&
                                !relatedPaths.Contains(candidate.RelativePath) &&
                                IsSoftExcludedCandidate(candidate))
            .OrderBy(static candidate => candidate.RelativePath, StringComparer.OrdinalIgnoreCase)
            .Take(12)
            .Select(static candidate => candidate.ToScopeFile())
            .ToArray();

        var uncertainty = BuildUncertainty(pack, primary, related, terms, candidates.Count, maxPrimaryFiles);
        var evidence = primary
            .Concat(related)
            .SelectMany(static file => file.Evidence.Select(item => $"{file.RelativePath}: {item}"))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(24)
            .ToArray();

        return new WorkspaceTaskScope(taskText, primary, related, softExcluded, uncertainty, evidence);
    }

    private static Dictionary<string, CandidateDraft> BuildCandidateMap(WorkspaceEvidencePack pack)
    {
        var candidates = new Dictionary<string, CandidateDraft>(StringComparer.OrdinalIgnoreCase);

        foreach (var entry in pack.Candidates.EntryPoints)
        {
            AddCandidate(candidates, entry.RelativePath, 2, "entrypoint_candidate");
        }

        foreach (var role in pack.Candidates.FileRoles)
        {
            AddCandidate(candidates, role.RelativePath, 0, $"file_role:{role.Role}");
        }

        foreach (var unit in pack.Candidates.ProjectUnits)
        {
            foreach (var manifest in unit.Manifests)
            {
                AddCandidate(candidates, manifest, 1, $"project_unit:{unit.RootPath}");
            }

            foreach (var entryPoint in unit.EntryPoints)
            {
                AddCandidate(candidates, entryPoint, 2, $"project_unit_entry:{unit.RootPath}");
            }
        }

        foreach (var profile in pack.Candidates.RunProfiles)
        {
            AddCandidate(candidates, profile.SourcePath, 1, $"run_profile:{profile.Kind}");
        }

        foreach (var edge in pack.CodeEdges)
        {
            AddCandidate(candidates, edge.FromPath, 1, $"edge_from:{edge.Kind}:{edge.Resolution}");
            AddCandidate(candidates, edge.ToPath, edge.Resolution == WorkspaceEvidenceEdgeResolution.Unresolved ? 0 : 1, $"edge_to:{edge.Kind}:{edge.Resolution}");
        }

        foreach (var hint in pack.SignatureHints)
        {
            AddCandidate(candidates, hint.RelativePath, 1, $"signature:{hint.Kind}");
        }

        foreach (var material in pack.Materials)
        {
            AddCandidate(candidates, material.RelativePath, 0, $"material:{material.Kind}");
        }

        foreach (var dependency in pack.DependencySurface)
        {
            AddCandidate(candidates, dependency.SourcePath, 1, $"dependency_manifest:{dependency.Scope}");
        }

        return candidates;
    }

    private static WorkspaceTaskScopeFile[] BuildRelatedFiles(
        WorkspaceEvidencePack pack,
        IDictionary<string, CandidateDraft> candidates,
        HashSet<string> primaryPaths,
        IReadOnlyList<string> terms,
        IReadOnlyList<string> allowed,
        int maxRelatedFiles)
    {
        var related = new Dictionary<string, CandidateDraft>(StringComparer.OrdinalIgnoreCase);
        foreach (var edge in pack.CodeEdges)
        {
            if (primaryPaths.Contains(edge.FromPath))
            {
                AddRelated(related, candidates, edge.ToPath, 6, $"neighbor:{edge.Kind}:{edge.Resolution}");
            }

            if (primaryPaths.Contains(edge.ToPath))
            {
                AddRelated(related, candidates, edge.FromPath, 6, $"neighbor:{edge.Kind}:{edge.Resolution}");
            }
        }

        foreach (var candidate in candidates.Values.Where(static candidate => candidate.Score > 0))
        {
            if (primaryPaths.Contains(candidate.RelativePath))
            {
                continue;
            }

            AddRelated(related, candidates, candidate.RelativePath, Math.Max(1, candidate.Score / 2), "term_related");
        }

        foreach (var candidate in related.Values)
        {
            ScoreCandidate(candidate, terms, allowed);
        }

        return related.Values
            .Where(candidate => !primaryPaths.Contains(candidate.RelativePath))
            .OrderByDescending(static candidate => candidate.Score)
            .ThenBy(static candidate => candidate.RelativePath, StringComparer.OrdinalIgnoreCase)
            .Take(Math.Max(0, maxRelatedFiles))
            .Select(static candidate => candidate.ToScopeFile())
            .ToArray();
    }

    private static string[] BuildUncertainty(
        WorkspaceEvidencePack pack,
        IReadOnlyList<WorkspaceTaskScopeFile> primary,
        IReadOnlyList<WorkspaceTaskScopeFile> related,
        IReadOnlyList<string> terms,
        int candidateCount,
        int maxPrimaryFiles)
    {
        var notes = new List<string>();
        if (primary.Count == 0)
        {
            notes.Add("No primary files met the deterministic term/evidence threshold.");
        }

        if (primary.Count >= maxPrimaryFiles && candidateCount > primary.Count)
        {
            notes.Add($"Primary scope was capped at {maxPrimaryFiles} files.");
        }

        var scopedPaths = primary.Concat(related).Select(static file => file.RelativePath).ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (pack.CodeEdges.Any(edge => scopedPaths.Contains(edge.FromPath) && edge.Resolution == WorkspaceEvidenceEdgeResolution.Ambiguous))
        {
            notes.Add("Some selected files have ambiguous local code edges.");
        }

        if (pack.CodeEdges.Any(edge => scopedPaths.Contains(edge.FromPath) && edge.Resolution == WorkspaceEvidenceEdgeResolution.Unresolved))
        {
            notes.Add("Some selected files have unresolved local code references.");
        }

        if (terms.Count == 0)
        {
            notes.Add("Task text did not expose usable scope terms.");
        }

        return notes.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
    }

    private static void ScoreCandidate(CandidateDraft candidate, IReadOnlyList<string> terms, IReadOnlyList<string> allowed)
    {
        if (allowed.Count > 0 && !allowed.Any(path => IsPathWithin(candidate.RelativePath, path)))
        {
            candidate.Score -= 100;
            candidate.Evidence.Add("outside_allowed_paths_soft_exclusion");
        }

        var normalizedPath = NormalizeForMatching(candidate.RelativePath);
        var fileName = NormalizeForMatching(Path.GetFileNameWithoutExtension(candidate.RelativePath));
        foreach (var term in terms.SelectMany(ExpandTerm).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (normalizedPath.Contains(term, StringComparison.OrdinalIgnoreCase))
            {
                candidate.Score += 8;
                candidate.Evidence.Add($"path_term:{term}");
            }

            if (fileName.Contains(term, StringComparison.OrdinalIgnoreCase))
            {
                candidate.Score += 4;
                candidate.Evidence.Add($"filename_term:{term}");
            }
        }
    }

    private static bool IsSoftExcludedCandidate(CandidateDraft candidate)
    {
        return candidate.Score <= 0 &&
               candidate.Evidence.Any(static item =>
                   item.Contains("file_role:test", StringComparison.OrdinalIgnoreCase) ||
                   item.Contains("file_role:asset", StringComparison.OrdinalIgnoreCase) ||
                   item.Contains("file_role:ui", StringComparison.OrdinalIgnoreCase) ||
                   item.Contains("outside_allowed_paths", StringComparison.OrdinalIgnoreCase));
    }

    private static void AddCandidate(IDictionary<string, CandidateDraft> candidates, string relativePath, int score, string evidence)
    {
        if (string.IsNullOrWhiteSpace(relativePath))
        {
            return;
        }

        var normalized = NormalizePath(relativePath);
        if (!candidates.TryGetValue(normalized, out var candidate))
        {
            candidate = new CandidateDraft(normalized);
            candidates[normalized] = candidate;
        }

        candidate.Score += score;
        candidate.Evidence.Add(evidence);
    }

    private static void AddRelated(
        IDictionary<string, CandidateDraft> related,
        IDictionary<string, CandidateDraft> source,
        string relativePath,
        int score,
        string evidence)
    {
        var normalized = NormalizePath(relativePath);
        if (!source.TryGetValue(normalized, out var sourceCandidate))
        {
            return;
        }

        if (!related.TryGetValue(normalized, out var candidate))
        {
            candidate = new CandidateDraft(normalized);
            foreach (var item in sourceCandidate.Evidence)
            {
                candidate.Evidence.Add(item);
            }

            related[normalized] = candidate;
        }

        candidate.Score += score;
        candidate.Evidence.Add(evidence);
    }

    private static string[] ExtractTerms(string taskText)
    {
        var terms = new List<string>();
        var current = new List<char>();
        foreach (var ch in taskText)
        {
            if (char.IsLetterOrDigit(ch))
            {
                current.Add(char.ToLowerInvariant(ch));
                continue;
            }

            FlushTerm(current, terms);
        }

        FlushTerm(current, terms);
        return terms
            .Where(static term => term.Length >= 3)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static IEnumerable<string> ExpandTerm(string term)
    {
        yield return term;
        if (term is "doc" or "docs" or "document" or "documents")
        {
            yield return "doc";
            yield return "docs";
            yield return "document";
        }

        if (term is "preview" or "previews")
        {
            yield return "preview";
        }

        if (term is "promote" or "promotion" or "promoted")
        {
            yield return "promote";
            yield return "promotion";
            yield return "canonical";
        }

        if (term is "reject" or "rejected" or "rejection")
        {
            yield return "reject";
            yield return "rejected";
        }
    }

    private static void FlushTerm(ICollection<char> current, ICollection<string> terms)
    {
        if (current.Count > 0)
        {
            terms.Add(new string(current.ToArray()));
            current.Clear();
        }
    }

    private static string[] NormalizeAllowedPaths(IReadOnlyList<string>? allowedPaths)
    {
        return allowedPaths is null
            ? Array.Empty<string>()
            : allowedPaths
                .Select(NormalizePath)
                .Where(static path => !string.IsNullOrWhiteSpace(path))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
    }

    private static bool IsPathWithin(string relativePath, string allowedPath)
    {
        return string.Equals(relativePath, allowedPath, StringComparison.OrdinalIgnoreCase) ||
               relativePath.StartsWith(allowedPath.TrimEnd('\\') + "\\", StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizePath(string path)
    {
        return path.Trim().Replace('/', '\\').Trim('\\');
    }

    private static string NormalizeForMatching(string value)
    {
        return value.Replace('\\', ' ').Replace('/', ' ').Replace('-', ' ').Replace('_', ' ').ToLowerInvariant();
    }

    private sealed class CandidateDraft(string relativePath)
    {
        public string RelativePath { get; } = relativePath;

        public int Score { get; set; }

        public HashSet<string> Evidence { get; } = new(StringComparer.OrdinalIgnoreCase);

        public WorkspaceTaskScopeFile ToScopeFile()
        {
            return new WorkspaceTaskScopeFile(
                RelativePath,
                Score,
                Evidence.OrderBy(static item => item, StringComparer.OrdinalIgnoreCase).ToArray());
        }
    }
}
