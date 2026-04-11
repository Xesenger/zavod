using System;

namespace zavod.Execution;

public sealed record NetworkBrokerService(
    RuntimeAccessMode AccessMode,
    bool RequiresHostApproval,
    bool UsesAllowlist,
    bool RecordsAuditTrail,
    string Summary)
{
    public NetworkBrokerService Normalize()
    {
        return this with { Summary = Summary.Trim() };
    }

    public void Validate()
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(Summary);
    }
}
