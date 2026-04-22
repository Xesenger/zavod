# ZAVOD v1 — Project Truth Documents

## Purpose

Defines the five canonical project documents that ZAVOD produces
and maintains for every managed project:

- `project.md`
- `direction.md`
- `roadmap.md`
- `canon.md`
- `capsule.md`

This canon specifies what each document means, how it is derived,
how it is promoted through stages, and what the system state "5/5
canonical" denotes.

Storage layout for these files is defined separately in
`project_truth_storage_layout_v1.md`. This canon is about the
**bodies**, not the **paths**.

---

## The 5/5 Reading Contract

A new contributor opening the five documents must be able to
understand the project without reading code.

The contract is read-only and reader-centric:

- open the project
- read the five documents
- understand:
  - what this system is
  - how it is structured
  - where it is going
- close the project

If the reader needs to open the code or interview someone to
answer these questions, the project is not at 5/5.

5/5 is a property of the **project state**, not of a single
document. It is the minimum canonical coverage ZAVOD guarantees
for a managed project.

---

## The Five Documents

Each document answers exactly one question. Purposes do not overlap.
Bleed between documents is a canon violation that must be corrected
at promotion time, not after.

### `project.md` — what this project is and why it exists

Canonical identification and purpose document.

Answers:

- What is this project?
- Why does it exist?
- What is its type and scope?
- What is its current status?

Does not contain: future plans, architectural rules, or compressed
summaries. Those belong to `roadmap.md`, `canon.md`, and
`capsule.md` respectively.

### `direction.md` — where this project is going and why

Canonical direction document. The current project vector.

Answers:

- What direction is the project moving?
- Why this direction?
- What constraints bound the direction?
- What is explicitly not in the direction?

Does not contain: short-term tasks (those belong to shift/task
layer) or architectural invariants (those belong to `canon.md`).

### `roadmap.md` — what is being done next

Canonical roadmap: real plan, not wishlist.

Answers:

- What is the current phase?
- What are the upcoming phases?
- What does "done" mean for each phase?
- What is the status of each phase?

Does not contain: unmotivated future ideas, feature fantasy,
or decisions not tied to direction.

### `canon.md` — invariants, rules, architectural boundaries

Canonical law document for the project itself.

Answers:

- What invariants must never be broken?
- What are the architectural boundaries?
- What are the review and execution rules?
- What are the document rules?

Does not contain: state that changes per shift, transient
decisions, or operational details.

### `capsule.md` — 2–3 minute compressed entry point (Capsule v2)

Canonical compressed summary and stable project re-entry layer.
The fastest path to understanding, and the source from which
future compact/resume behavior assembles context.

#### Nature

Capsule is **always derived**, never manually edited. It is not
an independent truth layer. It is a rendered view over Layer A.

Capsule has **no own stage**. It does not move through
ImportPreview → PreviewDocs → CanonicalDocs on its own track.
Instead, each capsule carries an explicit **source stage marker**
that reflects the stage of the Layer A material it was derived
from:

- `source_stage: preview` — derived from preview docs because
  canonical docs are not yet promoted for this project
- `source_stage: canonical` — derived from canonical docs
- `source_stage: mixed` — derived from a mix; mixed capsule
  must list per-section which stage the content came from

A capsule with `source_stage: preview` must remain clearly below
canonical truth in every reader surface. It is a projection of
preview material, not a canonical document in its own right.

#### Derivation sources

Capsule is derived **primarily from Layer A** (`project.md`,
`direction.md`, `roadmap.md`, `canon.md`). When canonical docs
are absent, capsule derives from Layer E preview docs with
`source_stage: preview`.

Active shift/task state may appear **only as a clearly marked
overlay** (the "Current focus" section). The overlay is a
minority section, not the spine of the document. A capsule that
leads with runtime state instead of Layer A truth is a canon
violation.

Capsule MUST NOT contain:

- raw chat history
- raw LLM output
- a second copy of roadmap or canon content beyond 1–2 line
  compressed references
- runtime telemetry or journal excerpts
- preview content presented as canonical (source stage must be
  honest)

#### Sections

A capsule has the following sections, in this order:

1. **Project identity** — name, type, scope
2. **What this project is** — 2–4 lines from `project.md`
3. **Current direction** — 2–4 lines from `direction.md`
4. **Current roadmap phase** — 1–2 lines from `roadmap.md`
5. **Core canon rules** — 3–6 invariants from `canon.md`,
   compressed to one line each
6. **Current focus** *(overlay)* — active shift/task state if
   any; clearly marked as runtime overlay, not Layer A truth
7. **Open risks / unresolved items** — 2–5 bullets
8. **Canon completeness status** — e.g. `preview 2/5`,
   `canonical 5/5`; derived from 5/5 state, not narrated

Any section may be empty with an explicit "none" marker.
Fabrication is forbidden.

#### Regeneration triggers

Capsule must be re-derivable. Re-derivation is required when:

- any Layer A canonical doc changes
- 5/5 canonical status changes (promotion, staleness, archival)
- source_stage transitions (e.g. preview → canonical after
  promotion)

Re-derivation of capsule is **not** a project-shaping act — it
is an automation-permitted derivation per
`project_automation_invariant_v1.md`. The user does not approve
capsule refreshes.

#### Reader obligation

Any reader surface consuming capsule content MUST:

- honor the `source_stage` marker
- never treat `source_stage: preview` capsule as canonical truth
- never hide the "Current focus" overlay marker from the reader

---

## Stage Pipeline

Four of the five documents (`project.md`, `direction.md`,
`roadmap.md`, `canon.md`) move through three stages:

```
ImportPreview → PreviewDocs → CanonicalDocs
```

`capsule.md` is exempt from this pipeline. It is always derived
and carries a `source_stage` marker reflecting the stage of the
material it was derived from (see Capsule v2 above).

### ImportPreview

Raw output of the Scanner + Importer pass.

Rules:

- Produced for **all five kinds**, even when evidence is thin.
- Each section explicitly marked by confidence:
  Confirmed / Likely / Unknown.
- Unknown is a valid content. Fabricated content is worse than
  an explicit gap.
- Imported foreign documents (README.md, ROADMAP.md from a
  cloned repo) may **inform** the preview but must not **become**
  the preview verbatim. Path or filename does not grant truth
  authority.

### PreviewDocs

Interpreted, structured output after Importer evaluation.

Rules:

- Reviewed for internal consistency across the five kinds.
- Confidence markers may still exist.
- Not yet authoritative for the project. A contributor may
  still correct or reject content.

### CanonicalDocs

Authoritative project truth.

Rules:

- Content here is treated as law by the rest of the system.
- Changes require explicit promotion (not automatic overwrite).
- Staleness (see below) may be marked without loss of canonical
  status; invalidation is a separate decision.

---

## Promotion Rules

### ImportPreview → PreviewDocs

Automatic after Importer interpretation pass completes.

### PreviewDocs → CanonicalDocs

Requires an explicit promotion act. Either:

- contributor approval through the project surface, or
- a system-level rule (future) that promotes when evidence
  coverage and consistency across the five kinds exceed a
  declared threshold.

Silent auto-promotion is forbidden. Canonical status must be
traceable to an explicit decision, manual or rule-based.

---

## Derivation Contract (Importer Obligations)

### Current behavior (today's code)

The Importer produces an ImportPreview body only for kinds where
evidence meaningfully supports derivation. When evidence is
insufficient, the preview file remains **absent** rather than
being fabricated. This is enforced by test assertions ("unsupported
preview doc must remain absent instead of being guessed"). In
practice, today's cssDOOM import reaches 2 of 5 preview kinds
(`preview_project.md` and `preview_capsule.md`); the other three
kinds (`direction`, `roadmap`, `canon`) remain absent by design.

### Target capability (future code evolution)

The Importer evolves toward producing preview for **all five kinds**,
with explicit Unknown placeholder sections when evidence is thin.
An Unknown section lists what evidence is missing and what input
would unblock derivation. Reaching this target is a deliberate code
slice, not a current promise.

### Invariant (holds across current and target)

Under either form, the Importer must:

- prefer silence (absent file) or explicit Unknown sections over
  invented content
- mark confidence per section when the section exists
- not copy imported foreign documents (e.g. `README.md` from the
  source repository) into canonical position verbatim — those
  belong to preserved context, not to `/project/`

The Importer must not:

- fabricate architectural invariants that are not visible in
  evidence (a common failure mode when deriving `canon.md`)
- invent a roadmap from commit history alone
- synthesize a direction statement from aspirational README
  language

### Consequence for 5/5

Today, a project may stand at 2/5 preview and therefore require
additional evidence-gathering, manual contributor authoring, or
importer enhancement before promotion to 5/5 canonical is possible.
Reaching 5/5 canonical is explicitly a product capability in
evolution, not an automatic output of import.

---

## Staleness

When a Worker-accepted apply modifies project content in a way
that falsifies a section of a canonical document, that section
is marked stale.

Rules:

- Stale sections remain readable.
- Stale sections must display staleness to the reader (structural
  marker, not commentary inside the text).
- Stale status does not revoke canonical authority automatically.
  Invalidation is an explicit act.
- Staleness detection via structural diff against evidence is a
  planned capability; until then, staleness is marked on demand
  during review.

A document with stale sections is still part of the 5/5 count.
A document with *only* stale sections no longer satisfies the
reading contract and should be re-derived.

---

## Relation to Other Canon

- **`project_truth_storage_layout_v1.md`** defines where the five
  files live. This canon defines what they contain.
- **`project_meta_contract_v1.md`** defines `project.json`, which
  explicitly does not store the bodies of the five documents.
- **`project_state_model_v1.md`** consumes document presence and
  stage as part of typed state, not document bodies.
- **`read_before_write_v1.md`** applies: Worker must read the
  relevant canonical documents before proposing changes that
  touch their domains.

---

## Canons

- A ZAVOD project has exactly five canonical truth documents.
- Each document answers exactly one question; purposes do not
  overlap.
- 5/5 denotes completeness of canonical coverage, not quality.
- The Importer must produce all five at ImportPreview stage;
  Unknown is valid, fabrication is not.
- `capsule.md` is always derived, has no own stage, and carries
  an explicit `source_stage` marker (preview / canonical / mixed).
- `capsule.md` is derived primarily from Layer A; active shift/
  task state may appear only as a clearly marked overlay.
- `capsule.md` must not contradict the other four and must not
  become a second roadmap/canon copy.
- Promotion to CanonicalDocs requires an explicit act.
- Staleness marks sections; it does not delete or auto-invalidate
  documents.
- Imported foreign documents may inform preview; they may not
  occupy canonical positions verbatim.
- The five documents together must enable a reader to understand
  the project without reading code.

---

## Exclusions

The following must not live in the five truth documents:

- transient execution state (belongs to shift/task layer)
- runtime telemetry (belongs to journal/trace)
- cache and derived snapshots (belong to cache layer)
- decisions log (belongs to `decisions/`)
- foreign imported documents verbatim (belong to preserved
  context, not `/project/`)

If material does not fit the five questions above, it does not
belong in `/project/`.
