using System;
using System.Collections.Generic;
using System.Linq;
using zavod.Execution;
using zavod.Prompting;

namespace zavod.Tooling;

public static class RoleToolResolver
{
    public static RoleCapabilityProfile GetCapabilityProfile(PromptRole role)
    {
        return role switch
        {
            PromptRole.Worker => RoleCapabilityProfile.ExternalBrokered,
            PromptRole.ShiftLead => RoleCapabilityProfile.WorkspaceOperator,
            PromptRole.Qc => RoleCapabilityProfile.ReadOnly,
            PromptRole.SeniorSpecialist => RoleCapabilityProfile.AnalysisSupport,
            _ => throw new ToolingException("role tool resolver", "role", $"Role '{role}' is not supported by role tool resolver.")
        };
    }

    public static IReadOnlyList<ResolvedTool> ListVisibleTools(
        PromptRole role,
        RuntimeProfile? runtimeProfile = null)
    {
        return ListVisibleTools(GetCapabilityProfile(role), runtimeProfile);
    }

    public static IReadOnlyList<ResolvedTool> ListVisibleTools(
        RoleCapabilityProfile capabilityProfile,
        RuntimeProfile? runtimeProfile = null)
    {
        runtimeProfile ??= RuntimeProfileDefaults.ScopedLocalDefault;
        runtimeProfile.Validate();
        runtimeProfile = runtimeProfile.Normalize();

        return TypedToolCatalog.ListAll()
            .Where(contract => IsVisible(capabilityProfile, contract))
            .Select(contract => BuildResolvedTool(capabilityProfile, runtimeProfile, contract))
            .OrderBy(static resolved => resolved.ToolName, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public static ResolvedTool ResolveRequired(
        PromptRole role,
        string toolName,
        RuntimeProfile? runtimeProfile = null)
    {
        return ResolveRequired(GetCapabilityProfile(role), toolName, runtimeProfile);
    }

    public static ResolvedTool ResolveRequired(
        RoleCapabilityProfile capabilityProfile,
        string toolName,
        RuntimeProfile? runtimeProfile = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(toolName);

        var contract = TypedToolCatalog.GetRequired(toolName);
        if (!IsVisible(capabilityProfile, contract))
        {
            throw new ToolingException(
                "role tool resolver",
                "capability profile",
                $"Tool '{toolName.Trim()}' is not visible for role capability profile '{capabilityProfile}'.");
        }

        runtimeProfile ??= RuntimeProfileDefaults.ScopedLocalDefault;
        return BuildResolvedTool(capabilityProfile, runtimeProfile, contract);
    }

    private static bool IsVisible(RoleCapabilityProfile capabilityProfile, TypedToolContract contract)
    {
        return capabilityProfile switch
        {
            RoleCapabilityProfile.ReadOnly =>
                contract.RiskTier == ToolRiskTier.ReadOnly,
            RoleCapabilityProfile.WorkspaceOperator =>
                contract.RiskTier is ToolRiskTier.ReadOnly or ToolRiskTier.WorkspaceWrite,
            RoleCapabilityProfile.ExternalBrokered =>
                contract.RiskTier is ToolRiskTier.ReadOnly or ToolRiskTier.WorkspaceWrite or ToolRiskTier.ExternalAccess,
            RoleCapabilityProfile.AnalysisSupport =>
                contract.RiskTier is ToolRiskTier.ReadOnly or ToolRiskTier.ExternalAccess,
            _ => false
        };
    }

    private static ResolvedTool BuildResolvedTool(
        RoleCapabilityProfile capabilityProfile,
        RuntimeProfile runtimeProfile,
        TypedToolContract contract)
    {
        runtimeProfile.Validate();
        runtimeProfile = runtimeProfile.Normalize();
        contract.Validate();
        contract = contract.Normalize();

        var substrate = RuntimeSubstrateBuilder.Build(runtimeProfile);
        var governance = TypedToolCatalog.BuildGovernancePolicy(contract.ToolName);
        var requiresAdditionalApproval =
            contract.ApprovalPolicy == ToolApprovalPolicy.HostApprovalRequired
            || contract.RequiresNetworkBroker
            || governance.DenyNetworkByDefault
            || (contract.RiskTier == ToolRiskTier.TrustedHostEscape && !runtimeProfile.TrustedOnly);

        var route = new ToolExecutionRoute(
            contract,
            capabilityProfile,
            runtimeProfile,
            substrate,
            governance,
            requiresAdditionalApproval,
            BuildRouteSummary(contract, capabilityProfile, runtimeProfile, substrate, requiresAdditionalApproval));

        route.Validate();

        var resolved = new ResolvedTool(
            contract.ToolName,
            route.Normalize(),
            $"Resolved role tool '{contract.ToolName}' for profile '{capabilityProfile}' on runtime '{runtimeProfile.ProfileId}'.");

        resolved.Validate();
        return resolved.Normalize();
    }

    private static string BuildRouteSummary(
        TypedToolContract contract,
        RoleCapabilityProfile capabilityProfile,
        RuntimeProfile runtimeProfile,
        RuntimeSubstrate substrate,
        bool requiresAdditionalApproval)
    {
        return
            $"Role route: tool={contract.ToolName}, capability={capabilityProfile}, runtime={runtimeProfile.ProfileId}, family={runtimeProfile.Family}, network={substrate.NetworkBroker.AccessMode}, approval={contract.ApprovalPolicy}, extraApproval={requiresAdditionalApproval}.";
    }
}
