using System.Collections.Generic;

namespace zavod.Tooling;

public sealed record ArtifactMetadataEntry(string Key, string Value);

public sealed record IntakeSourceInput(
    string Id,
    string Origin,
    string DisplayName,
    string? MediaType = null,
    string? FileExtension = null,
    string? RawContentReference = null,
    string? InlineText = null,
    IReadOnlyList<ArtifactMetadataEntry>? Metadata = null);

public sealed record IntakeArtifact(
    string Id,
    IntakeArtifactType Type,
    string Origin,
    string DisplayName,
    IReadOnlyList<ArtifactMetadataEntry> Metadata,
    string? NormalizedContentReference,
    IntakeArtifactStatus Status);

public sealed record ToolWarning(string Code, string Message);

public sealed record ToolDiagnostic(string Code, string Message);

public sealed record ToolOutputItem(
    string Id,
    string Kind,
    string Summary,
    string Reference);

public sealed record ToolExecutionResult(
    bool Success,
    string Summary,
    IReadOnlyList<IntakeArtifact> ProducedArtifacts,
    IReadOnlyList<ToolOutputItem> ExtractedItems,
    IReadOnlyList<ToolWarning> Warnings,
    ToolDiagnostic? Diagnostics = null);

public sealed record IntakeMaterialsRequest(
    string RequestId,
    IReadOnlyList<IntakeSourceInput> Inputs);

public sealed record PdfReadRequest(
    string RequestId,
    IntakeArtifact Artifact);

public sealed record ArchiveInspectRequest(
    string RequestId,
    IntakeArtifact Artifact);

public sealed record DocumentImportRequest(
    string RequestId,
    IntakeSourceInput Input);

public sealed record ImageIntakeRequest(
    string RequestId,
    IntakeArtifact Artifact);

public sealed record WebSearchRequest(
    string RequestId,
    string Query,
    int Limit = 5);

public sealed record WorkspaceInspectRequest(
    string RequestId,
    string WorkspaceRoot,
    IReadOnlyList<string>? IncludePaths = null);

public interface IWebSearchTool
{
    ToolExecutionResult Execute(WebSearchRequest request);
}

public interface IArchiveTool
{
    ToolExecutionResult Execute(ArchiveInspectRequest request);
}

public interface IPdfReadTool
{
    ToolExecutionResult Execute(PdfReadRequest request);
}

public interface IDocumentImportTool
{
    ToolExecutionResult Execute(DocumentImportRequest request);
    ToolExecutionResult Execute(IntakeMaterialsRequest request);
}

public interface IImageIntakeTool
{
    ToolExecutionResult Execute(ImageIntakeRequest request);
}

public interface IWorkspaceTool
{
    ToolExecutionResult Execute(WorkspaceInspectRequest request);
}
