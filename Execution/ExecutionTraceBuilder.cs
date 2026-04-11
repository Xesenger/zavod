using System;
using System.Linq;

namespace zavod.Execution;

public static class ExecutionTraceBuilder
{
    public static RuntimeExecutionTrace Start()
    {
        return new RuntimeExecutionTrace(Array.Empty<ExecutionTraceEntry>());
    }

    public static ExecutionTraceEntry BuildEntry(
        ExecutionRunResult runResult,
        ProjectStateMutationResult mutationResult,
        ProjectStateSaveResult saveResult)
    {
        ArgumentNullException.ThrowIfNull(runResult);
        ArgumentNullException.ThrowIfNull(mutationResult);
        ArgumentNullException.ThrowIfNull(saveResult);

        return new ExecutionTraceEntry(
            runResult.Record.Target,
            runResult.Record.OutcomeStatus,
            mutationResult.Status,
            saveResult.SaveStatus,
            runResult.Record.Message);
    }

    public static RuntimeExecutionTrace Append(RuntimeExecutionTrace trace, ExecutionTraceEntry entry)
    {
        ArgumentNullException.ThrowIfNull(trace);
        ArgumentNullException.ThrowIfNull(entry);

        return new RuntimeExecutionTrace(trace.Entries.Concat(new[] { entry }).ToArray());
    }
}
