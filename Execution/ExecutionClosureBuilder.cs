using System;
using zavod.Outcome;

namespace zavod.Execution;

public static class ExecutionClosureBuilder
{
    public static ExecutionClosureCandidate Build(ExecutionRunResult runResult)
    {
        ArgumentNullException.ThrowIfNull(runResult);

        var status = runResult.Outcome.Status;
        var isRejected = status == ExecutionOutcomeStatus.Rejected;
        var requiresFollowup = status != ExecutionOutcomeStatus.NoOp;
        var isClosable = status == ExecutionOutcomeStatus.NoOp;

        return new ExecutionClosureCandidate(
            runResult.Record.Target,
            runResult.Record.OutcomeStatus,
            runResult.Record.Message,
            isClosable,
            requiresFollowup,
            isRejected);
    }
}
