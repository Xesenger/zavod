namespace zavod.Execution;

public sealed record RuntimeSelectionDecision(
    RuntimeProfile Profile,
    bool IsAllowed,
    string Reason);
