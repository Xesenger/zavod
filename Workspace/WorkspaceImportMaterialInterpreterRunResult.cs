using zavod.Execution;

namespace zavod.Workspace;

public sealed record WorkspaceImportMaterialInterpreterRunResult(
    WorkspaceImportMaterialPreviewPacket PreviewPacket,
    WorkspaceImportMaterialPromptRequest PromptRequest,
    OpenRouterExecutionRequest ExecutionRequest,
    OpenRouterExecutionResponse ExecutionResponse,
    WorkspaceImportMaterialInterpretationResult Interpretation,
    WorkspaceEvidenceArtifactBundle? ArtifactBundle,
    string SummaryLine);
