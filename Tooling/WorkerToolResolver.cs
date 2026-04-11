using System;
using System.Collections.Generic;
using System.Linq;
using zavod.Execution;

namespace zavod.Tooling;

public static class WorkerToolResolver
{
    public static IReadOnlyList<ResolvedTool> ListVisibleTools(
        WorkerCapabilityProfile capabilityProfile,
        RuntimeProfile? runtimeProfile = null)
    {
        return RoleToolResolver.ListVisibleTools(ToRoleCapabilityProfile(capabilityProfile), runtimeProfile);
    }

    public static ResolvedTool ResolveRequired(
        WorkerCapabilityProfile capabilityProfile,
        string toolName,
        RuntimeProfile? runtimeProfile = null)
    {
        return RoleToolResolver.ResolveRequired(ToRoleCapabilityProfile(capabilityProfile), toolName, runtimeProfile);
    }

    private static RoleCapabilityProfile ToRoleCapabilityProfile(WorkerCapabilityProfile capabilityProfile)
    {
        return capabilityProfile switch
        {
            WorkerCapabilityProfile.ReadOnly => RoleCapabilityProfile.ReadOnly,
            WorkerCapabilityProfile.WorkspaceOperator => RoleCapabilityProfile.WorkspaceOperator,
            WorkerCapabilityProfile.ExternalBrokered => RoleCapabilityProfile.ExternalBrokered,
            WorkerCapabilityProfile.AnalysisSupport => RoleCapabilityProfile.AnalysisSupport,
            _ => RoleCapabilityProfile.ReadOnly
        };
    }
}
