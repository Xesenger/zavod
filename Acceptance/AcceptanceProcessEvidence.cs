using zavod.Execution;

namespace zavod.Acceptance;

public sealed record AcceptanceProcessEvidence(
    int? ExitCode,
    string StdoutSummary,
    string StderrSummary,
    bool TimedOut,
    bool WasCanceled,
    RuntimeInterruptionRecord? RuntimeInterruption = null);
