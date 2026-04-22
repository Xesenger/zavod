using System.Collections.Generic;

namespace zavod.Sage;

// Typed contexts for the 4 S2a hook points.
//
// Every field is a readonly snapshot. Sage observers must NOT mutate
// these records and MUST NOT return any value that influences the
// caller's control flow (v2.1a guardrail #2: no direct role prompt
// influence; Sage observes, QC adjudicates).

public sealed record SageAfterIntentContext(
    string ProjectId,
    string ProjectRoot,
    string? ActiveTaskId,
    string UserMessage,
    string FinalIntentState,
    bool LeadSuccess,
    string? TaskBrief,
    string? LeadReply);

public sealed record SageBeforeExecutionContext(
    string ProjectId,
    string ProjectRoot,
    string TaskId,
    string TaskDescription,
    IReadOnlyList<string> Scope,
    int AnchorCount,
    int AdvisoryNoteCount,
    bool IsRevision,
    int RevisionNoteCount);

public sealed record SageBeforeResultContext(
    string ProjectId,
    string ProjectRoot,
    string TaskId,
    string WorkerStatus,
    int StagedArtifactCount,
    int WorkerBlockerCount,
    int WorkerWarningCount);

public sealed record SageAfterResultContext(
    string ProjectId,
    string ProjectRoot,
    string TaskId,
    SageResultOutcome Outcome,
    string? Rationale);
