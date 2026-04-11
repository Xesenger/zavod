using System;
using zavod.Dispatching;
using zavod.Entry;
using zavod.Outcome;

namespace zavod.Execution;

public sealed class ExecutionPipeline(ExecutionDispatcher dispatcher)
{
    private readonly ExecutionDispatcher _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));

    public ExecutionRunResult Execute(TaskExecutionContext context, ExecutionTarget target)
    {
        return Execute(context, target, RuntimeProfileDefaults.ScopedLocalDefault);
    }

    public ExecutionRunResult Execute(TaskExecutionContext context, ExecutionTarget target, RuntimeProfile runtimeProfile)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(runtimeProfile);

        if (string.IsNullOrWhiteSpace(context.ShiftId))
        {
            throw new InvalidOperationException("Execution requires shift-bound context.");
        }

        if (string.IsNullOrWhiteSpace(context.TaskId))
        {
            throw new InvalidOperationException("Execution requires task-bound context.");
        }

        runtimeProfile.Validate();
        runtimeProfile = runtimeProfile.Normalize();

        var result = _dispatcher.Dispatch(target);
        var outcome = ExecutionOutcomeBuilder.Build(target, result);
        var record = new ExecutionRecord(context.ShiftId, context.TaskId, target, outcome.Status, outcome.Message, runtimeProfile);
        return new ExecutionRunResult(outcome, record, runtimeProfile);
    }
}
