using System;
using System.Collections.Generic;
using zavod.Execution;

namespace zavod.Tooling;

public sealed record TypedToolExecutionRequest(
    TypedToolContract Contract,
    RuntimeProfile RuntimeProfile,
    string WorkingDirectory,
    IReadOnlyDictionary<string, string> Arguments)
{
    public TypedToolExecutionRequest Normalize()
    {
        Contract.Validate();
        RuntimeProfile.Validate();

        return this with
        {
            Contract = Contract.Normalize(),
            RuntimeProfile = RuntimeProfile.Normalize(),
            WorkingDirectory = WorkingDirectory.Trim()
        };
    }

    public void Validate()
    {
        Contract.Validate();
        RuntimeProfile.Validate();
        ArgumentException.ThrowIfNullOrWhiteSpace(WorkingDirectory);
    }
}
