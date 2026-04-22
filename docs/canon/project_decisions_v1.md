# ZAVOD v1 — Project Decisions

## Purpose

Defines Layer C of the project memory architecture: the **decision
record**. Records only project-shaping acts that change the trajectory
or boundaries of the project itself.

Derives from `project_architecture_layers_v1.md`. Storage path is
specified in `project_truth_storage_layout_v1.md`.

---

## Central Rule

> Decisions record **why the project changed shape**.
> Routine execution outcomes are not decisions — they are history.

Every entry in `decisions/` must answer: "what was the choice that
altered the project's direction, boundaries, or canonical truth,
and why?"

If the event can be answered by reading journal or shift/task
records ("what happened"), it does not belong here. It belongs in
Layer D.

---

## In Scope

A decision entry must be written for each of the following acts:

### 1. Canonical promotion

Any PreviewDocs → CanonicalDocs promotion for a Layer A document.
The decision captures:

- which document kind was promoted
- source preview content hash or snapshot reference
- what changed vs prior canonical (if replacing)
- contributor identity
- why the preview is approved as truth at this moment

### 2. Archival act

Moving a Layer A document, a closed Layer D shift bundle, or a
superseded decision into Layer F. The decision captures:

- what artifact is archived
- reason for archival (superseded / deprecated / pivoted / obsoleted)
- superseded-by reference (if replacement exists)
- link to the archival journal entry

### 3. Architectural decision

Introduction of a new invariant to `canon.md`, a resolved trade-off,
a chosen approach between alternatives that affects system design.
The decision captures:

- context (problem being solved)
- options considered (real alternatives, not straw men)
- chosen option
- rationale (why this over the others)
- invalidation criteria (what evidence would cause this decision
  to be revisited)

### 4. Explicit high-impact reject

Rejection of an approach, direction, or pivot where the rejection
itself is material — not routine task rejection. Typical signal:
the rejection closes a door that the team had considered open.

The decision captures:

- what was rejected
- by whom (contributor, not Worker/QC)
- rationale
- preserved notes for future consideration (if any)

---

## Out of Scope (belongs in Layer D, not here)

A decision must **not** be written for:

- routine Worker-result Accept / Reject / Abandon (→ task record
  + journal entry)
- individual task closure (→ task record)
- individual shift closure (→ shift record + journal entry)
- LLM telemetry (→ lab, Local Ephemeral)
- Sage observations (→ `.zavod.local/sage/observations.jsonl`)
- preview regeneration events (→ journal)
- evidence re-scans (→ journal)

The discriminator: if a new contributor reading only decisions/
can reconstruct the project's trajectory of shape changes,
the log is correctly scoped. If they would need decisions/ to
understand daily execution, the log is bloated with history that
belongs in Layer D.

---

## File Contract

### Naming

```
decisions/DEC-NNNN-<slug>.md
```

- `NNNN` is a zero-padded monotonically increasing counter per
  project
- `<slug>` is a short kebab-case identifier derived from the
  decision subject
- File extension is `.md`

Example: `DEC-0012-promote-canon-md-v2.md`,
`DEC-0019-reject-frontend-rewrite.md`

### Required Frontmatter

```yaml
---
id: DEC-NNNN
type: canonical_promotion | archival | architectural | high_impact_reject
timestamp: <ISO 8601 UTC>
contributor: <stable identifier>
supersedes: [DEC-XXXX, ...]   # optional
superseded_by: null            # set later if superseded
related_shift: <shift_id>      # optional, if act emerged from a shift
related_task: <task_id>        # optional
related_journal: <journal_line_id>   # required; the corresponding journal event
---
```

### Required Body Sections

All four sections must appear, in order. Empty is not valid;
"not applicable" is valid and must be written explicitly.

1. **Context** — what state the project was in that required a
   decision
2. **Options considered** — list of real alternatives with brief
   pros/cons; at least two options, including "do nothing" when
   it was a live alternative
3. **Chosen option** — which alternative was selected
4. **Rationale** — why this option, referencing evidence where
   possible (canon sections, journal events, evidence bundle,
   decision supersessions)
5. **Invalidation criteria** — what would cause this decision to
   be revisited; specific, not hand-waving

---

## Supersession Protocol

A decision may supersede one or more prior decisions.

Rules:

- The superseding decision lists superseded decisions in frontmatter
  (`supersedes:`)
- Each superseded decision has its `superseded_by` frontmatter
  updated (single-file amendment allowed for this field only;
  body remains immutable)
- A superseded decision remains in `decisions/`. It is not moved
  to archive.
- A superseding decision must cite why supersession is warranted
  in its Rationale section.

Supersession is not cancellation. Both records remain readable;
only authority shifts.

---

## Writer Discipline

- **Who writes:** contributor via promotion / archival / decision
  UI surface. Runtime does not write decisions directly.
- **When:** at the moment of the act, not after.
- **Atomicity:** decision file + journal entry + artifact move
  (if applicable) form one logical transaction. Partial state
  after failure must not produce decision record without its
  journal counterpart.
- **Immutability:** once written, body is immutable. Only
  `superseded_by` frontmatter may be later amended.

---

## Reader Discipline

- Decisions are authoritative for "why this shape" questions.
- Decisions must be read **in order** to understand trajectory:
  early decisions establish foundations that later ones build on
  or supersede.
- A decision's current authority depends on its supersession
  status. Readers (human or system) must check `superseded_by`
  before treating a decision as active guidance.
- Decisions do not override `canon.md`. If a decision and
  `canon.md` disagree, either the canonical document is stale
  and must be promoted with updated content, or the decision
  itself is superseded.

---

## Relation to Other Layers

- **Layer A (canonical docs):** a canonical promotion decision
  precedes every A-document change. No decision → no promotion.
- **Layer D (journal):** every decision has a corresponding
  journal event (`decision_recorded`), cross-referenced both
  ways.
- **Layer F (archive):** archival acts are decisions. Archive
  is populated via decision, never silently.
- **Layer B (capsule):** capsule regenerates when Layer A changes.
  Since A changes require decisions, capsule's regeneration
  trail is always traceable to a decision.

---

## Canons

- Decisions record only project-shaping acts.
- Routine execution outcomes are Layer D, never Layer C.
- Every decision has a matching journal entry and is written
  atomically with it.
- Decision bodies are immutable; supersession is additive.
- Readers must check supersession status before treating a
  decision as active.
- Runtime does not write decisions directly; contributor act
  is required.
- A decision that contradicts `canon.md` without superseding or
  updating it is a defect.
- An act that is not among the four in-scope kinds (promotion,
  archival, architectural, high-impact reject) must not produce
  a decision entry.
