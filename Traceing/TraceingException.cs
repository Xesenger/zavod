using System;

namespace zavod.Traceing;

public sealed class TraceingException(string area, string missingRequirement, string reason)
    : InvalidOperationException($"Traceing area '{area}' failed at '{missingRequirement}': {reason}")
{
    public string Area { get; } = area;
    public string MissingRequirement { get; } = missingRequirement;
    public string Reason { get; } = reason;
}
