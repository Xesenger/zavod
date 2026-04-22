namespace zavod.Sage;

// Pipeline hook points per SAGE v2.1.
// S1 declares the enum so observations can be tagged with their
// origin point even before S2 wires the actual hooks.
// before_execution is fast-path-only (v2.1a guardrail #3).
public enum SageStage
{
    AfterIntent,
    BeforeExecution,
    DuringExecution,
    BeforeResult,
    AfterResult
}
