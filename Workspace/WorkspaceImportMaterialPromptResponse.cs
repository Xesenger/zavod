using System;
using System.Collections.Generic;

namespace zavod.Workspace;

public sealed record WorkspaceImportMaterialPromptResponse(
    string Summary,
    IReadOnlyList<string> Details,
    IReadOnlyList<string> ConfirmedSignals,
    IReadOnlyList<string> LikelySignals,
    IReadOnlyList<string> UnknownSignals,
    IReadOnlyList<string> StageSignals,
    IReadOnlyList<string> CurrentSignals,
    IReadOnlyList<string> PlannedSignals,
    IReadOnlyList<string> PossiblyStaleSignals,
    IReadOnlyList<string> Conflicts,
    IReadOnlyList<WorkspaceImportMaterialLayerInterpretation> Layers,
    IReadOnlyList<WorkspaceImportMaterialModuleInterpretation> Modules,
    IReadOnlyList<WorkspaceImportMaterialEntryPointInterpretation> EntryPoints,
    ArchitectureDiagramSpec DiagramSpec,
    IReadOnlyList<WorkspaceImportMaterialPromptResponseItem> Materials)
{
    public WorkspaceImportMaterialPromptResponse(
        string summary,
        IReadOnlyList<string> details,
        IReadOnlyList<string> stageSignals,
        IReadOnlyList<string> currentSignals,
        IReadOnlyList<string> plannedSignals,
        IReadOnlyList<string> possiblyStaleSignals,
        IReadOnlyList<string> conflicts,
        IReadOnlyList<WorkspaceImportMaterialLayerInterpretation> layers,
        IReadOnlyList<WorkspaceImportMaterialModuleInterpretation> modules,
        IReadOnlyList<WorkspaceImportMaterialEntryPointInterpretation> entryPoints,
        ArchitectureDiagramSpec diagramSpec,
        IReadOnlyList<WorkspaceImportMaterialPromptResponseItem> materials)
        : this(
            summary,
            details,
            Array.Empty<string>(),
            Array.Empty<string>(),
            Array.Empty<string>(),
            stageSignals,
            currentSignals,
            plannedSignals,
            possiblyStaleSignals,
            conflicts,
            layers,
            modules,
            entryPoints,
            diagramSpec,
            materials)
    {
    }
}
