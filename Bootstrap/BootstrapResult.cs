namespace zavod.Bootstrap;

/// <summary>
/// Bootstrap state summary for upper layers.
/// HasValidState reflects a successfully loaded state.
/// Invalid persisted state is not represented here because bootstrap fails fast with an exception.
/// Therefore HasValidState == false is not used in the normal flow.
/// </summary>
public sealed record BootstrapResult(
    bool IsColdStart,
    bool HasValidState,
    bool HasActiveShift);
