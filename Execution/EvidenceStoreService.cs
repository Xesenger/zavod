using System;

namespace zavod.Execution;

public sealed record EvidenceStoreService(
    bool StoresDecisionPackets,
    bool CapturesRuntimeProfile,
    bool CapturesNetworkAccess,
    bool KeepsEvidenceSeparateFromTruth,
    string Summary)
{
    public EvidenceStoreService Normalize()
    {
        return this with { Summary = Summary.Trim() };
    }

    public void Validate()
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(Summary);
    }
}
