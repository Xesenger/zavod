using System;
using zavod.Bootstrap;

namespace zavod.Dispatching;

public sealed class BootstrapSubsystem(BootstrapResult bootstrapState) : IBootstrapSubsystem
{
    private readonly BootstrapResult _bootstrapState = bootstrapState ?? throw new ArgumentNullException(nameof(bootstrapState));

    public SubsystemHandleResult Handle()
    {
        if (_bootstrapState.IsColdStart)
        {
            return new SubsystemHandleResult(
                SubsystemHandleStatus.Deferred,
                "Cold start detected. Bootstrap flow is permitted but not implemented yet.");
        }

        if (_bootstrapState.HasActiveShift)
        {
            return new SubsystemHandleResult(
                SubsystemHandleStatus.Deferred,
                "Active shift already exists. Bootstrap subsystem defers real execution.");
        }

        return new SubsystemHandleResult(
            SubsystemHandleStatus.Deferred,
            "Valid project state without active shift detected. Bootstrap subsystem defers real execution.");
    }
}
