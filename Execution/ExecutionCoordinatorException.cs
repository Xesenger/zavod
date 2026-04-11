using System;

namespace zavod.Execution;

public sealed class ExecutionCoordinatorException(string area, string missingRequirement, string reason)
    : InvalidOperationException($"Execution area '{area}' failed at '{missingRequirement}': {reason}")
{
    public string Area { get; } = area;
    public string MissingRequirement { get; } = missingRequirement;
    public string Reason { get; } = reason;
}
