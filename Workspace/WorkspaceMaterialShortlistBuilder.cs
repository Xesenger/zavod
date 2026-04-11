using System;
using System.Collections.Generic;
using System.Linq;

namespace zavod.Workspace;

public static class WorkspaceMaterialShortlistBuilder
{
    public const int DefaultMaxCandidates = 12;

    public static IReadOnlyList<WorkspaceMaterialPreviewCandidate> Build(
        WorkspaceScanResult scanResult,
        int maxCandidates = DefaultMaxCandidates)
    {
        ArgumentNullException.ThrowIfNull(scanResult);
        if (maxCandidates <= 0)
        {
            return Array.Empty<WorkspaceMaterialPreviewCandidate>();
        }

        return scanResult.MaterialCandidates
            .Where(static material => IsPreviewEligible(material.Kind))
            .OrderBy(static material => GetKindPriority(material.Kind))
            .ThenBy(static material => GetPathDepth(material.RelativePath))
            .ThenBy(static material => material.RelativePath, StringComparer.OrdinalIgnoreCase)
            .Take(maxCandidates)
            .Select(static material => new WorkspaceMaterialPreviewCandidate(
                material.RelativePath,
                material.Kind,
                GetSelectionReason(material)))
            .ToArray();
    }

    private static bool IsPreviewEligible(WorkspaceMaterialKind kind)
    {
        return kind is WorkspaceMaterialKind.TextDocument
            or WorkspaceMaterialKind.PdfDocument
            or WorkspaceMaterialKind.OfficeDocument
            or WorkspaceMaterialKind.Spreadsheet
            or WorkspaceMaterialKind.Presentation;
    }

    private static int GetKindPriority(WorkspaceMaterialKind kind)
    {
        return kind switch
        {
            WorkspaceMaterialKind.TextDocument => 0,
            WorkspaceMaterialKind.PdfDocument => 1,
            WorkspaceMaterialKind.OfficeDocument => 2,
            WorkspaceMaterialKind.Spreadsheet => 3,
            WorkspaceMaterialKind.Presentation => 4,
            _ => int.MaxValue
        };
    }

    private static int GetPathDepth(string relativePath)
    {
        return relativePath.Count(static ch => ch is '\\' or '/');
    }

    private static string GetSelectionReason(WorkspaceMaterialCandidate material)
    {
        return material.Kind switch
        {
            WorkspaceMaterialKind.TextDocument => "text-first-preview",
            WorkspaceMaterialKind.PdfDocument => "pdf-preview",
            WorkspaceMaterialKind.OfficeDocument => "office-preview",
            WorkspaceMaterialKind.Spreadsheet => "spreadsheet-preview",
            WorkspaceMaterialKind.Presentation => "presentation-preview",
            _ => "not-preview-eligible"
        };
    }
}
