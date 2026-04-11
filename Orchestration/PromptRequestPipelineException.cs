using System;
using zavod.Prompting;

namespace zavod.Orchestration;

public sealed class PromptRequestPipelineException : InvalidOperationException
{
    public PromptRequestPipelineException(PromptRole role, string missingRequirement, string reason)
        : base($"Prompt request blocked for role '{role}': {missingRequirement}. {reason}")
    {
        Role = role;
        MissingRequirement = missingRequirement;
        Reason = reason;
    }

    public PromptRole Role { get; }

    public string MissingRequirement { get; }

    public string Reason { get; }
}
