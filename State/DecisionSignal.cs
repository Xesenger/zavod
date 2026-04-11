namespace zavod.State;

public sealed record DecisionSignal(
    bool Exists,
    bool AffectsStructureOrDirection);
