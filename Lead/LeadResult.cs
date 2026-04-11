namespace zavod.Lead;

/// <summary>
/// Lead evaluation result.
/// Mode is the only source of truth for system behavior.
/// Reason is only a user-facing explanation and must not be used for logic, branching, or parsing.
/// </summary>
public sealed record LeadResult(
    LeadMode Mode,
    string Reason);
