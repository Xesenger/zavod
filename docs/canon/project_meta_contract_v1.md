# ZAVOD v1 — Project Meta Contract

## Purpose

`.zavod/meta/project.json`

- defines the minimal persisted technical meta contract for a project
- serves as the technical entry file used for `ProjectState` assembly
- does not replace `project.md`, `direction.md`, `roadmap.md`, `canon.md`, or `capsule.md`
- stores only the minimum meta needed by the system to enter and assemble the active project view

---

## Definition

`project.json` is the minimal persisted project meta file.

It exists to provide:

- stable project identity
- layout/version compatibility
- active linkage
- entry-related technical state

It is not:

- a project truth document
- a meaning document
- a decision log
- a lifecycle history container

---

## JSON Contract v1

{
  "version": "1.0",
  "projectId": "string",
  "projectName": "string",
  "layoutVersion": "v1",
  "entryMode": "cold_start",
  "activeShiftId": null,
  "activeTaskId": null
}

---

## Required Fields

- `version`
- `projectId`
- `projectName`
- `layoutVersion`
- `entryMode`
- `activeShiftId`
- `activeTaskId`

All required fields must be present,
even if some of them are explicitly `null`.

---

## Field Meanings

### `version`

Version of the `project.json` contract itself.

Used for:

- schema compatibility
- migration support
- validation of the meta file format

---

### `projectId`

Stable project identifier.

Used as the canonical technical identity of the project.

---

### `projectName`

Human-readable project name.

Used for display and identification,
but not as the stable technical key.

---

### `layoutVersion`

Version of the `.zavod/` layout expected by the system.

Used to validate storage structure compatibility.

---

### `entryMode`

Project-level entry mode.

This field describes the technical entry condition of the project,
not a global application runtime mode.

In v1 it is expected to remain narrow and entry-related.

---

### `activeShiftId`

Identifier of the currently active shift,
or `null` if no active shift exists.

This is active linkage,
not a dump of shift lifecycle state.

---

### `activeTaskId`

Identifier of the currently active task,
or `null` if no active task exists.

This is active linkage,
not a substitute for task storage.

---

## Minimality Rule

`project.json` must stay minimal in v1.

It should store only what is required to:

- identify the project
- validate layout compatibility
- determine active linkage
- assemble `ProjectState`

It must not become a monolithic technical container for unrelated system data.

---

## Relation to Project State Model

`project_state_model_v1.md` defines the in-memory typed `ProjectState`.

`project.json` provides part of the persisted technical input used to assemble it.

Formula:

`project.json`
→ builder input
→ `ProjectState`

`project.json` does not itself equal `ProjectState`.

---

## Relation to Project State Builder

`project_state_builder_v1.md` consumes `project.json` as part of deterministic state assembly.

This contract defines:

- which fields builder can rely on
- what minimum meta must exist
- what meanings these fields carry

The builder must not guess missing required fields.

If the minimal contract is not satisfied,
state assembly should fail deterministically.

---

## Relation to Truth Documents

`project.json` does not store the bodies or semantic content of:

- `project.md`
- `direction.md`
- `roadmap.md`
- `canon.md`
- `capsule.md`

These remain separate truth owners.

`project.json` only supports the system technically.
It does not replace project meaning.

---

## Cold Start Compatibility

The contract must support valid cold start.

Therefore:

- `activeShiftId` may be `null`
- `activeTaskId` may be `null`
- cold start without active shift is valid

The absence of active work must not make `project.json` invalid.

---

## Validation Expectations

A valid `project.json` in v1 must:

- be readable as JSON
- contain all required fields
- respect the expected types
- remain minimal and technical

Invalid, malformed, or semantically incomplete `project.json`
should be treated as an `InvalidProjectMeta` condition by the builder.

---

## Canons

- `project.json` is not a project truth document
- `project.json` stores only minimal technical meta
- `entryMode` is project-entry related, not global app runtime mode
- `activeShiftId` and `activeTaskId` may be `null`
- cold start without active shift is valid
- derived/cache data must not be stored here
- semantic project documents must remain outside `project.json`
- the file must stay minimal in v1

---

## Exclusions

Excluded from `project.json` in v1:

- roadmap content
- canon content
- capsule text
- decisions
- trace or journal data
- snapshots
- execution/runtime state details
- cache data
- model settings
- user preferences
- arbitrary timestamps without concrete need
- arbitrary future-proof fields

---

## Boundary Note

If richer data is needed,
it must live in its proper owner layer:

- truth documents
- shift/task storage
- decision layer
- snapshot layer
- cache/derived layers

`project.json` must stay small, technical, and boring.
