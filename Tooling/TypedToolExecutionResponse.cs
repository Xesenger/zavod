using System;

namespace zavod.Tooling;

public sealed record TypedToolExecutionResponse(
    TypedToolContract Contract,
    ToolExecutionResult Result,
    string RuntimeRouteSummary,
    string EvidenceSummary)
{
    public TypedToolExecutionResponse Normalize()
    {
        Contract.Validate();
        return this with
        {
            Contract = Contract.Normalize(),
            RuntimeRouteSummary = RuntimeRouteSummary.Trim(),
            EvidenceSummary = EvidenceSummary.Trim()
        };
    }

    public void Validate()
    {
        Contract.Validate();
        ArgumentNullException.ThrowIfNull(Result);
        ArgumentException.ThrowIfNullOrWhiteSpace(RuntimeRouteSummary);
        ArgumentException.ThrowIfNullOrWhiteSpace(EvidenceSummary);
    }
}
