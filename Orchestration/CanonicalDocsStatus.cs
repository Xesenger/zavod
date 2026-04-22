namespace zavod.Orchestration;

// Structured 5/5 state for the Work Packet canonical_docs_status field
// (see project_work_packet_v1.md).
//
// Each of the five project document kinds (Project, Direction, Roadmap,
// Canon, Capsule) carries its current state. The packet assembler
// produces this from Persistence.ProjectDocumentSourceSelection + any
// staleness signal the caller has on hand.
public sealed record CanonicalDocsStatus(
    DocumentCanonicalState Project,
    DocumentCanonicalState Direction,
    DocumentCanonicalState Roadmap,
    DocumentCanonicalState Canon,
    DocumentCanonicalState Capsule)
{
    public int CanonicalCount =>
        Count(DocumentCanonicalState.Canonical);

    public int AtLeastPreviewCount =>
        Count(DocumentCanonicalState.Preview) +
        Count(DocumentCanonicalState.Canonical) +
        Count(DocumentCanonicalState.Stale);

    private int Count(DocumentCanonicalState target)
    {
        var n = 0;
        if (Project == target) n++;
        if (Direction == target) n++;
        if (Roadmap == target) n++;
        if (Canon == target) n++;
        if (Capsule == target) n++;
        return n;
    }
}
