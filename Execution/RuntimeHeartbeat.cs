using System;

namespace zavod.Execution;

public sealed record RuntimeHeartbeat(
    DateTimeOffset ObservedAt,
    string Source,
    string Summary)
{
    public RuntimeHeartbeat Normalize()
    {
        return this with
        {
            Source = Source.Trim(),
            Summary = Summary.Trim()
        };
    }

    public void Validate()
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(Source);
        ArgumentException.ThrowIfNullOrWhiteSpace(Summary);
    }
}
