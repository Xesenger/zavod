namespace zavod.Sage;

// SAGE v2.1 isolation contract:
// Observations are written to sage_only by default and never
// reach role prompts (Lead/Worker/QC). S1 ships exactly one
// channel. Adding a new channel requires re-reading v2.1a
// guardrail #2 — "no direct role prompt influence".
public enum SageChannel
{
    SageOnly
}
