using System;
using System.Collections.Generic;
using System.Linq;
using zavod.Tooling;

namespace zavod.Retrieval;

public static class BasicCandidateSelector
{
    public static RetrievalResult Select(IReadOnlyList<ArtifactInventory> inventories, RetrievalRequest request)
    {
        ArgumentNullException.ThrowIfNull(inventories);
        ArgumentNullException.ThrowIfNull(request);

        Require(request.TargetArtifacts is { Count: > 0 }, "candidate selection", "target artifacts", "At least one target artifact is required.");
        Require(request.MaxCandidates > 0, "candidate selection", "max candidates", "MaxCandidates must be greater than zero.");

        var normalizedHints = NormalizeStrings(request.IntentHints ?? Array.Empty<string>());
        var extensions = NormalizeStrings(request.Filters?.Extensions ?? Array.Empty<string>());
        var pathFilters = NormalizeStrings(request.Filters?.PathContains ?? Array.Empty<string>());
        var entryTypeFilters = (request.Filters?.EntryTypes ?? Array.Empty<ArtifactInventoryEntryType>())
            .OrderBy(static value => value)
            .ToArray();

        var candidates = inventories
            .SelectMany(inventory => inventory.Entries.Select(entry => EvaluateCandidate(inventory.ArtifactId, entry, normalizedHints, extensions, pathFilters, entryTypeFilters)))
            .Where(static candidate => candidate is not null)
            .Select(static candidate => candidate!)
            .OrderByDescending(static candidate => candidate.RelevanceScore)
            .ThenBy(static candidate => candidate.SourceArtifactId, StringComparer.Ordinal)
            .ThenBy(static candidate => candidate.Reference, StringComparer.Ordinal)
            .Take(request.MaxCandidates)
            .ToArray();

        var totalMatches = inventories.Sum(inventory => inventory.Entries.Count(entry => IsPotentialMatch(entry, normalizedHints, extensions, pathFilters, entryTypeFilters)));
        var warnings = totalMatches > request.MaxCandidates
            ? new[] { new ToolWarning("SHORTLIST_TRUNCATED", $"Candidate shortlist truncated to top {request.MaxCandidates}.") }
            : Array.Empty<ToolWarning>();

        return new RetrievalResult(
            candidates,
            $"Selected {candidates.Length} candidate(s) from {inventories.Sum(inventory => inventory.Entries.Count)} inventory entry(s).",
            warnings);
    }

    private static Candidate? EvaluateCandidate(
        string artifactId,
        ArtifactInventoryEntry entry,
        IReadOnlyList<string> hints,
        IReadOnlyList<string> extensions,
        IReadOnlyList<string> pathFilters,
        IReadOnlyList<ArtifactInventoryEntryType> entryTypeFilters)
    {
        if (!IsPotentialMatch(entry, hints, extensions, pathFilters, entryTypeFilters))
        {
            return null;
        }

        var tags = new List<string>();
        double score = 1;

        foreach (var hint in hints)
        {
            if (Contains(entry.NameOrPath, hint) || entry.Metadata.Any(metadata => Contains(metadata.Value, hint)))
            {
                score += 10;
                tags.Add($"hint:{hint}");
            }
        }

        foreach (var filter in pathFilters)
        {
            if (Contains(entry.NameOrPath, filter))
            {
                score += 5;
                tags.Add($"path:{filter}");
            }
        }

        foreach (var extension in extensions)
        {
            if (entry.NameOrPath.EndsWith(extension, StringComparison.OrdinalIgnoreCase))
            {
                score += 4;
                tags.Add($"extension:{extension}");
            }
        }

        if (entryTypeFilters.Contains(entry.Type))
        {
            score += 3;
            tags.Add($"type:{entry.Type}");
        }

        var preview = entry.Metadata
            .FirstOrDefault(metadata => string.Equals(metadata.Key, "preview", StringComparison.OrdinalIgnoreCase))
            ?.Value;

        return new Candidate(
            $"CAND-{artifactId}-{entry.EntryId}",
            artifactId,
            entry.NameOrPath,
            score,
            tags.OrderBy(static tag => tag, StringComparer.Ordinal).ToArray(),
            preview);
    }

    private static bool IsPotentialMatch(
        ArtifactInventoryEntry entry,
        IReadOnlyList<string> hints,
        IReadOnlyList<string> extensions,
        IReadOnlyList<string> pathFilters,
        IReadOnlyList<ArtifactInventoryEntryType> entryTypeFilters)
    {
        var anyFilters = hints.Count > 0 || extensions.Count > 0 || pathFilters.Count > 0 || entryTypeFilters.Count > 0;
        if (!anyFilters)
        {
            return true;
        }

        if (entryTypeFilters.Count > 0 && !entryTypeFilters.Contains(entry.Type))
        {
            return false;
        }

        var hintMatch = hints.Count == 0 || hints.Any(hint => Contains(entry.NameOrPath, hint) || entry.Metadata.Any(metadata => Contains(metadata.Value, hint)));
        var pathMatch = pathFilters.Count == 0 || pathFilters.Any(filter => Contains(entry.NameOrPath, filter));
        var extensionMatch = extensions.Count == 0 || extensions.Any(extension => entry.NameOrPath.EndsWith(extension, StringComparison.OrdinalIgnoreCase));

        return hintMatch && pathMatch && extensionMatch;
    }

    private static IReadOnlyList<string> NormalizeStrings(IReadOnlyList<string> values)
    {
        return values
            .Where(static value => !string.IsNullOrWhiteSpace(value))
            .Select(static value => value.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(static value => value, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static bool Contains(string value, string target)
    {
        return value.Contains(target, StringComparison.OrdinalIgnoreCase);
    }

    private static void Require(bool condition, string area, string missingRequirement, string reason)
    {
        if (!condition)
        {
            throw new RetrievalException(area, missingRequirement, reason);
        }
    }
}
