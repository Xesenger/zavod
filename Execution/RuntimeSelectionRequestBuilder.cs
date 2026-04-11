using System;

namespace zavod.Execution;

public static class RuntimeSelectionRequestBuilder
{
    public static RuntimeSelectionRequest Build(RuntimeLaunchContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        return new RuntimeSelectionRequest(
            ExplicitProfile: context.ExplicitProfile,
            RequestedProfileId: context.RequestedProfileId,
            RequestedFamily: context.RequestedFamily,
            IsTrustedDevelopmentScenario: context.IsTrustedDevelopmentScenario,
            RequiresHeavierIsolation: context.RequiresHeavierIsolation,
            RequiresHardIsolation: context.RequiresHardIsolation,
            RequiresDetachedExecution: context.RequiresDetachedExecution);
    }

    public static RuntimeSelectionRequest BuildDefault()
    {
        return Build(new RuntimeLaunchContext());
    }
}
