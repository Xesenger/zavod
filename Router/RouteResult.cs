namespace zavod.Router;

/// <summary>
/// Routing result for the active user-facing scenario.
/// Scenario is the only source of truth for the user-facing route.
/// Reason is only a user-facing explanation and must not be used for logic, branching, or parsing.
/// </summary>
public sealed record RouteResult(
    Scenario Scenario,
    string Reason);
