using System;

namespace zavod.Tooling;

public sealed record ResolvedTool(
    string ToolName,
    ToolExecutionRoute Route,
    string Summary)
{
    public ResolvedTool Normalize()
    {
        Route.Validate();
        return this with
        {
            ToolName = ToolName.Trim(),
            Route = Route.Normalize(),
            Summary = Summary.Trim()
        };
    }

    public void Validate()
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ToolName);
        Route.Validate();
        ArgumentException.ThrowIfNullOrWhiteSpace(Summary);
    }
}
