# ZAVOD v1 — Work Packet

## Purpose

Defines the **Work Packet**: the exact, deterministic, runtime-only
payload that ZAVOD assembles for a single work cycle and delivers
to the LLM.

This canon specifies:

- what a Work Packet is (and what it is not)
- where it sits in the memory architecture
- what fields it contains
- how each field is sourced
- how history is selected (the dangerous field)
- how the first-cycle variant works
- what is forbidden in a Work Packet body

This is the **contract between project memory and the model**.
Everything the model sees about a project flows through this
packet. No other channel is authorized.

---

## Position in the Architecture

Work Packet is a new layer: **Layer R (Runtime Packet)**.

- Temporal: **Active**, single-cycle lifetime
- Origin: **System-assembled** from Layer A / B / C / D / E and
  active shift/task state
- Persistence: **Ephemeral by definition** — not project truth
- Mutability: **Immutable after dispatch** — a packet that has
  been sent is not edited in place; a new packet replaces it

Work Packet is NOT:

- Layer A canonical truth
- Layer C decision record
- Layer D journal entry
- Layer E preview
- Layer F archive
- Local Ephemeral cache (though it may be **debug-dumped** into
  Local; see below)

Work Packet **reads** from the truth layers. It does not **write**
to any truth layer. A Work Packet assembly that writes back into
Layer A / C / F is an invariant violation per
`project_automation_invariant_v1.md`.

---

## Central Invariant

```
Work Packet is the only authorized channel from project memory
to the model.
Work Packet is runtime-only and never becomes project truth.
```

Corollaries:

- The model does not see project memory except through a Work
  Packet. No side-channel reads, no ambient prompt injection,
  no raw file streaming.
- A Work Packet is never promoted to Layer A, C, D, E, or F.
- A Work Packet may be debug-dumped to Local Ephemeral for
  diagnosis; that dump is not truth.

---

## Assembly Contract

A Work Packet is assembled from three source kinds:

1. **Truth** — Layer A canonical docs, Layer C decisions, Layer E
   preview docs (only when canonical absent and clearly marked).
2. **Active runtime state** — current shift id, current task id,
   active constraints from shift/task, active intent.
3. **Selected history** — a bounded, task-scoped, criteria-filtered
   slice of Layer D journal and recent conversation turns.

Assembly is **deterministic**. Given identical inputs (truth
state, runtime state, selection criteria), assembly produces
identical output. Non-determinism at assembly time is a defect.

Assembly is **minimal and task-scoped**. A Work Packet contains
what the current cycle needs, not the project's full memory.
Over-stuffing the packet dilutes signal and is forbidden.

---

## Fields

The Work Packet has the following fields. Absence of a field must
be explicit (`null` or documented empty state), never silent.

### Identity

- **`project_id`** — stable project identifier
- **`project_name`** — human-readable name from `project.md`

### Truth snapshot

- **`capsule_snapshot`** — current capsule body with its
  `source_stage` marker preserved (see
  `project_truth_documents_v1.md` Capsule v2)
- **`canonical_docs_status`** — structured 5/5 state:
  `{ project, direction, roadmap, canon, capsule }` each marked
  as `canonical | preview | absent | stale`
- **`preview_status`** — present only when canonical < 5/5;
  lists which kinds exist as preview and must be surfaced as
  below-canonical
- **`missing_truth_warnings`** — explicit list of gaps the model
  should not silently paper over (e.g. "canon.md absent, do not
  invent architectural invariants")

### Active state

- **`current_shift_id`** — active shift id, or `null` if none
- **`current_task_id`** — active task id, or `null` if none
- **`active_constraints`** — constraints from current shift/task
  scope, explicit list
- **`user_intent`** — current user intent as classified by the
  intent system; never the raw user turn verbatim

### Selected context

- **`relevant_decisions`** — Layer C decision entries relevant to
  current task scope (see selection rule below)
- **`relevant_recent_history`** — bounded slice of prior turns
  and journal events (see selection rule below — this is the
  dangerous field)
- **`attachments_summary`** — metadata about attached files:
  name, kind, size, derived summary if any; never raw body
  unless the cycle explicitly requires it

### First-cycle marker

- **`is_first_cycle`** — boolean; `true` only on the first work
  cycle after project open when no prior cycle exists in this
  session

### Assembly metadata

- **`packet_id`** — unique id for this packet
- **`assembled_at`** — UTC timestamp
- **`assembler_version`** — version of the assembly code that
  produced this packet

Unknown or future fields MUST NOT be added ad-hoc. Schema
extension is a canon-level change.

---

## History Selection Rule

`relevant_recent_history` is the field with the highest risk of
turning into a raw-chat back door. This section is load-bearing.

### Sources

History is selected from two pools:

1. **Journal events** (Layer D) — structured, audit-grade
2. **Recent conversation turns** — current session turns only;
   no cross-session chat resurrection

### Selection criteria

A history item is included only if ALL of the following hold:

- it is scoped to the current task or shift, OR directly
  referenced by an active decision in `relevant_decisions`
- it is within the **recency window** (default: current shift
  duration; hard cap: last 72 hours)
- it is within the **item budget** (default: 20 items; hard cap:
  50 items)
- it is within the **token budget** for this packet (hard cap
  declared by the assembler configuration)

Items outside these bounds are excluded. Budget enforcement is
non-negotiable; the assembler fails loud when bounds cannot be
satisfied, it does not silently truncate mid-turn.

### Content rules

Each history item MUST be one of:

- a structured journal event (from Layer D, already sanitized)
- a prior user turn or assistant turn in the current session,
  included as a bounded excerpt with explicit turn boundaries

Raw LLM responses, raw tool outputs, raw Sage observation bodies,
and raw preview content MUST NOT appear in `relevant_recent_history`.

### Forbidden selection patterns

- "include last N turns regardless of scope" — forbidden, violates
  task-scoping
- "include everything in the shift" — forbidden, violates item
  budget
- "fall back to full chat if nothing matches" — forbidden, silent
  back door
- "summarize the whole session and include the summary" — forbidden
  at this layer; summarization is a separate derivation with its
  own canon

---

## First-Cycle Variant

The first Work Packet for a project in a session is a standard
Work Packet with `is_first_cycle: true` and the following
differences:

- `current_shift_id` and `current_task_id` are usually `null`
- `relevant_recent_history` is empty (no prior turns in this
  session)
- `missing_truth_warnings` is typically more populated (5/5 status
  is often < 5/5 on first open)
- `user_intent` may be absent (user has not yet spoken) — in which
  case the packet carries an `orientation_mode` signal

### First-cycle system instruction

When `is_first_cycle: true`, the assembler injects a system-level
instruction that tells Lead:

- this is the first work cycle for this project context
- determine whether project memory is mature enough for direct
  execution
- if memory is thin (preview-only, < 5/5, missing sections),
  prefer orientation, clarification, or doc-promotion guidance
- do not pretend the project is fully understood when only preview
  material exists

This instruction is **derived from structured state**, not a free
LLM prompt. It is assembled from `canonical_docs_status` and
`preview_status`.

First-cycle is NOT a separate packet kind. It is the Work Packet
with a flag and a state-driven system instruction.

---

## Debug Dump

For diagnosis, a Work Packet MAY be dumped to Local Ephemeral at:

```
.zavod.local/runtime/packets/YYYY-MM-DD/<packet_id>.json
```

Rules:

- dump is opt-in, controlled by runtime config
- dump is Local Ephemeral (git-ignored), never Truth
- dump retention is bounded (default: 7 days)
- dump redacts fields marked sensitive in the schema

Dumping a packet into `.zavod/` (Truth) is forbidden per
`local_workspace_layout_v1.md`.

---

## Forbidden Content

A Work Packet MUST NOT contain:

- raw unfiltered chat history across sessions
- raw LLM response bodies from prior cycles
- raw Sage observation bodies (Sage observations are a separate
  derivation; they do not enter the packet directly)
- raw tool invocation outputs beyond what `attachments_summary`
  describes
- preview content presented as canonical (source stage must be
  honest per Capsule v2)
- fabricated "synthesized" state that does not trace back to a
  concrete Layer A / C / D / E source
- cross-project content (a Work Packet belongs to exactly one
  project)

---

## Reader Obligations (Server / LLM Side)

The runtime surface that consumes a Work Packet MUST:

- honor the `source_stage` marker on `capsule_snapshot` and treat
  preview-sourced capsule as below canonical
- surface `preview_status` and `missing_truth_warnings` to the
  model; never hide them to make the model look more confident
- respect `active_constraints` as binding, not advisory
- treat `is_first_cycle: true` as a signal to prefer orientation
  over execution when truth is thin

The runtime surface MUST NOT:

- merge multiple Work Packets into a single model call without
  declaring it
- inject additional project memory that did not flow through the
  packet
- strip warnings or stage markers on the way to the model

---

## Assembly Authority

Work Packet assembly is performed by a dedicated **Packet
Assembler** component. Assembly authority is:

- assembler **reads** Layer A, B, C, D, E, active runtime state
- assembler **writes** only to Local Ephemeral debug dump (when
  enabled)
- assembler **may not** promote, archive, or modify any truth
  layer during assembly
- assembler **must fail loud** on budget overrun, on schema
  violation, on missing required field — never silently degrade

Per `project_automation_invariant_v1.md`, the assembler is an
automation actor. It may run without the user, but may not
reshape truth.

---

## Relation to Other Canon

- **`project_automation_invariant_v1.md`** — assembler is
  background automation; packet assembly is permitted, any truth
  write during assembly is forbidden
- **`project_architecture_layers_v1.md`** — defines Layers A–F
  and Local Ephemeral that the assembler reads from
- **`project_truth_documents_v1.md`** — Capsule v2 source_stage
  marker carried into `capsule_snapshot`
- **`project_decisions_v1.md`** — source for `relevant_decisions`
- **`project_journal_v1.md`** — source for structured history
  items in `relevant_recent_history`
- **`local_workspace_layout_v1.md`** — `.zavod.local/runtime/`
  path for debug dump
- **`prompt_assembly_v1.md`** — downstream consumer; Work Packet
  fields feed prompt assembly, Work Packet is the upstream
  boundary
- **`context_builder_v1.md`** — related derivation; must not
  duplicate packet assembly scope

If `prompt_assembly_v1.md` or `context_builder_v1.md` currently
describes project-memory fetching that bypasses Work Packet, that
is a canon conflict to be resolved at those files.

---

## Canons

- Work Packet is the only authorized channel from project memory
  to the model.
- Work Packet is runtime-only and never becomes project truth.
- Assembly is deterministic, minimal, and task-scoped.
- `relevant_recent_history` is bounded by scope, recency, item
  budget, and token budget; silent fallback to full chat is
  forbidden.
- `is_first_cycle` is a flag on a standard packet, not a separate
  packet kind.
- `missing_truth_warnings` and `preview_status` must reach the
  model; they are never stripped for cosmetic confidence.
- Debug dump lives in Local Ephemeral only.
- Assembler writes nothing to truth layers.

---

## Exclusions

Out of scope for this canon:

- prompt string templating (covered by `prompt_assembly_v1.md`)
- Sage observation scheduling and emission (covered by
  `observation_layer_v1.md` and Sage canons)
- project state typed model (covered by `project_state_model_v1.md`)
- resume contract across sessions (covered by
  `resume_contract_v1.md`); resume may consume Work Packet
  structure but is a separate concern
- UI rendering of capsule or packet (Work Packet does not render
  itself)

---

## Status

Locked as contract. Schema fields may be extended through a canon
edit; field removal or semantic change is a versioned canon bump
(`project_work_packet_v2.md`), not an in-place rewrite.
