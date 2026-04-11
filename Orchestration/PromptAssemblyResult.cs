using zavod.Prompting;

namespace zavod.Orchestration;

public sealed record PromptAssemblyMetadata(
    string ShiftId,
    string TaskId,
    int AnchorCount,
    PromptTruthMode TruthMode);

public sealed record PromptAssemblyResult(
    string FinalPrompt,
    PromptRole Role,
    PromptTruthMode TruthMode,
    PromptAssemblyMetadata Metadata);
