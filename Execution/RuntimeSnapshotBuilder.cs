using System;
using System.Linq;

namespace zavod.Execution;

public static class RuntimeSnapshotBuilder
{
    public static RuntimeSnapshot Build(ShiftTrace trace)
    {
        ArgumentNullException.ThrowIfNull(trace);

        var relevantEntries = trace.Entries
            .Where(static entry => entry.IsShiftRelevant)
            .ToArray();

        if (relevantEntries.Length == 0)
        {
            return new RuntimeSnapshot(
                LastExecutionTarget: null,
                LastOutcomeStatus: null,
                LastSaveStatus: null,
                ShiftRelevantEntriesCount: 0,
                LastMessage: null);
        }

        var last = relevantEntries[^1];
        return new RuntimeSnapshot(
            last.Target,
            last.OutcomeStatus,
            last.SaveStatus,
            relevantEntries.Length,
            last.Message);
    }
}
