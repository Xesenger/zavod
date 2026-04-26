using System;
using System.Net.Http;
using zavod.Execution;

namespace zavod.Workspace;

public sealed class WorkspaceImportMaterialInterpreterRuntime
{
    private readonly WorkspaceMaterialRuntimeFront _runtimeFront;
    private readonly IOpenRouterExecutionClient? _openRouterExecutionClient;
    private readonly WorkspaceEvidenceArtifactRuntimeService _artifactRuntimeService;
    private readonly RoleProfile _profile;
    private readonly Func<RoleProfile, IOpenRouterExecutionClient> _clientFactory;

    public WorkspaceImportMaterialInterpreterRuntime(
        WorkspaceMaterialRuntimeFront? runtimeFront = null,
        IOpenRouterExecutionClient? openRouterExecutionClient = null,
        WorkspaceEvidenceArtifactRuntimeService? artifactRuntimeService = null,
        ModelRoutingConfiguration? modelRoutingConfiguration = null,
        Func<RoleProfile, IOpenRouterExecutionClient>? clientFactory = null)
    {
        _runtimeFront = runtimeFront ?? new WorkspaceMaterialRuntimeFront();
        _openRouterExecutionClient = openRouterExecutionClient;
        _artifactRuntimeService = artifactRuntimeService ?? new WorkspaceEvidenceArtifactRuntimeService();
        _profile = (modelRoutingConfiguration ?? ModelRoutingConfiguration.LoadOrDefault()).Importer;
        _clientFactory = clientFactory ?? DefaultClientFactory;
    }

    public WorkspaceImportMaterialInterpreterRunResult Interpret(
        WorkspaceScanResult scanResult,
        int maxMaterials = WorkspaceMaterialRuntimeFront.DefaultMaxMaterials,
        int maxCharsPerMaterial = WorkspaceMaterialRuntimeFront.DefaultMaxCharsPerMaterial,
        bool writeArtifacts = true)
    {
        ArgumentNullException.ThrowIfNull(scanResult);

        var previewPacket = _runtimeFront.BuildPreviewPacket(scanResult, maxMaterials, maxCharsPerMaterial);
        var promptRequest = WorkspaceImportMaterialPromptRequestBuilder.Build(previewPacket);
        var executionRequest = new OpenRouterExecutionRequest(
            "workspace.import.interpreter",
            promptRequest.SystemPrompt,
            promptRequest.UserPrompt,
            _profile.Model,
            _profile.Temperature,
            Attachments: null,
            MaxTokens: _profile.MaxTokens);
        var client = _openRouterExecutionClient ?? _clientFactory(_profile);
        var executionResponse = client.Execute(executionRequest);
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

    private static IOpenRouterExecutionClient DefaultClientFactory(RoleProfile profile)
    {
        var configuration = OpenRouterConfiguration.FromEnvironment();
        var httpClient = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(profile.TimeoutSeconds)
        };
        return new OpenRouterExecutionClient(configuration, httpClient, allowEnvironmentFallback: false);
    }
}
