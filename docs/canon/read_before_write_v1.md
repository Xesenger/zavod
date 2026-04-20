# ZAVOD v1 - Read Before Write Gate

## Purpose

`Read Before Write Gate v1`
- prevents blind mutation
- requires grounded understanding before any write
- protects the project from string-level guessing

## Definition

Before any write or edit, the system must complete the minimum required read phase.

That read phase must establish:
- target file(s)
- immediate usage context
- scope boundary
- relevant truth constraints
- relevant anchors

## Core Principle

```text
no write without read

Write is not allowed to begin from:

chat wording alone
vague memory
guessed structure
assumed file relationships
symbolic names without grounding
Why This Gate Exists

Read Before Write Gate exists to prevent:

blind text replacement
accidental edits in the wrong file
scope drift during mutation
fake confidence from partial context
“I think this is the right place” style execution

This gate forces execution to begin from confirmed local reality.

Minimum Gate

Before write, the system must:

read the primary target
inspect immediate related usage if relevant
confirm scope
confirm anchor basis
confirm that the write belongs to validated intent
1. Primary Target Read

The system must read the main mutation target before any edit.

Examples:

file to change
config to update
symbol to modify
artifact to rewrite

If the primary target was not read, write must not begin.

2. Immediate Related Usage

If the task implies dependency or usage sensitivity, the system must inspect the immediate surrounding context.

Examples:

where a method is called
where a config value is consumed
what interface the file implements
what symbol relationships are affected

This step is not “read the whole project”.
It is “read enough to avoid blind mutation”.

3. Scope Confirmation

Before write, the system must confirm:

current task scope
allowed paths
whether the current target actually belongs to that scope

If the candidate edit falls outside allowed scope:

write must stop
the system must escalate, revise scope, or refuse
4. Anchor Basis

Before write, the system must confirm relevant anchors.

Examples:

task anchor
file anchor
symbol anchor
canon anchor
decision anchor

Critical missing anchors must block safe write.

5. Validated Intent Basis

Before write, the system must confirm that the mutation belongs to:

current validated intent
current task
current execution cycle

The system must not write based on:

stale intent
invalidated task meaning
leftover UI state
adjacent “while we are here” logic
Relation to Probing

Probing may satisfy part of the read phase.

Difference:

Probing discovers local structure and likely touch-points
Read Before Write Gate blocks mutation until grounding is sufficient

So:

probing helps prepare
the gate decides whether write may begin
Relation to Context Builder

Context Builder prepares bounded execution context.

Read Before Write Gate validates that the prepared context is sufficient for actual mutation.

Difference:

Context Builder assembles context
Read Before Write Gate enforces minimum grounding before write
Relation to Worker

Worker must obey this gate.

Worker must not:

edit files not sufficiently read
mutate guessed structures
operate from remembered phrasing instead of inspected targets
use “likely correct” as a substitute for grounding
Forbidden Behavior

The system must not:

write based on guessed structure
mutate files it did not inspect sufficiently
rely on chat memory instead of actual read phase
expand outside scope during grounding without explicit basis
use search results as a substitute for local read
treat naming similarity as sufficient grounding
Safe Outcome When Gate Fails

If the gate fails, valid outcomes include:

additional probing
additional read
clarification request
escalation
refusal

Gate failure is not a crash.
It is a valid stop condition before unsafe mutation.

Minimal Safe Write Basis

A safe write should normally have:

current validated intent
current task basis
read primary target
scope confirmation
anchor basis
enough immediate local context to avoid blind mutation
Rules
write is gated by grounded read
primary targets must be read before mutation
immediate related context must be inspected when relevant
scope must be confirmed before mutation
critical missing anchors must block safe write
unsafe mutation must stop before write, not after damage
Canons
no write without read
read-before-write is mandatory for safe execution
mutation without sufficient read is invalid execution
this gate protects against blind replace behavior
safe grounding is required before any project mutation