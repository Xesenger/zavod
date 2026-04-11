namespace zavod.Tooling;

public enum ToolRiskTier
{
    ReadOnly = 0,
    WorkspaceWrite = 1,
    ExternalAccess = 2,
    TrustedHostEscape = 3
}
