# ZAVOD v1 — Execution Verification Pipeline

## Purpose

`Execution Verification Pipeline v1`

- defines the system-level verification layer inside execution
- introduces mandatory verification before Result UI
- separates verification from generation, QC role, and project truth mutation
- keeps verification below result decision and below closure

---

## Definition

`Execution Verification Pipeline` is the runtime pipeline that verifies Worker output before it is shown on the Result surface.

It:

- runs after Worker execution
- runs before Result UI
- belongs to the execution layer
- is not part of project truth ownership
- does not mutate project truth

---

## Position in Architecture

Canonical position:

Worker
→ execution output
→ Execution Verification Pipeline
→ Result UI
→ QC Role (if used)
→ user decision

Verification is therefore part of execution,
not a separate truth or closure layer.

---

## Distinction from QC Role

Execution Verification Pipeline is not the same thing as `QC Role`.

### Execution Verification Pipeline

- system-level
- deterministic where possible
- tool-based where possible
- mandatory part of execution
- validates whether output is structurally/task/project safe enough to reach Result UI

### QC Role

- review role
- operates on top of the verified result
- may judge alignment against intent, scope, anchors, and project truth at a higher semantic level
- does not replace the pipeline

Formula:

pipeline verifies first
QC reviews second

---

## Why Verification Exists

Verification exists to answer:

- is this result structurally acceptable?
- is it task-acceptable?
- is it project-safe enough to be shown as a result?

before it reaches the user-facing result boundary.

It prevents:

- raw unchecked output from appearing as final result
- blind trust in Worker self-report
- accidental drift between execution output and project truth constraints

---

## Verification Layers

Execution Verification Pipeline v1 is layered.

### 1. Mechanical Verification

Mechanical verification checks whether the output is structurally valid and operationally coherent where applicable.

Typical checks:

- lint if relevant
- format validity if relevant
- build if relevant
- artifact existence
- diff coherence
- structural validity of produced output

Examples:

- file exists
- artifact opens
- diff is not malformed
- code still builds when build is relevant
- produced JSON / config / asset is structurally valid when applicable

---

### 2. Task Verification

Task verification checks whether the output matches the currently validated work contract.

Typical checks:

- matches validated intent
- stays inside allowed scope
- respects anchors
- contains no hidden expansion
- contains the required outcome

Task verification asks:

- did Worker actually do the requested task?
- did Worker stay inside the current task boundary?
- did Worker avoid unrelated mutation?

---

### 3. Project Verification

Project verification checks whether the output, even if locally valid, remains globally acceptable for the system/project.

Typical checks:

- does not violate canon
- does not contradict active truth
- does not break decision-layer constraints
- does not introduce truth-level drift

Project verification asks:

- is the result locally valid but globally wrong?
- does the result conflict with the project system even if it technically “works”?

---

## Canonical Flow

Minimal v1 verification flow:

Worker result
→ mechanical verification
→ task verification
→ project verification
→ structured verification output

This is the minimum path before result projection.

---

## Verification Output

The pipeline returns structured verification output.

Suggested v1 model:

`ExecutionVerificationResult`

- `Status`: `OK` | `WARNINGS` | `ERRORS`
- `Verified`
- `Issues`
- `ToolOutputs` (optional)
- `NextAction`

---

## Status Meanings

### `OK`

Verification passed at the current required level.
The result may proceed to Result UI.

### `WARNINGS`

The result may still be usable,
but explicit cautions must remain visible.

### `ERRORS`

The result must not be presented as verified-ready final result.

---

## Verified Block

`Verified` contains only confirmed facts.

It must not contain:

- guesses
- optimism
- vague interpretation
- “probably fine” language
- hidden semantic overreach

Rule:

verified means evidence-backed only

---

## Issues Block

`Issues` contains:

- concrete failures
- concrete gaps
- concrete violations
- concrete warnings

Issues must be:

- specific
- testable where possible
- attributable to an actual verification finding

---

## Tool Outputs

`ToolOutputs` may contain:

- lint output
- build output
- validation logs
- structured verification messages

Tool output is optional,
but should remain inspectable where useful.

---

## Next Action

`NextAction` reflects what the system should do after verification.

Typical values:

- continue to Result UI
- return for revision
- block result presentation
- escalate for review

`NextAction` is derived from the verification outcome.
It is not a free-form opinion.

---

## Rules

- verification is mandatory before result presentation
- verification does not auto-fix the result
- verification does not auto-commit
- verification does not auto-apply changes
- verification does not update project truth
- verification must be explicit and visible
- verification must not be hidden inside model reasoning

---

## Relation to Worker

Worker may provide:

- self-check
- known risks
- implementation notes

But Worker does not define final verification status.

Execution Verification Pipeline is the first system-level boundary after execution.

---

## Relation to Result Surface

Result UI appears only after the execution cycle reaches the result boundary.

Verification Pipeline is still part of execution.

Result UI is the first user-facing decision surface after verification.

Therefore:

- no verification → no result projection
- unchecked output must not appear as verified-ready result

---

## Relation to Execution Loop

Verification is a sub-boundary inside the broader execution cycle.

Execution Loop owns:

- preflight
- bounded context handoff
- read-before-write gate
- execution runtime
- verification boundary
- result projection

This document owns the verification boundary specifically.

---

## Relation to Truth

Execution Verification Pipeline:

- does not create truth
- does not mutate truth
- does not bypass closure
- does not replace the decision layer
- does not replace closure as the truth-update wall

It only validates execution output before user-facing result handling.

---

## Failure Behavior

If verification fails:

- unchecked output must not be presented as verified-ready result
- execution may return for revision
- the system may surface warnings or errors
- escalation may happen if the issue is not purely execution-level

Failure is therefore a valid bounded pipeline outcome,
not a collapse of the system model.

---

## Canons

- verification belongs to the execution layer
- verification is mandatory before result presentation
- verification does not mutate project truth
- system verification and QC role are separate concepts
- no raw unchecked execution result should appear as final result
- verified facts must remain evidence-based
- verification may block, warn, or allow,
  but may not silently transform truth

---

## Exclusions

This document does not define:

- worker generation strategy
- result decision semantics in full
- shift closure
- snapshot creation
- project-level decision logging
- archive/history layout
- UI composition details beyond the verification boundary

Those belong to adjacent canon documents.

