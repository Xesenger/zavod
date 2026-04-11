namespace zavod.Execution;

public enum RuntimeAccessMode
{
    DenyByDefault = 0,
    WorkspaceOnly = 1,
    BrokeredAllowlist = 2,
    TrustedHostEscape = 3
}
