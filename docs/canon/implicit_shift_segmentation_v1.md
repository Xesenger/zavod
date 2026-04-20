# ZAVOD v1 — Implicit Shift Segmentation

## Purpose

`Implicit Shift Segmentation v1`
- defines how shifts close automatically without requiring explicit user action
- introduces two checkpoint modes: hard and soft
- keeps shift lifecycle unchanged internally while making the user flow continuous

---

## Core Rule

A shift closes automatically when a checkpoint is detected.

Two types of checkpoints exist in v1:

- **Hard checkpoint** — triggered by a structural or directional decision
- **Soft checkpoint** — triggered by score-based domain/intent drift between tasks

---

## Hard Checkpoint — `CheckpointDetectorV1`

A hard checkpoint fires when all of the following are true:

1. The current task has `Status == Completed`
2. A `DecisionSignal` exists with `AffectsStructureOrDirection == true`

Source: `DecisionCheckpointRuleV1.IsStructuralOrDirectionalDecision()` evaluates whether an accepted result carries a structural/directional decision flag.

Hard checkpoint is deterministic: same inputs always produce the same result.

---

## Soft Checkpoint — `SoftCheckpointSignalResolverV1`

A soft checkpoint fires when accumulated drift between consecutive finalized tasks exceeds a score threshold.

### Inputs

- `ShiftState` (to find the previous finalized task)
- Completed `TaskState`
- `WorkerExecutionResult` (modified file paths)

### Domain Resolution

Each task is classified into one of:

- `Ui` — XAML/QML files, paths under `/ui/`, `/views/`, `/screens/`
- `CoreLifecycle` — Flow/, State/, Execution/, Persistence/, Bootstrap/, Orchestration/, etc.
- `ProjectTruth` — direction.md, roadmap.md, canon.md, `/decisions/`
- `Unknown` — unclassifiable

Domain is resolved first from actual modified file paths in the execution result, then from task scope and description as fallback.

### Intent Type Resolution

Task descriptions are classified into:

- `Architecture` — architectural/lifecycle/phase/flow keywords
- `Redesign` — redesign/refactor/rework keywords
- `Implementation` — add/implement/create/build keywords
- `Fix` — fix/correct/adjust/patch keywords
- `Unknown`

Both Russian and English keyword sets are supported.

### Scoring

| Signal | Score |
|--------|-------|
| Domain shift between consecutive tasks | +2 |
| Intent type change between consecutive tasks | +1 |

Soft checkpoint fires when `Score >= 2`.

### Output

```
SoftCheckpointSignal(
    ShouldCreateSnapshot: bool,
    Score: int,
    Reasons: ["domain_shift", "intent_type_change"]
)
```

---

## Dual Update Model

### Step-level (cheap)

Runs after each accepted step.

- Updates derived projections (ProjectHome, summaries, activity)
- Updates lightweight state only
- Does NOT close shift, build snapshot, or update canonical documents

### Checkpoint-level (heavy)

Runs only when a hard or soft checkpoint is detected.

- Closes current shift
- Builds snapshot
- Assigns final shift name
- Updates derived documents if required
- Opens new active shift internally

---

## Determinism Requirement

Checkpoint detection must be:

- deterministic
- reproducible
- based on observable state transitions only

No implicit or non-traceable heuristics are allowed in v1.

---

## Non-Breaking Guarantee

This addition does NOT modify:

- shift lifecycle semantics
- snapshot structure
- decision model
- canonical document definitions

It only changes the trigger conditions for shift closure.

---

## Canons

- shifts close automatically when a checkpoint is detected
- hard checkpoints are triggered by structural/directional decisions
- soft checkpoints are triggered by domain or intent drift between tasks
- checkpoint detection is deterministic and based on observable state only
- user-facing flow is continuous; shift management is internal
