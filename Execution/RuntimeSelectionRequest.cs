namespace zavod.Execution;

public sealed record RuntimeSelectionRequest(
    RuntimeProfile? ExplicitProfile = null,
    string? RequestedProfileId = null,
    RuntimeFamily? RequestedFamily = null,
    bool IsTrustedDevelopmentScenario = false,
    bool RequiresHeavierIsolation = false,
    bool RequiresHardIsolation = false,
    bool RequiresDetachedExecution = false);
