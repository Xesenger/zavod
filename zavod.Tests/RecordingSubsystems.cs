using zavod.Dispatching;

internal sealed class RecordingBootstrapSubsystem(SubsystemHandleResult result) : IBootstrapSubsystem
{
    public int CallCount { get; private set; }

    public SubsystemHandleResult Handle()
    {
        CallCount++;
        return result;
    }
}

internal sealed class RecordingIdleSubsystem(SubsystemHandleResult result) : IIdleSubsystem
{
    public int CallCount { get; private set; }

    public SubsystemHandleResult Handle()
    {
        CallCount++;
        return result;
    }
}

internal sealed class RecordingActiveShiftSubsystem(SubsystemHandleResult result) : IActiveShiftSubsystem
{
    public int CallCount { get; private set; }

    public SubsystemHandleResult Handle()
    {
        CallCount++;
        return result;
    }
}
