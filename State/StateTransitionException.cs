using System;

namespace zavod.State;

public sealed class StateTransitionException : InvalidOperationException
{
    public StateTransitionException(string entity, string action, string reason)
        : base($"State transition blocked for '{entity}' on '{action}': {reason}")
    {
    }
}
