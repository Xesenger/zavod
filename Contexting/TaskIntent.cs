using System;

namespace zavod.Contexting;

public sealed record TaskIntent(
    string IntentId,
    string Description,
    ContextIntentState Status)
{
    public TaskIntent Refine()
    {
        if (Status is ContextIntentState.Validated or ContextIntentState.Invalidated)
        {
            throw new InvalidOperationException("Only non-terminal intent can enter refining state.");
        }

        return this with { Status = ContextIntentState.Refining };
    }

    public TaskIntent MarkReadyForValidation()
    {
        if (Status is not (ContextIntentState.Candidate or ContextIntentState.Refining))
        {
            throw new InvalidOperationException("Only candidate or refining intent can become ready for validation.");
        }

        return this with { Status = ContextIntentState.ReadyForValidation };
    }

    public TaskIntent Validate()
    {
        if (Status != ContextIntentState.ReadyForValidation)
        {
            throw new InvalidOperationException("Only ready_for_validation intent can be validated for canonical task creation.");
        }

        return this with { Status = ContextIntentState.Validated };
    }

    public TaskIntent Invalidate()
    {
        if (Status == ContextIntentState.Validated)
        {
            throw new InvalidOperationException("Validated intent is terminal for the interaction loop and cannot be invalidated.");
        }

        return this with { Status = ContextIntentState.Invalidated };
    }
}
