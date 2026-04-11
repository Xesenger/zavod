namespace zavod.Dispatching;

public sealed record SubsystemHandleResult(
    SubsystemHandleStatus Status,
    string? Message = null);
