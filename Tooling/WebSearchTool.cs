using System;
using System.Linq;
using zavod.Execution;

namespace zavod.Tooling;

public sealed class WebSearchTool(BraveSearchRuntimeService? runtimeService = null) : IWebSearchTool
{
    private readonly BraveSearchRuntimeService _runtimeService = runtimeService ?? new BraveSearchRuntimeService();

    public ToolExecutionResult Execute(WebSearchRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        Require(!string.IsNullOrWhiteSpace(request.Query), "web search", "query", "Web search query is required.");
        Require(request.Limit > 0, "web search", "limit", "Web search limit must be greater than zero.");

        var runtimeResult = _runtimeService.Search(request.Query, request.Limit);
        if (!runtimeResult.Success)
        {
            return new ToolExecutionResult(
                false,
                runtimeResult.SummaryLine,
                Array.Empty<IntakeArtifact>(),
                Array.Empty<ToolOutputItem>(),
                Array.Empty<ToolWarning>(),
                runtimeResult.Diagnostic is null ? null : new ToolDiagnostic(runtimeResult.Diagnostic.Code, runtimeResult.Diagnostic.Message));
        }

        var outputs = runtimeResult.Items
            .Select((item, index) => new ToolOutputItem(
                $"OUTPUT-{request.RequestId}-{index + 1}",
                "web_result",
                string.IsNullOrWhiteSpace(item.Snippet) ? item.Title : $"{item.Title} — {item.Snippet}",
                item.Url))
            .ToArray();

        return new ToolExecutionResult(
            true,
            runtimeResult.SummaryLine,
            Array.Empty<IntakeArtifact>(),
            outputs,
            Array.Empty<ToolWarning>(),
            null);
    }

    private static void Require(bool condition, string area, string missingRequirement, string reason)
    {
        if (!condition)
        {
            throw new ToolingException(area, missingRequirement, reason);
        }
    }
}
