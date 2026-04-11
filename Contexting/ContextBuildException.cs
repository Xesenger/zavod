using System;

namespace zavod.Contexting;

public sealed class ContextBuildException : InvalidOperationException
{
    public ContextBuildException(string area, string missingRequirement, string reason)
        : base($"Context build blocked in '{area}': {missingRequirement}. {reason}")
    {
        Area = area;
        MissingRequirement = missingRequirement;
        Reason = reason;
    }

    public string Area { get; }

    public string MissingRequirement { get; }

    public string Reason { get; }
}
