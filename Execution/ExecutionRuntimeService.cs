using System;

namespace zavod.Execution;

public sealed record ExecutionRuntimeService(
    RuntimeProfile RuntimeProfile,
    bool ProcessTreeSupervisionRequired,
    bool RestrictedTokenPlanned,
    bool DetachedExecutionSupported,
    bool WindowsFirstHostReality,
    string Summary)
{
    public ExecutionRuntimeService Normalize()
    {
        RuntimeProfile.Validate();
        return this with
        {
            RuntimeProfile = RuntimeProfile.Normalize(),
            Summary = Summary.Trim()
        };
    }

    public void Validate()
    {
        RuntimeProfile.Validate();
        ArgumentException.ThrowIfNullOrWhiteSpace(Summary);
    }
}
