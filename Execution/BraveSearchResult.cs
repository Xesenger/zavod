using System.Collections.Generic;

namespace zavod.Execution;

public sealed record BraveSearchItem(
    string Title,
    string Url,
    string Snippet);

public sealed record BraveSearchResult(
    bool Success,
    IReadOnlyList<BraveSearchItem> Items,
    BraveSearchDiagnostic? Diagnostic,
    int? StatusCode,
    string SummaryLine);
