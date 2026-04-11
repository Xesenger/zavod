using System;

namespace zavod.Prompting;

public sealed class PromptAssemblyException : InvalidOperationException
{
    public PromptAssemblyException(PromptRole role, string missingRequirement, string reason)
        : base($"Prompt assembly blocked for role '{role}': {missingRequirement}. {reason}")
    {
        Role = role;
        MissingRequirement = missingRequirement;
        Reason = reason;
    }

    public PromptRole Role { get; }

    public string MissingRequirement { get; }

    public string Reason { get; }
}
