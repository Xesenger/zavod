using System;

namespace zavod.Boundary;

public sealed class ResultCommitCoordinatorException(string area, string missingRequirement, string reason)
    : InvalidOperationException($"Result boundary area '{area}' failed at '{missingRequirement}': {reason}")
{
    public string Area { get; } = area;
    public string MissingRequirement { get; } = missingRequirement;
    public string Reason { get; } = reason;
}
