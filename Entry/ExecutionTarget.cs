namespace zavod.Entry;

/// <summary>
/// Permitted execution target for the next handoff.
/// ExecutionTarget is the only source of truth for which subsystem may accept execution next.
/// Upstream data, including presentation text, must not be used for logic, branching, or parsing.
/// </summary>
public enum ExecutionTarget
{
    BootstrapSubsystem,
    IdleSubsystem,
    ActiveShiftSubsystem
}
