using System.Collections.Generic;

namespace zavod.Execution;

public sealed record ShiftTrace(
    IReadOnlyList<ShiftTraceEntry> Entries);
