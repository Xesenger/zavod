# ZAVOD v1 — Project Journal

## Purpose

Defines the audit event stream inside Layer D (Execution History):
the **journal**. A chronological, append-only, audit-grade record
of what happened in the project, written automatically by the
runtime as a by-product of real execution.

Derives from `project_architecture_layers_v1.md`. Complements
`shift_lifecycle_v1.md`, `task_model_v1.md`, `execution_loop_work_cycle_v1.md`.
Storage path is specified in `project_truth_storage_layout_v1.md`.

---

## Central Rule

> Journal is **audit-grade**. Lab is **debug-grade**.
> Journal is Truth. Lab is Local Ephemeral.

The two exist for different readers and under different guarantees.
Nothing that belongs in one is acceptable in the other.

| Aspect | Journal (Truth) | Lab (Local) |
|---|---|---|
| Tier | `.zavod/journal/trace/` | `.zavod.local/lab/` |
| Reader | any contributor, any tool, any audit | diagnosing contributor |
| Content | high-level events | LLM request/response bodies |
| Format | structured JSONL | per-call folder with artefacts |
| Persistence | append-only, immutable lines | per-call snapshots, pruneable |
| Git | tracked | ignored |
| Purpose | "what happened and when" | "why this call behaved as it did" |

---

## Storage

```
.zavod/journal/
  trace/
    YYYY-MM-DD.jsonl
```

Rules:

- One file per UTC calendar day; events are written to the file
  matching the UTC date of their timestamp
- Within a file: JSONL, one event per line, UTF-8 no BOM
- Files are append-only; no edits, no deletes
- Rotation is by date; there is no size-based rotation (size
  stays manageable because events are compact)

---

## Event Schema

Every event is a JSON object on its own line with the following
fields:

### Required

- `event_type` — enumerated string (see Event Kinds below)
- `timestamp` — ISO 8601 UTC with subsecond precision
- `event_id` — stable unique identifier for cross-reference
  (format: `EVT-<utc_compact>-<hash4>`)

### Conditional (required when applicable to the event kind)

- `shift_id` — present for any event that occurred within a shift
- `task_id` — present for any event scoped to a task
- `role` — present for role-attributed events (`lead` / `worker` / `qc`)
- `decision_id` — present for events that correspond to a decision
  (Layer C cross-reference)
- `payload` — object with event-specific fields

### Forbidden

The payload of a journal event must **never** contain:

- raw LLM request bodies
- raw LLM response text
- raw prompt text
- full staging diffs
- Sage observation bodies
- any contents of `.zavod.local/`

Those belong to lab (diagnostic) or to sage-only channel. If a
journal event needs to reference them, it carries a **pointer**
(path or id), not the content.

---

## Event Kinds

The enumerated list is the **only** set of permitted `event_type`
values. Adding a new kind requires extending this canon.

### Shift lifecycle

- `shift_opened` — payload: `{reason, seed_context_id?}`
- `shift_closed` — payload: `{reason, outcome_summary}`
- `shift_resumed` — payload: `{previous_close_timestamp}`

### Task lifecycle

- `task_created` — payload: `{description, scope, acceptance_criteria}`
- `task_started` — payload: `{attempt_number}`
- `task_revised` — payload: `{previous_attempt_id, revision_notes_count}`
- `task_accepted` — payload: `{commit_id, applied_files_count}`
- `task_rejected` — payload: `{rationale_ref}`
- `task_abandoned` — payload: `{reason, staging_quarantined?}`

### Role events

- `lead_intent_captured` — payload: `{intent_state, task_brief_ref?}`
- `worker_result_produced` — payload: `{status, edits_count, telemetry_dir}`
- `qc_decision_rendered` — payload: `{decision, rationale_ref}`

### Apply / acceptance pipeline

- `staging_written` — payload: `{attempt_number, edits_count}`
- `apply_committed` — payload: `{commit_id, files, sha_guard_ok}`
- `apply_refused` — payload: `{reason}`
- `staging_quarantined` — payload: `{destination}`

### Document pipeline

- `evidence_scanned` — payload: `{bundle_id}`
- `preview_regenerated` — payload: `{kinds}`
- `preview_rejected` — payload: `{kind, preview_ref, contributor}`
- `canonical_promoted` — payload: `{kind, from_preview_ref}` (cross-refs `decision_id`)
- `document_archived` — payload: `{kind, archive_path}` (cross-refs `decision_id`)

### Decision boundary

- `decision_recorded` — payload: `{decision_id, type}`

---

## Writer Discipline

- **Who writes:** runtime components at the moment of the event.
  No component may delay a journal write past the moment of the
  real event.
- **Ordering:** events in a file are in monotonic timestamp order
  per writer; across writers, timestamps may tie-break by
  `event_id`.
- **Atomicity with state:** a state transition (shift close, task
  accept, apply commit, canonical promotion) must not be
  considered complete until its journal event is flushed.
  Failure to flush means failure to transition.
- **Idempotency:** if a writer retries an event, the event must
  carry a stable `event_id`. Readers may deduplicate by id.

---

## Reader Discipline

- Readers may rely on journal as the **authoritative answer** to
  "what happened in this project".
- Readers must treat missing events as missing reality — not as
  absence of the event. If a state exists but no journal event
  recorded its creation, the state is a candidate defect.
- Readers must not infer LLM behavior details from journal. For
  that, follow the lab pointer (`telemetry_dir` in payload).

---

## Relation to Other Layers

- **Layer A:** canonical promotions journal as `canonical_promoted`
  with a `decision_id` pointing to the corresponding Layer C entry.
- **Layer B (capsule):** regeneration is not journaled — capsule
  is derived and regenerates continuously; noise would overwhelm
  audit value.
- **Layer C (decisions):** every decision emits one
  `decision_recorded` event; every promotion / archival / etc.
  also emits its own specific event that references the decision.
- **Layer D (shifts / tasks):** shifts and tasks carry their own
  detailed state files; journal records the lifecycle boundaries
  and role events, not the full state snapshots.
- **Layer E (evidence / preview):** scan, regen, and explicit
  preview rejection events are journaled; evidence bundle bodies
  and preview bodies are not (they live in Layer E itself,
  referenced by id / path / hash).
- **Layer F (archive):** archival is journaled via
  `document_archived`. Archive contents themselves are not
  re-journaled after archival.
- **Local Ephemeral (lab):** journal events reference `telemetry_dir`
  paths pointing into lab; lab content is not copied into journal.
- **Sage:** Sage observations are never journal events. Sage is
  not audit-grade; it is an advisory tenant in Local Ephemeral.
  If a Sage observation causes a contributor act (e.g. a decision),
  the resulting act is journaled — the observation that preceded
  it is not.

---

## Canons

- Journal is audit-grade, append-only, in Truth; lab is debug-grade,
  prunable, in Local Ephemeral.
- Every permitted event kind is listed in this canon; adding new
  kinds requires extending the canon.
- Events must never carry raw LLM bodies, prompts, or Sage
  observation content — only pointers.
- State transitions are complete only after their journal event
  is flushed.
- Cross-layer actions (promotions, archivals, decisions) emit
  paired entries in Layer C and Layer D with mutual references.
- Sage never writes to the journal; Sage never appears as a
  `role` in a journal event.
- Readers treat journal as the authoritative trajectory of
  project events.
