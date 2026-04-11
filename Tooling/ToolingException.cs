using System;

namespace zavod.Tooling;

public sealed class ToolingException(string area, string missingRequirement, string reason)
    : InvalidOperationException($"Tooling area '{area}' failed at '{missingRequirement}': {reason}")
{
    public string Area { get; } = area;
    public string MissingRequirement { get; } = missingRequirement;
    public string Reason { get; } = reason;
}
