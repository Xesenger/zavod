namespace zavod.Planning;

/// <summary>
/// Planning result for the next allowed system scenario.
/// NextAction is the only source of truth for what may happen next.
/// Reason is only a user-facing explanation and must not be used for logic, branching, or parsing.
/// </summary>
public sealed record PlanningResult(
    NextAction NextAction,
    string Reason);
