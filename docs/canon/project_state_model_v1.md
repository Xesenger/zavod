# ZAVOD v1 — Project State Model

## Purpose

`ProjectState v1`

- defines the minimal typed representation of the current active project state
- is assembled from `.zavod/meta/project.json` and active truth pointers
- is used by the system as a runtime/read model
- is not a replacement for project truth documents

---

## Definition

`ProjectState` is the in-memory typed active view of the project.

It exists to give the system a stable, minimal, structured representation of:

- project identity/meta
- active linkage
- active truth document refs
- required system paths

`ProjectState` does not own project meaning.
It reflects currently active project truth through typed fields and pointers.

---

## Nature

- `ProjectState` is a runtime/read model
- `ProjectState` is assembled, not authored as a meaning document
- `ProjectState` is derived from persisted meta + truth refs
- `ProjectState` may be rebuilt deterministically
- losing an in-memory `ProjectState` must not mean losing project truth

---

## Typed Contract v1

`ProjectState`

- `Version`
- `ProjectId`
- `ProjectName`
- `LayoutVersion`
- `EntryMode`
- `ActiveShiftId`
- `ActiveTaskId`

### `ProjectPaths`

- `ProjectRoot`
- `ZavodRoot`
- `MetaFilePath`
- `ProjectTruthRoot`

### `TruthPointers`

- `ProjectDocumentPath`
- `DirectionDocumentPath`
- `RoadmapDocumentPath`
- `CanonDocumentPath`
- `CapsuleDocumentPath`

---

## Blocks

`ProjectState v1` contains only a few bounded blocks:

- `identity/meta` — minimal technical identity of the project
- `paths` — filesystem roots needed by the system
- `truth pointers` — refs to active truth documents
- `active refs` — current active shift/task linkage or `null`

In `v1`, paths to `shifts/` and `decisions/` are derived from `ZavodRoot`.
Tasks are not a separate directory — they are nested inside each shift's JSON
(see `project_truth_storage_layout_v1.md`). Separate typed pointers for these
layouts are not required at this stage.

---

## Active Linkage Meaning

`ActiveShiftId` and `ActiveTaskId` represent active linkage,
not a full lifecycle dump.

They indicate:

- whether an active shift exists
- whether an active task exists
- which shift/task storage unit should be consulted by adjacent layers

They do not by themselves encode:

- execution runtime state
- result state
- closure state
- snapshot history

---

## Truth Ownership Difference

`ProjectState` must remain separate from actual truth owners.

Ownership split:

- `.zavod/meta/project.json` — persisted technical entry/meta file
- `ProjectState` — assembled in-memory typed view
- `project.md` — current project identity
- `direction.md` — current direction
- `roadmap.md` — current route
- `canon.md` — rules and boundaries
- `capsule.md` — compact derived summary of active project truth

`ProjectState` points to these documents.
It does not replace them.

The canonical truth pointers may still be valid even when some pointed files are not yet materialized.
Missing pointed files must not be guessed into truth by the read model.

---

## Relation to Persistence

Persistence owns the stored meta and truth refs.

`ProjectState` is what the system obtains after reading persisted project meta and resolving the current active pointers.

Formula:

persisted meta + truth refs
→ `ProjectState`

This means:

- `ProjectState` is not itself the persisted owner of all project meaning
- persistence may store the minimal data needed to rebuild it
- rebuilding `ProjectState` must be deterministic

---

## Relation to Builder

`project_state_builder_v1.md` owns the assembly mechanism.

This document owns the model being assembled.

Ownership split:

- `Project State Model` = what fields/state exist
- `Project State Builder` = how the system reads storage and constructs them

The model must stay stable enough that the builder is a pure assembly layer,
not a place where new product semantics are invented.

---

## Cold Start Compatibility

`ProjectState` must remain valid when:

- `ActiveShiftId == null`
- `ActiveTaskId == null`

This means:

- cold start is a valid system state
- bootstrap-capable entry remains possible
- active work is not required for `ProjectState` to exist
- full materialization of project truth documents is not required for `ProjectState` to exist

Formula:

no active shift does not mean no valid project state

---

## Canons

- `ProjectState v1` reflects only current active project state
- `ProjectState v1` is a typed read model, not a truth owner
- `ProjectState v1` does not store decisions content
- `ProjectState v1` does not store snapshot content
- `ProjectState v1` does not store trace/journal data
- `ProjectState v1` does not store cache or derived context packages
- `ProjectState v1` may be built with `ActiveShiftId = null`
- cold start remains a valid state
- `ProjectState` must be rebuildable from persisted meta and active truth refs

---

## Exclusions

Excluded from `ProjectState v1`:

- execution state machine data
- retrieval data
- prompt data
- model/provider fields
- user settings
- history collections
- raw decisions content
- raw snapshot bodies
- trace/journal bodies
- speculative future fields
- arbitrary timestamps without concrete need

---

## Boundary Note

If the system needs richer lifecycle or historical information,
it must read the proper owner layer:

- shift storage
- task storage
- decision layer
- snapshot layer
- trace layer

`ProjectState` should remain small, typed, current, and honest.
