using System;

namespace zavod.Tooling;

public sealed record ToolExecutionEnvelope(
    ResolvedTool ResolvedTool,
    ToolExecutionResult Result,
    string EvidenceSummary,
    string Summary)
{
    public ToolExecutionEnvelope Normalize()
    {
        ResolvedTool.Validate();
        return this with
        {
            ResolvedTool = ResolvedTool.Normalize(),
            EvidenceSummary = EvidenceSummary.Trim(),
            Summary = Summary.Trim()
        };
    }

    public void Validate()
    {
        ResolvedTool.Validate();
        ArgumentNullException.ThrowIfNull(Result);
        ArgumentException.ThrowIfNullOrWhiteSpace(EvidenceSummary);
        ArgumentException.ThrowIfNullOrWhiteSpace(Summary);
    }
}
