using System;

namespace zavod.Bootstrap;

public sealed record FirstShiftBootstrapRequest(
    string ShiftGoal,
    string? InitialTaskDescription,
    DateTimeOffset Timestamp);
