using zavod.Persistence;

namespace zavod.Execution;

public sealed record ProjectStateSaveResult(
    ProjectState State,
    ProjectStateSaveStatus SaveStatus,
    bool WasPersisted,
    string? Reason = null);
