using System;

namespace zavod.Execution;

public sealed record WorkspaceBoundaryService(
    RuntimeAccessMode AccessMode,
    bool EnforcesContainedPaths,
    bool DetectsExternalDrift,
    bool AllowsWritesInsideWorkspaceOnly,
    string Summary)
{
    public WorkspaceBoundaryService Normalize()
    {
        return this with { Summary = Summary.Trim() };
    }

    public void Validate()
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(Summary);
    }
}
