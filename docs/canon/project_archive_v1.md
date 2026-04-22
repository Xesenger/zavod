# ZAVOD v1 — Project Archive

## Purpose

Defines Layer F of the project memory architecture: the **archive**.
Preserves formerly-active artefacts for historical reference without
letting them influence current system behavior.

Derives from `project_architecture_layers_v1.md`. Works in concert
with `project_decisions_v1.md` (every archival is a decision) and
`project_journal_v1.md` (every archival emits a journal event).

---

## Central Rule

> Archive is **graceful deletion**, not deletion.
> Archived content remains readable forever; archived content
> never drives current behavior.

An artefact enters the archive when it is removed from its active
layer (A / C / D). The artefact itself is preserved; its authority
is extinguished.

Archive is the project's memory of its former selves.

---

## In Scope

Archive accepts:

### 1. Superseded Layer A documents

A canonical document that has been replaced by a newer version
may be archived. The replacement is recorded via a decision
(`type: canonical_promotion`); the old document is archived via
a separate decision (`type: archival`) or as part of the same
atomic promotion if policy dictates.

### 2. Superseded Layer C decisions (optional)

Decisions themselves are append-only and remain in `decisions/`
even when superseded. Archival of a decision file is uncommon
and reserved for decisions that are structurally obsolete
(e.g. referring to a layer concept that no longer exists in
canon). Supersession alone does not warrant archival.

### 3. Closed Layer D shift/task bundles (optional)

A fully closed shift or task bundle may be archived when:

- the shift/task is closed (sealed in Layer D)
- its active value to current work is exhausted
- a contributor explicitly chooses to archive it for tidiness

Routine closed shifts do not require archival — they live
harmlessly in Layer D. Archival is opt-in.

---

## Out of Scope

Archive does **not** accept:

- imported foreign documents (README, ROADMAP, notes from the
  source repository) — these live in preserved context from
  import; they were never in active truth, so they cannot be
  archived
- lab artefacts (Local Ephemeral, prunable by contributor, no
  archive value)
- Sage observations (Local Ephemeral)
- staging sandbox contents (Local Ephemeral; abandoned staging
  goes to `.zavod.local/staging/_abandoned/`, which is **not**
  the archive)
- preview_docs snapshots (Layer E; each import produces a fresh
  snapshot, old snapshots are superseded by the next scan, not
  archived)
- canonical documents that are merely stale — staleness is a
  marker, not a reason to archive. Archive occurs only when a
  document is formally superseded or deprecated.

---

## Storage Layout

```
.zavod/archive/
  docs/
    <kind>/<YYYY-MM-DD>-<DEC-id>-<slug>.md
  shifts/
    <shift-id>/
      <bundled files>
  tasks/
    <task-id>.json
  decisions/
    <DEC-id>.md
```

Rules:

- Top-level folders mirror the originating layer (`docs/` for
  Layer A, `shifts/`/`tasks/` for Layer D, `decisions/` for
  Layer C)
- Archived files carry **archival metadata frontmatter** (see
  below)
- No reorganization after archival — the path at time of
  archival is the path forever

---

## Archival Metadata Frontmatter

Every archived file gets a YAML frontmatter block prepended on
archival:

```yaml
---
archived_at: <ISO 8601 UTC>
archived_by: <contributor id>
archived_via_decision: DEC-NNNN
archived_reason: superseded | deprecated | obsoleted | pivoted | tidy
superseded_by: <reference to replacement, optional>
original_path: <path within .zavod/ at time of archival>
---
```

For file formats that do not support YAML frontmatter natively
(e.g. JSON shift/task files), a sibling `<filename>.archival.yaml`
holds the same metadata.

---

## Archival Act Protocol

An archival is a transaction composed of three writes:

1. **Decision entry** in `decisions/DEC-NNNN-archive-<slug>.md`
   (type: `archival`, see `project_decisions_v1.md`)
2. **Journal event** of kind `document_archived` (see
   `project_journal_v1.md`) with `decision_id` cross-reference
3. **File move** from source layer path to archive path, with
   archival metadata frontmatter applied

All three must be committed atomically. A half-archived artefact
(moved but no decision, or decision but no move) is a defect.

---

## Immutability

Once archived, an artefact is immutable:

- Its file contents never change after archival
- Its path never changes
- Its archival metadata never changes

If an archived artefact needs correction, the correction is a
**new** artefact. The archived version remains as evidence of
the state that was corrected.

---

## Read Rules

### Runtime reads

Runtime components (Lead / Worker / QC / execution pipeline /
derivation generators / Sage) must **not** consult archive
content for current behavior. Specifically:

- Archive is not part of anchor pack assembly
- Archive is not part of active truth assembly
- Archive is not a fallback for missing active truth
- Sage does not treat archive content as patterns or constraints

Archive exists for contributor orientation and historical
audit; it does not inform execution.

### Contributor reads

Contributors may browse archive freely. Archive readers must
understand:

- Archive shows **what the project used to think**, not what it
  currently thinks
- Archived decisions are not revived by reading them
- Archived canonical documents are not restored by reading them

Restoration requires a new promotion decision; the restored
content becomes a new active artefact, not a reinstated old one.

---

## 5/5 Accounting

Archived Layer A documents **do not count** toward the 5/5
canonical coverage.

A project with archived `direction.md` and no replacement sits
at 4/5. It must acquire a new `direction.md` through the normal
E→A promotion path to return to 5/5.

Archive is not a shortcut to coverage.

---

## Relation to Other Layers

- **Layer A:** archival removes from A. Until replacement is
  promoted, 5/5 is reduced by one.
- **Layer C:** every archival is a decision. Archive cannot
  exist without a matching decision entry.
- **Layer D:** every archival emits a journal event.
- **Layer E:** evidence bundles are snapshots per import; old
  bundles are not archived, they are simply superseded on next
  scan. Preview docs follow the same rule.
- **Layer F (self):** archive never archives itself. The archive
  layer is terminal.
- **Local Ephemeral:** abandoned staging lives under
  `.zavod.local/staging/_abandoned/` — this is a quarantine
  pattern inside Local, not an archive. The two must not be
  conflated.

---

## Canons

- Archive is graceful deletion, not deletion.
- Archived content is immutable: contents, path, and metadata
  frozen forever.
- Runtime does not read archive for current behavior; archive
  never informs execution.
- Every archival is a decision (Layer C) and a journal event
  (Layer D). No archival without both.
- Imported foreign documents and Local Ephemeral artefacts are
  never archived — archival is a Truth-tier act for Truth-tier
  artefacts.
- Archived Layer A documents do not count toward 5/5.
- Restoration of archived content is a new promotion, not a
  reversal.
- Abandoned staging is not archive; archive is not staging
  quarantine.
