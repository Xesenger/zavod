using System;

namespace zavod.Execution;

public interface IOpenRouterExecutionClient
{
    OpenRouterExecutionResponse Execute(OpenRouterExecutionRequest request);
}

public interface IOpenRouterStreamingExecutionClient : IOpenRouterExecutionClient
{
    OpenRouterExecutionResponse ExecuteStreaming(OpenRouterExecutionRequest request, Action<string> onContentDelta);
}
