using System.Collections.Generic;
using zavod.Execution;
using zavod.Tooling;
using zavod.Workspace;

namespace zavod.Acceptance;

public sealed record AcceptanceEvidence(
    string ExecutionResultSummary,
    IReadOnlyList<string> TouchedFiles,
    WorkspaceState WorkspaceObservation,
    WorkspaceBaseline Baseline,
    ExecutionBase ExecutionBase,
    RuntimeSubstrate RuntimeSubstrate,
    ToolExecutionEnvelope? ToolExecution,
    RuntimeInterruptionRecord? RuntimeInterruption,
    string ChangePayloadSummary,
    AcceptanceProcessEvidence ProcessEvidence,
    AcceptanceInputs Inputs,
    string SummaryLine);
