using System;
using zavod.Execution;
using zavod.Workspace;

namespace zavod.Tooling;

public sealed class PdfReadTool(PdfExtractionRuntimeService? runtimeService = null) : IPdfReadTool
{
    private readonly PdfExtractionRuntimeService _runtimeService = runtimeService ?? new PdfExtractionRuntimeService();

    public ToolExecutionResult Execute(PdfReadRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        Require(request.Artifact.Type == IntakeArtifactType.Pdf, "pdf read", "artifact type", "PdfReadTool requires pdf artifact.");

        var runtimeResult = _runtimeService.Prepare(new MaterialRuntimeRequest(
            request.Artifact.DisplayName,
            MaterialRuntimeToolResultBuilder.FindContentReference(request.Artifact),
            WorkspaceMaterialKind.PdfDocument,
            "tool:intake.pdf.read",
            MaxChars: 800));

        return runtimeResult.Status == MaterialRuntimeStatus.Prepared
            ? MaterialRuntimeToolResultBuilder.BuildSuccess(request.Artifact, runtimeResult, "pdf_extract")
            : MaterialRuntimeToolResultBuilder.BuildFailure(request.Artifact, runtimeResult);
    }

    private static void Require(bool condition, string area, string missingRequirement, string reason)
    {
        if (!condition)
        {
            throw new ToolingException(area, missingRequirement, reason);
        }
    }
}
