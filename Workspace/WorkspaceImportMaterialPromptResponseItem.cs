namespace zavod.Workspace;

public sealed record WorkspaceImportMaterialPromptResponseItem(
    string RelativePath,
    WorkspaceMaterialContextUsefulness PossibleUsefulness,
    string Summary,
    WorkspaceMaterialTemporalStatus TemporalStatus,
    string StatusNote);
