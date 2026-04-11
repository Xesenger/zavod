using System;
using System.Collections.Generic;

namespace zavod.Tooling;

public sealed record BrokeredToolSource(
    string SourceId,
    string DisplayName,
    bool BuiltIn,
    bool RequiresHostGovernance,
    IReadOnlyList<ExternalToolCapability> Capabilities,
    string Summary)
{
    public BrokeredToolSource Normalize()
    {
        return this with
        {
            SourceId = SourceId.Trim(),
            DisplayName = DisplayName.Trim(),
            Summary = Summary.Trim()
        };
    }

    public void Validate()
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(SourceId);
        ArgumentException.ThrowIfNullOrWhiteSpace(DisplayName);
        ArgumentException.ThrowIfNullOrWhiteSpace(Summary);
    }
}
