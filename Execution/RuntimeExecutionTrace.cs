using System.Collections.Generic;

namespace zavod.Execution;

public sealed record RuntimeExecutionTrace(
    IReadOnlyList<ExecutionTraceEntry> Entries);
