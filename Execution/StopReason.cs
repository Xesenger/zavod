namespace zavod.Execution;

public enum StopReason
{
    UserCancel = 0,
    TimeoutExceeded = 1,
    HeartbeatLost = 2,
    NoProgressObserved = 3,
    PolicyViolation = 4
}
