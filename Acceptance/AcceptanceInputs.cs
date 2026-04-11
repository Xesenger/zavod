using zavod.Execution;
using zavod.Tooling;

namespace zavod.Acceptance;

public sealed record AcceptanceInputs(
    string BaselineId,
    string ExecutionId,
    RuntimeProfile RuntimeProfile,
    RuntimeSubstrate RuntimeSubstrate,
    ToolExecutionEnvelope? ToolExecution,
    RuntimeInterruptionRecord? RuntimeInterruption,
    string RuntimeSelectionReason,
    string CurrentWorkspaceCheckResult,
    AcceptanceClassification Classification);
