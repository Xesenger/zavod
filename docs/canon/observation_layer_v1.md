# ZAVOD v1 - Observation Layer

## Purpose

`Observation Layer v1`
- defines a lightweight hint layer for repeated project quirks and local constraints
- helps the system avoid repeating the same mistakes
- remains strictly below truth and decision layers

## Definition

`Observation` is a system hint, not truth.

Observation may record:
- local architecture quirks
- known non-fatal weirdness
- temporary constraints
- review hints
- recurring risk notes

## Core Principle

```text
observation = hints, not truth

Why Observation Exists

Observation exists to help the system:

avoid asking the same thing repeatedly
avoid stepping on known quirks
preserve local practical knowledge that is useful, but not truth-level
carry soft warnings across execution and resume

Observation is for assistance, not for authority.

What Observation Is Not

Observation is not:

canon
decision
task truth
shift snapshot
project truth
cache truth

Observation must never become a hidden replacement for:

project.md
direction.md
roadmap.md
canon.md
decisions
ProjectState
Location

Suggested location:

.zavod/meta/agent-observations.json

Observation lives:

near meta/cache layers
inside the repo
outside active truth documents
outside decisions
Minimal Record

{
  "id": "OBS-0001",
  "scope": "src/ui/MainWindow.xaml.cs",
  "kind": "architecture_hint",
  "note": "File is overloaded but currently acts as an intentional assembly point.",
  "confidence": 0.72,
  "status": "active",
  "source": "system",
  "createdAt": "2026-04-01T12:00:00Z"
}

Recommended Fields
id
scope
kind
note
confidence
status
source
createdAt

Optional:

updatedAt
relatedAnchors
supersededBy
Kinds

Suggested kind values:

architecture_hint
known_quirk
temporary_constraint
legacy_behavior
review_note
risk_hint
Status

Suggested status values:

active
obsolete
confirmed
Meaning
active
currently relevant and usable as a hint
obsolete
no longer relevant
must not continue affecting execution
confirmed
repeatedly observed and considered stable as a hint
still not truth
Sources

Observation may come from:

repeated system analysis
confirmed code behavior
explicit user remark
repeated review findings
shift trace / continuation context
resume-related reconstruction hints

Observation must not come from:

pure guesswork
unverified model intuition
vague chat impressions without basis
Usage

Observation may be used in:

Context Builder
Resume Intelligence
execution warnings
QC review hints
task preparation
risk explanation

Observation may help the system say:

“this area is known to be fragile”
“this file is intentionally weird”
“tests fail here for known non-task reasons”
Usage Boundary

Observation may influence:

caution
warnings
prioritization
local context preparation

Observation may not influence:

truth mutation
decision creation
canon override
scope expansion by itself
Relation to Truth

Observation stays below truth.

If a note becomes a real project rule:

it must move to canon

If a note changes project direction:

it must become a decision

If a note changes active project state:

it must flow through closure / ProjectState update
Relation to Decisions

Observation is not a decision.

Difference:

Observation = useful local hint
Decision = accepted project-level change

A decision changes the project.
An observation only helps the system work more safely.

Relation to Trace

Observation may be informed by trace,
but trace does not automatically become observation.

Only repeated or useful distilled hints should enter Observation Layer.

Relation to Cache

Observation is not the same as cache.

Difference:

Cache = acceleration / derived convenience
Observation = soft project hint

Cache may be rebuilt freely.
Observation must remain interpretable and reviewable.

Lifecycle Rules
observations may be added conservatively
stale observations should become obsolete
contradictory observations should not remain active together
observations may be reviewed and cleaned over time
observations should stay short and specific
Forbidden Behavior

Observation Layer must not:

redefine canon
silently create rules
act as hidden memory of truth
justify execution outside scope
override anchored facts
replace real code inspection
replace validated intent
replace decisions
Rules
observation is optional helper layer
observation must stay below truth and decisions
observation may guide system behavior, but cannot redefine truth
stale observations must be downgraded or removed
observations must remain explicit and reviewable
Canons
observation = hints, not truth
observation belongs to meta-level support, not active truth
observation may help execution and resume
observation must never become hidden canon
if a hint becomes a rule, it must move to canon or decision