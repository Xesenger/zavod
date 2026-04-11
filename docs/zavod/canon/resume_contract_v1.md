# ZAVOD v1 — Resume Contract

## Purpose

`Resume Contract v1`

- fixes what the system may honestly restore after restart
- defines the allowed and forbidden sources of resume in v1
- establishes honest restoration rules after restart
- separates truthful resume from fake continuity

---

## Position

`Resume Contract` answers only:

- what may be restored honestly
- which sources are allowed
- which surfaces are allowed
- what must be normalized conservatively

It does not define the reconstruction algorithm itself.

That belongs to:

- `resume_intelligence_v1.md`

Formula:

Resume Contract = what is allowed
Resume Intelligence = how it is reconstructed

---

## Source Alignment

This document aligns with:

- `project_state_model_v1.md`
- `project_state_persistence_v1.md`
- `cold_start_behavior_v1.md`
- `shift_lifecycle_v1.md`

It is complemented by:

- `resume_intelligence_v1.md`

---

## Core Principle

Resume in ZAVOD is truth-based, not memory-based.

The system must not pretend to remember.
It must reconstruct the next valid entry from persisted truth and allowed derived state only.

Formula:

resume = reconstruction from persisted truth and allowed derived state only

---

## Allowed Resume Sources

Resume in v1 may rely only on allowed sources.

Canonical allowed sources:

- `ProjectState`
- `ActiveShiftId`
- `ActiveTaskId`
- persisted `resume-stage` evidence
- shift/task storage
- current shift trace, if allowed by the layer boundary
- derived cache only as acceleration
- observations only as hints

Rule:

no derived source may override persisted truth.

---

## Forbidden Resume Sources

Resume must not use:

- UI-local memory
- hidden model memory
- guessed last state
- temporary runtime-only state as if it were truth
- fake reconstruction of live execution from impression alone
- arbitrary agent memory

Rule:

runtime memory is not a canonical resume source.

---

## Resume Cases

### Case 1 — No Active Shift

If `ActiveShiftId == null`, the system resumes into idle / bootstrap-capable project state.

Cold start without active shift remains valid.

---

### Case 1a — Active Discussion / Interaction Context

Active discussion / interaction context may be resumed only if it is backed by persisted truth.

Interaction-only UI memory is not a valid resume source.

---

### Case 2 — Active Shift Exists

If an active shift exists, the system may resume into active work context,
but only within the stage honestly supported by persisted truth.

Active shift/task truth is restored from:

- persisted project state
- active refs
- shift/task storage
- persisted stage evidence

---

## Allowed Resume Surfaces

Resume may honestly restore only these surfaces when they are genuinely supported:

- idle / bootstrap-capable state
- active discussion / interaction context only if backed by persisted truth
- active work context
- interrupted work context
- pending result review only if backed by persisted truth
- revision intake only if the corresponding persisted stage evidence exists

Resume must not restore a richer surface than persisted truth can support.

---

## Forbidden Fake Resume

Resume must not pretend that live execution is still running after restart unless that running state is actually backed by persisted truth.

Forbidden examples:

- reconstructing live execution from the last visual impression
- reconstructing UI-local or runtime-only state as canonical truth
- reconstructing pending result purely from temporary runtime memory
- reconstructing revision intake purely from UI-local state

---

## Interrupted / Stopped Truth

Interrupted or stopped work must not be resumed as normal in-progress execution unless persisted truth actually supports that reading.

Resumed work context must remain truthful to persisted state,
not to the last UI impression before restart.

Interrupted state may be restored only when that stage is actually fixed in persisted support.

If persisted support is insufficient,
the system must degrade to a safer honest state.

---

## Result Boundary

Pending result / review may be resumed only when that state follows from persisted truth.

Revision intake may be restored only when the corresponding persisted stage evidence exists.

Result review is resume-able only when the persisted resume-stage / runtime snapshot truth supports it.

---

## Resume Normalization

When persisted combinations are dirty, incomplete, or ambiguous,
the system must normalize them before hydrate.

Normalization goals:

- collapse ambiguous persisted state to one safe lifecycle stage
- prevent fake restoration of live running after process death
- preserve honesty of resumed entry
- prefer degraded truthful restore over visually impressive fake continuity

---

## Normalization Examples

- persisted “running” after process death
  → normalize away from fake live running

- incomplete mixed runtime / UI state
  → collapse to one safe resume stage

- active truth exists but richer runtime stage is missing
  → degrade to interrupted or safer active work context

---

## Honest User-Facing Behavior

Resume may feel like “memory” to the user,
but in ZAVOD it is not memory.

The system reconstructs a truthful resumed entry from:

- project truth
- active refs
- persisted stage evidence
- allowed derived aids

Wrong style:

- “I remember what we were doing”

Correct style:

- Active shift detected.
- Last persisted valid stage: ...
- Next honest entry: ...

---

## Contract Boundary

In v1, persisted project truth does not store as canonical resume source:

- UI-local state
- hidden reasoning state
- arbitrary agent memory
- runtime trace as project truth
- full execution history as canonical truth

Therefore resume must remain conservative.

Formula:

degraded honest restore is better than fake continuity

---

## Canons

- resume is truth-based, not memory-based
- only persisted truth and allowed derived aids may inform resume
- UI-local memory is not a valid resume source
- fake live-running restore after restart is forbidden
- normalization is mandatory when persisted state is ambiguous
- degraded honest restore is better than fake continuity
- pending result and revision intake require actual persisted stage support
- resume reconstructs the next honest entry, not the last visual impression

