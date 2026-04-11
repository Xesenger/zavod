using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using zavod.Tooling;

namespace zavod.Retrieval;

public static class ArtifactInventoryBuilder
{
    public static ArtifactInventory Build(IntakeArtifact artifact)
    {
        ArgumentNullException.ThrowIfNull(artifact);

        Require(!string.IsNullOrWhiteSpace(artifact.Id), "inventory build", "artifact id", "Artifact id is required.");

        var entries = artifact.Type switch
        {
            IntakeArtifactType.Archive => BuildArchiveEntries(artifact),
            IntakeArtifactType.Pdf => BuildPdfEntries(artifact),
            IntakeArtifactType.Text or IntakeArtifactType.Document => BuildTextEntries(artifact),
            IntakeArtifactType.Image => BuildImageEntries(artifact),
            _ => BuildUnknownEntries(artifact)
        };

        return new ArtifactInventory(artifact.Id, entries);
    }

    private static IReadOnlyList<ArtifactInventoryEntry> BuildArchiveEntries(IntakeArtifact artifact)
    {
        var groupedEntries = artifact.Metadata
            .Where(static entry => entry.Key.StartsWith("archive.entry.", StringComparison.OrdinalIgnoreCase))
            .GroupBy(static entry => entry.Key.Split('.')[2], StringComparer.Ordinal)
            .OrderBy(static group => group.Key, StringComparer.Ordinal)
            .Select((group, index) =>
            {
                var path = group.FirstOrDefault(entry => entry.Key.EndsWith(".path", StringComparison.OrdinalIgnoreCase))?.Value
                    ?? $"{artifact.DisplayName}/entry-{index + 1}";
                var typeValue = group.FirstOrDefault(entry => entry.Key.EndsWith(".type", StringComparison.OrdinalIgnoreCase))?.Value;
                var entryType = string.Equals(typeValue, "directory", StringComparison.OrdinalIgnoreCase)
                    ? ArtifactInventoryEntryType.Directory
                    : ArtifactInventoryEntryType.File;

                return new ArtifactInventoryEntry(
                    $"ENTRY-{artifact.Id}-{index + 1:000}",
                    path,
                    entryType,
                    NormalizeMetadata(group));
            })
            .ToArray();

        if (groupedEntries.Length > 0)
        {
            return groupedEntries;
        }

        return
        [
            new ArtifactInventoryEntry(
                $"ENTRY-{artifact.Id}-001",
                artifact.DisplayName,
                ArtifactInventoryEntryType.File,
                Array.Empty<ArtifactMetadataEntry>())
        ];
    }

    private static IReadOnlyList<ArtifactInventoryEntry> BuildPdfEntries(IntakeArtifact artifact)
    {
        var pageCount = GetMetadataInt(artifact.Metadata, "page_count") ?? 1;

        return Enumerable.Range(1, pageCount)
            .Select(page => new ArtifactInventoryEntry(
                $"ENTRY-{artifact.Id}-{page:000}",
                $"page:{page}",
                ArtifactInventoryEntryType.Page,
                new[] { new ArtifactMetadataEntry("page_number", page.ToString(CultureInfo.InvariantCulture)) }))
            .ToArray();
    }

    private static IReadOnlyList<ArtifactInventoryEntry> BuildTextEntries(IntakeArtifact artifact)
    {
        var inlineText = GetMetadataValue(artifact.Metadata, "inline_text");
        if (string.IsNullOrWhiteSpace(inlineText))
        {
            return
            [
                new ArtifactInventoryEntry(
                    $"ENTRY-{artifact.Id}-001",
                    artifact.DisplayName,
                    ArtifactInventoryEntryType.TextChunk,
                    Array.Empty<ArtifactMetadataEntry>())
            ];
        }

        var segments = SplitText(inlineText);
        return segments
            .Select((segment, index) => new ArtifactInventoryEntry(
                $"ENTRY-{artifact.Id}-{index + 1:000}",
                $"segment:{index + 1}",
                ArtifactInventoryEntryType.TextChunk,
                new[] { new ArtifactMetadataEntry("preview", segment) }))
            .ToArray();
    }

    private static IReadOnlyList<ArtifactInventoryEntry> BuildImageEntries(IntakeArtifact artifact)
    {
        return
        [
            new ArtifactInventoryEntry(
                $"ENTRY-{artifact.Id}-001",
                artifact.DisplayName,
                ArtifactInventoryEntryType.Image,
                Array.Empty<ArtifactMetadataEntry>())
        ];
    }

    private static IReadOnlyList<ArtifactInventoryEntry> BuildUnknownEntries(IntakeArtifact artifact)
    {
        return
        [
            new ArtifactInventoryEntry(
                $"ENTRY-{artifact.Id}-001",
                artifact.DisplayName,
                ArtifactInventoryEntryType.File,
                Array.Empty<ArtifactMetadataEntry>())
        ];
    }

    private static IReadOnlyList<ArtifactMetadataEntry> NormalizeMetadata(IEnumerable<ArtifactMetadataEntry> metadata)
    {
        return metadata
            .Where(static entry => !string.IsNullOrWhiteSpace(entry.Key) && !string.IsNullOrWhiteSpace(entry.Value))
            .Select(static entry => new ArtifactMetadataEntry(entry.Key.Trim(), entry.Value.Trim()))
            .OrderBy(static entry => entry.Key, StringComparer.OrdinalIgnoreCase)
            .ThenBy(static entry => entry.Value, StringComparer.Ordinal)
            .ToArray();
    }

    private static string? GetMetadataValue(IReadOnlyList<ArtifactMetadataEntry> metadata, string key)
    {
        return metadata
            .FirstOrDefault(entry => string.Equals(entry.Key, key, StringComparison.OrdinalIgnoreCase))
            ?.Value;
    }

    private static int? GetMetadataInt(IReadOnlyList<ArtifactMetadataEntry> metadata, string key)
    {
        var value = GetMetadataValue(metadata, key);
        return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) ? parsed : null;
    }

    private static IReadOnlyList<string> SplitText(string inlineText)
    {
        var blocks = inlineText
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Split("\n\n", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        if (blocks.Length > 1)
        {
            return blocks
                .Select(static block => block.Length > 120 ? block[..120] : block)
                .ToArray();
        }

        return inlineText
            .Chunk(120)
            .Select(static chunk => new string(chunk))
            .ToArray();
    }

    private static void Require(bool condition, string area, string missingRequirement, string reason)
    {
        if (!condition)
        {
            throw new RetrievalException(area, missingRequirement, reason);
        }
    }
}
