using System;

namespace zavod.Execution;

public sealed record ArtifactStoreService(
    RuntimeAccessMode AccessMode,
    bool StoresImmutableReferences,
    bool KeepsWorkspaceAndTruthSeparated,
    bool PrefersWorkspaceLocalResidency,
    string Summary)
{
    public ArtifactStoreService Normalize()
    {
        return this with { Summary = Summary.Trim() };
    }

    public void Validate()
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(Summary);
    }
}
