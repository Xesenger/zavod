using System;
using zavod.Execution;

namespace zavod.Workspace;

public sealed class WorkspaceImportMaterialInterpreterRuntime(
    WorkspaceMaterialRuntimeFront? runtimeFront = null,
    IOpenRouterExecutionClient? openRouterExecutionClient = null,
    WorkspaceEvidenceArtifactRuntimeService? artifactRuntimeService = null)
{
    private readonly WorkspaceMaterialRuntimeFront _runtimeFront = runtimeFront ?? new WorkspaceMaterialRuntimeFront();
    private readonly IOpenRouterExecutionClient _openRouterExecutionClient = openRouterExecutionClient ?? new OpenRouterExecutionClient();
    private readonly WorkspaceEvidenceArtifactRuntimeService _artifactRuntimeService = artifactRuntimeService ?? new WorkspaceEvidenceArtifactRuntimeService();

    public WorkspaceImportMaterialInterpreterRunResult Interpret(
        WorkspaceScanResult scanResult,
        int maxMaterials = WorkspaceMaterialRuntimeFront.DefaultMaxMaterials,
        int maxCharsPerMaterial = WorkspaceMaterialRuntimeFront.DefaultMaxCharsPerMaterial,
        bool writeArtifacts = true)
    {
        ArgumentNullException.ThrowIfNull(scanResult);

        var previewPacket = _runtimeFront.BuildPreviewPacket(scanResult, maxMaterials, maxCharsPerMaterial);
        var promptRequest = WorkspaceImportMaterialPromptRequestBuilder.Build(previewPacket);
        var executionRequest = new OpenRouterExecutionRequest("workspace.import.interpreter", promptRequest.SystemPrompt, promptRequest.UserPrompt);
        var executionResponse = _openRouterExecutionClient.Execute(executionRequest);
        var interpretation = executionResponse.Success
            ? WorkspaceImportMaterialInterpretationResultBuilder.BuildFromResponse(previewPacket, WorkspaceImportMaterialPromptResponseParser.Parse(executionResponse.Content))
            : WorkspaceImportMaterialInterpretationResultBuilder.BuildEmpty(previewPacket);
        WorkspaceEvidenceArtifactBundle? artifactBundle = null;
        if (executionResponse.Success && writeArtifacts)
        {
            artifactBundle = _artifactRuntimeService.WriteBundle(new WorkspaceImportMaterialInterpreterRunResult(
                previewPacket,
                promptRequest,
                executionRequest,
                executionResponse,
                interpretation,
                null,
                string.Empty));
        }

        var summaryLine = executionResponse.Success
            ? $"Import interpreter runtime completed: materials={previewPacket.Materials.Count}, truth=context_only, upstream=OpenRouter."
            : $"Import interpreter runtime degraded honestly: materials={previewPacket.Materials.Count}, truth=context_only, upstream_failure={executionResponse.Diagnostic?.Code ?? "unknown"}.";

        return new WorkspaceImportMaterialInterpreterRunResult(
            previewPacket,
            promptRequest,
            executionRequest,
            executionResponse,
            interpretation,
            artifactBundle,
            summaryLine);
    }
}
