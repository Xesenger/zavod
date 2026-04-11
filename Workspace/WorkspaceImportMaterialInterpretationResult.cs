using System;
using System.Collections.Generic;

namespace zavod.Workspace;

public sealed record WorkspaceImportMaterialInterpretationResult(
    string WorkspaceRoot,
    WorkspaceImportKind ImportKind,
    IReadOnlyList<string> SourceRoots,
    IReadOnlyList<string> ProjectDetails,
    IReadOnlyList<string> ConfirmedSignals,
    IReadOnlyList<string> LikelySignals,
    IReadOnlyList<string> UnknownSignals,
    IReadOnlyList<string> ProjectStageSignals,
    IReadOnlyList<string> CurrentSignals,
    IReadOnlyList<string> PlannedSignals,
    IReadOnlyList<string> PossiblyStaleSignals,
    IReadOnlyList<string> Conflicts,
    IReadOnlyList<WorkspaceImportMaterialLayerInterpretation> Layers,
    IReadOnlyList<WorkspaceImportMaterialModuleInterpretation> Modules,
    IReadOnlyList<WorkspaceImportMaterialEntryPointInterpretation> EntryPoints,
    ArchitectureDiagramSpec DiagramSpec,
    IReadOnlyList<WorkspaceMaterialPreviewInterpretation> Materials,
    string SummaryLine)
{
    public ProjectInterpretationMode InterpretationMode { get; init; } = ProjectInterpretationMode.SingleProject;

    // Compatibility shim for older tests that still pass an extra legacy summary/details slice.
    public WorkspaceImportMaterialInterpretationResult(
        string workspaceRoot,
        WorkspaceImportKind importKind,
        IReadOnlyList<string> sourceRoots,
        IReadOnlyList<string> legacySummaryHints,
        IReadOnlyList<string> projectDetails,
        IReadOnlyList<string> projectStageSignals,
        IReadOnlyList<string> currentSignals,
        IReadOnlyList<string> plannedSignals,
        IReadOnlyList<string> possiblyStaleSignals,
        IReadOnlyList<string> conflicts,
        IReadOnlyList<WorkspaceImportMaterialLayerInterpretation> layers,
        IReadOnlyList<WorkspaceImportMaterialEntryPointInterpretation> entryPoints,
        ArchitectureDiagramSpec diagramSpec,
        IReadOnlyList<WorkspaceMaterialPreviewInterpretation> materials,
        string summaryLine)
        : this(
            workspaceRoot,
            importKind,
            sourceRoots,
            projectDetails,
            Array.Empty<string>(),
            Array.Empty<string>(),
            Array.Empty<string>(),
            projectStageSignals,
            currentSignals,
            plannedSignals,
            possiblyStaleSignals,
            conflicts,
            layers,
            System.Array.Empty<WorkspaceImportMaterialModuleInterpretation>(),
            entryPoints,
            diagramSpec,
            materials,
            summaryLine)
    {
        _ = legacySummaryHints;
    }
}

public enum ProjectInterpretationMode
{
    SingleProject = 0,
    MultipleIndependentProjects = 1,
    AmbiguousContainer = 2
}
