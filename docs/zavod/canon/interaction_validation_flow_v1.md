# ZAVOD v1 — Interaction Validation Flow

## Purpose

`Interaction Validation Flow v1`

- defines the interaction path from discussion to explicit intent validation
- formalizes the boundary between interaction semantics and work-entry materialization
- separates validation flow from intent lifecycle, bootstrap routing, and first shift creation mechanics

---

## Definition

`Interaction Validation Flow` is the explicit pre-work path through which discussion becomes a user-confirmed intent.

It governs:

- how interaction moves toward validation
- when explicit validation may be offered
- what happens when validation succeeds or fails

It does not govern:

- startup routing
- bootstrap ownership
- shift/task persistence mechanics
- execution runtime

---

## Position in Architecture

This flow sits between:

- raw discussion / interaction
and
- validated entry into work

Formula:

discussion
→ intent formation
→ explicit validation
→ work-entry unlocking

The flow is therefore interaction-facing,
but still pre-execution.

---

## Source Alignment

This document should align with:

- `cold_start_behavior_v1.md`
- `bootstrap_flow_v1.md`
- `intent_system_v1.md`
- `first_shift_creation_v1.md`

Ownership split:

- `intent_system_v1.md` owns semantic intent states
- `bootstrap_flow_v1.md` owns no-active-shift bootstrap behavior
- `first_shift_creation_v1.md` owns first shift materialization
- this document owns the interaction path that leads into explicit validation

---

## Flow Summary

Canonical flow:

interaction
→ candidate intent
→ refining
→ ready_for_validation
→ explicit validation
→ validated intent
→ work-entry handoff

This flow ends at validated intent.

Actual task/shift materialization happens only after the validation boundary,
according to adjacent lifecycle documents.

---

## Discussion Phase

The interaction layer begins in discussion.

In discussion:

- the user may describe, clarify, revise, or redirect the desired work
- lead may interpret user input into evolving intent
- candidate meaning may appear gradually
- no work truth exists yet

Discussion is allowed to remain open until the system can honestly support explicit validation.

---

## Intent Formation Inside Interaction

During interaction:

- `candidate` intent may emerge
- the intent may enter `refining`
- the system may continue clarifying until intent becomes `ready_for_validation`

This document does not redefine intent states.
It only uses them as the preconditions of the validation path.

---

## Validation Readiness Condition

The interaction layer may offer explicit validation only when intent is in:

- `ready_for_validation`

This means:

- the current task framing is stable enough to present for confirmation
- the system can state the intended work honestly enough for the user to approve or reject

It does not mean:

- work has started
- a task already exists
- a shift has already been created

---

## Explicit Validation

Validation must be explicit.

The system may not silently infer that work has been approved.

Validation is the moment where the user confirms:

- yes, this is the task framing we are agreeing to

Consequences of explicit validation:

- intent becomes `validated`
- interaction loop ends as the active pre-work semantic loop
- the flow unlocks handoff into work-entry materialization

---

## Validation Failure / Return Path

If validation does not succeed,
the system must return to interaction rather than jumping forward into work.

Examples:

- the user rejects the framing
- the user clarifies meaning further
- the prior intent becomes invalidated
- the task statement needs adjustment before confirmation

Consequences:

- return to discussion / refining
- no execution begins
- no task/shift truth is created through failed validation

Formula:

failed validation returns to interaction
not to execution

---

## Action Contract

The explicit validation entry action belongs to the interaction layer,
but only under strict conditions.

Rules:

- the primary validation/start-entry action appears only at `ready_for_validation`
- the action reflects actual intent readiness
- the action is not arbitrary UI decoration
- the action leads to explicit validation,
  not directly to execution

This preserves the boundary between:

- semantic readiness
and
- actual work creation

---

## Exit Condition

The interaction validation flow exits only when:

- intent has been explicitly validated

At this point:

- interaction no longer owns the task framing loop as the active control surface
- the system may hand off into first-shift creation or other work-entry mechanics

This document does not own the next layer.
It only defines the last interaction step before it.

---

## Boundary to Task / Shift Creation

Interaction must not create task or shift truth directly.

Validation is the boundary.
Materialization comes after validation.

Therefore:

- interaction may interpret and refine intent
- interaction may present validation
- interaction may return to discussion when validation fails
- interaction may not create first task/shift directly as its own semantic act

The handoff after validated intent belongs to adjacent lifecycle documents.

---

## Truth Boundary

Canonical project truth must not be mutated by the interaction loop before validated intent exists.

Before validation:

- no task truth exists
- no shift truth exists through this flow
- no execution truth exists
- no result truth exists

Interaction is semantically meaningful,
but still pre-truth for work lifecycle.

---

## Relation to Execution

Interaction is not execution.
Validation is not execution.

Even at the moment of successful validation:

- the system has not yet entered execution runtime
- the system has only crossed the semantic approval boundary

Execution begins only after the proper downstream handoff.

---

## Canons

- interaction may interpret and refine intent
- only `ready_for_validation` may expose explicit validation entry
- validation must be explicit
- failed or incomplete validation returns to interaction
- interaction may not create task/shift truth directly
- validated intent is the only semantic exit from interaction into work-entry handoff
- interaction and validation remain strictly pre-execution
- canonical project truth must not be mutated by the interaction loop before validated intent

---

## Exclusions

This document does not define:

- startup routing
- bootstrap ownership
- intent state definitions in full
- readiness scoring heuristics
- first shift materialization internals
- execution runtime
- result handling
- closure or snapshot behavior

Those belong to adjacent canon documents.

