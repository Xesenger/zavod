using System.Collections.Generic;

namespace zavod.Acceptance;

public sealed record AcceptanceReasonSummary(
    string Summary,
    IReadOnlyList<string> Reasons);
