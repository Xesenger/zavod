using System;
using zavod.Execution;

namespace zavod.Tooling;

public sealed record ToolExecutionRoute(
    TypedToolContract Contract,
    RoleCapabilityProfile CapabilityProfile,
    RuntimeProfile RuntimeProfile,
    RuntimeSubstrate RuntimeSubstrate,
    ExternalToolGovernancePolicy GovernancePolicy,
    bool RequiresAdditionalApproval,
    string Summary)
{
    public ToolExecutionRoute Normalize()
    {
        Contract.Validate();
        RuntimeProfile.Validate();
        RuntimeSubstrate.Validate();
        GovernancePolicy.Validate();

        return this with
        {
            Contract = Contract.Normalize(),
            RuntimeProfile = RuntimeProfile.Normalize(),
            RuntimeSubstrate = RuntimeSubstrate.Normalize(),
            GovernancePolicy = GovernancePolicy.Normalize(),
            Summary = Summary.Trim()
        };
    }

    public void Validate()
    {
        Contract.Validate();
        RuntimeProfile.Validate();
        RuntimeSubstrate.Validate();
        GovernancePolicy.Validate();
        ArgumentException.ThrowIfNullOrWhiteSpace(Summary);
    }
}
