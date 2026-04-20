# ZAVOD v1 - Runtime Execution Extensions

## Purpose

`Runtime Execution Extensions v1`
- defines execution-layer extensions that strengthen runtime behavior
- keeps these features below truth layer
- groups related runtime additions in one place

## Definition

`Runtime Execution Extensions` are supporting execution-layer mechanisms that improve safety, observability, and practical runtime quality.

These extensions are:
- not project truth
- not decision layer
- not closure layer
- not task truth

They belong strictly to runtime / execution support.

## Core Principle

```text
runtime extensions may improve execution,
but may not redefine truth

Included Runtime Extensions

This layer currently includes:

Runtime Signals
Tool Memory
Symbolic Editing (LSP-first)
Shadow Workspace
1. Runtime Signals
Definition

Runtime Signals are derived execution-layer signals exposed to UI and orchestration.

Examples:

running
verifying
waiting
interrupted
error
recovery mode
Purpose

Runtime Signals help the system show:

what execution is doing now
whether execution is progressing
whether execution is blocked
whether recovery is needed
Rules
signals are derived
signals do not mutate truth
UI reflects signals; UI does not redefine them
signals may influence projection, but not canonical lifecycle truth
Relation to UI

UI may use Runtime Signals to:

show progress
show state labels
show warnings
show execution-stage transitions

UI must not treat Runtime Signals as truth-level state by themselves.

2. Tool Memory
Definition

Tool Memory is a derived cache of tool effectiveness and preferred execution patterns.

Examples:

grep works better than search in this repo
analyzer X is noisy for this codebase
formatter Y is preferred for this stack
this tool is slow but high confidence
this validator is required for a given file type
Purpose

Tool Memory exists to improve execution efficiency and quality.

It helps the system:

choose the better tool
avoid repeating known bad tool choices
optimize repeated execution patterns
Rules
tool memory is optimization only
tool memory is not project truth
tool memory may influence execution choice, but not canon
stale tool memory may be discarded freely
tool memory must not silently override anchored project facts
Relation to Cache

Tool Memory is a specialized execution-layer derived cache.

It may be rebuilt, updated, or cleared without damaging project truth.

3. Symbolic Editing (LSP-first)
Definition

Where possible, the system should prefer symbolic editing over raw text replacement.

Examples:

rename via symbol graph
references lookup
definition lookup
semantic navigation
symbol-aware edit targeting
Principle
symbolic editing is preferred over blind replace
Why It Exists

Symbolic editing reduces:

accidental rename damage
wrong-file edits
missed usages
string-level false confidence

It shifts execution from:

text guessing
to:
code-structure-aware mutation
Rules
use symbol-aware tools where available
do not claim symbolic certainty without actual tool support
semantic edit support belongs to runtime tool layer, not Core
symbolic editing improves execution safety, but does not replace truth checks
Relation to Read Before Write

Symbolic Editing strengthens grounding,
but does not remove the need for Read Before Write Gate.

4. Shadow Workspace
Definition

Shadow Workspace is a temporary isolated execution workspace used for experimentation, mutation, and verification.

Purpose

Shadow Workspace helps the system:

test mutations safely
avoid polluting main working state prematurely
isolate failed attempts
discard partial or bad experiments cleanly
Rules
Worker may execute in a controlled temporary workspace
discarded or failed experiments must not pollute project truth
shadow workspace is execution-layer, not truth-layer
deletion / disposal is allowed after the run
shadow workspace may be optional in early v1 implementation
Boundary

Shadow Workspace does not:

replace closure
replace accepted result application
replace project truth
create truth by itself

It is only an isolated execution aid.

Execution Layer Boundary

All runtime execution extensions:

belong below Core
belong below project truth
may support execution
may support UI visibility
may support safer mutation
must not redefine lifecycle truth
Failure / Recovery Rule

Runtime extensions may:

improve recovery
improve visibility
improve tool choice
improve edit safety

But if they fail, the system must degrade safely:

no fake truth mutation
no hidden state corruption
no silent promotion of runtime artifacts to truth
Rules
runtime extensions are support layers, not truth layers
runtime signals are derived only
tool memory is optimization, not truth
symbolic editing belongs to runtime tools
shadow workspace belongs to execution layer
runtime extensions must remain replaceable without changing canon truth
Canons
runtime extensions strengthen execution, but do not own truth
UI may reflect runtime signals, but may not redefine them as truth
tool choice memory is useful, but must stay derived
symbolic editing is preferred where real support exists
isolated execution workspace is valid as runtime support
runtime support must never silently become project truth