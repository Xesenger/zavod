namespace zavod.Workspace;

public sealed record WorkspaceMaterialPreviewInterpretation(
    string RelativePath,
    WorkspaceMaterialKind Kind,
    string Summary,
    WorkspaceMaterialContextUsefulness PossibleUsefulness,
    WorkspaceMaterialTemporalStatus TemporalStatus,
    string StatusNote,
    bool ContextOnly,
    WorkspaceEvidenceConfidenceLevel Confidence = WorkspaceEvidenceConfidenceLevel.Unknown);
