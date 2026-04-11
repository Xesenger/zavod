using System.Collections.Generic;

namespace zavod.Workspace;

public sealed record WorkspaceEvidencePack(
    WorkspaceProjectProfile ProjectProfile,
    // Transitional legacy field. Technical passport is a UX summary, not authoritative scanner truth.
    WorkspaceTechnicalPassport TechnicalPassport,
    // Transitional legacy field. Tree summary is no longer scanner-authored truth.
    string TreeSummary,
    IReadOnlyList<WorkspaceEvidenceObservation> RawObservations,
    IReadOnlyList<WorkspaceEvidencePattern> DerivedPatterns,
    IReadOnlyList<WorkspaceEvidenceSignalScore> SignalScores,
    WorkspaceEvidenceCandidates Candidates,
    IReadOnlyList<WorkspaceEvidenceCodeEdge> CodeEdges,
    IReadOnlyList<WorkspaceEvidenceSignatureHint> SignatureHints,
    IReadOnlyList<WorkspaceEvidenceSymbolHint> SymbolHints,
    IReadOnlyList<WorkspaceEvidenceDependencySurfaceItem> DependencySurface,
    IReadOnlyList<WorkspaceEvidenceConfidenceAnnotation> ConfidenceAnnotations,
    // Authoritative cold edge surface. Legacy DependencyEdges mirrors this for transition only.
    IReadOnlyList<WorkspaceEvidenceDependencyEdge> Edges,
    IReadOnlyList<WorkspaceEvidenceHotspot> Hotspots,
    IReadOnlyList<WorkspaceEvidenceEntryPoint> EntryPoints,
    // Transitional legacy field. Human-facing layers are importer-owned.
    IReadOnlyList<WorkspaceEvidenceLayer> ObservedLayers,
    // Transitional legacy field. Authoritative cold modules live under Candidates.ModuleCandidates.
    IReadOnlyList<WorkspaceEvidenceModule> ModuleCandidates,
    // Transitional legacy field. Authoritative cold edges live under Edges.
    IReadOnlyList<WorkspaceEvidenceDependencyEdge> DependencyEdges,
    IReadOnlyList<WorkspaceEvidenceMaterial> Materials,
    IReadOnlyList<WorkspaceEvidenceSignal> Signals,
    IReadOnlyList<WorkspaceEvidenceSnippet> EvidenceSnippets);

public sealed record WorkspaceProjectProfile(
    string WorkspaceRoot,
    WorkspaceImportKind ImportKind,
    WorkspaceHealthStatus Health,
    WorkspaceDriftStatus DriftStatus,
    int RelevantFileCount,
    int SourceFileCount,
    int BuildFileCount,
    int ConfigFileCount,
    int DocumentFileCount,
    int AssetFileCount,
    int BinaryFileCount,
    IReadOnlyList<string> SourceRoots,
    IReadOnlyList<string> BuildRoots,
    IReadOnlyList<string> StructuralAnomalies);

public sealed record WorkspaceTechnicalPassport(
    IReadOnlyList<string> ObservedLanguages,
    IReadOnlyList<string> BuildSystems,
    IReadOnlyList<string> Toolchains,
    IReadOnlyList<string> Frameworks,
    IReadOnlyList<string> VersionHints,
    IReadOnlyList<string> TargetPlatforms,
    IReadOnlyList<string> RuntimeSurfaces,
    IReadOnlyList<string> ConfigMarkers,
    IReadOnlyList<string> BuildVariants,
    IReadOnlyList<string> NotableOptions);

public sealed record WorkspaceEvidenceEntryPoint(
    string RelativePath,
    string Role,
    string Note);

public sealed record WorkspaceEvidenceLayer(
    string Name,
    string Root,
    string Responsibility,
    string BoundaryNote,
    string EvidenceNote);

public sealed record WorkspaceEvidenceModule(
    string Name,
    string Role,
    string LayerName,
    string EvidenceNote);

public sealed record WorkspaceEvidenceDependencyEdge(
    string From,
    string To,
    string Label,
    string Reason,
    string? EvidencePath = null);

public sealed record WorkspaceEvidenceMaterial(
    string RelativePath,
    WorkspaceMaterialKind Kind,
    string SelectionReason,
    string PreparationStatus,
    string BackendId,
    string EvidenceSummary,
    string PreviewText,
    bool WasTruncated);

public sealed record WorkspaceEvidenceSignal(
    string Category,
    string Code,
    string Reason,
    string? EvidencePath = null);

public sealed record WorkspaceEvidenceObservation(
    string Kind,
    string Value,
    string? EvidencePath = null);

public sealed record WorkspaceEvidencePattern(
    string Code,
    string Reason,
    IReadOnlyList<string> EvidencePaths);

public sealed record WorkspaceEvidenceSignalScore(
    string Signal,
    double Score);

public enum WorkspaceEvidenceConfidenceLevel
{
    Unknown = 0,
    Likely = 1,
    Confirmed = 2
}

public sealed record WorkspaceEvidenceCandidates(
    IReadOnlyList<WorkspaceEvidenceEntryPoint> EntryPoints,
    IReadOnlyList<WorkspaceEvidenceModule> ModuleCandidates,
    IReadOnlyList<WorkspaceEvidenceFileRole> FileRoles);

public sealed record WorkspaceEvidenceCodeEdge(
    string FromPath,
    string ToPath,
    string Kind,
    string Reason);

public sealed record WorkspaceEvidenceSignatureHint(
    string RelativePath,
    string Kind,
    string Signature,
    string Reason);

public sealed record WorkspaceEvidenceSymbolHint(
    string RelativePath,
    string Symbol,
    string Kind,
    string Reason);

public sealed record WorkspaceEvidenceDependencySurfaceItem(
    string Name,
    string SourcePath,
    string Scope,
    string Kind);

public sealed record WorkspaceEvidenceConfidenceAnnotation(
    string TargetKind,
    string TargetId,
    WorkspaceEvidenceConfidenceLevel Confidence,
    string Reason);

public sealed record WorkspaceEvidenceFileRole(
    string RelativePath,
    string Role,
    double Confidence,
    string Reason);

public sealed record WorkspaceEvidenceHotspot(
    string Code,
    string RelativePath,
    string Reason);

public sealed record WorkspaceEvidenceSnippet(
    string RelativePath,
    string Category,
    string PreviewText,
    bool WasTruncated);

public sealed record WorkspaceImportMaterialLayerInterpretation(
    string Name,
    string Responsibility,
    string EvidenceNote,
    WorkspaceEvidenceConfidenceLevel Confidence = WorkspaceEvidenceConfidenceLevel.Unknown);

public sealed record WorkspaceImportMaterialEntryPointInterpretation(
    string RelativePath,
    string Role,
    string Note,
    WorkspaceEvidenceConfidenceLevel Confidence = WorkspaceEvidenceConfidenceLevel.Unknown);

public sealed record WorkspaceImportMaterialModuleInterpretation(
    string Name,
    string Role,
    string EvidenceNote,
    WorkspaceEvidenceConfidenceLevel Confidence = WorkspaceEvidenceConfidenceLevel.Unknown);

public sealed record ArchitectureDiagramSpec(
    string Title,
    IReadOnlyList<ArchitectureDiagramNode> Nodes,
    IReadOnlyList<ArchitectureDiagramEdge> Edges,
    IReadOnlyList<ArchitectureDiagramGroup> Groups,
    IReadOnlyList<string> Notes,
    ArchitectureDiagramRenderHints RenderHints);

public sealed record ArchitectureDiagramNode(
    string Id,
    string Label,
    string Kind,
    string? GroupId = null,
    WorkspaceEvidenceConfidenceLevel Confidence = WorkspaceEvidenceConfidenceLevel.Unknown);

public sealed record ArchitectureDiagramEdge(
    string From,
    string To,
    string Label,
    string Kind,
    WorkspaceEvidenceConfidenceLevel Confidence = WorkspaceEvidenceConfidenceLevel.Unknown);

public sealed record ArchitectureDiagramGroup(
    string Id,
    string Label,
    IReadOnlyList<string> Members,
    WorkspaceEvidenceConfidenceLevel Confidence = WorkspaceEvidenceConfidenceLevel.Unknown);

public sealed record ArchitectureDiagramRenderHints(
    string LayoutDirection,
    IReadOnlyList<string> EmphasisNodes,
    bool ShowLegend);

public sealed record WorkspaceEvidenceArtifactBundle(
    string OutputDirectory,
    string PreviewPath,
    string PreviewProjectDocumentPath,
    string PreviewCapsuleDocumentPath,
    string ProjectProfilePath,
    string ProjectReportPath,
    string TechnicalPassportPath,
    string RawObservationsPath,
    string DerivedPatternsPath,
    string SignalScoresPath,
    string CodeEdgesPath,
    string SignatureHintsPath,
    string SymbolHintsPath,
    string DependencySurfacePath,
    string ConfidenceAnnotationsPath,
    string EntryPointsPath,
    string CandidatesPath,
    string LayerMapPath,
    string ModuleCandidatesPath,
    string DependencyEdgesPath,
    string HotspotsPath,
    string StageSignalsPath,
    string OriginDetectionPath,
    string EvidenceSnippetsPath,
    string DiagramSpecPath,
    string ArchitectureMapPath,
    string? ProjectReportPdfPath,
    string SummaryLine);
