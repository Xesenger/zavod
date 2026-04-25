# ZAVOD v1 — Welcome Surface

## Purpose

Defines the **Welcome Surface**: the first rendered view a user
sees after opening a project (or immediately after import / new
project creation), before any work cycle begins.

This canon specifies:

- what the Welcome Surface is (a rendered view, not a document)
- what structured state drives its content
- the **fixed state → action map** that selects next actions
- acceptance criteria for the first-open UX
- the boundary between Welcome Surface and the first Work Packet

The Welcome Surface replaces the empty-chat first-open experience.
The user must land on a non-empty, project-aware surface that
knows what project it opened, how much of it is understood, and
what the sensible next move is.

---

## Nature

Welcome Surface is a **rendered view**, not a document.

- It has no Layer A / C / D / E / F position
- It is not persisted as project truth
- It is assembled on demand when the project is opened
- It has no `source_stage` marker of its own; it honors the
  markers of the sources it reads

Welcome Surface is NOT:

- a chat message authored by the model (no free LLM narration)
- a cached preview
- a document kind that counts toward 5/5
- a substitute for a Work Packet

Welcome Surface IS:

- a project-open orientation view, rendered from structured state
- the seam between "project opened" and "first work cycle"

---

## Central Invariant

```
Welcome content is assembled from structured project state.
Welcome actions are selected from a fixed state → action map.
Welcome surface never fabricates what the system does not know.
```

Corollaries:

- Welcome text MAY be composed of structured fields rendered into
  prose; it may not be a free LLM generation that invents content.
- The set of offered next actions is bounded and deterministic:
  same state → same action set.
- If the system does not know something (5/5 incomplete, preview
  thin, direction absent), the Welcome Surface says so honestly.

---

## Assembly Sources

Welcome Surface reads from the same structured state that feeds
Work Packet assembly (see `project_work_packet_v1.md`):

- **`project_id`**, **`project_name`** — identity
- **`canonical_docs_status`** — 5/5 state per kind
- **`preview_status`** — which preview kinds exist
- **`capsule_snapshot`** — for the "what ZAVOD currently
  understands" section; honor `source_stage` marker
- **`current_shift_id`**, **`current_task_id`** — active state,
  usually `null` at first open
- **`missing_truth_warnings`** — gaps to surface honestly

Welcome Surface MUST NOT read raw chat history, raw LLM outputs,
raw Sage observation bodies, or cross-project content.

---

## Content Structure

The Welcome Surface has the following sections, in this order:

### 1. Project identity

- project name
- project type (imported / new / existing)
- source (for imported: original path; for new: creation context)

### 2. Current understanding

A compressed one-paragraph statement of what ZAVOD currently
knows. Sourced from `capsule_snapshot` when present. If capsule
is `source_stage: preview`, the surface marks it as
**preview-level understanding**, not canonical.

If capsule is absent and Layer E preview is also absent, this
section says so explicitly ("no understanding has been derived
yet"), without fabrication.

### 3. Document status

Structured display of `canonical_docs_status` and `preview_status`:

- canonical: `X/5` with per-kind breakdown
- preview: `Y/5` with per-kind breakdown
- stale: any sections marked stale

Rendered as a compact status block, not as prose narration.

### 4. What is ready now

What the user can do immediately given current state. This is
rendered from the **state → action map** (below), not from free
LLM choice.

### 5. What is missing

Gaps surfaced from `missing_truth_warnings`. Honest list, not
apologetic prose.

### 6. Offered next actions

2–4 concrete actions, selected by the state → action map. Each
action is a bounded, named operation ZAVOD can perform or a
bounded user act ZAVOD can guide.

---

## State → Action Map

Welcome actions are selected deterministically from project
state. The map is fixed; it is not LLM-authored.

### Action vocabulary

The following named actions are the permitted entries. New
actions require a canon edit.

- `review_preview_docs` — open preview doc viewer
- `promote_preview_to_canonical` — start promotion flow for a
  specific preview kind
- `author_canonical_doc` — open authoring flow for a canonical
  kind (manual drafting)
- `start_work_cycle` — open first work cycle with
  `is_first_cycle: true` Work Packet
- `continue_work_cycle` — resume existing shift/task
- `review_project_audit` — run project audit / orientation pass
- `review_stale_sections` — open stale-section review view
- `import_retry` — re-run importer (for thin imports)
- `reject_preview` — reject a preview kind and remove it
- `open_roadmap` — open roadmap surface (for projects with 5/5)
- `open_direction` — open direction surface

### Selection rules

Actions are selected by matching project state against the
following rules, in priority order. The first matching rule
produces the action set; later rules do not add.

**R1 — Active shift/task exists**:
Offered actions: `continue_work_cycle`, `open_roadmap`

**R2 — Canonical 5/5 complete, no active shift**:
Offered actions: `start_work_cycle`, `open_roadmap`,
`open_direction`

**R3 — Canonical partial (1–4 of 5), preview may fill gaps**:
Offered actions: `promote_preview_to_canonical` (per available
preview kind), `author_canonical_doc` (per missing kind),
`start_work_cycle` (only if caller confirms thin-memory mode)

**R4 — Canonical 0/5, preview ≥ 1 of 5**:
Offered actions: `review_preview_docs`,
`start_work_cycle`, `promote_preview_to_canonical`,
`review_project_audit`

**R5 — Canonical 0/5, preview 0/5 (empty / failed import)**:
Offered actions: `import_retry`, `author_canonical_doc`,
`review_project_audit`

**R6 — Stale sections present (overlay on any of R1–R5)**:
Add: `review_stale_sections`

Rules R1–R5 are mutually exclusive. R6 is additive overlay.

### Action count bound

The Welcome Surface offers **2–4 actions**. If a matching rule
produces more, the surface ranks by priority defined in the rule
and keeps the top 4. If a rule produces fewer than 2, the surface
adds `review_project_audit` as a safe default.

### No free-choice LLM actions

The Welcome Surface MUST NOT offer an action that is not in the
vocabulary above. LLM-suggested free-text actions are forbidden
at this surface — they belong to work cycle conversations, not
to the orientation view.

---

## First-Open UX Outcome

The Welcome Surface satisfies these acceptance criteria:

- user lands on a non-empty surface within one render cycle of
  project open
- the surface answers three questions without model help:
  1. "what project did I open"
  2. "how much of it is understood"
  3. "what is the sensible next move"
- the surface never pretends the project is fully understood when
  only preview exists
- the surface never offers `start_work_cycle` as the only action
  when truth is thin (< 3/5 canonical)
- the surface is renderable deterministically from structured
  state, without any LLM call

A Welcome Surface that requires an LLM call to render is a
regression.

---

## Relation to First Work Cycle

When the user selects `start_work_cycle` from the Welcome Surface,
the system opens the **first Work Packet** with `is_first_cycle:
true` (see `project_work_packet_v1.md` First-Cycle Variant).

The Welcome Surface hands off:

- `project_id`, `project_name`
- the structured state it already assembled
- the user-selected action as `user_intent` seed

The Welcome Surface does **not** inject prompts, narrate to the
model, or pre-write conversation turns. The first work cycle
begins with a Work Packet, not with a synthetic chat history.

---

## Relation to Other Canon

- **`project_work_packet_v1.md`** — downstream consumer for
  `start_work_cycle`; Welcome Surface state fields align with
  Work Packet fields
- **`project_truth_documents_v1.md`** — source of
  `canonical_docs_status`, honors Capsule v2 `source_stage`
- **`project_automation_invariant_v1.md`** — Welcome Surface
  assembly is automation-permitted (reads truth, writes nothing
  to truth)
- **`project_state_model_v1.md`** — typed state consumed for
  status fields
- **`cold_start_behavior_v1.md`** — Welcome Surface is the
  **cold-start-compatible rendered view** of the project-open
  condition. It satisfies Cold Start's "idle-capable entry" and
  "non-empty first-open" requirements simultaneously. Welcome
  Surface does NOT replace the bootstrap discussion surface; it
  is a pre-chat orientation view that routes INTO bootstrap
  discussion via the `start_work_cycle` action. It does not
  create shifts, tasks, or execution. All work materialization
  still flows through validated intent per cold start / bootstrap
  rules.
- **`bootstrap_flow_v1.md`** — Welcome Surface operates before
  bootstrap's interaction loop. Its `start_work_cycle` action
  opens the Work Packet that Lead consumes inside bootstrap mode;
  it never bypasses validated intent or materializes a first
  shift by itself.

---

## Canons

- Welcome Surface is a rendered view, not a document.
- Welcome content is assembled from structured project state.
- Welcome actions are selected from a fixed state → action map.
- Welcome Surface never offers free-text LLM-chosen actions.
- Welcome Surface renders deterministically without an LLM call.
- Welcome Surface never pretends understanding that the system
  does not have.
- `start_work_cycle` from Welcome Surface opens a first-cycle
  Work Packet, not a synthetic chat.

---

## Exclusions

Out of scope for this canon:

- visual design, layout, colors, typography (UI-team concerns)
- i18n and localization of surface strings
- keyboard shortcuts and input handling
- specific microcopy wording (the canon fixes **what** is shown,
  not the exact sentences)
- the work cycle conversation itself (covered by Work Packet and
  prompt assembly)

---

## Status

Locked as contract. The state → action vocabulary and rule set
may be extended through canon edits; free additions at the UI
or runtime layer are forbidden.
