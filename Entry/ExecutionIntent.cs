namespace zavod.Entry;

/// <summary>
/// Execution-facing intent for the next permitted handoff.
/// ExecutionIntent is the only source of truth for what execution-facing intent may be entered next.
/// Any text fields in upstream presentation models are presentation data only
/// and must not be used for logic, branching, or parsing.
/// </summary>
public enum ExecutionIntent
{
    StartBootstrapFlow,
    StayIdle,
    ResumeActiveShift
}
