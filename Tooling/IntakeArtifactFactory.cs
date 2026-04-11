using System;
using System.Collections.Generic;
using System.Linq;

namespace zavod.Tooling;

public static class IntakeArtifactFactory
{
    public static IntakeArtifact Normalize(IntakeSourceInput input)
    {
        ArgumentNullException.ThrowIfNull(input);

        Require(!string.IsNullOrWhiteSpace(input.Id), "artifact normalization", "id", "Stable input id is required.");
        Require(!string.IsNullOrWhiteSpace(input.Origin), "artifact normalization", "origin", "Input origin is required.");
        Require(!string.IsNullOrWhiteSpace(input.DisplayName), "artifact normalization", "display name", "Display name is required.");

        var normalizedMetadata = NormalizeMetadata(BuildMetadata(input));
        var type = DetermineType(input);
        var normalizedReference = BuildNormalizedReference(input.Id, type);
        var status = type == IntakeArtifactType.Unknown ? IntakeArtifactStatus.Unsupported : IntakeArtifactStatus.Normalized;

        return new IntakeArtifact(
            input.Id.Trim(),
            type,
            input.Origin.Trim(),
            input.DisplayName.Trim(),
            normalizedMetadata,
            normalizedReference,
            status);
    }

    public static IReadOnlyList<ArtifactMetadataEntry> NormalizeMetadata(IReadOnlyList<ArtifactMetadataEntry> metadata)
    {
        ArgumentNullException.ThrowIfNull(metadata);

        return metadata
            .Where(static entry => !string.IsNullOrWhiteSpace(entry.Key) && !string.IsNullOrWhiteSpace(entry.Value))
            .Select(static entry => new ArtifactMetadataEntry(entry.Key.Trim(), entry.Value.Trim()))
            .Distinct()
            .OrderBy(static entry => entry.Key, StringComparer.OrdinalIgnoreCase)
            .ThenBy(static entry => entry.Value, StringComparer.Ordinal)
            .ToArray();
    }

    private static IReadOnlyList<ArtifactMetadataEntry> BuildMetadata(IntakeSourceInput input)
    {
        var metadata = new List<ArtifactMetadataEntry>(input.Metadata ?? Array.Empty<ArtifactMetadataEntry>());

        if (!string.IsNullOrWhiteSpace(input.MediaType))
        {
            metadata.Add(new ArtifactMetadataEntry("media_type", input.MediaType.Trim()));
        }

        if (!string.IsNullOrWhiteSpace(input.FileExtension))
        {
            metadata.Add(new ArtifactMetadataEntry("file_extension", NormalizeExtension(input.FileExtension)));
        }

        if (!string.IsNullOrWhiteSpace(input.RawContentReference))
        {
            metadata.Add(new ArtifactMetadataEntry("content_reference", input.RawContentReference.Trim()));
        }

        if (!string.IsNullOrWhiteSpace(input.InlineText))
        {
            metadata.Add(new ArtifactMetadataEntry("inline_text", input.InlineText.Trim()));
        }

        return metadata;
    }

    public static IntakeArtifactType DetermineType(IntakeSourceInput input)
    {
        ArgumentNullException.ThrowIfNull(input);

        if (!string.IsNullOrWhiteSpace(input.InlineText))
        {
            return IntakeArtifactType.Text;
        }

        var mediaType = input.MediaType?.Trim().ToLowerInvariant();
        var extension = NormalizeExtension(input.FileExtension ?? input.DisplayName);

        if (mediaType == "application/pdf" || extension == ".pdf")
        {
            return IntakeArtifactType.Pdf;
        }

        if (mediaType is "application/zip" or "application/x-zip-compressed" or "application/x-7z-compressed"
            || extension is ".zip" or ".7z" or ".rar" or ".tar" or ".gz")
        {
            return IntakeArtifactType.Archive;
        }

        if (mediaType is "image/png" or "image/jpeg" or "image/webp" or "image/gif" or "image/bmp"
            || extension is ".png" or ".jpg" or ".jpeg" or ".webp" or ".gif" or ".bmp")
        {
            return IntakeArtifactType.Image;
        }

        if (mediaType == "text/plain" || extension is ".txt" or ".md" or ".json" or ".xml" or ".yaml" or ".yml")
        {
            return IntakeArtifactType.Text;
        }

        if (mediaType is "application/msword" or "application/vnd.openxmlformats-officedocument.wordprocessingml.document" or "application/rtf"
            || extension is ".doc" or ".docx" or ".rtf" or ".odt")
        {
            return IntakeArtifactType.Document;
        }

        return IntakeArtifactType.Unknown;
    }

    private static string? BuildNormalizedReference(string id, IntakeArtifactType type)
    {
        if (type == IntakeArtifactType.Unknown)
        {
            return null;
        }

        return $"normalized://{type.ToString().ToLowerInvariant()}/{id.Trim()}";
    }

    private static string NormalizeExtension(string rawValue)
    {
        if (string.IsNullOrWhiteSpace(rawValue))
        {
            return string.Empty;
        }

        var trimmed = rawValue.Trim();
        if (trimmed.StartsWith(".", StringComparison.Ordinal))
        {
            return trimmed.ToLowerInvariant();
        }

        var dotIndex = trimmed.LastIndexOf('.');
        return dotIndex >= 0 ? trimmed[dotIndex..].ToLowerInvariant() : string.Empty;
    }

    private static void Require(bool condition, string area, string missingRequirement, string reason)
    {
        if (!condition)
        {
            throw new ToolingException(area, missingRequirement, reason);
        }
    }
}
