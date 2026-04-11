using System;
using zavod.Entry;

namespace zavod.Dispatching;

/// <summary>
/// Routes an execution target to the permitted subsystem entry-point.
/// ExecutionTarget is the only source of truth for dispatch selection.
/// Dispatcher must not infer routing from any other data.
/// </summary>
public sealed class ExecutionDispatcher(
    IBootstrapSubsystem bootstrapSubsystem,
    IIdleSubsystem idleSubsystem,
    IActiveShiftSubsystem activeShiftSubsystem)
{
    private readonly IBootstrapSubsystem _bootstrapSubsystem = bootstrapSubsystem ?? throw new ArgumentNullException(nameof(bootstrapSubsystem));
    private readonly IIdleSubsystem _idleSubsystem = idleSubsystem ?? throw new ArgumentNullException(nameof(idleSubsystem));
    private readonly IActiveShiftSubsystem _activeShiftSubsystem = activeShiftSubsystem ?? throw new ArgumentNullException(nameof(activeShiftSubsystem));

    public SubsystemHandleResult Dispatch(ExecutionTarget target)
    {
        switch (target)
        {
            case ExecutionTarget.BootstrapSubsystem:
                return _bootstrapSubsystem.Handle();

            case ExecutionTarget.IdleSubsystem:
                return _idleSubsystem.Handle();

            case ExecutionTarget.ActiveShiftSubsystem:
                return _activeShiftSubsystem.Handle();

            default:
                throw new ArgumentOutOfRangeException(nameof(target), target, "Unsupported execution target.");
        }
    }
}
