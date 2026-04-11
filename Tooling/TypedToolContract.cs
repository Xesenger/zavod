using System;
using System.Collections.Generic;
using zavod.Execution;

namespace zavod.Tooling;

public sealed record TypedToolContract(
    string ToolName,
    string InputContractName,
    string Summary,
    int DefaultTimeoutSeconds,
    ToolRiskTier RiskTier,
    ToolApprovalPolicy ApprovalPolicy,
    RuntimeFamily PreferredRuntimeFamily,
    bool RequiresWorkspaceBoundary,
    bool RequiresNetworkBroker,
    bool RequiresArtifactQuarantine,
    bool EmitsEvidence,
    bool HostGoverned,
    IReadOnlyList<ExternalToolCapability> Capabilities)
{
    public TypedToolContract Normalize()
    {
        return this with
        {
            ToolName = ToolName.Trim(),
            InputContractName = InputContractName.Trim(),
            Summary = Summary.Trim()
        };
    }

    public void Validate()
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ToolName);
        ArgumentException.ThrowIfNullOrWhiteSpace(InputContractName);
        ArgumentException.ThrowIfNullOrWhiteSpace(Summary);

        if (DefaultTimeoutSeconds <= 0)
        {
            throw new InvalidOperationException("Typed tool contract requires positive timeout.");
        }
    }
}
