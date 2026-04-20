# ZAVOD v1 — Project State Builder

## Purpose

`ProjectStateBuilder v1`

- defines how the system assembles `ProjectState` from project filesystem state
- reads technical project meta and constructs the active typed project view
- does not execute work
- does not mutate project truth
- does not write into `.zavod/`

---

## Definition

`ProjectStateBuilder` is the deterministic assembly layer that builds `ProjectState` from:

- project root
- `.zavod/meta/project.json`
- canonical active truth pointer locations

It is not:

- execution
- lifecycle orchestration
- bootstrap logic
- resume logic
- truth mutation

Formula:

filesystem + project meta + canonical refs
→ `ProjectState`

---

## Input

Required input:

- `projectRootPath`

This input points to the repository/project root from which `.zavod/` is resolved.

---

## Output

Builder returns:

- valid `ProjectState`
or
- a deterministic builder error

The builder does not return partial product behavior decisions.
It only returns:

- assembled state
or
- failure to assemble state

---

## Assembly Algorithm

### Step 1 — Resolve `.zavod/`

- resolve `.zavod/` from `projectRootPath`
- if `.zavod/` does not exist → return `ZavodNotInitialized`

### Step 2 — Resolve meta file

- resolve `.zavod/meta/project.json`

### Step 3 — Read and parse meta

- read `project.json`
- parse it according to the project meta contract
- if file is missing, malformed, or semantically unusable → return `InvalidProjectMeta`

### Step 4 — Extract technical meta

Extract the minimum fields required for `ProjectState`, including:

- `projectId`
- `projectName`
- `layoutVersion`
- `entryMode`
- `activeShiftId`
- `activeTaskId`

### Step 5 — Build `ProjectPaths`

Construct typed paths:

- `ProjectRoot`
- `ZavodRoot`
- `MetaFilePath`
- `ProjectTruthRoot`

### Step 6 — Build `TruthPointers`

Construct canonical active truth pointers:

- `ProjectDocumentPath`
- `DirectionDocumentPath`
- `RoadmapDocumentPath`
- `CanonDocumentPath`
- `CapsuleDocumentPath`

These pointers are constructed deterministically from canonical locations,
even if the pointed documents do not yet exist.

### Step 7 — Assemble `ProjectState`

Build the typed `ProjectState` object from:

- extracted meta
- resolved paths
- resolved truth pointers
- active linkage fields

### Step 8 — Return result

- return the assembled `ProjectState`

---

## Behavioral Rules

Builder behavior is intentionally narrow.

The builder:

- reads only what is required for state assembly
- does not read markdown document bodies as project meaning
- does not validate business logic
- does not inspect shift/task bodies to invent richer lifecycle meaning
- does not depend on `history`, `cache`, or `journal` for core state assembly
- must remain valid when `ActiveShiftId == null`

Formula:

assemble current typed state,
do not interpret product meaning

---

## Cold Start Rule

`ProjectStateBuilder` must work when:

- `ActiveShiftId == null`
- `ActiveTaskId == null`

This ensures:

- cold start is a valid buildable project condition
- bootstrap-capable entry is possible
- active work is not required for successful `ProjectState` assembly

---

## Determinism Requirement

For the same filesystem/meta inputs,
`ProjectStateBuilder` must produce the same `ProjectState` result.

It must not:

- invent missing meaning
- use hidden runtime memory
- depend on UI state
- depend on prior agent state
- guess richer lifecycle state

---

## Error Contract

Minimum builder errors:

- `ZavodNotInitialized`
- `InvalidProjectMeta`

Meaning:

### `ZavodNotInitialized`

Returned when the repository/project root does not contain a valid `.zavod/` root.

### `InvalidProjectMeta`

Returned when `.zavod/meta/project.json` is missing, malformed, or cannot provide the minimal contract required for `ProjectState` assembly.

Builder errors should remain structural and deterministic,
not conversational or heuristic.

---

## Relation to Project State Model

`project_state_model_v1.md` owns:

- what `ProjectState` is
- which fields it contains
- what it means semantically

`ProjectStateBuilder` owns:

- how those fields are assembled from storage

Formula:

model defines
builder assembles

---

## Relation to Persistence

Persistence owns:

- stored meta
- filesystem layout
- active linkage storage
- active truth ref storage

Builder consumes persisted structure.
It does not own or mutate it.

Formula:

persistence stores
builder reads

---

## Relation to Bootstrap / Resume

Builder must remain below bootstrap and resume layers.

It does not:

- choose between bootstrap and resume
- decide lifecycle strategy
- classify honest resume stages
- initiate first shift creation

It only provides the assembled `ProjectState`
that those layers may use.

---

## Canons

- builder is deterministic
- builder has no side effects
- builder does not write to filesystem
- builder does not mutate project truth
- builder does not initiate bootstrap, resume, shift creation, or execution
- builder works with `ActiveShiftId = null`
- builder does not depend on cache/journal/history for core assembly
- builder constructs pointers even when pointed files are not yet present

---

## Exclusions

Excluded from `ProjectStateBuilder v1`:

- markdown content loading as semantic interpretation
- lifecycle reasoning
- bootstrap routing
- resume-stage reasoning
- DI concerns
- async orchestration
- retries
- system-wide logging policy
- execution/runtime services
- additional abstraction layers beyond direct deterministic assembly

---

## Boundary Note

If richer meaning is needed after `ProjectState` is built,
other layers must handle it:

- bootstrap layer
- resume layer
- shift/task storage readers
- lifecycle orchestration

`ProjectStateBuilder` must stay small, strict, and boring.

