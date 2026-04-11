namespace zavod.Workspace;

public enum WorkspaceMaterialTemporalStatus
{
    Unknown = 0,
    Current = 1,
    Planned = 2,
    Historical = 3,
    PossiblyStale = 4,
    Conflicting = 5,
}
