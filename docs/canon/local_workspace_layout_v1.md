# ZAVOD v1 — Local Workspace Layout

## Purpose

Defines `.zavod.local/` — the per-machine, non-shared, ephemeral
workspace. Establishes what may live there, who may write to it,
retention rules, and the central invariant: deletion of
`.zavod.local/` must never cause loss of project truth.

This canon derives from `project_architecture_layers_v1.md` (the
"Local Ephemeral" tier). It does not redefine the layer; it
specifies its contents.

---

## Central Invariant

> A contributor may delete `.zavod.local/` entirely without loss
> of project truth.

Every folder and file under `.zavod.local/` must satisfy this
invariant. Anything that would be lost by such deletion is
miscategorized and must move to `.zavod/` or to the project tree.

Deletion consequences that are acceptable:
- loss of in-flight Worker staging for uncommitted tasks
- loss of per-machine conversation history
- loss of LLM debug telemetry
- loss of Sage raw observation stream
- loss of caches and derived artifacts

Deletion consequences that would signal a defect:
- loss of any canonical document
- loss of any decision record
- loss of any journal entry
- loss of any archive entry
- loss of any evidence bundle
- loss of `project.json` or other `.zavod/meta/`

---

## Position in `.gitignore`

`.zavod.local/` must appear in `.gitignore`. This applies to:

- the ZAVOD source repository itself (already configured)
- every project managed by ZAVOD

ZAVOD's import flow should offer to append `.zavod.local/` to the
imported project's `.gitignore` if absent. Imported projects'
`.zavod/` (Truth tier) is tracked; their `.zavod.local/` is not.

---

## Folder Inventory

Each entry lists: purpose, writer, retention/cleanup policy.
Folders not listed here should not exist under `.zavod.local/`;
new entries require extending this canon.

### `.zavod.local/staging/`

**Purpose:** Worker edit sandbox. Accepted edits are applied from
here to the project tree; rejected edits are quarantined.

**Writer:** Worker runtime via `StagingWriter`.

**Retention:**
- In-flight task attempts under `<taskId>/attempt-<N>/`
- Accepted: deleted after apply completes
- Abandoned: moved to `staging/_abandoned/<taskId>-<utc>/`,
  kept for forensic review

**Cleanup policy:** `_abandoned/` may be pruned by the contributor
at any time; no system automation.

### `.zavod.local/lab/`

**Purpose:** Per-LLM-call debug artifacts. One folder per call:
`<UTC>-<role>-<taskId>/{request,response,parsed,meta}.json`.

**Writer:** Lead / Worker / QC agent runtimes via lab telemetry
writer.

**Retention:** append-only per run. Grows with usage.

**Cleanup policy:** contributor may delete older dates freely.
System does not auto-prune (manual control preferred over silent
loss of debug trail).

**Rationale for Local (not Truth):** per-machine noise, includes
raw prompt bodies and API responses that are diagnostic, not
audit-grade. The Truth-tier audit trail lives in
`.zavod/journal/trace/` (see `project_journal_v1.md`).

### `.zavod.local/sage/`

**Purpose:** Sage raw observation stream. Currently a single
`observations.jsonl` file (JSONL append). Future slices may add
per-session or per-session-cached derivatives.

**Writer:** `SageObservationSink` (via `BudgetedSageSink`).

**Retention:** append-only JSONL.

**Cleanup policy:** contributor may delete or truncate. Sage
starts a fresh stream on next observation. No system auto-prune.

**Rationale for Local:** Sage observations are a per-machine
runtime signal. Promotion of learned patterns from these raw
observations to Truth (`.zavod.local/sage/learned_patterns.jsonl` or
similar) is a future slice requiring explicit human act.

### `.zavod.local/conversations/`

**Purpose:** Per-machine conversation JSONL streams (chats mode +
projects mode).

**Writer:** `ConversationLogStorage` runtime.

**Retention:** append-only JSONL per conversation.

**Cleanup policy:** contributor may remove individual conversation
files. Active conversations are preserved by runtime unless the
contributor explicitly ends them.

### `.zavod.local/artifacts/`

**Purpose:** Per-conversation artifact bodies (markdown rendered
into files, attached content persisted for later reference).

**Writer:** `ConversationArtifactStorage`.

**Retention:** file-per-artifact.

**Cleanup policy:** contributor may delete; active conversation
items referencing missing artifacts must degrade gracefully, not
fail.

### `.zavod.local/attachments/`

**Purpose:** User-staged attachments (images, files) from composer
before they become part of a message.

**Writer:** `ConversationComposerDraftStore` or equivalent.

**Retention:** cleared when the composer is committed or reset.

**Cleanup policy:** safe to delete at any time; only active
composer drafts are affected.

### `.zavod.local/previews/`

**Purpose:** Local-cached rendering previews (HTML fragments,
projection snapshots) for UI rendering.

**Writer:** preview/projection generators.

**Retention:** regenerated on demand.

**Cleanup policy:** freely deletable; will regenerate.

### `.zavod.local/cache/`

**Purpose:** Generic derived cache (entry pack, capsule cache,
projection cache, token-level derivations).

**Writer:** derived generators.

**Retention:** regenerated from Truth sources when missing.

**Cleanup policy:** freely deletable. Cache must never be the
only source of any value that appears in Truth.

### `.zavod.local/meta/`

**Purpose:** Per-machine meta state for the local workspace
(not to be confused with `.zavod/meta/project.json`, which is
Truth).

**Writer:** runtime bootstrap.

**Retention:** small, machine-specific flags.

**Cleanup policy:** deletable; rebuilt on next bootstrap.

### `.zavod.local/resume/`

**Purpose:** Resume stage persistence (`resume-stage.json`).
Survives across process restarts to restore in-flight UI/runtime
state.

**Writer:** resume-stage normalizer + persistence.

**Retention:** single snapshot, rewritten on state transits.

**Cleanup policy:** deletable; system defaults to cold-start
bootstrap on next launch.

### `.zavod.local/runtime/`

**Purpose:** Reserved for per-machine runtime state that does
not fit other categories.

**Writer:** specialized runtime components.

**Retention:** component-defined.

**Cleanup policy:** deletable unless the specific runtime
component documents otherwise.

### `.zavod.local/index.json`

**Purpose:** Top-level index of local workspace state for fast
lookup without folder enumeration.

**Writer:** local workspace indexer.

**Retention:** single JSON file, rewritten on changes.

**Cleanup policy:** deletable; regenerated on next pass.

---

## Writer Discipline

Writers to `.zavod.local/` must:

- write only into their designated subfolder
- never write into the Truth tier (`.zavod/`) from a local path
- never assume files under `.zavod.local/` survive across
  contributors or across machines
- never read a Truth value from `.zavod.local/` as a fallback —
  if a value is not in Truth, it is not authoritative

---

## Reader Discipline

Readers of `.zavod.local/` must:

- treat any missing file as a valid cold-start condition
- never fail because a local cache is absent
- never promote a local-only value to a decision or a canonical
  document without explicit human act

---

## Relation to Truth Tier

| Concern | Truth (`.zavod/`) | Local (`.zavod.local/`) |
|---|---|---|
| Canonical docs | ✓ | ✗ |
| Journal audit trail | ✓ | ✗ |
| Evidence bundle | ✓ | ✗ |
| Decisions | ✓ | ✗ |
| Archive | ✓ | ✗ |
| Staging (pre-apply sandbox) | ✗ | ✓ |
| Lab debug telemetry | ✗ | ✓ |
| Sage raw observations | ✗ | ✓ |
| Conversation streams | ✗ | ✓ |
| Derived cache | ✗ | ✓ |
| Resume state | ✗ | ✓ |

An item must appear in exactly one of these two columns. If an
item is currently on the wrong side, it must be moved.

---

## Canons

- `.zavod.local/` is per-machine, ephemeral, and git-ignored.
- A contributor may delete `.zavod.local/` entirely without loss
  of project truth.
- Staging, lab, Sage raw observations, conversations, caches, and
  resume state live in `.zavod.local/`.
- Canonical docs, journal, evidence, decisions, and archive live
  in `.zavod/` (Truth) and never migrate to Local.
- Lab artifacts hold raw LLM bodies (per-call diagnostic); journal
  in Truth holds audit-grade events only.
- No writer may silently treat a `.zavod.local/` value as Truth.
- New subfolders under `.zavod.local/` require extension of this
  canon.
