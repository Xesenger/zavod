using System.Collections.Generic;

namespace zavod.State;

public sealed record ShiftSnapshot(
    string ShiftId,
    string Summary,
    IReadOnlyList<string> LastAcceptedResults,
    string? CurrentActiveTaskId);
