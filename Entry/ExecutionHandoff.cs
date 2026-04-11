using System;

namespace zavod.Entry;

public static class ExecutionHandoff
{
    public static ExecutionTarget Handoff(ExecutionIntent intent)
    {
        return intent switch
        {
            ExecutionIntent.StartBootstrapFlow => ExecutionTarget.BootstrapSubsystem,
            ExecutionIntent.StayIdle => ExecutionTarget.IdleSubsystem,
            ExecutionIntent.ResumeActiveShift => ExecutionTarget.ActiveShiftSubsystem,
            _ => throw new ArgumentOutOfRangeException(nameof(intent), intent, "Unsupported execution intent.")
        };
    }
}
