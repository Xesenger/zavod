namespace zavod.Orchestration;

// Per-document canonical state for Work Packet canonical_docs_status
// field (see project_work_packet_v1.md).
//
// Caller maps from ProjectDocumentSourceDescriptor + staleness to this enum.
public enum DocumentCanonicalState
{
    Absent = 0,
    Preview = 1,
    Canonical = 2,
    Stale = 3
}
