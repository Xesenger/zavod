using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using zavod.Tooling;
using zavod.UI.Text;

namespace zavod.Persistence;

public sealed record ConversationArtifactReference(
    string ReferenceId,
    string Label,
    string Preview,
    string RelativePath,
    string FilePath);

public sealed class ConversationArtifactStorage
{
    private const int LogPreviewMaxLines = 12;
    private const int ArtifactPreviewMaxChars = 280;

    private readonly string _projectRootPath;

    public ConversationArtifactStorage(string projectRootPath)
    {
        _projectRootPath = Path.GetFullPath(projectRootPath);
    }

    public ConversationArtifactReference SaveLog(string fullText, string? preview = null, string? referenceId = null, string? label = null)
    {
        var resolvedReferenceId = string.IsNullOrWhiteSpace(referenceId)
            ? Guid.NewGuid().ToString("N")
            : referenceId.Trim();
        var filePath = Path.Combine(ZavodLocalStorageLayout.GetArtifactLogsRoot(_projectRootPath), $"{resolvedReferenceId}.log");
        return SaveReference(
            filePath,
            resolvedReferenceId,
            label ?? "Log",
            BuildLogPreview(fullText, preview),
            fullText ?? string.Empty);
    }

    public ConversationArtifactReference SaveArtifact(string label, string fullText, string extension, string? preview = null, string? referenceId = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(label);

        var resolvedReferenceId = string.IsNullOrWhiteSpace(referenceId)
            ? Guid.NewGuid().ToString("N")
            : referenceId.Trim();
        var normalizedExtension = NormalizeExtension(extension);
        var filePath = Path.Combine(ZavodLocalStorageLayout.GetArtifactsRoot(_projectRootPath), $"{resolvedReferenceId}{normalizedExtension}");
        return SaveReference(
            filePath,
            resolvedReferenceId,
            label.Trim(),
            BuildArtifactPreview(fullText, preview),
            fullText ?? string.Empty);
    }

    public ConversationArtifactReference SaveConversationTextArtifact(
        string conversationId,
        string label,
        string fullText,
        string extension,
        string? preview = null,
        string? referenceId = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(conversationId);
        ArgumentException.ThrowIfNullOrWhiteSpace(label);

        var resolvedReferenceId = string.IsNullOrWhiteSpace(referenceId)
            ? Guid.NewGuid().ToString("N")
            : referenceId.Trim();
        var normalizedExtension = NormalizeExtension(extension);
        var filePath = Path.Combine(
            ZavodLocalStorageLayout.GetConversationArtifactsRoot(_projectRootPath, conversationId),
            $"{resolvedReferenceId}{normalizedExtension}");
        return SaveReference(
            filePath,
            resolvedReferenceId,
            label.Trim(),
            BuildArtifactPreview(fullText, preview),
            fullText ?? string.Empty);
    }

    public ConversationArtifactReference ImportConversationFile(
        string conversationId,
        string sourceFilePath,
        string? displayName = null,
        string? preview = null,
        string? referenceId = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(conversationId);
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceFilePath);

        var normalizedSourceFilePath = Path.GetFullPath(sourceFilePath);
        if (!File.Exists(normalizedSourceFilePath))
        {
            throw new FileNotFoundException("Source artifact file was not found.", normalizedSourceFilePath);
        }

        var resolvedReferenceId = string.IsNullOrWhiteSpace(referenceId)
            ? Guid.NewGuid().ToString("N")
            : referenceId.Trim();
        var extension = NormalizeExtension(Path.GetExtension(normalizedSourceFilePath));
        var label = string.IsNullOrWhiteSpace(displayName)
            ? Path.GetFileName(normalizedSourceFilePath)
            : displayName.Trim();
        var filePath = Path.Combine(
            ZavodLocalStorageLayout.GetConversationArtifactsRoot(_projectRootPath, conversationId),
            $"{resolvedReferenceId}{extension}");

        ZavodLocalStorageLayout.EnsureInitialized(_projectRootPath);
        Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);
        File.Copy(normalizedSourceFilePath, filePath, overwrite: true);
        var relativePath = Path.GetRelativePath(ZavodLocalStorageLayout.GetRoot(_projectRootPath), filePath);
        return new ConversationArtifactReference(
            resolvedReferenceId,
            label,
            string.IsNullOrWhiteSpace(preview) ? BuildConversationFilePreview(label, extension) : preview.Trim(),
            relativePath,
            filePath);
    }

    public static IReadOnlyDictionary<string, string> BuildReferenceMetadata(
        string payloadKind,
        ConversationArtifactReference reference,
        IReadOnlyDictionary<string, string>? existing = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(payloadKind);
        ArgumentNullException.ThrowIfNull(reference);

        var metadata = existing is null
            ? new Dictionary<string, string>(StringComparer.Ordinal)
            : new Dictionary<string, string>(existing, StringComparer.Ordinal);
        metadata["payload-kind"] = payloadKind.Trim();
        metadata["reference-id"] = reference.ReferenceId;
        metadata["reference-label"] = reference.Label;
        metadata["reference-path"] = reference.FilePath;
        metadata["reference-relative-path"] = reference.RelativePath;
        return metadata;
    }

    public static IReadOnlyDictionary<string, string> BuildConversationInputMetadata(
        string conversationId,
        string? projectId,
        string origin,
        string intakeType,
        string displayName,
        ConversationArtifactReference reference,
        IReadOnlyDictionary<string, string>? existing = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(conversationId);
        ArgumentException.ThrowIfNullOrWhiteSpace(origin);
        ArgumentException.ThrowIfNullOrWhiteSpace(intakeType);
        ArgumentException.ThrowIfNullOrWhiteSpace(displayName);
        ArgumentNullException.ThrowIfNull(reference);

        var metadata = BuildReferenceMetadata("artifact", reference, existing).ToDictionary(static pair => pair.Key, static pair => pair.Value, StringComparer.Ordinal);
        metadata["conversation-id"] = conversationId.Trim();
        metadata["origin"] = origin.Trim();
        metadata["intake-type"] = intakeType.Trim();
        metadata["display-name"] = displayName.Trim();
        if (!string.IsNullOrWhiteSpace(projectId))
        {
            metadata["project-id"] = projectId.Trim();
        }

        return metadata;
    }

    public static string[] BuildAttachments(IReadOnlyDictionary<string, string>? metadata)
    {
        if (metadata is null || metadata.Count == 0)
        {
            return Array.Empty<string>();
        }

        return new[] { "file-path", "reference-path" }
            .Where(key => metadata.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value))
            .Select(key => metadata[key])
            .Distinct(StringComparer.Ordinal)
            .ToArray();
    }

    private ConversationArtifactReference SaveReference(string filePath, string referenceId, string label, string preview, string content)
    {
        ZavodLocalStorageLayout.EnsureInitialized(_projectRootPath);
        Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);
        File.WriteAllText(filePath, content, Encoding.UTF8);
        var relativePath = Path.GetRelativePath(ZavodLocalStorageLayout.GetRoot(_projectRootPath), filePath);
        return new ConversationArtifactReference(referenceId, label, preview, relativePath, filePath);
    }

    private static string NormalizeExtension(string extension)
    {
        if (string.IsNullOrWhiteSpace(extension))
        {
            return ".txt";
        }

        var trimmed = extension.Trim();
        return trimmed.StartsWith(".", StringComparison.Ordinal) ? trimmed : $".{trimmed}";
    }

    private static string BuildArtifactPreview(string fullText, string? preview)
    {
        if (!string.IsNullOrWhiteSpace(preview))
        {
            return preview.Trim();
        }

        var normalized = NormalizePreviewText(fullText);
        if (normalized.Length <= ArtifactPreviewMaxChars)
        {
            return normalized;
        }

        return $"{normalized[..(ArtifactPreviewMaxChars - 3)].TrimEnd()}...";
    }

    private static string BuildLogPreview(string fullText, string? preview)
    {
        if (!string.IsNullOrWhiteSpace(preview))
        {
            return preview.Trim();
        }

        var normalized = (fullText ?? string.Empty)
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n');
        var lines = normalized
            .Split('\n')
            .Where(line => line.Length > 0)
            .ToArray();

        if (lines.Length == 0)
        {
            return string.Empty;
        }

        var tail = lines
            .Skip(Math.Max(0, lines.Length - LogPreviewMaxLines))
            .ToArray();
        if (tail.Length == lines.Length)
        {
            return string.Join(Environment.NewLine, tail);
        }

        return $"...{Environment.NewLine}{string.Join(Environment.NewLine, tail)}";
    }

    private static string NormalizePreviewText(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : value
                .Replace("\r", " ", StringComparison.Ordinal)
                .Replace("\n", " ", StringComparison.Ordinal)
                .Trim();
    }

    private static string BuildConversationFilePreview(string label, string extension)
    {
        var type = IntakeArtifactFactory.DetermineType(new IntakeSourceInput(
            Guid.NewGuid().ToString("N"),
            "user_upload",
            label,
            FileExtension: extension));
        return type switch
        {
            IntakeArtifactType.Pdf => AppText.Current.Get("conversation.preview.pdf"),
            IntakeArtifactType.Archive => AppText.Current.Get("conversation.preview.archive"),
            IntakeArtifactType.Document => AppText.Current.Get("conversation.preview.document"),
            IntakeArtifactType.Image => AppText.Current.Get("conversation.preview.image"),
            _ => AppText.Current.Get("conversation.preview.file")
        };
    }
}
