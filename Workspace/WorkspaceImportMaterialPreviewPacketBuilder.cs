using System;
using System.Collections.Generic;
using System.Linq;

namespace zavod.Workspace;

public static class WorkspaceImportMaterialPreviewPacketBuilder
{
    public const int DefaultMaxMaterials = 6;
    public const int DefaultMaxCharsPerMaterial = 800;

    public static WorkspaceImportMaterialPreviewPacket Build(
        WorkspaceScanResult scanResult,
        int maxMaterials = DefaultMaxMaterials,
        int maxCharsPerMaterial = DefaultMaxCharsPerMaterial)
    {
        ArgumentNullException.ThrowIfNull(scanResult);
        if (maxMaterials <= 0 || maxCharsPerMaterial <= 0)
        {
            return new WorkspaceImportMaterialPreviewPacket(
                scanResult.State.WorkspaceRoot,
                scanResult.State.ImportKind,
                scanResult.State.Summary.SourceRoots,
                Array.Empty<WorkspaceTechnicalPreviewInput>(),
                Array.Empty<WorkspaceMaterialPreviewInput>());
        }

        var workspaceRoot = scanResult.State.WorkspaceRoot;
        var materials = WorkspaceMaterialShortlistBuilder.Build(scanResult, maxMaterials)
            .Select(candidate => WorkspaceMaterialTextEqualizer.Build(workspaceRoot, candidate, maxCharsPerMaterial))
            .Where(static extract => extract.Status == WorkspaceMaterialTextExtractStatus.Extracted)
            .Select(static extract => new WorkspaceMaterialPreviewInput(
                extract.RelativePath,
                extract.Kind,
                extract.SelectionReason,
                extract.PreviewText,
                extract.WasTruncated))
            .ToArray();

        return new WorkspaceImportMaterialPreviewPacket(
            workspaceRoot,
            scanResult.State.ImportKind,
            scanResult.State.Summary.SourceRoots,
            Array.Empty<WorkspaceTechnicalPreviewInput>(),
            materials);
    }
}
