namespace zavod.Sage;

// MVP observation types per SAGE v2.1 S1.
//
// Stored as string (not enum) so future slices (S3+) can add
// typed variants without breaking forward compatibility of the
// sage_only JSONL store.
//
//   semantic_gap     - intent and artefact diverge
//   pattern_repeat   - the system is about to replay a known failure
//   attention_miss   - something relevant was not considered
//
// Meta types (emitted by infrastructure, not content emitters):
//
//   flood_suppressed - budget exceeded; one or more observations dropped
public static class SageObservationType
{
    public const string SemanticGap = "semantic_gap";
    public const string PatternRepeat = "pattern_repeat";
    public const string AttentionMiss = "attention_miss";

    public const string FloodSuppressed = "flood_suppressed";
}
