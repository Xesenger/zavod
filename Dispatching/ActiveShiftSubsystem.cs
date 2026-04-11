using System;
using zavod.Persistence;

namespace zavod.Dispatching;

public sealed class ActiveShiftSubsystem(ProjectState projectState) : IActiveShiftSubsystem
{
    private readonly ProjectState _projectState = projectState ?? throw new ArgumentNullException(nameof(projectState));

    public SubsystemHandleResult Handle()
    {
        if (!string.IsNullOrWhiteSpace(_projectState.ActiveShiftId))
        {
            return new SubsystemHandleResult(
                SubsystemHandleStatus.Deferred,
                "Active shift is present. Resume path is permitted but not implemented yet.");
        }

        if (!string.IsNullOrWhiteSpace(_projectState.ActiveTaskId))
        {
            return new SubsystemHandleResult(
                SubsystemHandleStatus.Rejected,
                "Active task exists without active shift. Active shift subsystem cannot resume this state.");
        }

        return new SubsystemHandleResult(
            SubsystemHandleStatus.Rejected,
            "No active shift is available to resume.");
    }
}
