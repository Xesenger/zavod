namespace zavod.Contexting;

public enum ContextIntentState
{
    None,
    Orientation,
    Candidate,
    Refining,
    ReadyForValidation,
    Validated,
    Invalidated
}
