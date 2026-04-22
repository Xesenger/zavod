# ZAVOD v1 — Project Memory Architecture

## Purpose

Defines the architectural layers of ZAVOD's project memory: what
the system remembers, where each kind of memory lives, who is
allowed to write into it, and how artifacts move between layers.

This is the **root architectural canon** for project persistence.
All other persistence-related canons (`project_truth_documents_v1`,
`project_truth_storage_layout_v1`, `project_meta_contract_v1`,
`project_decisions_v1`, `project_journal_v1`, `project_archive_v1`,
`local_workspace_layout_v1`) derive their scope from this file.

If this canon is changed, downstream canons must be re-validated
against it.

---

## Four Axes

Every artifact ZAVOD persists is located at a specific position
on four orthogonal axes. An artifact whose position is ambiguous
is an architectural defect and must be corrected at its writer,
not patched at its reader.

### Temporal

- **Active** — current system truth. Read for orientation.
- **Historical** — record of what happened. Read for audit, learning,
  and context reconstruction.
- **Archival** — formerly Active, now explicitly superseded. Read
  for historical understanding; never drives current behavior.

### Origin

- **Human-curated** — placed into position by explicit contributor
  act. No silent auto-write.
- **System-derived** — auto-generated from other layers. Never
  hand-edited; regenerated when sources change.
- **System-recorded** — written by the runtime as a by-product of
  real execution events.
- **Evidence-raw** — produced by Scanner/Importer from the project
  itself; independent of any agent interpretation.

### Persistence

- **Project Truth** — lives under `.zavod/`, part of the repository,
  tracked by git, shared with all contributors.
- **Local Ephemeral** — lives under `.zavod.local/`, not tracked
  by git, per-machine, disposable without truth loss.

### Mutability

- **Mutable via promotion** — can change, but only through an
  explicit promotion act.
- **Append-only** — grows over time, never rewrites past entries.
- **Derived** — regenerated from sources; direct edits are undefined.
- **Immutable** — frozen once written (Archive semantics).

---

## Six Layers

Each layer is the unique intersection of axis values that it occupies.
An artifact belongs to exactly one layer. Cross-layer artifacts
must be split.

### Layer A — Active Truth

**Position:** Active · Human-curated · Truth · Mutable via promotion

**Contents:**
- `project.md`
- `direction.md`
- `roadmap.md`
- `canon.md`
- `meta/project.json` (technical meta; body not part of the 5/5
  reading contract)

These four `.md` documents plus the meta file define what the
project **is** right now. The documents are the 4 truth docs; the
meta file is technical identity.

`capsule.md` is **not** in this layer — it is derived (Layer B).

**Writer rule:** only via explicit promotion from Layer E (Evidence
preview) or via contributor edit through the promotion surface.
Runtime, agents, and system derivations may not write here.

### Layer B — Derived Active Surface

**Position:** Active · System-derived · Truth · Derived

**Contents:**
- `capsule.md`

**Source hierarchy:**
1. **Primary source:** Layer A (the four truth documents). Capsule's
   stable content is derived from these. A reader of capsule.md
   between shifts must see a consistent project picture.
2. **Overlay:** optional active-shift state (current focus, immediate
   next step). The overlay is a **minority section**, clearly
   separated from stable content. It may be absent.

Capsule is never a session-dependent document. If shift state is
the dominant source, the document is miscategorized.

**Regeneration trigger:** any change to Layer A. The shift-state
overlay re-renders on shift transit. Capsule is never hand-edited.

### Layer C — Decision Record

**Position:** Historical · Human-curated · Truth · Append-only

**Contents:**
- `decisions/DEC-NNNN-*.md`

**Scope — when a decision must be recorded:**
- Canonical promotion (PreviewDocs → CanonicalDocs for any Layer A
  document)
- Archival act (A → F, D → F)
- Architectural decision (new invariant added to `canon.md`,
  trade-off resolved, approach chosen between alternatives)
- Explicit high-impact reject (rejecting an approach, pivot,
  direction change)

**Scope — when a decision must NOT be recorded:**
- Routine Accept/Reject/Abandon of a Worker result (that is
  execution history, Layer D)
- Every task outcome
- Every shift closure

Decisions are project-shaping acts. If it does not change the
shape of the project, it is not a decision — it is history.

**Supersession:** a decision may supersede another. The superseded
decision remains; it is not edited. The superseding decision
references it explicitly.

### Layer D — Execution History

**Position:** Historical · System-recorded · Truth · Append-only, sealed at closure

**Contents:**
- `shifts/` — shift records with open/close boundaries
- `tasks/` — task records linked to shifts
- `journal/trace/*.jsonl` — high-level event stream

**Writer rule:** system-written during execution. Each entry is
written once; modification after closure is forbidden.

**Journal scope:**
- Shift opened / closed
- Task started / abandoned / accepted / rejected / revised
- Apply committed
- Canonical promotion performed (cross-reference to Layer C entry)
- Role events worth audit (Lead intent captured, Worker result
  produced, QC decision rendered)

Journal is the **audit trail** of the project. It is Truth. It is
readable by all contributors.

**Journal must not contain:**
- LLM request/response bodies (those belong to lab, Local Ephemeral)
- Raw prompt text
- Debug telemetry
- Per-machine runtime noise

### Layer E — Evidence

**Position:** Active · Evidence-raw → System-derived · Truth · Snapshot per import

**Contents:**
- `import_evidence_bundle/` — raw Scanner output
- `preview_docs/` — Importer interpretation before promotion to A

**Authority rule (critical):**

> Preview is **not truth**. It is **candidate understanding**
> before promotion. Preview must remain clearly below canonical
> truth and must never be treated as approved project memory.

This rule is a **reader obligation**, not only a writer obligation.
No component — runtime, agent, derivation generator, contributor
workflow, or external tool — may consult preview as an authoritative
source of project truth. Approved project memory lives only in
Layer A, and only after explicit promotion.

**Writer rule:** Scanner and Importer only. Preview is produced
for **all five kinds** (matching Layer A + capsule), even when
evidence is thin. Missing evidence must manifest as Unknown
sections, not fabricated content.

**Reader rule:**
- Preview may be **consulted for review** by the contributor
  before promotion.
- Preview may be **read by Sage** as evidence of what the Importer
  derived, never as a rule or constraint.
- Preview must **never** be resolved in a "truth or fallback"
  chain where A is checked first and E is used when A is missing.
  A missing Layer A document means the project is not at 5/5;
  preview does not substitute for it.

**Promotion to Layer A:** explicit human act (see Promotion Protocol).

### Layer F — Archive

**Position:** Archival · Human-curated (by act) · Truth · Immutable

**Contents:**
- `archive/` — formerly-Active artifacts preserved for reference

**Archival act rules:**
- Explicit, never silent.
- Obligatory metadata: timestamp, reason, superseded-by reference
  (if applicable), referencing decision ID (Layer C).
- Source artifact is moved, not copied. It no longer occupies
  its Layer A/C/D position.

**Runtime view:** Archive is read-only. The execution pipeline
never consults Archive for current behavior. Archive exists for
contributor understanding and historical reconstruction.

**5/5 accounting:** Archived Layer A documents do not contribute
to the 5/5 canonical coverage count.

**Out of scope for Archive:**
- Imported foreign documents (README.md, ROADMAP.md from source
  repository) — these live in preserved context, never occupy
  Layer A, therefore cannot be archived (they were never active
  truth).

### Local Ephemeral — not a numbered layer

**Position:** any temporal · System / derived · **Local** · transient

**Contents** (see `local_workspace_layout_v1.md` for full spec):
- `.zavod.local/staging/` — Worker edit sandbox
- `.zavod.local/conversations/` — per-machine conversation history
- `.zavod.local/lab/` — per-LLM-call debug artifacts
- `.zavod.local/cache/` — derived caches
- `.zavod.local/sage/observations.jsonl` — Sage raw observations

**Contract:** a contributor may delete `.zavod.local/` entirely
without loss of project truth. Anything that would be lost by
such deletion is miscategorized.

---

## Writer / Reader Matrix

Writer columns (top) vs layer rows. `✓` = allowed writer, `✗` =
forbidden writer, `~` = conditional (see notes).

| Layer → | A (Truth) | B (Capsule) | C (Decision) | D (History) | E (Evidence) | F (Archive) | Local |
|---|---|---|---|---|---|---|---|
| **Contributor** | ✓ via promotion | ✗ | ✓ per decision | ✗ | ✗ | ✓ via archival act | — |
| **Scanner / Importer** | ✗ | ✗ | ✗ | ✗ | ✓ | ✗ | — |
| **Lead runtime** | ✗ | ✗ | ✗ | ✓ journal only | ✗ | ✗ | ✓ lab |
| **Worker runtime** | ✗ | ✗ | ✗ | ✓ journal only | ✗ | ✗ | ✓ staging + lab |
| **QC runtime** | ✗ | ✗ | ✗ | ✓ journal only | ✗ | ✗ | ✓ lab |
| **Apply / Accept cycle** | ✗ | ✗ | ~ only if canonical promotion | ✓ shift/task closure + journal | ✗ | ✗ | ✓ cleanup |
| **Derived generators** | ✗ | ✓ regen | ✗ | ✗ | ✗ | ✗ | ✓ cache |
| **Sage** | **✗ never** | ✗ | ✗ | ✗ | ✗ | ✗ | ✓ observations.jsonl raw |

**Critical invariants enforced by this matrix:**

1. **Worker Accept does not write Layer A.** Worker modifies
   project files (src/, etc.) which are part of the project tree,
   not `.zavod/`. Canonical documents change only via explicit
   human promotion, even when the changes are correct.
2. **Sage never writes Truth.** Sage's memory is Local Ephemeral.
   Promotion of learned patterns to Truth is a future slice
   requiring explicit human act, never automatic.
3. **Apply cycle writes to C only if the act is project-shaping.**
   A routine Worker-result Accept produces a Layer D entry
   (task closure + journal), not a Layer C decision.

---

## Promotion Protocol

Artifacts move between layers only through defined routes. Each
route has a trigger, a writer, and an explicit-act requirement.

| Route | Trigger | Writer | Explicit Act Required? |
|---|---|---|---|
| **E → A** | Contributor reviews preview and promotes | Contributor | Yes |
| **A → A** (update) | Contributor edits existing canonical | Contributor | Yes (promotion of edited preview or direct edit through surface) |
| **A → B** | Any Layer A change | Derived generator | No (auto-regen) |
| **Shift state → B overlay** | Shift transit | Derived generator | No (auto-regen overlay only) |
| **A → F** | Contributor archives superseded artifact | Contributor | Yes, with reason |
| **D → F** | Contributor archives closed shift/task bundle | Contributor | Yes, with reason |
| **Any event → D (journal)** | System runtime events | Runtime | No (auto-append) |
| **Project-shaping act → C** | Promotion / archival / architectural decision / high-impact reject | Contributor | Yes |
| **Local observation → A (learned pattern)** | Explicit human promotion | Contributor | Yes (future slice) |

**Forbidden routes:**
- B → A (derived cannot become source of truth)
- F → A (archive is one-way; un-archive must be a new promotion,
  not a reversal)
- Local → Truth without explicit act
- Runtime → A direct write
- Runtime → C direct write (runtime writes journal, not decisions)

---

## Sage's Architectural Position

Sage is a **derivation layer over Truth and History**, not a
writer of either.

**What Sage reads:**
- Layer A for semantic grounding (task contradicts a rule in
  `canon.md`)
- Layer D for pattern signals (this failure mode repeated N times)
- Layer C for constraints (decision DEC-NNNN forbids this approach)
- Layer E for evidence gaps (user references file X, evidence does
  not cover X)

**What Sage writes:**
- Only to `.zavod.local/sage/observations.jsonl` (Local Ephemeral)
- Never to Layer A / B / C / D / E / F

**How Sage influences behavior:**
- Through UI surface: a contributor sees an observation and decides
- Through S3 deterministic rules (future): accumulated observations
  of one kind may suggest a constraint; the constraint is a Layer C
  decision written by a contributor, not by Sage
- **Never** through role prompt injection (enforced by SAGE v2.1a #2)

**Consequence for architecture:** if any code path writes Sage
output into a truth-layer file automatically, it is a critical
architectural defect. Sage is a tenant in Local Ephemeral; it has
no key to the Truth tier.

---

## 5/5 Canonical Coverage

A project reaches **5/5 canonical** when all of the following hold:

1. `project.md`, `direction.md`, `roadmap.md`, `canon.md`,
   `capsule.md` exist at CanonicalDocs stage in Layer A (capsule
   derived per Layer B rules).
2. None is empty or only TBD placeholder text.
3. Each answers its specific question (no bleed between purposes).
4. The five together do not contradict each other.

5/5 is a property of the **project state**, not of any single
document. A project may drop to 4/5 after archival of a superseded
document until a new one is promoted.

5/5 is **completeness**, not **quality**. Quality is enforced by
Evidence → Preview → Canonical pipeline (explicit confidence marks,
contributor review, no fabrication).

---

## Extensibility Rule

A new persistence artifact type must declare:

- its layer (one of six, or Local Ephemeral)
- its writer (matching the Writer/Reader matrix)
- its mutability
- its retention policy (for Local Ephemeral)
- its promotion route (if any)

No artifact may span layers. Multi-layer concerns are solved by
splitting into per-layer artifacts. For example: staging is Local
Ephemeral; when Accept commits it, the resulting files in the
project tree are plain source code under git, and the journal
entry recording the apply is Layer D. Three artifacts, one act.

---

## Canons

- ZAVOD project memory is layered, not flat.
- Every persistent artifact occupies exactly one layer.
- Layer A is human-curated; runtime and Sage may not write there.
- Layer B (capsule) is derived primarily from Layer A; shift state
  is an overlay, never an equal source.
- Layer C (decisions) records only project-shaping acts, not
  routine execution outcomes.
- Layer D (history + journal) records every runtime event; the
  journal is truth and audit-grade.
- Layer E (evidence + preview) is Importer's output; promotion to
  Layer A is always explicit.
- Preview is candidate understanding, never approved project
  memory; no component may read preview as a truth fallback when
  Layer A is missing.
- Layer F (archive) is immutable and does not contribute to 5/5.
- Local Ephemeral carries runtime noise, debug artifacts, and Sage
  raw observations; deletable without truth loss.
- Sage reads Truth and History; Sage writes only to Local
  Ephemeral; Sage never influences prompts.
- Cross-layer artifacts are architectural defects.
