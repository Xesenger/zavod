using zavod.Prompting;

namespace zavod.Orchestration;

// Metadata carried alongside the assembled prompt. Work Packet
// fields (CanonicalDocsStatus, PreviewStatus, IsFirstCycle) are
// observable in the output so downstream surfaces can honor
// source stage and preview markers per project_work_packet_v1.md.
public sealed record PromptAssemblyMetadata(
    string ShiftId,
    string TaskId,
    int AnchorCount,
    PromptTruthMode TruthMode,
    CanonicalDocsStatus? CanonicalDocsStatus = null,
    PreviewStatus? PreviewStatus = null,
    bool IsFirstCycle = false);

public sealed record PromptAssemblyResult(
    string FinalPrompt,
    PromptRole Role,
    PromptTruthMode TruthMode,
    PromptAssemblyMetadata Metadata);
