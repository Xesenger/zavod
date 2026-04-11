namespace zavod.Execution;

public sealed record RuntimeLaunchContext(
    bool IsTrustedDevelopmentScenario = false,
    bool RequiresHeavierIsolation = false,
    bool RequiresHardIsolation = false,
    bool RequiresDetachedExecution = false,
    RuntimeFamily? RequestedFamily = null,
    string? RequestedProfileId = null,
    RuntimeProfile? ExplicitProfile = null);
