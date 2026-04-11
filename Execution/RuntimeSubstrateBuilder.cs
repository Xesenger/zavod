using System;

namespace zavod.Execution;

public static class RuntimeSubstrateBuilder
{
    public static RuntimeSubstrate Build(RuntimeProfile runtimeProfile)
    {
        ArgumentNullException.ThrowIfNull(runtimeProfile);

        runtimeProfile.Validate();
        runtimeProfile = runtimeProfile.Normalize();

        var workspaceBoundary = BuildWorkspaceBoundary(runtimeProfile);
        var executionRuntime = BuildExecutionRuntime(runtimeProfile);
        var networkBroker = BuildNetworkBroker(runtimeProfile);
        var artifactStore = BuildArtifactStore(runtimeProfile);
        var artifactQuarantine = BuildArtifactQuarantine(runtimeProfile);
        var evidenceStore = BuildEvidenceStore(runtimeProfile);
        var hostGovernanceSummary =
            $"Host governance remains outside the tool plane: windowsFirst=true, trustedOnly={runtimeProfile.TrustedOnly}, sandbox={runtimeProfile.UsesSandbox}, family={runtimeProfile.Family}.";
        var summaryLine =
            $"Runtime substrate assembled: profile={runtimeProfile.ProfileId}, family={runtimeProfile.Family}, workspace={workspaceBoundary.AccessMode}, network={networkBroker.AccessMode}, quarantine={artifactQuarantine.Enabled}.";

        var substrate = new RuntimeSubstrate(
            workspaceBoundary,
            executionRuntime,
            networkBroker,
            artifactStore,
            artifactQuarantine,
            evidenceStore,
            hostGovernanceSummary,
            summaryLine);

        substrate.Validate();
        return substrate.Normalize();
    }

    private static WorkspaceBoundaryService BuildWorkspaceBoundary(RuntimeProfile runtimeProfile)
    {
        return runtimeProfile.Family switch
        {
            RuntimeFamily.LocalUnsafe => new WorkspaceBoundaryService(
                RuntimeAccessMode.TrustedHostEscape,
                EnforcesContainedPaths: false,
                DetectsExternalDrift: true,
                AllowsWritesInsideWorkspaceOnly: false,
                "Trusted local runtime can observe workspace drift but may escape workspace boundaries by policy."),
            RuntimeFamily.ScopedLocalWorkspace => new WorkspaceBoundaryService(
                RuntimeAccessMode.WorkspaceOnly,
                EnforcesContainedPaths: true,
                DetectsExternalDrift: true,
                AllowsWritesInsideWorkspaceOnly: true,
                "Scoped local runtime is limited to workspace-contained paths and keeps external drift visible."),
            RuntimeFamily.Container => new WorkspaceBoundaryService(
                RuntimeAccessMode.WorkspaceOnly,
                EnforcesContainedPaths: true,
                DetectsExternalDrift: true,
                AllowsWritesInsideWorkspaceOnly: true,
                "Container runtime keeps mounted workspace scope explicit and rejects path escape."),
            RuntimeFamily.VmOrSandbox => new WorkspaceBoundaryService(
                RuntimeAccessMode.WorkspaceOnly,
                EnforcesContainedPaths: true,
                DetectsExternalDrift: true,
                AllowsWritesInsideWorkspaceOnly: true,
                "VM or sandbox runtime keeps workspace projection explicit and contained."),
            RuntimeFamily.Remote => new WorkspaceBoundaryService(
                RuntimeAccessMode.BrokeredAllowlist,
                EnforcesContainedPaths: true,
                DetectsExternalDrift: true,
                AllowsWritesInsideWorkspaceOnly: true,
                "Remote runtime operates on brokered workspace materialization only."),
            _ => throw new InvalidOperationException($"Unsupported runtime family for workspace boundary: {runtimeProfile.Family}.")
        };
    }

    private static ExecutionRuntimeService BuildExecutionRuntime(RuntimeProfile runtimeProfile)
    {
        return new ExecutionRuntimeService(
            runtimeProfile,
            ProcessTreeSupervisionRequired: true,
            RestrictedTokenPlanned: runtimeProfile.Family is not RuntimeFamily.LocalUnsafe,
            DetachedExecutionSupported: runtimeProfile.Family is RuntimeFamily.Remote,
            WindowsFirstHostReality: true,
            $"Execution runtime is Windows-first and keeps process-tree control explicit for profile '{runtimeProfile.ProfileId}'.");
    }

    private static NetworkBrokerService BuildNetworkBroker(RuntimeProfile runtimeProfile)
    {
        return runtimeProfile.Family switch
        {
            RuntimeFamily.LocalUnsafe => new NetworkBrokerService(
                RuntimeAccessMode.TrustedHostEscape,
                RequiresHostApproval: true,
                UsesAllowlist: false,
                RecordsAuditTrail: true,
                "Trusted local runtime may reach host network only through explicit host governance."),
            RuntimeFamily.ScopedLocalWorkspace => new NetworkBrokerService(
                RuntimeAccessMode.DenyByDefault,
                RequiresHostApproval: true,
                UsesAllowlist: true,
                RecordsAuditTrail: true,
                "Scoped local runtime denies network by default and expects brokered approval for exceptions."),
            RuntimeFamily.Container => new NetworkBrokerService(
                RuntimeAccessMode.BrokeredAllowlist,
                RequiresHostApproval: true,
                UsesAllowlist: true,
                RecordsAuditTrail: true,
                "Container runtime exposes network only through a brokered allowlist."),
            RuntimeFamily.VmOrSandbox => new NetworkBrokerService(
                RuntimeAccessMode.BrokeredAllowlist,
                RequiresHostApproval: true,
                UsesAllowlist: true,
                RecordsAuditTrail: true,
                "VM or sandbox runtime keeps network behind an allowlisted broker path."),
            RuntimeFamily.Remote => new NetworkBrokerService(
                RuntimeAccessMode.BrokeredAllowlist,
                RequiresHostApproval: true,
                UsesAllowlist: true,
                RecordsAuditTrail: true,
                "Remote runtime requires host-governed broker approval for network reachability."),
            _ => throw new InvalidOperationException($"Unsupported runtime family for network broker: {runtimeProfile.Family}.")
        };
    }

    private static ArtifactStoreService BuildArtifactStore(RuntimeProfile runtimeProfile)
    {
        var accessMode = runtimeProfile.Family == RuntimeFamily.LocalUnsafe
            ? RuntimeAccessMode.TrustedHostEscape
            : RuntimeAccessMode.WorkspaceOnly;

        return new ArtifactStoreService(
            accessMode,
            StoresImmutableReferences: true,
            KeepsWorkspaceAndTruthSeparated: true,
            PrefersWorkspaceLocalResidency: runtimeProfile.Family is not RuntimeFamily.Remote,
            $"Artifact store keeps runtime outputs separate from truth for profile '{runtimeProfile.ProfileId}'.");
    }

    private static ArtifactQuarantineService BuildArtifactQuarantine(RuntimeProfile runtimeProfile)
    {
        return new ArtifactQuarantineService(
            Enabled: runtimeProfile.Family is not RuntimeFamily.LocalUnsafe,
            PromotionRequiresReview: true,
            SuspiciousArtifactsStayOutOfTruth: true,
            $"Artifact quarantine is {(runtimeProfile.Family is RuntimeFamily.LocalUnsafe ? "soft" : "active")} for profile '{runtimeProfile.ProfileId}'.");
    }

    private static EvidenceStoreService BuildEvidenceStore(RuntimeProfile runtimeProfile)
    {
        return new EvidenceStoreService(
            StoresDecisionPackets: true,
            CapturesRuntimeProfile: true,
            CapturesNetworkAccess: true,
            KeepsEvidenceSeparateFromTruth: true,
            $"Evidence store records runtime family/profile and brokered side effects for '{runtimeProfile.ProfileId}'.");
    }
}
