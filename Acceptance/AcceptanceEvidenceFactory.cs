using System;
using System.Collections.Generic;
using zavod.Execution;
using zavod.Tooling;
using zavod.Workspace;

namespace zavod.Acceptance;

public static class AcceptanceEvidenceFactory
{
    public static AcceptanceEvidence CreateFromComponents(
        WorkspaceState workspaceObservation,
        WorkspaceBaseline baseline,
        ExecutionBase executionBase,
        RuntimeSelectionDecision? runtimeSelection,
        ExecutionRunResult runResult,
        WorkerExecutionResult? workerResult,
        AcceptanceProcessEvidence processEvidence,
        ToolExecutionEnvelope? toolExecution,
        string currentWorkspaceCheckResult,
        AcceptanceClassification classification = AcceptanceClassification.Unknown)
    {
        ArgumentNullException.ThrowIfNull(workspaceObservation);
        ArgumentNullException.ThrowIfNull(baseline);
        ArgumentNullException.ThrowIfNull(executionBase);
        ArgumentNullException.ThrowIfNull(runResult);
        ArgumentNullException.ThrowIfNull(processEvidence);
        ArgumentException.ThrowIfNullOrWhiteSpace(currentWorkspaceCheckResult);

        return AcceptanceEvidenceBuilder.BuildFromExecution(
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
    }

    public static AcceptanceEvidence Create(
        string workspaceRoot,
        IReadOnlyList<string> touchedRelativePaths,
        string executionId,
        RuntimeSelectionDecision? runtimeSelection,
        ExecutionRunResult runResult,
        WorkerExecutionResult? workerResult,
        AcceptanceProcessEvidence processEvidence,
        ToolExecutionEnvelope? toolExecution,
        string currentWorkspaceCheckResult,
        AcceptanceClassification classification = AcceptanceClassification.Unknown,
        IReadOnlyList<string>? includePaths = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workspaceRoot);
        ArgumentNullException.ThrowIfNull(touchedRelativePaths);
        ArgumentException.ThrowIfNullOrWhiteSpace(executionId);
        ArgumentNullException.ThrowIfNull(runResult);
        ArgumentNullException.ThrowIfNull(processEvidence);
        ArgumentException.ThrowIfNullOrWhiteSpace(currentWorkspaceCheckResult);

        var scanResult = WorkspaceScanner.Scan(new WorkspaceScanRequest(workspaceRoot, includePaths));
        var baseline = WorkspaceBaselineBuilder.Build(scanResult);
        var executionBase = ExecutionBaseBuilder.Build(workspaceRoot, touchedRelativePaths, executionId);

        return CreateFromComponents(
            scanResult.State,
            baseline,
            executionBase,
            runtimeSelection,
            runResult,
            workerResult,
            processEvidence,
            toolExecution,
            currentWorkspaceCheckResult,
            classification);
    }
}
