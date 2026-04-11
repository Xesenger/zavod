using System;

namespace zavod.Tooling;

public sealed record ExternalToolGovernancePolicy(
    BrokeredToolSource Source,
    ToolApprovalPolicy ApprovalPolicy,
    bool DenyNetworkByDefault,
    bool RequiresAuditTrail,
    bool AllowsExecutionInsideTrustedRuntimeOnly,
    string Summary)
{
    public ExternalToolGovernancePolicy Normalize()
    {
        Source.Validate();
        return this with
        {
            Source = Source.Normalize(),
            Summary = Summary.Trim()
        };
    }

    public void Validate()
    {
        Source.Validate();
        ArgumentException.ThrowIfNullOrWhiteSpace(Summary);
    }
}
