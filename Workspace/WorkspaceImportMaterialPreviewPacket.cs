using System.Collections.Generic;

namespace zavod.Workspace;

public sealed record WorkspaceImportMaterialPreviewPacket(
    string WorkspaceRoot,
    WorkspaceImportKind ImportKind,
    IReadOnlyList<string> SourceRoots,
    IReadOnlyList<WorkspaceTechnicalPreviewInput> TechnicalEvidence,
    IReadOnlyList<WorkspaceMaterialPreviewInput> Materials,
    WorkspaceEvidencePack? EvidencePack = null);
