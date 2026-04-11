namespace zavod.Orchestration;

internal static class PromptAssemblyOrchestrator
{
    public static PromptAssemblyResult Build(PromptRequestInput input)
    {
        return PromptRequestPipeline.Execute(input);
    }
}
