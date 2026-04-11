using System;

namespace zavod.Execution;

public sealed record RuntimeSubstrate(
    WorkspaceBoundaryService WorkspaceBoundary,
    ExecutionRuntimeService ExecutionRuntime,
    NetworkBrokerService NetworkBroker,
    ArtifactStoreService ArtifactStore,
    ArtifactQuarantineService ArtifactQuarantine,
    EvidenceStoreService EvidenceStore,
    string HostGovernanceSummary,
    string SummaryLine)
{
    public RuntimeSubstrate Normalize()
    {
        ArgumentNullException.ThrowIfNull(WorkspaceBoundary);
        ArgumentNullException.ThrowIfNull(ExecutionRuntime);
        ArgumentNullException.ThrowIfNull(NetworkBroker);
        ArgumentNullException.ThrowIfNull(ArtifactStore);
        ArgumentNullException.ThrowIfNull(ArtifactQuarantine);
        ArgumentNullException.ThrowIfNull(EvidenceStore);

        return this with
        {
            WorkspaceBoundary = WorkspaceBoundary.Normalize(),
            ExecutionRuntime = ExecutionRuntime.Normalize(),
            NetworkBroker = NetworkBroker.Normalize(),
            ArtifactStore = ArtifactStore.Normalize(),
            ArtifactQuarantine = ArtifactQuarantine.Normalize(),
            EvidenceStore = EvidenceStore.Normalize(),
            HostGovernanceSummary = HostGovernanceSummary.Trim(),
            SummaryLine = SummaryLine.Trim()
        };
    }

    public void Validate()
    {
        WorkspaceBoundary.Validate();
        ExecutionRuntime.Validate();
        NetworkBroker.Validate();
        ArtifactStore.Validate();
        ArtifactQuarantine.Validate();
        EvidenceStore.Validate();
        ArgumentException.ThrowIfNullOrWhiteSpace(HostGovernanceSummary);
        ArgumentException.ThrowIfNullOrWhiteSpace(SummaryLine);
    }
}
