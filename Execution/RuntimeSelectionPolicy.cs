using System;

namespace zavod.Execution;

public static class RuntimeSelectionPolicy
{
    public static RuntimeSelectionDecision Select(RuntimeSelectionRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.ExplicitProfile is not null)
        {
            request.ExplicitProfile.Validate();
            var explicitProfile = request.ExplicitProfile.Normalize();
            return EvaluateCandidate(explicitProfile, request, "explicit runtime profile");
        }

        if (!string.IsNullOrWhiteSpace(request.RequestedProfileId))
        {
            var requestedProfile = RuntimeProfileResolver.ResolveByProfileId(request.RequestedProfileId);
            return EvaluateCandidate(requestedProfile, request, $"requested profile '{requestedProfile.ProfileId}'");
        }

        if (request.RequestedFamily is not null)
        {
            var requestedFamilyProfile = RuntimeProfileResolver.ResolveByFamily(request.RequestedFamily);
            return EvaluateCandidate(requestedFamilyProfile, request, $"requested family '{request.RequestedFamily}'");
        }

        return BuildDefaultDecision(request);
    }

    private static RuntimeSelectionDecision BuildDefaultDecision(RuntimeSelectionRequest request)
    {
        if (request.RequiresDetachedExecution)
        {
            return Deny(
                RuntimeProfileCatalog.GetDefaultFor(RuntimeFamily.Remote),
                "Remote runtime is not enabled by default in the current phase.");
        }

        if (request.RequiresHardIsolation)
        {
            return Deny(
                RuntimeProfileCatalog.GetDefaultFor(RuntimeFamily.VmOrSandbox),
                "VM or sandbox runtime is not enabled by default in the current phase.");
        }

        if (request.RequiresHeavierIsolation)
        {
            var container = RuntimeProfileCatalog.GetDefaultFor(RuntimeFamily.Container);
            return Allow(container, "Container runtime selected because heavier isolation was requested.");
        }

        return Allow(RuntimeProfileCatalog.Default, "Scoped local workspace is the default runtime policy.");
    }

    private static RuntimeSelectionDecision EvaluateCandidate(
        RuntimeProfile candidate,
        RuntimeSelectionRequest request,
        string source)
    {
        if (candidate.Family == RuntimeFamily.LocalUnsafe && !request.IsTrustedDevelopmentScenario)
        {
            return Deny(candidate, $"Runtime {source} is denied because local unsafe is allowed only for trusted development scenarios.");
        }

        if (candidate.Family == RuntimeFamily.Remote && !request.RequiresDetachedExecution)
        {
            return Deny(candidate, $"Runtime {source} is denied because remote execution is not enabled by default.");
        }

        if (candidate.Family == RuntimeFamily.VmOrSandbox && !request.RequiresHardIsolation)
        {
            return Deny(candidate, $"Runtime {source} is denied because VM or sandbox execution is not enabled by default.");
        }

        if (request.RequiresHeavierIsolation
            && candidate.Isolation != RuntimeIsolationLevel.Container
            && candidate.Isolation != RuntimeIsolationLevel.VirtualMachine
            && candidate.Isolation != RuntimeIsolationLevel.RemoteEphemeral)
        {
            return Deny(candidate, $"Runtime {source} is denied because heavier isolation was required.");
        }

        return Allow(candidate, $"Runtime {source} is allowed by current runtime policy.");
    }

    private static RuntimeSelectionDecision Allow(RuntimeProfile profile, string reason)
    {
        return new RuntimeSelectionDecision(profile, IsAllowed: true, reason);
    }

    private static RuntimeSelectionDecision Deny(RuntimeProfile profile, string reason)
    {
        return new RuntimeSelectionDecision(profile, IsAllowed: false, reason);
    }
}
