using System;
using zavod.Execution;
using zavod.Workspace;

namespace zavod.Tooling;

public sealed class ArchiveTool(ArchiveInspectionRuntimeService? runtimeService = null) : IArchiveTool
{
    private readonly ArchiveInspectionRuntimeService _runtimeService = runtimeService ?? new ArchiveInspectionRuntimeService();

    public ToolExecutionResult Execute(ArchiveInspectRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        Require(request.Artifact.Type == IntakeArtifactType.Archive, "archive inspect", "artifact type", "ArchiveTool requires archive artifact.");

        var runtimeResult = _runtimeService.Prepare(new MaterialRuntimeRequest(
            request.Artifact.DisplayName,
            MaterialRuntimeToolResultBuilder.FindContentReference(request.Artifact),
            WorkspaceMaterialKind.ArchiveArtifact,
            "tool:intake.archive.inspect",
            MaxChars: 800));

        return runtimeResult.Status == MaterialRuntimeStatus.Prepared
            ? MaterialRuntimeToolResultBuilder.BuildSuccess(request.Artifact, runtimeResult, "archive_manifest")
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
