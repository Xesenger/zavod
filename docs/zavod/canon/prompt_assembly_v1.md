# ZAVOD v1 - Prompt Assembly

## Purpose

`Prompt Assembly v1`
- defines how the system constructs model requests
- prevents raw chat dump prompting
- establishes deterministic request composition
- separates stable role behavior from changing work context

## Definition

`Prompt Assembly` is the system layer that constructs the final execution request for a role.

It does not:
- validate intent by itself
- create project truth
- invent task scope
- replace Context Builder
- replace Anchor System

It does:
- assemble structured execution input
- keep request composition explicit
- preserve deterministic prompt structure
- reduce drift caused by free-form chat accumulation

## Core Principle

```text
chat != execution context

ZAVOD does not treat raw chat history as the default execution input.

Execution input must be assembled by the system from structured parts.

Assembly Formula
Role Core
+ Shift / Context Package
+ Task Block
= Execution Request
Layer 1 - Role Core

Role Core is the stable role prefix.

It contains:

role definition
behavioral rules
refusal rules
truth boundaries
output contract
non-negotiable system constraints

Examples:

Worker execution rules
Lead interpretation rules
QC review rules
prohibition on truth mutation
requirement to stay inside scope
structured output format
Properties

Role Core:

should remain as stable as possible
should not drift with random chat wording
is system-controlled
is not user-controlled
is reusable across requests of the same role
Layer 2 - Shift / Context Package

Shift / Context Package is the bounded working context for the current step.

It may contain:

active shift state
active task or validated intent
relevant project truth excerpts
relevant decisions
bounded project context
allowed paths / scope
short current execution state
selected observations
selected cache-derived helpers

It must not contain:

full chat dump
unrelated historical discussion
whole project by default
unbounded archive
implicit hidden memory
Principle
context must be curated, not accumulated
Layer 3 - Task Block

Task Block contains the current executable unit.

It includes:

current task
acceptance criteria
scope
exclusions
required result shape
execution constraints
relevant anchors

Task Block is the execution-facing block of the request.

It must remain:

specific
current
bounded
aligned with validated intent
Determinism Rule

Prompt Assembly must be deterministic at the system level.

That means:

same role + same context basis + same task basis
should produce the same request structure
hidden prompt mutations are forbidden
chat phrasing must not silently become truth
prompt structure must not depend on UI-local accidents
Chat Boundary

Chat may:

help form intent
provide clarification
provide user wording
provide examples or constraints

Chat may not:

become default project truth
bypass validation
replace structured task input
replace scope definition
replace anchor resolution
Relation to Intent

Prompt Assembly begins only after the system has enough structured basis for role execution.

That basis may include:

validated intent
active task
execution-ready revision
explicit review target for QC
explicit interpretation target for Lead

Prompt Assembly does not decide intent validity by itself.

Relation to Context Builder

Prompt Assembly consumes the output of Context Builder.

Difference:

Context Builder decides what bounded context should enter execution
Prompt Assembly decides how that context is placed into the final request structure
Relation to Anchor System

Prompt Assembly consumes anchors.

Anchors may appear inside:

Shift / Context Package
Task Block

Prompt Assembly does not define anchor truth.
It only places already resolved anchors into the request.

Relation to Roles

Prompt Assembly is role-agnostic at the system level, but role-specific in its final output.

Examples:

Worker gets execution-oriented request
Lead gets interpretation / validation-oriented request
QC gets review-oriented request

The assembly pattern stays stable even when the role changes.

Stable vs Dynamic Parts

Prompt Assembly should keep a clear distinction between:

Stable
role behavior
truth boundaries
output rules
refusal rules
non-negotiable system constraints
Dynamic
current task
current shift state
current bounded context
current anchors
current acceptance criteria

This keeps:

prompts understandable
behavior less noisy
role identity stable across turns
Hidden Layer Rule

There must be no hidden prompt layer that:

overrides visible structured input
silently injects truth
mutates scope invisibly
rewrites task meaning outside system flow

If the system needs something in the request,
it must exist as an explicit assembly layer.

Minimal Valid Request

A minimal safe execution request should normally include:

Role Core
current task basis
current scope
current truth constraints if relevant
bounded context package
relevant anchors

Without this basis, the request should be considered under-assembled.

Failure Behavior

If Prompt Assembly cannot construct a sufficiently grounded request:

execution must not proceed
the system may request clarification
the system may trigger more context construction
the system may refuse unsafe handoff
Rules
prompt assembly is system-controlled
roles receive structured input only
raw chat is not project truth
execution input must remain explicit
bounded context is required
hidden prompt layers are forbidden
stable and dynamic prompt parts must remain distinct
Canons
Prompt Assembly is a system layer
chat history is not the default execution prompt
execution input must be assembled from structured parts
Role Core, Context Package, and Task Block are distinct layers
prompt assembly must remain deterministic
the system must prefer explicit request structure over accumulated conversational mass