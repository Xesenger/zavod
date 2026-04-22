namespace zavod.Sage;

// SAGE v2.1a guardrail #1:
//   hint     -> sage_only; UI may hide
//   warning  -> sage_only; UI must show in Sage panel
//   critical -> sage_only; UI must show + mark as critical
//               NEVER blocks execution.
//               NEVER injected into role prompts.
//               May influence behavior only via typed S3 rules.
public enum SageSeverity
{
    Hint,
    Warning,
    Critical
}
