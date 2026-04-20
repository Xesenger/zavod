# ZAVOD v1 — Execution Loop / Work Cycle

## Purpose

`Execution Loop / Work Cycle v1`

- describes how the system works inside an active shift
- connects intent, work preparation, execution, verification, result, and post-result decisions into one bounded cycle
- defines execution boundaries between interaction semantics, runtime work, result decision, and truth-update walls
- keeps execution strictly below project truth ownership

---

## Definition

`Execution Loop` is the repeating work cycle that runs inside an already active shift.

A single execution cycle:

- begins only after validated intent and active work truth exist
- runs through preparation, execution, verification, and result projection
- ends in a user/system decision on the result
- does not update project truth directly

Execution Loop is not the whole shift lifecycle.
A shift may contain multiple execution cycles.

---

## Position in Architecture

Execution Loop exists after:

- interaction has produced `validated intent`
- the relevant active work context has been materialized

It exists before:

- explicit shift closure
- snapshot creation
- project-level truth updates through closure

Formula:

validated intent
→ active work context
→ execution cycle
→ result decision
→ continue shift or close shift later

---

## Core Principle

Execution is runtime work inside an active shift.

It is not:

- chat discussion
- intent formation
- project truth mutation
- closure
- snapshot creation

Formula:

execution != truth update
closure = truth update wall

---

## Execution Entry Rule

Execution may begin only when all required preconditions are satisfied.

Execution does not begin from:

- raw chat
- `candidate` intent
- `refining` intent
- UI-local impression
- guessed active state

Execution begins only after:

- explicit validation
- active shift truth exists
- active work context exists
- execution preflight is satisfied

Rule:

no validated intent
→ no execution

---

## Execution Cycle Summary

Canonical execution cycle:

- validated intent exists
- active work context exists
- execution preflight
- bounded context handoff
- read-before-write gate
- execution runtime
- execution verification
- result projection
- result decision

Possible result decisions:

- accept
- revise
- abandon

After result decision:

- the shift may remain active for more work
or
- the shift may later be closed through closure

Execution cycle completion does not automatically imply shift completion.

---

## Execution Preflight

Before execution begins, the system must confirm:

- validated intent exists
- relevant active shift/work truth exists
- scope is defined
- execution belongs to the current active work context
- worker handoff will be based on structured bounded input

Preflight is:

- not execution
- not result
- not closure
- not truth mutation

It is the final runtime readiness boundary before actual work starts.

---

## Context Handoff

Before execution, the system must assemble a bounded working input.

Execution must not receive:

- full chat history
- full project history
- the whole repository without boundary
- hidden model memory as working truth

Execution may receive:

- validated intent
- active work context
- scope / allowed paths
- anchor set
- relevant truth context
- bounded execution context package

The context package is prepared runtime input.
It is not project truth.

---

## Read-Before-Write Gate

Before any mutation, the system / worker must pass the minimum grounding gate.

Minimum gate:

- read the primary target
- confirm immediate context / usage if relevant
- confirm scope boundary
- confirm anchor basis
- confirm the write belongs to the validated intent

Rule:

no write without read

Forbidden:

- writing by guess
- mutating an unread file
- relying on chat memory instead of actual read phase
- leaving scope during grounding without valid basis

---

## Worker Execution Strategy

Worker is not a blind executor.

Default behavior:

not understood
→ inspect
→ probe
→ search if needed
→ escalate if still blocked

Worker may:

- analyze code
- probe inside scope
- use system tools
- inspect structure before write
- perform limited internal refinement

Worker may not:

- change validated intent
- expand scope without basis
- mutate project truth directly
- override canon with external knowledge
- continue execution when the task conflicts with truth / canon

---

## Refusal

Refusal is a valid execution outcome.

Worker must stop if:

- the task conflicts with canon
- the task conflicts with project truth
- scope is violated
- grounding is insufficient
- a truth-level decision is needed instead of an implementation decision

Refusal does not break the system model.
It is a legitimate bounded outcome of execution.

---

## Internal Refinement

Worker may perform limited internal refinement inside one validated intent.

Allowed:

- compare candidate approaches
- choose the safer/minimal path
- reduce avoidable mistakes

Forbidden:

- silently expanding scope
- turning refinement into a new task
- using internal refinement as hidden truth mutation

---

## Bug-Fix Strategy

For defect-oriented work, the preferred path is:

reproduce
→ isolate
→ fix
→ verify

This is a preferred strategy,
not a universal mandatory shape for every execution cycle.

---

## Execution Verification Boundary

After execution and before result projection, the system must pass the verification boundary.

Verification:

- belongs to the execution/runtime layer
- happens before result projection
- is mandatory
- does not mutate project truth
- does not hide inside model reasoning

Verification layers may include:

- mechanical verification
- task verification
- project verification

Meaning:

- no verification → no result presentation
- raw unchecked output must not appear as final result
- verified result enters the result surface

If a QC role is used,
it operates on top of the verified result,
not instead of the verification boundary.

---

## Result Boundary

Result is a post-execution decision surface.

Result begins only when:

- the internal execution cycle is complete
- verification has passed sufficiently for projection
- the system has a user-facing result package

Result surface is used for:

- acceptance
- revision request
- abandon decision

Result surface is not:

- code generation
- raw execution continuation
- direct project truth mutation

Formula:

result = decision surface after execution

---

## Result Decisions

### Accept

Accept means:

- the produced result is accepted for apply / task-level completion path
- the current work cycle resolves successfully
- the shift may still remain active afterwards

Accept does not by itself imply shift closure.

---

### Revise

Revise means:

- the result is not accepted as final
- the current active work context continues into another execution cycle
- the system returns to a revision-capable work path

Revise does not create a new shift.
It continues work inside the active shift/work container.

---

### Abandon

Abandon means:

- the current work result is rejected as an outcome
- the relevant work item may be marked abandoned according to task/result rules
- produced history remains historical rather than silently deleted

Abandon is a valid result decision.
It is not a hidden reset.

---

## Safety & Control Rules v1

### Scope / allowed paths

Worker operates only inside the allowed task scope and allowed project paths.

### Change size guard

Small and medium changes may proceed inside the active work cycle.
Large changes require explicit confirmation before continuation and must not be auto-split to bypass this rule.

### Revision vs continuation

Meaning change requires a new revision.
Clarification or direct continuation stays inside the current revision/work cycle.

### Explicit not-done section

Each result must explicitly state what was not done.

### Error = valid outcome

Error is a valid execution-cycle outcome and does not break the system model.

### Empty result handling

If no change happened, the system must explicitly explain why.

### No unsolicited improvements

The agent does not introduce extra improvements without explicit request.

### No-LLM mode

The project may be opened, inspected, and routed without starting a model.

### Minimum action log

The system records at least:

- what was requested
- what was changed
- whether the result was accepted, revised, or abandoned

---

## Projection Boundary

Runtime projections are derived-only.

Rules:

- projections never mutate canonical lifecycle truth
- lifecycle → projection is allowed
- projection → lifecycle truth mutation is forbidden
- runtime snapshot is not the canonical immutable snapshot
- runtime task view is not lifecycle task truth
- entry pack and capsule are read-only working surfaces
- closure remains the only production truth-update wall

---

## Relation to Shift Lifecycle

Execution Loop runs inside an active shift.

Consequences:

- execution requires active shift truth
- multiple execution cycles may occur inside one shift
- task/result decisions do not automatically complete the shift
- the shift remains active until explicit closure moves it to completed

Execution Loop therefore depends on shift lifecycle,
but does not replace it.

---

## Relation to Closure

Execution Loop does not update project truth directly.

Execution may produce:

- accepted result
- refusal
- error
- no-op / empty outcome
- revision cycle
- abandoned outcome

But truth update still happens only through:

- closure
- project-level update path behind closure
- snapshot creation behind closure/history layers

Execution prepares outcomes.
Closure performs truth-finalizing work.

---

## Canons

- execution begins only after validated intent and active work context exist
- execution is bounded by scope, anchors, and prepared context
- no write without read
- refusal is a valid outcome
- error is a valid outcome
- empty result is valid if explicitly explained
- verification is mandatory before result presentation
- result is a post-execution decision surface
- execution does not mutate project truth directly
- accept does not automatically close the shift
- revise continues the active work path inside the current shift
- abandon remains a valid explicit outcome
- closure remains the only production truth-update wall

---

## Exclusions

This document does not define:

- cold start routing
- bootstrap behavior
- intent state machine in full
- first shift creation
- full task model internals
- shift closure internals
- snapshot structure
- archive implementation
- multi-shift concurrency
- product-level UI composition details

These belong to adjacent canon documents.
