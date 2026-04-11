using System;
using System.Collections.Generic;
using System.Linq;
using zavod.Execution;

namespace zavod.Tooling;

public static class TypedToolCatalog
{
    private static readonly IReadOnlyList<TypedToolContract> Contracts = new[]
    {
        new TypedToolContract(
            "workspace.inspect",
            nameof(WorkspaceInspectRequest),
            "Observe workspace reality through the workspace boundary service without mutating truth.",
            DefaultTimeoutSeconds: 15,
            RiskTier: ToolRiskTier.ReadOnly,
            ApprovalPolicy: ToolApprovalPolicy.RuntimePolicy,
            PreferredRuntimeFamily: RuntimeFamily.ScopedLocalWorkspace,
            RequiresWorkspaceBoundary: true,
            RequiresNetworkBroker: false,
            RequiresArtifactQuarantine: false,
            EmitsEvidence: true,
            HostGoverned: false,
            Capabilities: new[] { ExternalToolCapability.WorkspaceRead, ExternalToolCapability.EvidenceEmission }),
        new TypedToolContract(
            "intake.document.import",
            nameof(DocumentImportRequest),
            "Normalize incoming document material into bounded intake artifacts under host-governed runtime policy.",
            DefaultTimeoutSeconds: 30,
            RiskTier: ToolRiskTier.WorkspaceWrite,
            ApprovalPolicy: ToolApprovalPolicy.RuntimePolicy,
            PreferredRuntimeFamily: RuntimeFamily.ScopedLocalWorkspace,
            RequiresWorkspaceBoundary: true,
            RequiresNetworkBroker: false,
            RequiresArtifactQuarantine: true,
            EmitsEvidence: true,
            HostGoverned: true,
            Capabilities: new[] { ExternalToolCapability.WorkspaceRead, ExternalToolCapability.WorkspaceWrite, ExternalToolCapability.ArtifactExtraction, ExternalToolCapability.EvidenceEmission }),
        new TypedToolContract(
            "intake.pdf.read",
            nameof(PdfReadRequest),
            "Read PDF artifacts through the bounded extraction seam and quarantine produced extracts until reviewed.",
            DefaultTimeoutSeconds: 30,
            RiskTier: ToolRiskTier.WorkspaceWrite,
            ApprovalPolicy: ToolApprovalPolicy.RuntimePolicy,
            PreferredRuntimeFamily: RuntimeFamily.ScopedLocalWorkspace,
            RequiresWorkspaceBoundary: true,
            RequiresNetworkBroker: false,
            RequiresArtifactQuarantine: true,
            EmitsEvidence: true,
            HostGoverned: true,
            Capabilities: new[] { ExternalToolCapability.WorkspaceRead, ExternalToolCapability.ArtifactExtraction, ExternalToolCapability.EvidenceEmission }),
        new TypedToolContract(
            "intake.archive.inspect",
            nameof(ArchiveInspectRequest),
            "Inspect archive artifacts through runtime-backed hands without promoting contents into truth automatically.",
            DefaultTimeoutSeconds: 45,
            RiskTier: ToolRiskTier.WorkspaceWrite,
            ApprovalPolicy: ToolApprovalPolicy.RuntimePolicy,
            PreferredRuntimeFamily: RuntimeFamily.Container,
            RequiresWorkspaceBoundary: true,
            RequiresNetworkBroker: false,
            RequiresArtifactQuarantine: true,
            EmitsEvidence: true,
            HostGoverned: true,
            Capabilities: new[] { ExternalToolCapability.WorkspaceRead, ExternalToolCapability.ArtifactExtraction, ExternalToolCapability.EvidenceEmission }),
        new TypedToolContract(
            "intake.image.inspect",
            nameof(ImageIntakeRequest),
            "Prepare bounded image intake artifacts while keeping richer understanding separate from truth.",
            DefaultTimeoutSeconds: 30,
            RiskTier: ToolRiskTier.WorkspaceWrite,
            ApprovalPolicy: ToolApprovalPolicy.RuntimePolicy,
            PreferredRuntimeFamily: RuntimeFamily.ScopedLocalWorkspace,
            RequiresWorkspaceBoundary: true,
            RequiresNetworkBroker: false,
            RequiresArtifactQuarantine: true,
            EmitsEvidence: true,
            HostGoverned: true,
            Capabilities: new[] { ExternalToolCapability.WorkspaceRead, ExternalToolCapability.ArtifactExtraction, ExternalToolCapability.EvidenceEmission }),
        new TypedToolContract(
            "web.search",
            nameof(WebSearchRequest),
            "Reach external web search only through the network broker and explicit host governance.",
            DefaultTimeoutSeconds: 20,
            RiskTier: ToolRiskTier.ExternalAccess,
            ApprovalPolicy: ToolApprovalPolicy.HostApprovalRequired,
            PreferredRuntimeFamily: RuntimeFamily.Container,
            RequiresWorkspaceBoundary: false,
            RequiresNetworkBroker: true,
            RequiresArtifactQuarantine: false,
            EmitsEvidence: true,
            HostGoverned: true,
            Capabilities: new[] { ExternalToolCapability.NetworkAccess, ExternalToolCapability.EvidenceEmission })
    }
    .Select(static contract => contract.Normalize())
    .ToArray();

    public static IReadOnlyList<TypedToolContract> ListAll() => Contracts;

    public static TypedToolContract? TryGet(string toolName)
    {
        if (string.IsNullOrWhiteSpace(toolName))
        {
            return null;
        }

        return Contracts.FirstOrDefault(contract =>
            string.Equals(contract.ToolName, toolName.Trim(), StringComparison.OrdinalIgnoreCase));
    }

    public static TypedToolContract GetRequired(string toolName)
    {
        var contract = TryGet(toolName);
        if (contract is null)
        {
            throw new InvalidOperationException($"Unknown typed tool contract: {toolName}.");
        }

        return contract;
    }

    public static ExternalToolGovernancePolicy BuildGovernancePolicy(string toolName)
    {
        var contract = GetRequired(toolName);
        var source = new BrokeredToolSource(
            SourceId: contract.ToolName,
            DisplayName: contract.ToolName,
            BuiltIn: true,
            RequiresHostGovernance: contract.HostGoverned,
            Capabilities: contract.Capabilities,
            Summary: $"Brokered source for typed tool '{contract.ToolName}'.");

        var policy = new ExternalToolGovernancePolicy(
            source,
            contract.ApprovalPolicy,
            DenyNetworkByDefault: contract.RequiresNetworkBroker,
            RequiresAuditTrail: contract.EmitsEvidence,
            AllowsExecutionInsideTrustedRuntimeOnly: contract.RiskTier == ToolRiskTier.TrustedHostEscape,
            Summary: $"Tool '{contract.ToolName}' is governed above substrate with approval={contract.ApprovalPolicy} and risk={contract.RiskTier}.");

        policy.Validate();
        return policy.Normalize();
    }
}
