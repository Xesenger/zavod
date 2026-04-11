# ZAVOD v1 — Task Model

## Purpose

`Task Model v1`

- defines the minimal unit of work inside an active shift
- formalizes the link between validated work intent and execution outcome
- establishes task truth separately from execution runtime richness
- provides the task-level object used by result, trace, and closure layers

---

## Definition

A `task` is the minimal active work unit inside a shift.

A task represents:

- what work is being performed
- why it is being performed
- what counts as sufficient completion
- what its final task-level outcome is

A task is narrower than a shift.
A shift may contain one or more task/work cycles over time.

---

## Position in Architecture

Task exists:

- after validated intent has crossed into active work truth
- inside an already active shift
- before shift closure finalizes the broader historical outcome

Task is:

- work truth

Task is not:

- project truth
- runtime execution state
- UI presentation state
- snapshot history

---

## Structure (v1)

A task in v1 contains the minimum bounded fields:

- `TaskId`
- `Description`
- `Intent`
- `Acceptance`
- `Status`

### Field meanings

#### `TaskId`

Stable technical identifier of the task.

#### `Description`

Short human-readable description of the work being done.

#### `Intent`

The work intent that justifies why this task exists.

This should reflect the validated work framing that entered execution,
not raw chat history.

#### `Acceptance`

The minimum completion expectation for this task.

In v1 this remains simple and bounded.
It is not a full test-plan or project-level definition of done.

#### `Status`

The task-level lifecycle state.

---

## Task States (v1)

Task Model v1 contains only three states:

- `active`
- `completed`
- `abandoned`

These states describe task truth,
not execution/runtime richness.

---

## Initial State

Any newly materialized task begins in state:

- `active`

This is the only valid initial state in v1.

---

## State Meanings

### `active`

The task currently exists as the active work unit of the shift.

`active` does not mean:

- worker is necessarily running right now
- result is currently pending
- runtime status is “in progress” in a richer technical sense

It means only:

- this is the current live task truth

---

### `completed`

The task has reached an accepted successful outcome through the result/apply path.

`completed` is terminal in v1.

A completed task:

- is no longer the active task
- remains historical truth inside the shift history
- may contribute to closure and snapshot summary

---

### `abandoned`

The task has ended without accepted successful completion.

This is the explicit task-level outcome for the result-abandon path.

An abandoned task:

- is terminal in v1
- does not return to `active`
- remains historical truth
- does not count as accepted successful completion

---

## Lifecycle

Canonical task lifecycle:

- task is created inside an active shift
- initial state = `active`
- task may become `completed`
- task may become `abandoned`
- no terminal task returns to `active`

Canonical transitions:

- `active -> completed`
- `active -> abandoned`

Forbidden transitions:

- `completed -> active`
- `abandoned -> active`
- `completed -> abandoned`
- `abandoned -> completed`

---

## Creation Rule

A task may be created only inside an active shift.

Task creation requires:

- active shift truth exists
- validated work framing exists
- the system is entering an active work cycle

Task must not be created from:

- raw discussion alone
- candidate intent
- UI-local interaction only
- guessed active work state

---

## Completion Rule

A task becomes `completed` only through the accepted result path.

Task completion is not caused by:

- runtime disappearance
- UI collapse
- worker stopping
- shift closure by itself

Formula:

accepted result
→ task completed

---

## Abandon Rule

A task becomes `abandoned` only through the explicit abandon/reject result path.

Abandon is:

- explicit
- historical
- terminal

It is not:

- silent deletion
- rollback into no task ever existing
- a hidden reset of work truth

Formula:

abandon result
→ task abandoned

---

## Active Task Uniqueness

At most one task may be active inside the current active shift work context in v1.

Consequences:

- no concurrent active tasks in one active work slot
- switching to a new active task requires the previous active task to have left `active`
- `ActiveTaskId` resolves to zero or one task at a time

---

## Relation to Execution

Execution runtime operates on top of task truth.

This means:

- execution may run while the task is `active`
- runtime richness does not create additional task states
- result decisions are evaluated against the current active task

Task therefore remains the stable truth object,
while execution remains a runtime process around it.

---

## Relation to Result

Result surface makes decisions about the current active task outcome.

Possible task-level outcomes from result:

- accept → `completed`
- revise → task remains the current active work truth
- abandon → `abandoned`

Important rule:

revision does not create a new terminal task state.
It continues work inside the same active task/work container unless a separate revision/task policy says otherwise.

---

## Relation to Shift

Task exists inside shift lifecycle.

Consequences:

- a shift may outlive one task
- task completion does not automatically complete the shift
- task abandonment does not automatically create a special shift state
- shift closure may happen only after task/work resolution according to adjacent rules

Task is narrower than shift.
Shift remains the broader active work container.

---

## Relation to Trace

Trace records activity around task work,
but trace does not replace task truth.

Trace may include:

- task-related actions
- work progress
- execution notes
- result-related activity

But task truth remains in the task object itself.

Formula:

trace observes
task records task truth

---

## Relation to Closure

Closure may summarize:

- which tasks completed
- which tasks were abandoned
- what remained unresolved

Task model helps closure distinguish:

- finished work
- abandoned work
- active/unresolved work

But closure does not redefine the task model.

---

## Relation to Decisions

A task may lead to a project-level decision,
but is not itself a decision.

If task work changes project direction or rules,
that must be fixed separately in the decision layer.

Task and decision must remain separate owners of meaning.

---

## Canons

- task is the minimal unit of active work truth inside a shift
- task states in v1 are only `active`, `completed`, and `abandoned`
- task begins as `active`
- task completion requires accepted result path
- task abandonment requires explicit abandon/reject path
- terminal task states do not return to `active`
- task truth remains separate from runtime execution richness
- task completion does not automatically complete the shift
- task abandonment does not erase task history
- task must stay simple and bounded in v1

---

## Boundaries

Task model does not define:

- roadmap planning
- backlog management
- priorities
- deadlines
- nested tasks
- task dependencies
- rich runtime states such as blocked/review/in-progress subtypes
- project-level truth mutation
- shift closure internals

Those belong to adjacent layers.

---

## Exclusions

Excluded from `Task Model v1`:

- priorities
- deadlines
- dependencies between tasks
- nested tasks
- complex workflow states
- rich planning metadata
- multi-task concurrency inside one active work slot
- backlog semantics

