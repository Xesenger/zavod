using System;

namespace zavod.Sage;

// Typed observation record per SAGE v2.1 S1.
//
// Fields:
//   Type          - one of SageObservationType constants
//   Severity      - hint / warning / critical (v2.1a #1)
//   Message       - short human-readable text (for UI, never for prompts)
//   Stage         - pipeline hook point that produced the observation
//   Channel       - always SageOnly in S1 (v2.1 isolation contract)
//   ObservedAt    - UTC timestamp
//   ProjectId,
//   ShiftId,
//   TaskId        - opaque identifiers; may be null when not applicable
//   AffectedLayer - optional label (e.g. "worker", "lead", "qc", "preflight")
//   EvidenceRef   - pointer to supporting evidence (e.g. lab telemetry path)
//   AnchorRef     - pointer to anchor / file involved
//   ExpiresAt     - time-bound staleness (v2.1 pattern memory safety)
//   Degraded      - true when the emitting hook exceeded its fast-path
//                   budget (v2.1a #3) and the observation may be incomplete.
public sealed record SageObservation(
    string Type,
    SageSeverity Severity,
    string Message,
    SageStage Stage,
    SageChannel Channel,
    DateTimeOffset ObservedAt,
    string? ProjectId = null,
    string? ShiftId = null,
    string? TaskId = null,
    string? AffectedLayer = null,
    string? EvidenceRef = null,
    string? AnchorRef = null,
    DateTimeOffset? ExpiresAt = null,
    bool Degraded = false);
