using System;
using System.Linq;
using zavod.Execution;
using zavod.Tooling;

namespace zavod.Acceptance;

public static class ExecutionAcceptanceAdapter
{
    public static AcceptanceEvaluation Evaluate(
        string projectRoot,
        ExecutionRuntimeState runtimeState,
        AcceptanceProcessEvidence processEvidence,
        string currentWorkspaceCheckResult,
        AcceptanceClassification classification = AcceptanceClassification.Unknown,
        ToolExecutionEnvelope? toolExecution = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(projectRoot);
        ArgumentNullException.ThrowIfNull(runtimeState);
        ArgumentNullException.ThrowIfNull(processEvidence);
        ArgumentException.ThrowIfNullOrWhiteSpace(currentWorkspaceCheckResult);

        if (runtimeState.Result is null)
        {
            throw new InvalidOperationException("Execution acceptance evaluation requires a produced worker result.");
        }

        var touchedRelativePaths = runtimeState.Result.Modifications
            .Where(static modification => !string.IsNullOrWhiteSpace(modification.Path))
            .Select(static modification => modification.Path.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return AcceptanceEvaluationFactory.Create(
            workspaceRoot: projectRoot,
            touchedRelativePaths: touchedRelativePaths,
            executionId: runtimeState.Session.SessionId,
            runtimeSelection: runtimeState.RuntimeSelection,
            runResult: runtimeState.RunResult,
            workerResult: runtimeState.Result,
            processEvidence: processEvidence,
            currentWorkspaceCheckResult: currentWorkspaceCheckResult,
            classification: classification,
            toolExecution: toolExecution);
    }
}
