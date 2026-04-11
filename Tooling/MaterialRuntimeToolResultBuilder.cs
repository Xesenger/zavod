using System;
using System.Linq;
using zavod.Execution;
using zavod.Workspace;

namespace zavod.Tooling;

internal static class MaterialRuntimeToolResultBuilder
{
    public static ToolExecutionResult BuildSuccess(IntakeArtifact artifact, MaterialRuntimeResult runtimeResult, string outputKind)
    {
        var outputs = new[]
        {
            new ToolOutputItem(
                $"OUTPUT-{artifact.Id}",
                outputKind,
                runtimeResult.Summary,
                artifact.NormalizedContentReference ?? $"runtime://{outputKind}/{artifact.Id}")
        };

        var warnings = runtimeResult.Warnings
            .Select(static warning => new ToolWarning("RUNTIME_WARNING", warning))
            .ToArray();

        return new ToolExecutionResult(
            Success: true,
            runtimeResult.Summary,
            new[] { artifact },
            outputs,
            warnings,
            null);
    }

    public static ToolExecutionResult BuildFailure(IntakeArtifact artifact, MaterialRuntimeResult runtimeResult)
    {
        return new ToolExecutionResult(
            Success: false,
            runtimeResult.Summary,
            new[] { artifact },
            Array.Empty<ToolOutputItem>(),
            runtimeResult.Warnings.Select(static warning => new ToolWarning("RUNTIME_WARNING", warning)).ToArray(),
            runtimeResult.Diagnostic is null ? null : new ToolDiagnostic(runtimeResult.Diagnostic.Code, runtimeResult.Diagnostic.Message));
    }

    public static string? FindContentReference(IntakeArtifact artifact)
    {
        ArgumentNullException.ThrowIfNull(artifact);
        return artifact.Metadata
            .FirstOrDefault(static entry => entry.Key.Equals("content_reference", StringComparison.OrdinalIgnoreCase))
            ?.Value;
    }
}
