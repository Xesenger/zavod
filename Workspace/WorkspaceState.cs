using System;
using System.Collections.Generic;

namespace zavod.Workspace;

public sealed record WorkspaceState(
    string WorkspaceRoot,
    WorkspaceHealthStatus Health,
    WorkspaceDriftStatus DriftStatus,
    WorkspaceImportKind ImportKind,
    DateTimeOffset LastScanAt,
    WorkspaceChangeSummary Summary,
    IReadOnlyList<WorkspaceStructuralAnomaly> StructuralAnomalies,
    bool HasRecognizableProjectStructure,
    bool HasSourceFiles,
    bool HasBuildFiles);
