using System.Collections.Generic;

namespace zavod.Workspace;

public sealed record WorkspaceEvidencePack(
    WorkspaceScanRun ScanRun,
    IReadOnlyList<WorkspaceEvidencePredicate> PredicateRegistry,
    WorkspaceScanBudgetReport? ScanBudget,
    WorkspaceProjectProfile ProjectProfile,
    WorkspaceEvidenceTopology Topology,
    // Transitional legacy field. Technical passport is a UX summary, not authoritative scanner truth.
    WorkspaceTechnicalPassport TechnicalPassport,
    // Transitional legacy field. Tree summary is no longer scanner-authored truth.
    string TreeSummary,
    IReadOnlyList<WorkspaceEvidenceObservation> RawObservations,
    IReadOnlyList<WorkspaceEvidencePattern> DerivedPatterns,
    IReadOnlyList<WorkspaceEvidenceSignalScore> SignalScores,
    IReadOnlyList<WorkspaceEvidenceFileIndexItem> FileIndex,
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

public sealed record WorkspaceScanRun(
    string ScanRunId,
    // Compatibility property name: value is a structural scan fingerprint,
    // not a content-integrity hash of the repository root.
    string RepoRootHash,
    string ScannerVersion,
    IReadOnlyDictionary<string, string> ExtractorVersions,
    string StartedAtUtc,
    string CompletedAtUtc,
    string Mode);

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
    int IgnoredNoiseFileCount,
    IReadOnlyList<string> SourceRoots,
    IReadOnlyList<string> BuildRoots,
    IReadOnlyList<string> StructuralAnomalies);

public sealed record WorkspaceEvidenceTopology(
    string Kind,
    string SafeImportMode,
    IReadOnlyList<WorkspaceEvidenceTopologyZone> ObservedZones,
    IReadOnlyList<string> LikelyActiveSourceRoots,
    IReadOnlyList<string> ReleaseOutputZones,
    IReadOnlyList<string> IgnoredNoiseZones,
    IReadOnlyList<string> UncertaintyReasons,
    WorkspaceEvidenceMarker? EvidenceMarker = null);

public sealed record WorkspaceEvidenceTopologyZone(
    string Root,
    string Role,
    int FileCount,
    IReadOnlyList<string> Evidence,
    WorkspaceEvidenceConfidenceLevel Confidence,
    WorkspaceEvidenceMarker? EvidenceMarker = null);

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
    string Note,
    int Score,
    IReadOnlyList<string> Evidence,
    WorkspaceEvidenceMarker? EvidenceMarker = null);

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
    string EvidenceNote,
    WorkspaceEvidenceMarker? EvidenceMarker = null);

public sealed record WorkspaceEvidenceDependencyEdge(
    string From,
    string To,
    string Label,
    string Reason,
    string? EvidencePath = null,
    WorkspaceEvidenceEdgeResolution Resolution = WorkspaceEvidenceEdgeResolution.Unknown,
    WorkspaceEvidenceMarker? EvidenceMarker = null);

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
    string? EvidencePath = null,
    string Id = "",
    string DisplayId = "",
    string Predicate = "",
    string Source = "",
    string ExtractorVersion = "");

public sealed record WorkspaceEvidencePattern(
    string Code,
    string Reason,
    IReadOnlyList<string> EvidencePaths);

public sealed record WorkspaceEvidenceSignalScore(
    string Signal,
    double Score);

public sealed record WorkspaceEvidenceMarker(
    string EvidenceKind,
    string? SourcePath,
    string Reason,
    WorkspaceEvidenceConfidenceLevel Confidence,
    bool IsPartial,
    bool IsBounded);

public sealed record WorkspaceEvidenceFileIndexItem(
    string RelativePath,
    string Extension,
    long SizeBytes,
    string Zone,
    string Role,
    bool IsSensitive,
    string MaterialKind,
    string Evidence);

public enum WorkspaceEvidenceConfidenceLevel
{
    Unknown = 0,
    Likely = 1,
    Confirmed = 2,
    Conflict = 3
}

public enum WorkspaceEvidenceEdgeResolution
{
    Unknown = 0,
    Lexical = 1,
    Resolved = 2,
    Ambiguous = 3,
    Unresolved = 4,
    Manifest = 5
}

public sealed record WorkspaceEvidenceCandidates(
    IReadOnlyList<WorkspaceEvidenceEntryPoint> EntryPoints,
    IReadOnlyList<WorkspaceEvidenceModule> ModuleCandidates,
    IReadOnlyList<WorkspaceEvidenceFileRole> FileRoles,
    IReadOnlyList<WorkspaceEvidenceProjectUnit> ProjectUnits,
    IReadOnlyList<WorkspaceEvidenceRunProfile> RunProfiles);

public sealed record WorkspaceEvidenceProjectUnit(
    string Id,
    string RootPath,
    string Kind,
    IReadOnlyList<string> Manifests,
    IReadOnlyList<string> EntryPoints,
    WorkspaceEvidenceConfidenceLevel Confidence,
    IReadOnlyList<string> Evidence,
    WorkspaceEvidenceMarker? EvidenceMarker = null);

public sealed record WorkspaceEvidenceRunProfile(
    string Id,
    string Kind,
    string Command,
    string WorkingDirectory,
    string SourcePath,
    WorkspaceEvidenceConfidenceLevel Confidence,
    IReadOnlyList<string> Evidence,
    WorkspaceEvidenceMarker? EvidenceMarker = null);

public sealed record WorkspaceEvidenceCodeEdge(
    string FromPath,
    string ToPath,
    string Kind,
    string Reason,
    WorkspaceEvidenceEdgeResolution Resolution = WorkspaceEvidenceEdgeResolution.Unknown,
    WorkspaceEvidenceMarker? EvidenceMarker = null);

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
    string Reason,
    WorkspaceEvidenceMarker? EvidenceMarker = null);

public sealed record WorkspaceEvidenceHotspot(
    string Code,
    string RelativePath,
    string Reason,
    WorkspaceEvidenceMarker? EvidenceMarker = null);

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
    string ScanRunPath,
    string ProjectProfilePath,
    string ProjectReportPath,
    string ScanSummaryPath,
    string FilesIndexPath,
    string ManifestsIndexPath,
    string SymbolsIndexPath,
    string EdgesIndexPath,
    string EntryPointsIndexPath,
    string ModulesMapPath,
    string ProjectUnitsIndexPath,
    string RunProfilesIndexPath,
    string TopologyIndexPath,
    string PredicateRegistryPath,
    string ScanBudgetPath,
    string UncertaintyReportPath,
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
