using System;

namespace zavod.Execution;

public sealed record RuntimeProgressSignal(
    DateTimeOffset ObservedAt,
    string Kind,
    string Summary)
{
    public RuntimeProgressSignal Normalize()
    {
        return this with
        {
            Kind = Kind.Trim(),
            Summary = Summary.Trim()
        };
    }

    public void Validate()
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(Kind);
        ArgumentException.ThrowIfNullOrWhiteSpace(Summary);
    }
}
