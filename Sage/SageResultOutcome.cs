namespace zavod.Sage;

// Terminal outcomes of a work cycle, observed by the AfterResult hook.
// Required for v2.1a guardrail #4 (pattern memory counter-evidence rule):
// patterns are invalidated when a predicted "bad" case ends up Applied.
//
//   QcAccepted     - QC said ACCEPT; user approval pending.
//   QcRevise       - QC said REVISE; revision cycle opens.
//   QcReject       - QC said REJECT; task abandoned.
//   WorkerRefused  - Worker refused/failed before QC could run.
//   Applied        - User accepted and staged edits committed on disk.
//
// LLM-unavailable (BRANCH D) does NOT fire AfterResult: the cycle is
// preserved, not terminated. The task survives for user retry.
public enum SageResultOutcome
{
    QcAccepted,
    QcRevise,
    QcReject,
    WorkerRefused,
    Applied
}
