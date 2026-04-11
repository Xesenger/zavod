using System.Collections.Generic;

namespace zavod.Acceptance;

public sealed record AcceptanceDecision(
    AcceptanceClassification Classification,
    AcceptanceDecisionStatus Status,
    IReadOnlyList<AcceptanceConflict> Conflicts,
    IReadOnlyList<string> NonOverlappingChanges,
    AcceptanceReasonSummary ReasonSummary);
