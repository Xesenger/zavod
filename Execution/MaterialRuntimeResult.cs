using System;
using System.Collections.Generic;
using zavod.Workspace;

namespace zavod.Execution;

public sealed record MaterialRuntimeResult(
    string DisplayPath,
    WorkspaceMaterialKind Kind,
    string SelectionReason,
    MaterialRuntimeStatus Status,
    string BackendId,
    bool FallbackUsed,
    string ExtractedText,
    bool WasTruncated,
    IReadOnlyList<string> Warnings,
    MaterialRuntimeDiagnostic? Diagnostic,
    string EvidenceSummary,
    string Summary)
{
    public MaterialRuntimeResult Normalize()
    {
        return new MaterialRuntimeResult(
            DisplayPath?.Trim() ?? string.Empty,
            Kind,
            SelectionReason?.Trim() ?? string.Empty,
            Status,
            BackendId?.Trim() ?? string.Empty,
            FallbackUsed,
            ExtractedText?.Trim() ?? string.Empty,
            WasTruncated,
            Warnings ?? Array.Empty<string>(),
            Diagnostic,
            EvidenceSummary?.Trim() ?? string.Empty,
            Summary?.Trim() ?? string.Empty);
    }
}
