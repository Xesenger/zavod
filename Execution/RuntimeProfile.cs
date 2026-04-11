using System;

namespace zavod.Execution;

public sealed record RuntimeProfile(
    string ProfileId,
    RuntimeFamily Family,
    RuntimeIsolationLevel Isolation,
    bool UsesSandbox,
    bool TrustedOnly,
    string Summary)
{
    public static RuntimeProfile ScopedLocalDefault { get; } = new(
        "scoped-local-default",
        RuntimeFamily.ScopedLocalWorkspace,
        RuntimeIsolationLevel.ScopedWorkspace,
        UsesSandbox: false,
        TrustedOnly: false,
        "Scoped local workspace runtime.");

    public RuntimeProfile Normalize()
    {
        return this with
        {
            ProfileId = ProfileId.Trim(),
            Summary = Summary.Trim()
        };
    }

    public void Validate()
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ProfileId);
        ArgumentException.ThrowIfNullOrWhiteSpace(Summary);
    }
}
