using System;
using System.Collections.Generic;
using System.Linq;

namespace zavod.Tooling;

public sealed class DocumentImportTool : IDocumentImportTool
{
    public ToolExecutionResult Execute(DocumentImportRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        return Execute(new IntakeMaterialsRequest(request.RequestId, new[] { request.Input }));
    }

    public ToolExecutionResult Execute(IntakeMaterialsRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        Require(request.Inputs is { Count: > 0 }, "document import", "inputs", "At least one intake input is required.");

        var artifacts = request.Inputs
            .Select(IntakeArtifactFactory.Normalize)
            .ToArray();

        var unsupported = artifacts.Where(static artifact => artifact.Type == IntakeArtifactType.Unknown).ToArray();
        var warnings = unsupported
            .Select(static artifact => new ToolWarning("UNSUPPORTED_ARTIFACT_TYPE", $"Unsupported artifact type for '{artifact.DisplayName}'."))
            .ToArray();

        var outputs = artifacts
            .Where(static artifact => artifact.Status == IntakeArtifactStatus.Normalized)
            .Select(static artifact => new ToolOutputItem(
                $"OUTPUT-{artifact.Id}",
                "normalized_artifact",
                $"Normalized {artifact.Type.ToString().ToLowerInvariant()} artifact.",
                artifact.NormalizedContentReference!))
            .ToArray();

        var supportedCount = artifacts.Length - unsupported.Length;
        var success = unsupported.Length == 0;
        var summary = $"Normalized {supportedCount} artifact(s); unsupported {unsupported.Length}.";

        return new ToolExecutionResult(
            success,
            summary,
            artifacts,
            outputs,
            warnings,
            unsupported.Length == 0 ? null : new ToolDiagnostic("IMPORT_PARTIAL", "One or more inputs could not be classified into supported artifact types."));
    }

    private static void Require(bool condition, string area, string missingRequirement, string reason)
    {
        if (!condition)
        {
            throw new ToolingException(area, missingRequirement, reason);
        }
    }
}
