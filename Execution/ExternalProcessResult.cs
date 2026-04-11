namespace zavod.Execution;

public sealed record ExternalProcessResult(
    int ExitCode,
    string StdOut,
    string StdErr,
    bool TimedOut);
