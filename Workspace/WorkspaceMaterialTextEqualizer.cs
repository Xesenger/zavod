using System;
using System.IO;

namespace zavod.Workspace;

public static class WorkspaceMaterialTextEqualizer
{
    public static WorkspaceMaterialTextExtract Build(
        string workspaceRoot,
        WorkspaceMaterialPreviewCandidate candidate,
        int maxCharsPerMaterial)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workspaceRoot);
        ArgumentNullException.ThrowIfNull(candidate);

        if (maxCharsPerMaterial <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxCharsPerMaterial), maxCharsPerMaterial, "Preview char cap must be positive.");
        }

        if (!SupportsTextExtraction(candidate.Kind))
        {
            return new WorkspaceMaterialTextExtract(
                candidate.RelativePath,
                candidate.Kind,
                candidate.SelectionReason,
                WorkspaceMaterialTextExtractStatus.UnsupportedKind,
                string.Empty,
                false,
                "unsupported-kind");
        }

        var sensitiveReason = WorkspaceSensitiveFilePolicy.GetSensitiveReason(candidate.RelativePath);
        if (!string.IsNullOrWhiteSpace(sensitiveReason))
        {
            return new WorkspaceMaterialTextExtract(
                candidate.RelativePath,
                candidate.Kind,
                candidate.SelectionReason,
                WorkspaceMaterialTextExtractStatus.SensitiveSkipped,
                string.Empty,
                false,
                sensitiveReason);
        }

        var fullPath = Path.Combine(workspaceRoot, candidate.RelativePath);
        if (!File.Exists(fullPath))
        {
            return new WorkspaceMaterialTextExtract(
                candidate.RelativePath,
                candidate.Kind,
                candidate.SelectionReason,
                WorkspaceMaterialTextExtractStatus.MissingFile,
                string.Empty,
                false,
                "missing-file");
        }

        var content = File.ReadAllText(fullPath);
        var normalized = NormalizePreviewText(content);
        if (normalized.Length == 0)
        {
            return new WorkspaceMaterialTextExtract(
                candidate.RelativePath,
                candidate.Kind,
                candidate.SelectionReason,
                WorkspaceMaterialTextExtractStatus.EmptyText,
                string.Empty,
                false,
                "empty-text");
        }

        var wasTruncated = normalized.Length > maxCharsPerMaterial;
        var preview = wasTruncated
            ? normalized[..maxCharsPerMaterial]
            : normalized;

        return new WorkspaceMaterialTextExtract(
            candidate.RelativePath,
            candidate.Kind,
            candidate.SelectionReason,
            WorkspaceMaterialTextExtractStatus.Extracted,
            preview,
            wasTruncated,
            "extracted");
    }

    private static bool SupportsTextExtraction(WorkspaceMaterialKind kind)
    {
        return kind == WorkspaceMaterialKind.TextDocument;
    }

    private static string NormalizePreviewText(string content)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return string.Empty;
        }

        return string.Join(
            ' ',
            content.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
    }
}
