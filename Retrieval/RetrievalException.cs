using System;

namespace zavod.Retrieval;

public sealed class RetrievalException(string area, string missingRequirement, string reason)
    : InvalidOperationException($"Retrieval area '{area}' failed at '{missingRequirement}': {reason}")
{
    public string Area { get; } = area;
    public string MissingRequirement { get; } = missingRequirement;
    public string Reason { get; } = reason;
}
