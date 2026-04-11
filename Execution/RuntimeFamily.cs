namespace zavod.Execution;

public enum RuntimeFamily
{
    LocalUnsafe = 0,
    ScopedLocalWorkspace = 1,
    Container = 2,
    VmOrSandbox = 3,
    Remote = 4
}
