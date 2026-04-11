using System;
using zavod.Execution;
using zavod.Workspace;

namespace zavod.Tooling;

public sealed class ImageIntakeTool(ImageInspectionRuntimeService? runtimeService = null) : IImageIntakeTool
{
    private readonly ImageInspectionRuntimeService _runtimeService = runtimeService ?? new ImageInspectionRuntimeService();

    public ToolExecutionResult Execute(ImageIntakeRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        Require(request.Artifact.Type == IntakeArtifactType.Image, "image intake", "artifact type", "ImageIntakeTool requires image artifact.");

        var runtimeResult = _runtimeService.Prepare(new MaterialRuntimeRequest(
            request.Artifact.DisplayName,
            MaterialRuntimeToolResultBuilder.FindContentReference(request.Artifact),
            WorkspaceMaterialKind.ImageAsset,
            "tool:intake.image.inspect",
            MaxChars: 800));

        return runtimeResult.Status == MaterialRuntimeStatus.Prepared
            ? MaterialRuntimeToolResultBuilder.BuildSuccess(request.Artifact, runtimeResult, "image_intake")
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
