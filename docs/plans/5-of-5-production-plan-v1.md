# ZAVOD — 5/5 Canonical Docs Production Plan (v1)

Status: **active plan of record**
Created: 2026-04-22
Scope: production pipeline for the five canonical project documents
  — `project.md`, `direction.md`, `roadmap.md`, `canon.md`, `capsule.md`

---

## Purpose

Defines the slice order and acceptance contract for bringing the
system from the original **2/5 preview capability** (project +
capsule v1) to a full **5/5 canonical production** capability.

This is a plan, not canon. Canon for the five documents lives in
`docs/canon/project_truth_documents_v1.md`. This plan decides
**how** we get there, **in what order**, with **what blockers**.

---

## Original Baseline (as of 2026-04-22)

**Present in code at plan start:**

- `ProjectDocumentRuntimeService.WritePreviewDocs` produces
  `preview_project.md` + `preview_capsule.md` (v1 shape)
- `ConfirmPreviewDocs` promotes project + capsule to canonical
- `ProjectDocumentPathResolver` knows paths for all 5 kinds;
  direction/roadmap/canon writers do not yet exist
- Scanner + Importer produce structured Interpretation:
  SummaryLine, ProjectDetails, Materials, EntryPoints, Modules,
  Confirmed/Likely/Unknown Signals, TechnicalPassport, ProjectProfile

**Missing at plan start:**

- preview writers for direction, roadmap, canon
- canonical promotion for direction, roadmap, canon
- Capsule v2 schema (source_stage, 8 sections, overlay, regeneration)
- UI surface for per-kind promotion and contributor authoring
- 5/5-state awareness in project state / welcome surface / work packet

## Current Checkpoint (as of 2026-04-23)

**Present in code:**

- `ProjectDocumentRuntimeService.WritePreviewDocs` produces
  `preview_project.md`, `preview_direction.md`,
  `preview_roadmap.md`, `preview_canon.md`, and
  `preview_capsule.md`
- S1-S5 preview writers are implemented:
  project refinement, observed canon preview, direction extractor,
  candidate-level roadmap preview, and capsule v2
- import preview surface can read and display all 5 document kinds
- canonical promotion still requires contributor action; preview
  generation does not silently create canonical truth

**Still missing / next:**

- S6 per-kind promotion / reject / edit / author UI
- Layer C decision and Layer D journal attribution for promotions
- S7 production 5/5 state awareness through project state, welcome
  surface, and Work Packet metadata

---

## Slice Order

Chosen: **S1 project → S2 canon → S3 direction → S4 roadmap → S5
capsule v2 → S6 promotion UI → S7 5/5 state awareness**.

**Reasoning:**

- `project.md` is the **root identity layer**; everything else
  references it.
- `canon.md` has the **strongest current grounding in real evidence**
  after project (TechnicalPassport + Modules give observed technical
  invariants honestly; authored rules are deferred to contributor).
- `direction.md` precedes `roadmap.md` because roadmap phases must
  point toward a declared direction target. Reversing the order
  produces phases in a vacuum.
- `capsule.md` v2 stays last because it **aggregates the full 5/5
  picture**. Writing v2 before S1–S4 complete would render a capsule
  against mostly-absent Layer A, requiring a rewrite when the other
  kinds come online. Capsule v2 **can** honestly render with absent
  kinds (per source_stage and 8-section overlay rules), but doing so
  at the end gives one proper render instead of two.
- `promotion UI` (S6) requires all 5 preview writers to exist so the
  UI has five handoff targets to wire.
- `5/5 state awareness` (S7) is the last cross-cutting slice: with
  all 5 kinds producible, the project state builder, welcome surface,
  and work packet can start reporting true 5/5 counts.

---

## S1 — project.md v2 refinement

**Current:** works. Produces preview and canonical. Coarse but honest.

**Gap:** multi-project container handling is weak; confidence markers
per section exist but could be tightened; identity is not yet stable
across re-imports when source root changes.

**Inputs:** `WorkspaceImportMaterialInterpreterRunResult` (already
consumed today).

**Acceptance:**

- preview project.md carries explicit Confirmed / Likely / Unknown
  markers on every section
- container / multi-project mode renders a bounded, honest preview
  without inventing a unified architecture
- re-import on the same root produces stable Identity block
  (deterministic project_id, project_name)
- tests cover: single-project happy path, container case, re-import
  idempotence

**Confidence:** Medium. Evidence is real; work is refinement.

**Risk tier:** MEDIUM (touches import pipeline output, but well-scoped).

---

## S2 — canon.md (observed technical invariants only)

**Current:** no writer exists.

**Gap:** canon.md is conceptually hardest because most of it
(review rules, execution rules, intent rules) is **authored**, not
observed. Auto-deriving those from code is canon-forbidden
fabrication.

**Inputs:** `TechnicalPassport` (Languages, Frameworks, BuildSystems,
Toolchains), `Modules` structure, `EntryPoints` patterns from
Interpretation.

**Acceptance — THREE EXPLICIT SECTIONS:**

1. **Observed technical invariants** — Confirmed confidence.
   Examples: `C# 13`, `.NET 8`, `WinUI 3`, `x64`, build commands
   from build system evidence. Derived from TechnicalPassport.
2. **Contributor-authored rules** — empty on auto-preview. Placeholder
   section with explicit "No authored rules yet. Contributor must
   add review rules / execution rules / intent rules here." Must
   survive promotion as the authored surface.
3. **Unknown / not-yet-established** — gaps list. Explicit "What is
   not yet canonical: review workflow, execution boundaries, refusal
   rules, truth mutation limits, scope discipline." Contributor
   picks from this list to author rules.

Writer produces all three sections. **The writer never fabricates
rules in section 2 from observed signals.** If a framework forces an
architectural pattern, that fact goes in section 1 (observed), not
section 2 (authored).

**Confidence:** Medium for section 1 (real evidence). Zero for
section 2 (contributor-owned). Medium for section 3 (honest gap list).

**Risk tier:** MEDIUM (new writer, new canon section contract, but
  isolated from existing preview code).

---

## S3 — direction.md (extractor + preview writer)

**Current:** no writer, no extractor.

**Gap:** today's scanner classifies signals as Confirmed / Likely /
Unknown but does not classify them as "direction signals". Imported
README/ROADMAP.md are present as materials but canon forbids using
foreign documents verbatim.

**Required:**

- new extractor: `DirectionSignalInterpreter` — reads imported README
  material, entry point purposes, module naming patterns
- extractor output: candidate direction statements with confidence
  markers, **never** verbatim README copy
- writer: `BuildPreviewDirectionMarkdown` — renders extractor output
  as Confirmed / Likely / Unknown split
- every rendered line traces to a concrete evidence signal; no
  "we think the project wants to..." invention

**Acceptance:**

- preview_direction.md exists for imports with README material
- for imports without README: preview_direction.md contains
  **only** the Unknown section listing what evidence would unblock
  derivation
- writer never copies README body verbatim (test assertion)
- contributor can always reject / rewrite preview; promotion requires
  explicit contributor act

**Confidence:** Low to Medium. Quality depends on README quality.
Honest Unknown-heavy output is the expected default.

**Risk tier:** MEDIUM-HIGH (new interpretation pass, new writer, must
  honor anti-fabrication rules strictly).

---

## S4 — roadmap.md (git history reader + preview writer)

**Current:** no writer, no reader.

**Gap:** scanner does not read git history. Imported ROADMAP.md is
available as material but cannot be used verbatim.

**Required:**

- new reader: read git log within project root (commit messages,
  tags, branch names)
- new extractor: extract candidate phase markers from commit patterns
  ("feat:", "phase-N", release tags)
- writer: `BuildPreviewRoadmapMarkdown` — renders candidate phases
  with their evidence source (commit range, tag reference, imported
  ROADMAP hint)

**Critical constraint — CANDIDATE-LEVEL ONLY:**

The roadmap preview is **always candidate-level**. It must never
present phases as if the system knows project intent better than
the contributor.

Every rendered phase must:

- be marked as `Candidate` confidence (not Confirmed, not Likely)
- carry a trace to the specific evidence source (which commits, which
  tag, which ROADMAP.md section)
- be prefixed with contributor-facing framing: "Candidate phase from
  [evidence]. Contributor must confirm or replace."

The writer must **not**:

- rank phases by priority (priority is contributor intent, not
  system-observable)
- declare "done criteria" (done is contributor-authored)
- merge multiple evidence sources into a synthetic narrative

**Acceptance:**

- preview_roadmap.md exists for imports with git history
- phases shown as `Candidate` markers only
- every phase line traces to evidence
- imports without git history render Unknown-only roadmap preview
- writer never uses "the project will", "next we plan to", or other
  intent-claiming phrasing
- contributor always authors canonical roadmap; promotion is
  contributor act

**Confidence:** Low. Real roadmap is intent; observable evidence is
footprints. Gap between them is permanent.

**Risk tier:** HIGH (first slice that reads git; new reader; largest
  fabrication risk of any slice).

---

## S5 — capsule.md v2 upgrade

**Current:** v1 writer produces simple derived summary from
canonical project + preview capsule.

**Gap:** v1 shape does not match `project_truth_documents_v1.md`
Capsule v2 contract:

- no `source_stage` marker
- no 8-section ordered layout
- no active shift/task overlay section
- no regeneration triggers on Layer A / 5/5 state change

**Required:**

- rewrite `BuildCanonicalCapsuleMarkdown` (and preview variant) to
  v2 schema per canon
- add `source_stage` resolution: `canonical` when all 5 are canonical,
  `preview` when all drawn from preview, `mixed` otherwise
- add "Current focus" overlay section (minority, clearly marked) that
  reads active shift/task state when present, empty otherwise
- add regeneration hook: re-derive capsule when any Layer A doc
  changes OR 5/5 state changes (automation-permitted per
  `project_automation_invariant_v1.md`)

**Acceptance:**

- capsule.md has exactly 8 sections in canon order
- capsule.md carries `source_stage` field in frontmatter or explicit
  marker block
- capsule with `source_stage: preview` reads as clearly below
  canonical in UI surfaces (reader obligation honored)
- regeneration triggers fire deterministically; same inputs → same
  capsule bytes
- mixed-source capsule lists per-section which stage the content
  came from

**Confidence:** Medium. Writer work is mechanical; v2 schema is
specified in canon.

**Risk tier:** MEDIUM (touches existing writer, but v2 contract is
  explicit; isolated from other kinds).

---

## S6 — Promotion UI

**Current:** `ConfirmPreviewDocs` handles project + capsule only.

**Gap:** no per-kind promotion for direction / roadmap / canon.
No UI surface for promote / reject / author actions.

**Required:**

- extend `ConfirmPreviewDocs` (or split into per-kind handlers):
  `PromoteDirection`, `PromoteRoadmap`, `PromoteCanon`
- each promotion writes to `.zavod/project/<kind>.md` AND records
  Layer C decision entry AND Layer D journal event (per
  `project_automation_invariant_v1.md` attribution rule)
- UI surface (projects-web): per-kind preview viewer with
  `[Promote]`, `[Reject]`, `[Edit before promote]`, `[Author from
  scratch]` actions
- reject removes preview and logs Layer D event (no silent deletion)

**Acceptance:**

- each of 5 kinds has its own promotion path
- every promotion creates a decision entry (contributor identity,
  timestamp, per-kind diff reference)
- UI shows preview with explicit "preview only" marker
- contributor can always author a kind from scratch bypassing preview
- no silent auto-promotion

**Confidence:** Medium. Mechanical; contract is clear from canon.

**Risk tier:** MEDIUM-HIGH (touches persistence writers, Layer C
  decisions, UI integration — multi-surface change).

---

## S7 — 5/5 state awareness (cross-cutting)

**Current:** project state builder reports coarse state; welcome
surface is a pure function but lacks true 5/5 input in production;
work packet builder is ready but not called.

**Gap:** nothing in production reports "canonical 3/5, preview 4/5"
honestly yet.

**Required:**

- `ProjectStateBuilder` outputs authoritative 5/5 counts per kind
- welcome surface selector receives real production state
- work packet assembly calls `WorkPacketBuilder` to populate
  `CanonicalDocsStatus` and `PreviewStatus`
- `missing_truth_warnings` are produced from real gaps
- `canonical_docs_status` flows end-to-end to the model via Work
  Packet

**Acceptance:**

- project open renders welcome surface with correct R1..R6 rule
  matching real docs state
- Work Packet metadata carries accurate canonical / preview / stale
  counts
- model-visible warnings list real gaps, not placeholders
- no surface silently hides a gap to look more confident

**Confidence:** High. Wiring, not invention.

**Risk tier:** HIGH (cross-cutting through state builder, welcome
  surface, work packet, UI — one of the biggest integration slices).

**Dependency:** requires B3 (first-cycle path) and runtime wiring
gap from `integration-debt-v1.md` §2 to be resolved first, otherwise
the wiring has nowhere to land.

---

## Cross-Cutting Invariants (apply to every slice)

Per `project_truth_documents_v1.md` and
`project_automation_invariant_v1.md`:

- **no fabrication:** every preview line traces to a concrete
  evidence signal or is explicitly marked Unknown / authored-by
- **no verbatim foreign copy:** README, ROADMAP, and other imported
  documents may inform preview but may not become preview body
- **no silent promotion:** preview → canonical is always a
  contributor act with Layer C decision + Layer D event
- **no hidden contributor intent claim:** especially in S3 and S4 —
  the system never pretends to know direction / roadmap better than
  the contributor

Every slice acceptance must include a test that asserts the slice
does not violate these invariants.

---

## Relation to Integration Debt

This plan (S1–S7) defines how project memory is **built**.
`docs/plans/integration-debt-v1.md` tracks how the system actually
**runs and surfaces** that memory.

S7 (5/5 state awareness) has an **explicit dependency** on the
integration debt items 1 and 2 (B3 first-cycle path + runtime
wiring). Those must close before S7 can land meaningfully.

S1–S6 can proceed in parallel with integration debt work — they
produce preview/canonical files on disk regardless of whether the
runtime wires those files to the model.

---

## Execution Cadence

- one slice at a time
- per-slice: design → writer → tests → canon anti-fabrication assertion → integration
- after each slice: update this file's "Progress" section
- no cross-slice speculation: don't design S4 while working S2

---

## Progress

*(Updated as slices land. Do not delete completed entries.)*

- [x] S1 project.md v2 refinement
- [x] S2 canon.md (observed technical invariants only)
- [x] S3 direction.md (extractor + preview writer)
- [x] S4 roadmap.md (git reader + preview writer, candidate-level only)
- [x] S5 capsule.md v2 upgrade
- [ ] S6 promotion UI
  - 2026-04-23: service-level per-kind preview → canonical promotion
    landed for all five document kinds. `ConfirmPreviewDocs` now
    materializes 5/5 canonical docs and writes Layer C promotion
    decisions plus Layer D journal events. Projects Home now exposes
    per-kind truth-doc status and calls per-kind promotion for preview
    docs. UI actions for reject / edit-before-promote /
    author-from-scratch remain pending.
  - Blocker discovered 2026-04-23: reject-preview needs an allowed
    Layer D journal event kind. `project_journal_v1.md` currently
    permits `preview_regenerated` and `canonical_promoted`, but no
    preview-rejected / preview-removed event. Do not invent this in
    runtime code before the canon event vocabulary is extended.
  - 2026-04-23: blocker resolved in canon by adding
    `preview_rejected` as a Layer D document-pipeline event.
    Runtime now removes rejected preview docs only after writing the
    journal event, and Projects Home exposes a per-kind reject action
    for preview-only documents. Edit-before-promote and
    author-from-scratch remain pending.
- [ ] S7 5/5 state awareness

---

## Maintenance Rule

This file is a plan of record. It is updated when:

- a slice lands → mark complete in Progress, keep the slice spec
- a slice acceptance criterion is refined → edit in place with note
- a new blocker is discovered → append under the affected slice as
  "Blocker discovered YYYY-MM-DD: …"
- the order changes → requires explicit canon-level discussion; do
  not reorder silently

This plan is not canon. It may be revised. Canon it references
(`project_truth_documents_v1.md`, `project_automation_invariant_v1.md`,
`project_welcome_surface_v1.md`, `project_work_packet_v1.md`) is
authoritative; this plan conforms to it.
