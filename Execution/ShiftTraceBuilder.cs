using System;
using System.Linq;
using zavod.Entry;

namespace zavod.Execution;

public static class ShiftTraceBuilder
{
    public static ShiftTrace Start()
    {
        return new ShiftTrace(Array.Empty<ShiftTraceEntry>());
    }

    public static ShiftTraceEntry BuildEntry(ExecutionTraceEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);

        return new ShiftTraceEntry(
            entry.Target,
            entry.OutcomeStatus,
            entry.SaveStatus,
            IsShiftRelevant: entry.Target == ExecutionTarget.ActiveShiftSubsystem,
            entry.Message);
    }

    public static ShiftTrace Append(ShiftTrace trace, ShiftTraceEntry entry)
    {
        ArgumentNullException.ThrowIfNull(trace);
        ArgumentNullException.ThrowIfNull(entry);

        return new ShiftTrace(trace.Entries.Concat(new[] { entry }).ToArray());
    }
}
