using System;
using System.Collections.Generic;

namespace zavod.Bootstrap;

public sealed record FirstShiftBootstrapRequest(
    string ShiftGoal,
    string? InitialTaskDescription,
    DateTimeOffset Timestamp,
    IReadOnlyList<string>? Scope = null,
    IReadOnlyList<string>? AcceptanceCriteria = null);
