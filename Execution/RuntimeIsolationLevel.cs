namespace zavod.Execution;

public enum RuntimeIsolationLevel
{
    None = 0,
    ScopedWorkspace = 1,
    Container = 2,
    VirtualMachine = 3,
    RemoteEphemeral = 4
}
