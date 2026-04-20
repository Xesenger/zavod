# ZAVOD v1 — Shift Lifecycle

## Purpose

`Shift Lifecycle v1`

- defines the minimal lifecycle of a shift after it has been created
- establishes the allowed shift states and transitions in v1
- applies only after active shift truth already exists
- remains separate from task execution, QC, and snapshot internals

---

## Definition

A `shift` is the active work container of the project lifecycle.

Once created, a shift remains in a minimal lifecycle until it is explicitly completed.

In v1, shift lifecycle is intentionally narrow.
It does not attempt to encode detailed runtime states.

---

## States

`Shift Lifecycle v1` contains only two states:

- `active`
- `completed`

These states describe shift truth,
not execution/runtime richness.

---

## Initial State

Any newly created shift begins in state:

- `active`

This is the only valid initial shift state in v1.

---

## Transition Model

The canonical transition is:

- `active -> completed`

No other shift-state transitions exist in v1.

---

## Meaning of `active`

`active` means:

- the shift currently exists as the live work container
- `ActiveShiftId` points to this shift
- work may continue within this shift lifecycle
- closure has not yet finalized the shift

`active` does not mean:

- worker is currently running
- a task is necessarily executing right now
- a result is currently pending
- execution/runtime state is “in progress” in a richer sense

---

## Meaning of `completed`

`completed` means:

- the shift has been explicitly closed through the closure path
- the shift is no longer the active work container
- `ActiveShiftId` must no longer point to it
- the shift has moved into historical/project archive semantics

`completed` is the only terminal shift state in v1.

---

## Completion Condition

A shift becomes `completed` only through the closure path.

Closure may occur when:

- the accepted result path has completed and the shift is explicitly closed
- the active step/task has been resolved and the user/system performs shift closure
- the active task was abandoned and the shift is then explicitly closed through the same closure boundary

Important rule:

shift completion is not identical to task completion.

A task may complete while the shift remains active.
The shift becomes `completed` only when closure is actually performed.

---

## Closure Boundary

Shift lifecycle does not own closure internals,
but it depends on closure as the only valid way to leave `active`.

Formula:

closure finalizes
shift lifecycle records

This means:

- no silent completion
- no implicit completion from UI disappearance
- no completion from runtime state alone

---

## Output of Completion

When a shift becomes `completed`:

- shift status is updated to `completed`
- `ActiveShiftId` is cleared from active project linkage
- the shift becomes historical rather than active
- closure may trigger snapshot/archive behavior in adjacent layers

Shift lifecycle itself does not define full snapshot content.

---

## Semantic Outcome vs Lifecycle State

The semantic outcome of work inside the shift may differ,
for example:

- accepted outcome
- abandoned outcome

But these are not additional shift lifecycle states.

In v1:

- `completed` remains the only terminal lifecycle state
- semantic outcomes may be recorded by closure/task/snapshot layers
- `abandoned` does not replace `completed` as a shift lifecycle state

---

## Active Shift Uniqueness

At most one shift may be `active` at the project level.

Consequences:

- multiple concurrent active shifts are forbidden in v1
- opening a new active shift requires the previous active shift to have left `active`
- project-level active linkage must always resolve to zero or one active shift

---

## Irreversibility Rule

A shift may not return from `completed` to `active`.

Once closure has finalized the shift:

- it is historical
- it is no longer the active work container
- it may be read or referenced,
  but not resumed as the same active shift truth in v1

---

## Relation to Task Lifecycle

Shift lifecycle is broader than task lifecycle.

A shift may contain:

- one or more task/step cycles
- accepted or abandoned task outcomes
- continued work after an accepted task

Therefore:

- task completion does not automatically complete the shift
- task abandonment does not automatically define a special shift state
- shift lifecycle remains minimal even when task semantics are richer

---

## Relation to Resume

Resume may reconstruct an `active` shift only while it is still active in persisted truth.

A `completed` shift is not an active resume target.

Therefore:

- `active` shift truth may be resumed according to resume boundaries
- `completed` shift truth belongs to history/archive, not active resume

---

## Relation to Snapshot

Snapshot belongs to closure/history layers,
not to shift lifecycle itself.

However:

- when a shift is closed,
  adjacent layers may create snapshot/history artifacts
- lifecycle only records that the shift has become `completed`

Formula:

shift lifecycle transitions
snapshot records closure outcome

---

## Canons

- in v1 a shift has only two states: `active` and `completed`
- every new shift starts as `active`
- only closure may move a shift from `active` to `completed`
- task completion does not automatically complete the shift
- task abandonment does not create a new shift lifecycle state
- `completed` is terminal in v1
- at most one active shift may exist at once
- shift lifecycle remains separate from runtime/execution richness

---

## Boundaries

Shift lifecycle does not define:

- task execution states
- worker/QC runtime behavior
- retry/rollback logic
- result review logic
- snapshot structure
- archive implementation details
- multi-shift concurrency
- nested shifts
- automatic completion heuristics

These belong to adjacent canon documents.

---

## Exclusions

Excluded from `Shift Lifecycle v1`:

- `paused`
- `failed`
- `cancelled`
- `abandoned` as a shift lifecycle state
- multi-shift concurrency
- nested shifts
- implicit/automatic completion transitions
- rich runtime substate modeling
