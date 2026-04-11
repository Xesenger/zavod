namespace zavod.Execution;

public sealed record OpenRouterExecutionResponse(
    bool Success,
    string Content,
    string ModelId,
    int? StatusCode,
    OpenRouterDiagnostic? Diagnostic,
    string SummaryLine);
