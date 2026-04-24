namespace zavod.Workspace;

public enum WorkspaceHealthStatus
{
    Healthy,
    Missing,
    Unavailable,
    BrokenStructure,
    MaterialOnly,
    Degraded,
    ScanPending,
    ScanFailed
}
