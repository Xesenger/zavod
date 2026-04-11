using System;
using System.Collections.Generic;
using System.Linq;
using zavod.Execution;
using zavod.Prompting;
using zavod.Retrieval;
using zavod.Tooling;

namespace zavod.Orchestration;

public sealed class UnifiedToolLayer(
    IDocumentImportTool documentImportTool,
    IPdfReadTool pdfReadTool,
    IArchiveTool archiveTool,
    IImageIntakeTool imageIntakeTool,
    IWebSearchTool webSearchTool,
    IWorkspaceTool workspaceTool)
{
    public static UnifiedToolLayer CreateDefault()
    {
        return new UnifiedToolLayer(
            new DocumentImportTool(),
            new PdfReadTool(),
            new ArchiveTool(),
            new ImageIntakeTool(),
            new WebSearchTool(),
            new WorkspaceTool());
    }

    public IReadOnlyList<ResolvedTool> ListVisibleWorkerTools(
        WorkerCapabilityProfile capabilityProfile,
        RuntimeProfile? runtimeProfile = null)
    {
        return WorkerToolResolver.ListVisibleTools(capabilityProfile, runtimeProfile);
    }

    public IReadOnlyList<ResolvedTool> ListVisibleToolsForRole(
        PromptRole requestedBy,
        RuntimeProfile? runtimeProfile = null)
    {
        RequireToolPlaneRole(requestedBy, "tool discovery");
        return RoleToolResolver.ListVisibleTools(requestedBy, runtimeProfile);
    }

    public ToolExecutionResult ImportMaterials(PromptRole requestedBy, IntakeMaterialsRequest request)
    {
        RequireExecutionRole(requestedBy, "material import");
        ArgumentNullException.ThrowIfNull(request);
        return ExecuteThroughRoute(requestedBy, "intake.document.import", () => documentImportTool.Execute(request)).Result;
    }

    public ToolExecutionEnvelope ImportMaterialsWithEnvelope(PromptRole requestedBy, IntakeMaterialsRequest request)
    {
        RequireExecutionRole(requestedBy, "material import");
        ArgumentNullException.ThrowIfNull(request);
        return ExecuteThroughRoute(requestedBy, "intake.document.import", () => documentImportTool.Execute(request));
    }

    public ToolExecutionResult ImportDocument(PromptRole requestedBy, DocumentImportRequest request)
    {
        RequireExecutionRole(requestedBy, "document import");
        ArgumentNullException.ThrowIfNull(request);
        return ExecuteThroughRoute(requestedBy, "intake.document.import", () => documentImportTool.Execute(request)).Result;
    }

    public ToolExecutionEnvelope ImportDocumentWithEnvelope(PromptRole requestedBy, DocumentImportRequest request)
    {
        RequireExecutionRole(requestedBy, "document import");
        ArgumentNullException.ThrowIfNull(request);
        return ExecuteThroughRoute(requestedBy, "intake.document.import", () => documentImportTool.Execute(request));
    }

    public ToolExecutionResult ReadPdf(PromptRole requestedBy, PdfReadRequest request)
    {
        RequireExecutionRole(requestedBy, "pdf read");
        ArgumentNullException.ThrowIfNull(request);
        return ExecuteThroughRoute(requestedBy, "intake.pdf.read", () => pdfReadTool.Execute(request)).Result;
    }

    public ToolExecutionEnvelope ReadPdfWithEnvelope(PromptRole requestedBy, PdfReadRequest request)
    {
        RequireExecutionRole(requestedBy, "pdf read");
        ArgumentNullException.ThrowIfNull(request);
        return ExecuteThroughRoute(requestedBy, "intake.pdf.read", () => pdfReadTool.Execute(request));
    }

    public ToolExecutionResult InspectArchive(PromptRole requestedBy, ArchiveInspectRequest request)
    {
        RequireExecutionRole(requestedBy, "archive inspect");
        ArgumentNullException.ThrowIfNull(request);
        return ExecuteThroughRoute(requestedBy, "intake.archive.inspect", () => archiveTool.Execute(request)).Result;
    }

    public ToolExecutionEnvelope InspectArchiveWithEnvelope(PromptRole requestedBy, ArchiveInspectRequest request)
    {
        RequireExecutionRole(requestedBy, "archive inspect");
        ArgumentNullException.ThrowIfNull(request);
        return ExecuteThroughRoute(requestedBy, "intake.archive.inspect", () => archiveTool.Execute(request));
    }

    public ToolExecutionResult InspectImage(PromptRole requestedBy, ImageIntakeRequest request)
    {
        RequireExecutionRole(requestedBy, "image intake");
        ArgumentNullException.ThrowIfNull(request);
        return ExecuteThroughRoute(requestedBy, "intake.image.inspect", () => imageIntakeTool.Execute(request)).Result;
    }

    public ToolExecutionEnvelope InspectImageWithEnvelope(PromptRole requestedBy, ImageIntakeRequest request)
    {
        RequireExecutionRole(requestedBy, "image intake");
        ArgumentNullException.ThrowIfNull(request);
        return ExecuteThroughRoute(requestedBy, "intake.image.inspect", () => imageIntakeTool.Execute(request));
    }

    public ToolExecutionResult PerformWebSearch(PromptRole requestedBy, WebSearchRequest request)
    {
        RequireToolPlaneRole(requestedBy, "web search");
        ArgumentNullException.ThrowIfNull(request);
        return ExecuteThroughRoute(requestedBy, "web.search", () => webSearchTool.Execute(request)).Result;
    }

    public ToolExecutionEnvelope PerformWebSearchWithEnvelope(PromptRole requestedBy, WebSearchRequest request)
    {
        RequireToolPlaneRole(requestedBy, "web search");
        ArgumentNullException.ThrowIfNull(request);
        return ExecuteThroughRoute(requestedBy, "web.search", () => webSearchTool.Execute(request));
    }

    public ToolExecutionResult InspectWorkspace(PromptRole requestedBy, WorkspaceInspectRequest request)
    {
        RequireToolPlaneRole(requestedBy, "workspace inspect");
        ArgumentNullException.ThrowIfNull(request);
        return ExecuteThroughRoute(requestedBy, "workspace.inspect", () => workspaceTool.Execute(request)).Result;
    }

    public ToolExecutionEnvelope InspectWorkspaceWithEnvelope(PromptRole requestedBy, WorkspaceInspectRequest request)
    {
        RequireToolPlaneRole(requestedBy, "workspace inspect");
        ArgumentNullException.ThrowIfNull(request);
        return ExecuteThroughRoute(requestedBy, "workspace.inspect", () => workspaceTool.Execute(request));
    }

    public ArtifactInventory BuildInventory(PromptRole requestedBy, IntakeArtifact artifact)
    {
        RequireToolPlaneRole(requestedBy, "inventory build");
        ArgumentNullException.ThrowIfNull(artifact);
        return ArtifactInventoryBuilder.Build(artifact);
    }

    public RetrievalResult Retrieve(PromptRole requestedBy, RetrievalRequest request)
    {
        RequireToolPlaneRole(requestedBy, "retrieval");
        ArgumentNullException.ThrowIfNull(request);

        var inventories = request.TargetArtifacts
            .Select(ArtifactInventoryBuilder.Build)
            .ToArray();

        return BasicCandidateSelector.Select(inventories, request);
    }

    public ScopedContext BuildScopedContext(PromptRole requestedBy, RetrievalResult retrievalResult)
    {
        RequireToolPlaneRole(requestedBy, "scoped context build");
        ArgumentNullException.ThrowIfNull(retrievalResult);
        return ScopedContextBuilder.Build(retrievalResult);
    }

    public ScopedContext RetrieveScopedContext(PromptRole requestedBy, RetrievalRequest request)
    {
        var retrievalResult = Retrieve(requestedBy, request);
        return BuildScopedContext(requestedBy, retrievalResult);
    }

    private static void RequireExecutionRole(PromptRole role, string operation)
    {
        if (role is not (PromptRole.ShiftLead or PromptRole.Worker))
        {
            throw new ToolingException("tool orchestration", "request role", $"{operation} is allowed only for Shift Lead or Worker in the execution tool seam.");
        }
    }

    private static void RequireToolPlaneRole(PromptRole role, string operation)
    {
        if (role is not (PromptRole.ShiftLead or PromptRole.Worker or PromptRole.Qc or PromptRole.SeniorSpecialist))
        {
            throw new ToolingException("tool orchestration", "request role", $"{operation} is allowed only for role-bound tool-plane access.");
        }
    }

    private static ResolvedTool ResolveRoute(PromptRole role, string toolName)
    {
        return RoleToolResolver.ResolveRequired(role, toolName);
    }

    private static ToolExecutionEnvelope ExecuteThroughRoute(
        PromptRole role,
        string toolName,
        Func<ToolExecutionResult> executor)
    {
        ArgumentNullException.ThrowIfNull(executor);

        var route = ResolveRoute(role, toolName);
        route.Validate();
        var result = executor();
        var evidenceSummary = BuildEvidenceSummary(role, route, result);
        var summary =
            $"Tool execution envelope: role={role}, tool={route.ToolName}, success={result.Success}, outputs={result.ExtractedItems.Count}, artifacts={result.ProducedArtifacts.Count}.";

        var envelope = new ToolExecutionEnvelope(route, result, evidenceSummary, summary);
        envelope.Validate();
        return envelope.Normalize();
    }

    private static string BuildEvidenceSummary(PromptRole role, ResolvedTool route, ToolExecutionResult result)
    {
        var diagnosticCode = result.Diagnostics?.Code ?? "none";
        return
            $"Evidence: role={role}, tool={route.ToolName}, capability={route.Route.CapabilityProfile}, runtime={route.Route.RuntimeProfile.ProfileId}, network={route.Route.RuntimeSubstrate.NetworkBroker.AccessMode}, approval={route.Route.RequiresAdditionalApproval}, success={result.Success}, diagnostic={diagnosticCode}.";
    }

}
