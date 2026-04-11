# ZAVOD v1 — Resume Intelligence

## Purpose

`Resume Intelligence v1`

- defines how the system reconstructs working entry after restart
- complements `resume_contract_v1.md`
- separates truthful reconstruction from fake continuity
- provides the reconstruction flow used to build the next honest entry into work

---

## Definition

`Resume Intelligence` is the reconstruction layer that builds a resumed entry package from persisted truth and allowed derived aids.

It is not:

- agent memory
- UI memory
- hidden continuity
- guessed last state
- replay of the last visible screen

It is a truthful reconstruction mechanism.

---

## Relation to Resume Contract

`Resume Contract` defines:

- what may be restored honestly
- which sources are allowed
- which surfaces are allowed
- what must be normalized conservatively

`Resume Intelligence` defines:

- how the system evaluates persisted support
- how it chooses the next honest restorable stage
- how it degrades ambiguous state
- how it builds the resumed entry package

Formula:

contract = boundary
intelligence = reconstruction mechanism

---

## Reconstruction Goal

The goal of Resume Intelligence is:

- to determine the next honest entry point into work
- to reconstruct only the stage that is truly supported
- to preserve user trust through explicit, grounded restore behavior

It does not try to answer:

- what looked most impressive before restart
- what probably was happening
- what would feel like magical continuity

---

## Inputs

Resume Intelligence starts from persisted truth and allowed derived aids already permitted by `resume_contract_v1.md`.

Operationally it works with:

- `ProjectState`
- active refs (`ActiveShiftId`, `ActiveTaskId`)
- persisted resume-stage evidence
- shift/task storage
- bounded trace, if allowed
- cache only as acceleration
- observations only as hints

It treats these inputs with ordered trust:

persisted truth first,
derived support second,
accelerators and hints last.

---

## Reconstruction Flow

Resume Intelligence follows this flow:

1. read persisted project truth and active refs
2. determine whether active shift/task truth exists
3. read persisted stage evidence
4. evaluate which restorable stage is honestly supported
5. normalize ambiguous or over-rich combinations
6. degrade to a safer truthful stage when necessary
7. build resumed entry package
8. expose next allowed actions and optional explanation

Formula:

persisted truth
→ active refs
→ stage evidence
→ normalization
→ safe resumed entry

---

## Stage Detection

Resume Intelligence should determine:

- whether an active shift exists
- whether an active task exists
- whether a richer stage is actually backed by persisted evidence
- whether only a simpler stage can be restored honestly
- whether normalization is required before hydrate

The question is never:

- what was probably happening?

The real question is:

- what stage is provably restorable now?

---

## Stage Selection Strategy

Resume Intelligence must prefer the richest stage that is still honest.

Selection rule:

- if a richer resumed surface is fully supported by persisted evidence, it may be used
- if support is incomplete, ambiguous, or only visual/runtime-local, degrade to a simpler stage
- if no active shift exists, return bootstrap-capable idle entry

This produces a principled ladder such as:

- bootstrap / idle
- active discussion entry
- active work entry
- interrupted work entry
- pending result entry
- revision intake entry

but only when the chosen stage is actually supported.

---

## Normalization

Normalization is mandatory whenever persisted state is:

- ambiguous
- incomplete
- contradictory
- too rich to restore honestly after restart
- internally mixed across incompatible stage flags

Normalization goal:

prefer degraded truth over fake continuity

Normalization behavior:

- collapse mixed or impossible combinations to one safe lifecycle stage
- remove fake live-running illusions after process death
- preserve the highest truthful stage that survives persisted support
- prevent UI-rich restore from outrunning actual truth support

---

## Degradation Strategy

Degradation is not failure.
It is the honesty mechanism of Resume Intelligence.

Examples:

- persisted “running” after process death
  → do not restore fake live execution
  → degrade to interrupted or safe active work entry

- active shift exists but richer runtime-stage support is gone
  → restore safe work context, not invented live progress

- dirty mixed review/runtime flags
  → normalize to one safe resumed stage

Principle:

truthful degraded restore is better than fake continuity

---

## Use of Trace

Current shift trace may help Resume Intelligence:

- understand the latest known work context
- identify the last stable point for explanation
- enrich the resumed entry package with bounded context

But trace must not:

- override persisted truth
- create stage authority by itself
- invent stages not supported by storage
- simulate execution continuity

Trace assists explanation.
It does not own reconstruction truth.

---

## Use of Cache

Cache may accelerate reconstruction, for example through:

- prebuilt entry package
- cached capsule
- recent context summary

But cache must obey:

- cache may accelerate resume, but cannot define truth

If cache conflicts with persisted truth:

- truth wins
- cache is rebuilt or discarded

Cache is an optimization layer only.

---

## Use of Observations

Observations may help Resume Intelligence:

- warn about fragile areas
- warn about known quirks
- improve resumed context packaging
- avoid repeating already-known local warnings

But observations remain hints only.
They must not:

- create resume stages
- override truth
- simulate memory
- decide what the active lifecycle stage is

---

## Resumed Entry Package

Resume Intelligence should output a structured resumed entry package.

It may contain:

- active project identity
- active shift/task refs if present
- chosen resumed stage
- next allowed actions
- bounded resumed context
- optional explanation of why this stage was selected
- optional degradation note when a richer stage was unavailable

The package should be sufficient for:

- honest user entry
- correct UI projection
- safe continuation by the system

---

## User-Facing Behavior

Resume Intelligence should not surface itself as memory.

Wrong style:

- “I remember what we were doing”

Correct style:

- active shift detected
- last persisted valid stage identified
- next honest entry proposed

Example tone:

- Active shift detected.
- Last persisted valid stage: ...
- Next honest entry: ...

This preserves trust and makes resume feel grounded rather than magical.

---

## Failure Behavior

If Resume Intelligence cannot reconstruct a richer safe stage:

- it must fall back to a simpler honest stage
- it must not hallucinate continuity
- it may explain that richer runtime context was unavailable after restart

If persisted support is too weak for any rich resumed surface:

- return the safest valid entry,
  not the most impressive one

---

## Boundaries

Resume Intelligence does not:

- mutate project truth
- restore hidden model state
- replace closure
- replace lifecycle truth
- create decisions
- invent result state
- own the contract of what is allowed

It is a reconstruction layer only.

---

## Rules

- resume is reconstruction, not remembrance
- reconstruction begins from persisted truth
- stage choice must be evidence-backed
- normalization is mandatory when state is ambiguous
- trace may assist, but may not redefine truth
- cache may accelerate, but may not override truth
- observations may hint, but may not create state
- if support is insufficient, degrade to a safer honest stage

---

## Canons

- Resume Intelligence is not memory
- truthful degraded restore is better than fake continuity
- richer resume is allowed only when stage evidence supports it
- the system reconstructs the next honest entry, not the last visual impression
- reconstruction logic must remain strictly below truth ownership

