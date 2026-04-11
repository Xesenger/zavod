using System;
using System.Collections.Generic;
using zavod.Execution;
using zavod.Tooling;
using zavod.Workspace;

namespace zavod.Acceptance;

public static class AcceptanceEvaluationFactory
{
    public static AcceptanceEvaluation CreateFromComponents(
        WorkspaceState workspaceObservation,
        WorkspaceBaseline baseline,
        ExecutionBase executionBase,
        RuntimeSelectionDecision? runtimeSelection,
        ExecutionRunResult runResult,
        WorkerExecutionResult? workerResult,
        AcceptanceProcessEvidence processEvidence,
        string currentWorkspaceCheckResult,
        AcceptanceClassification classification = AcceptanceClassification.Unknown,
        ToolExecutionEnvelope? toolExecution = null)
    {
        var evidence = AcceptanceEvidenceFactory.CreateFromComponents(
            workspaceObservation,
            baseline,
            executionBase,
            runtimeSelection,
            runResult,
            workerResult,
            processEvidence,
            toolExecution,
            currentWorkspaceCheckResult,
            classification);

        var decision = AcceptanceGuard.Evaluate(evidence);
        return new AcceptanceEvaluation(evidence, decision);
    }

    public static AcceptanceEvaluation Create(
        string workspaceRoot,
        IReadOnlyList<string> touchedRelativePaths,
        string executionId,
        RuntimeSelectionDecision? runtimeSelection,
        ExecutionRunResult runResult,
        WorkerExecutionResult? workerResult,
        AcceptanceProcessEvidence processEvidence,
        string currentWorkspaceCheckResult,
        AcceptanceClassification classification = AcceptanceClassification.Unknown,
        ToolExecutionEnvelope? toolExecution = null,
        IReadOnlyList<string>? includePaths = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workspaceRoot);
        ArgumentNullException.ThrowIfNull(touchedRelativePaths);
        ArgumentException.ThrowIfNullOrWhiteSpace(executionId);
        ArgumentNullException.ThrowIfNull(runResult);
        ArgumentNullException.ThrowIfNull(processEvidence);
        ArgumentException.ThrowIfNullOrWhiteSpace(currentWorkspaceCheckResult);

        var evidence = AcceptanceEvidenceFactory.Create(
            workspaceRoot,
            touchedRelativePaths,
            executionId,
            runtimeSelection,
            runResult,
            workerResult,
            processEvidence,
            toolExecution,
            currentWorkspaceCheckResult,
            classification,
            includePaths);

        var decision = AcceptanceGuard.Evaluate(evidence);
        return new AcceptanceEvaluation(evidence, decision);
    }
}
