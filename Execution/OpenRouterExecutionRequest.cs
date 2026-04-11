namespace zavod.Execution;

public sealed record OpenRouterExecutionRequest(
    string RouteId,
    string SystemPrompt,
    string UserPrompt,
    string? ModelId = null,
    double Temperature = 0);
