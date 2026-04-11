# ZAVOD v1 — First Shift Creation

## Purpose

`First Shift Creation v1`

- defines the lifecycle boundary where validated intent materializes into the first active shift
- formalizes the minimum truth that must appear when project work begins for the first time
- separates first shift materialization from bootstrap, interaction, execution, and full shift lifecycle

---

## Definition

`First Shift Creation` is the explicit lifecycle transition that creates the first active shift after validated intent exists.

It is the point where the system moves from:

- pre-work semantic state

to:

- active shift-based work lifecycle

This transition is narrow and deliberate.
It is not the same as execution start.

---

## Preconditions

First shift creation is allowed only when all of the following are true:

- `ProjectState` exists
- `ActiveShiftId == null`
- a valid `validated intent` exists
- the system is at a legitimate work-entry boundary

If these conditions are not satisfied,
first shift creation must not occur.

---

## Input

Required inputs:

- `ProjectState`
- `validated intent`

Optional supporting inputs may exist,
but they do not replace the required boundary condition:

validated intent is mandatory.

---

## Transition

Canonical transition:

- no active shift
- validated intent achieved
- first shift is created
- `ActiveShiftId` becomes present
- system exits bootstrap-only mode
- shift-based lifecycle begins

Formula:

validated intent
→ first shift creation
→ active shift truth exists

---

## Output

The output of first shift creation is minimal and specific:

- a new persisted `shift`
- updated active linkage in project meta / active project state
- entry into shift-based lifecycle

It is not required to produce:

- execution runtime
- result state
- closure data
- history-rich shift body at creation time

---

## Minimal Shift Truth

A newly created first shift must contain only the minimum truth needed to establish valid active shift existence.

Minimum fields may include:

- `ShiftId`
- `CreatedAt`
- `InitialIntent`
- `Status`

Recommended meaning:

- `ShiftId` — unique shift identifier
- `CreatedAt` — creation timestamp
- `InitialIntent` — the validated intent that authorized this shift
- `Status` — initial lifecycle status (`active`)

The first shift begins as active.

---

## InitialIntent Rule

`InitialIntent` is mandatory for first shift creation.

The first shift must preserve the validated semantic entry that caused the work lifecycle to begin.

This ensures:

- honest provenance of the shift
- stable link between validated intent and active work context
- no magical shift creation detached from user-confirmed intent

---

## Active Linkage Update

First shift creation must update active linkage.

At minimum this means:

- `ActiveShiftId` becomes present in active project meta / state

This is the truth-level signal that the system is no longer only in bootstrap-capable idle mode.

---

## What First Shift Creation Does Not Do

First shift creation does not by itself:

- start worker execution
- create result truth
- create closure artifacts
- create snapshot history
- finalize task results
- mutate project-level meaning documents
- create journal/trace history as a substitute for shift truth

It only materializes the first active shift boundary.

---

## Relation to Bootstrap

`bootstrap_flow_v1.md` owns:

- behavior while `ActiveShiftId == null`
- bootstrap-capable interaction before work truth exists

`first_shift_creation_v1.md` owns:

- the exact transition where bootstrap ends
- the first materialization of active shift truth

Formula:

Bootstrap governs before the shift.
First Shift Creation ends bootstrap by creating the shift.

---

## Relation to Interaction Validation

`interaction_validation_flow_v1.md` owns:

- the path to explicit validation

This document does not redefine validation.

It begins only after:

- validated intent already exists

Therefore:

- validation authorizes work entry
- first shift creation materializes active shift truth

---

## Relation to Intent System

`intent_system_v1.md` owns:

- semantic lifecycle of intent

This document does not define candidate/refining/ready states.

It only consumes:

- `validated intent`

as the required precondition for first shift creation.

---

## Relation to Execution

First shift creation is not execution start.

The existence of an active shift means:

- work lifecycle has begun at the truth level

It does not automatically mean:

- worker is running
- execution pipeline is active
- runtime work is already in progress

Execution belongs to downstream lifecycle layers.

---

## Filesystem Relation

The first shift is persisted in:

- `.zavod/shifts/`

A separate shift storage unit may be created,
for example as a dedicated shift folder or equivalent persisted object.

In v1 this document does not fully standardize the storage layout internals.
It standardizes only:

- the fact of shift persistence
- the existence of active shift truth
- the linkage update

---

## Canons

- first shift may be created only after validated intent
- first shift creation requires `ActiveShiftId == null`
- first shift must preserve `InitialIntent`
- first shift begins with active status
- first shift creation updates active linkage
- first shift creation does not imply execution start
- first shift creation does not mutate project-level meaning docs
- first shift creation is the boundary where bootstrap-only mode ends

---

## Boundaries

This document does not define:

- full shift lifecycle
- task lifecycle
- execution stages
- revision/reopen behavior
- closure
- snapshots
- multi-intent policies
- retry / rollback policies
- advanced ownership models

Those belong to adjacent canon documents.

---

## Exclusions

Excluded from v1 first shift creation scope:

- automatic worker start
- automatic task/result history generation
- implicit project truth mutation beyond active linkage
- complex multi-step bootstrap orchestration
- restoration/resume logic
- task-materialization internals beyond first active shift boundary

