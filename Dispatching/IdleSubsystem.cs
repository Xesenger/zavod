using System;
using zavod.Persistence;

namespace zavod.Dispatching;

public sealed class IdleSubsystem(ProjectState projectState) : IIdleSubsystem
{
    private readonly ProjectState _projectState = projectState ?? throw new ArgumentNullException(nameof(projectState));

    public SubsystemHandleResult Handle()
    {
        if (!string.IsNullOrWhiteSpace(_projectState.ActiveShiftId))
        {
            return new SubsystemHandleResult(
                SubsystemHandleStatus.Rejected,
                "Idle target is not valid while an active shift exists.");
        }

        if (!string.IsNullOrWhiteSpace(_projectState.ActiveTaskId))
        {
            return new SubsystemHandleResult(
                SubsystemHandleStatus.Rejected,
                "Idle target is inconsistent because an active task exists without an active shift.");
        }

        return new SubsystemHandleResult(
            SubsystemHandleStatus.NoOp,
            "System is idle and consistent. Idle subsystem performs no action.");
    }
}
