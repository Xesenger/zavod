using System.Collections.Generic;

namespace zavod.Execution;

public sealed record IsolationBackendRequest(
    string BackendId,
    RuntimeIsolationLevel IsolationLevel,
    string WorkingDirectory,
    string Command,
    IReadOnlyList<string> Arguments,
    IReadOnlyDictionary<string, string> EnvironmentVariables);

public sealed record IsolationBackendResponse(
    bool Success,
    string BackendId,
    int? ExitCode,
    string StandardOutput,
    string StandardError,
    string SummaryLine);

public interface IIsolationBackend
{
    IsolationBackendResponse Execute(IsolationBackendRequest request);
}
