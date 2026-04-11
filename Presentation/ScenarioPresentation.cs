using zavod.Router;

namespace zavod.Presentation;

/// <summary>
/// Presentation payload for the active scenario.
/// Scenario is the only source of truth for presentation selection.
/// PrimaryAction is the only source of truth for the next product action.
/// Title, Description, and PrimaryActionLabel are presentation data only
/// and must not be used for logic, branching, or parsing.
/// </summary>
public sealed record ScenarioPresentation(
    Scenario Scenario,
    string Title,
    string Description,
    string PrimaryActionLabel,
    PrimaryAction PrimaryAction);
