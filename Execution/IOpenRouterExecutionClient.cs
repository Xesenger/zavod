namespace zavod.Execution;

public interface IOpenRouterExecutionClient
{
    OpenRouterExecutionResponse Execute(OpenRouterExecutionRequest request);
}
