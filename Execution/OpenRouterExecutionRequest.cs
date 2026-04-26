using System.Collections.Generic;

namespace zavod.Execution;

public sealed record OpenRouterExecutionRequest(
    string RouteId,
    string SystemPrompt,
    string UserPrompt,
    string? ModelId = null,
    double Temperature = 0,
    IReadOnlyList<OpenRouterAttachment>? Attachments = null,
    int? MaxTokens = null,
    bool ResponseFormatJsonObject = false,
    string? ReasoningEffort = null);
