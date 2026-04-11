using System;
using zavod.Dispatching;
using zavod.Entry;

namespace zavod.Outcome;

public static class ExecutionOutcomeBuilder
{
    public static ExecutionOutcome Build(ExecutionTarget target, SubsystemHandleResult result)
    {
        ArgumentNullException.ThrowIfNull(result);

        var status = result.Status switch
        {
            SubsystemHandleStatus.NoOp => ExecutionOutcomeStatus.NoOp,
            SubsystemHandleStatus.Deferred => ExecutionOutcomeStatus.Deferred,
            SubsystemHandleStatus.Rejected => ExecutionOutcomeStatus.Rejected,
            _ => throw new ArgumentOutOfRangeException(nameof(result), result.Status, "Unsupported subsystem handle status.")
        };

        return new ExecutionOutcome(target, status, result.Message);
    }
}
