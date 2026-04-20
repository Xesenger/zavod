# ZAVOD v1 — Cold Start Behavior

## Purpose

`Cold Start Behavior v1`

- describes app/session-level entry before any active project work is resumed
- defines the behavior of the system when startup begins without an active shift to continue
- separates cold start routing from bootstrap logic and resume reconstruction
- establishes that the system may start without project work already in progress

---

## Definition

`Cold Start` is the entry condition of the application/session when there is no active shift to resume into work.

Cold Start is an entry-routing condition.
It is not:

- execution
- resume reconstruction
- task lifecycle
- project truth mutation

Cold Start only determines how the system honestly enters the project/session before work begins.

---

## Core Principle

The system may start in a valid non-working state.

Cold Start must not:

- simulate active work
- imply remembered execution
- invent continuity
- materialize task/shift truth automatically

Formula:

cold start may be idle

---

## Entry Condition

Cold Start applies when startup enters a project/session without an active shift that should be resumed into work.

In practical terms this means:

- no active shift linkage exists
or
- no valid resume path exists into an active work surface

Cold Start therefore routes into a non-resume entry.

---

## Initial User State

At cold start the user may enter the system without:

- active shift
- active task
- active execution
- active pending result
- historical continuity presented as memory

This is a valid system state.

The user may still enter a project/session and begin interaction.

---

## Allowed Initial Surface

Cold Start may present:

- idle-capable entry
- bootstrap-capable discussion surface
- project/session entry without active work

The chat/discussion surface at cold start is used only for:

- intent formation
- clarification
- bootstrap-level lead interaction

It is not a work surface.

---

## Lead Behavior

During Cold Start:

- lead operates only at the bootstrap / intent-forming layer
- lead may interpret user input as candidate intent
- lead may help clarify direction
- lead may ask clarification questions about an imported project when current understanding is incomplete
- lead may refine preview understanding together with the user without turning that preview into truth
- lead must not create work truth automatically
- lead must not start execution
- lead must not simulate a resumed shift

Cold Start therefore allows guided entry,
but not fake continuity.

---

## Idle Validity

Idle without active shift is a valid state of the system.

Consequences:

- no shift is required for the system to be considered healthy
- no task is required for the system to be considered initialized
- no execution is implied by app startup
- the system may wait indefinitely for a real intent to form

---

## Transition Out of Cold Start

Cold Start does not itself create work.

The honest transition path is:

- cold start entry
- bootstrap-capable discussion
- candidate intent formation
- validated intent
- first shift creation
- shift-based lifecycle begins

This means:

- first shift creation is an explicit lifecycle transition
- it is not a side-effect of startup
- it is not a side-effect of merely opening chat

---

## No-Project / No-History Allowance

The system допускает старт без уже существующей рабочей истории.

Cold Start therefore supports scenarios such as:

- no active shift yet
- no prior working session to restore
- no active project work in progress

Startup without historical work is not a degraded mode.
It is a first-class valid entry condition.

---

## Relation to Bootstrap Flow

`Cold Start Behavior` does not define the full bootstrap lifecycle.

It only routes the system into the condition where bootstrap-capable interaction is allowed.

`bootstrap_flow_v1.md` owns:

- behavior when `activeShiftId == null`
- how first shift materializes after validated intent
- bootstrap-specific boundaries

Formula:

Cold Start routes
Bootstrap governs

---

## Relation to Resume

Cold Start is not the same as Resume.

- Cold Start = non-resume entry condition
- Resume = truthful reconstruction of an already active work context

If a valid active resume path exists,
startup should not behave as cold start.

If no valid resume path exists,
the system must not fake it.
It should fall back to cold start / idle-capable entry.

---

## Relation to Project Truth

Cold Start does not mutate project-level truth.

It does not directly create or update:

- project documents
- decisions
- snapshots
- execution results
- closure artifacts

At most,
Cold Start may lead into bootstrap interaction,
which may eventually create the first shift through the proper validated path.

---

## Canons

- app/session startup may begin without active work
- cold start may honestly enter idle
- cold start may expose chat/discussion only as intent-forming surface
- cold start does not imply execution
- cold start does not imply memory or continuity
- first shift creation is explicit and validated, never automatic
- no valid resume path means cold start fallback, not fake restore

---

## Boundaries

Cold Start Behavior does not define:

- intent state machine
- validation mechanics
- first shift materialization details
- resume reconstruction logic
- execution pipeline
- task lifecycle
- closure
- snapshot generation

Those belong to adjacent canon documents.

---

## Integration Notes

This canon should align with:

- project/state entry metadata
- bootstrap entry flow
- first shift creation path
- resume routing and fallback behavior

Cold Start should remain the cleanest entry boundary:

no active work to resume
→ honest idle/bootstrap-capable entry
