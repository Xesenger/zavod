# ZAVOD v1 — Intent System

## Purpose

`Intent System v1`

- defines the lifecycle of intent before task and shift materialization
- formalizes how user intent appears, evolves, becomes ready, and is invalidated
- separates intent state from bootstrap routing, interaction validation, execution, and shift lifecycle

---

## Source Alignment

This document does not replace:

- `bootstrap_flow_v1.md`
- `interaction_validation_flow_v1.md`
- `first_shift_creation_v1.md`

It only makes the v1 intent contract explicit in one place.

---

## Definition

`Intent` is the structured user-facing task intention that exists before execution begins.

Intent:

- may emerge through interaction
- may be refined across multiple messages
- is not execution
- is not task truth
- is not shift truth
- is not project truth
- may enter task/shift materialization only after explicit validation

Intent is the pre-work semantic layer between discussion and actual work lifecycle.

---

## Intent States

The v1 intent state model contains:

- `none`
- `candidate`
- `refining`
- `ready_for_validation`
- `validated`
- `invalidated`

These states describe semantic maturity,
not execution state.

---

## State Meanings

### `none`

No meaningful task intention is currently formed.

---

### `candidate`

A possible intent has emerged from interaction,
but it is still incomplete, unstable, or under-specified.

---

### `refining`

An active intent exists and is being clarified,
corrected, narrowed, or expanded before it reaches validation readiness.

---

### `ready_for_validation`

The intent is sufficiently formed for explicit user validation.

This is the only pre-validation state that may expose the validation entry action.

`ready_for_validation` does not mean work has started.
It only means the system can now honestly ask:

- is this the task we are agreeing to do?

---

### `validated`

The user has explicitly confirmed the intent.

Only validated intent may unlock the path into first shift creation,
task materialization,
or re-entry into work according to adjacent lifecycle rules.

---

### `invalidated`

A previously emerging or formed intent is no longer valid.

This happens when:

- meaning changes materially
- the prior framing is no longer correct
- current interaction no longer supports the earlier intent version

Invalidated intent cannot be used for validation or work entry.

---

## Rules

- input may produce a candidate intent
- candidate intent may evolve through repeated interaction
- refinement may continue across multiple turns
- only `ready_for_validation` may expose the explicit validation action
- explicit validation moves intent into `validated`
- only `validated` intent may unlock work-entry paths
- non-validated intent stays inside the interaction loop
- interaction before validation must not mutate canonical project truth
- intent state must never be mistaken for execution state

---

## Transition Summary

Canonical transitions:

- `none -> candidate`
- `candidate -> refining`
- `candidate -> ready_for_validation`
- `refining -> ready_for_validation`
- `ready_for_validation -> validated`
- `candidate -> invalidated`
- `refining -> invalidated`
- `ready_for_validation -> invalidated`
- `invalidated -> candidate` through new meaning formation

The system may move between `candidate` and `refining` as meaning matures,
but may not skip validation when entering actual work.

---

## Validation Boundary

Intent does not become work truth automatically.

Validation is required as the explicit boundary.

Consequences:

- no task/shift truth may materialize directly from raw discussion
- no execution may start directly from candidate or refining intent
- `ready_for_validation` is only readiness for validation,
  not readiness for execution by itself

Formula:

intent must be validated before work exists

---

## Invalidation Rule

Invalidation is required when the current meaning no longer honestly supports the prior intent version.

This prevents:

- stale validation paths
- misleading primary actions
- work entry based on outdated intent framing

Invalidation does not mean failure.
It means the semantic object must be rebuilt from the new meaning.

---

## Boundary

Intent is not:

- bootstrap routing
- task execution
- shift lifecycle
- closure
- snapshot history

Intent state may drive interaction actions,
but must not be mistaken for execution state.

---

## Relation to Adjacent Documents

The following meanings are owned elsewhere:

- startup entry routing → `cold_start_behavior_v1.md`
- no-active-shift bootstrap behavior → `bootstrap_flow_v1.md`
- explicit validation interaction path → `interaction_validation_flow_v1.md`
- first shift materialization path → `first_shift_creation_v1.md`
- work lifecycle and execution → execution / shift canon documents

This document owns only the semantic lifecycle of intent itself.

---

## Canons

- intent is a pre-work semantic object
- intent is not execution and not task truth
- intent may evolve across interaction
- only `ready_for_validation` may expose validation entry
- only `validated` intent may unlock work-entry paths
- invalidated intent must not remain usable
- non-validated interaction must not mutate canonical project truth
- intent lifecycle must remain separate from execution lifecycle

---

## Exclusions

This document does not define:

- UI presentation of the primary action
- scoring/confidence heuristics
- chat layout behavior
- detailed validation dialogue UX
- shift/task persistence mechanics
- resume reconstruction
- execution coordination

If those are needed,
they must be defined in adjacent canon documents,
not embedded into the intent lifecycle definition.
