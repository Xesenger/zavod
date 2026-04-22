# ZAVOD v1 — Automation Invariant

## Purpose

Defines the boundary between what ZAVOD may do on its own and
what requires an explicit human act.

This is a **foundational invariant**. Every other canon that
describes a writer, a background job, a scheduled task, a
derivation pipeline, or a Sage layer derives its authority
limits from this file.

If this canon is changed, all writer matrices in
`project_architecture_layers_v1.md`, `project_decisions_v1.md`,
`project_journal_v1.md`, `project_archive_v1.md`,
`project_truth_documents_v1.md`, and the future Work Packet
canon must be re-validated against it.

---

## Central Invariant

```
ZAVOD may continue working without the user.
ZAVOD may not redefine project truth without the user.
```

Both halves are load-bearing.

The first half authorizes autonomy: background jobs,
scheduled tasks, derivation pipelines, Sage observations,
preview generation, evidence collection, and journal writes
may proceed without synchronous user presence.

The second half forbids automation from silently reshaping
what the project *is*. Any act that would change the project's
canonical self-description is reserved for explicit human
approval.

---

## What counts as "project truth"

For the purposes of this invariant, project truth is everything
in **Layer A** (canonical docs), **Layer C** (decisions), and
**Layer F** (archive) as defined in
`project_architecture_layers_v1.md`.

Layer D (journal) is a **record** of truth-shaping events, not
truth itself — background jobs may write to it.

Layer E (evidence/preview) is **staging below truth** — background
jobs may write to it, but may not cross the E→A boundary alone.

Local Ephemeral is not project truth at all — background jobs
write there freely.

---

## Permitted Automation

Background jobs and automated pipelines MAY:

- write to Layer D (journal) — append-only event stream
- write to Layer E (evidence, preview docs, import artifacts)
- write to Local Ephemeral (`.zavod.local/` — sage, lab,
  cache, staging, runtime, index)
- derive Layer B (capsule, rendered views) from Layer A
- regenerate derived artifacts when sources change
- produce reports, checkpoints, observations, recommendations
- assemble Work Packets for runtime delivery
- emit Sage observations (S1+ typed observations only; never
  direct prompt injection per
  `project_architecture_layers_v1.md` Sage section)

None of the above requires user approval, because none of it
reshapes project truth.

---

## Forbidden Without Human Act

Background jobs and automated pipelines MUST NOT:

- write to Layer A (canonical docs) directly
- promote Layer E → Layer A (preview → canonical) silently
- write to Layer C (decisions) — decisions record human acts,
  not automation outcomes
- archive Layer A → Layer F without the decision entry that
  authorizes it
- rewrite or delete existing Layer A content
- rewrite or delete existing Layer C entries (supersession is
  additive and human-authored)
- rewrite or delete Layer F archive content (immutable)

Any of the above is a **project-shaping act** and requires an
explicit, attributable contributor action.

---

## Project-Shaping Acts (reserved for human approval)

The following acts reshape what the project *is* and must be
authored by a human contributor, not by automation:

1. **Canonical promotion** — Layer E preview doc becoming a
   Layer A canonical doc.
2. **Canonical revision** — replacing or meaningfully rewriting
   an existing Layer A doc.
3. **Architectural decision** — any Layer C decision entry.
4. **Archival** — moving a Layer A doc to Layer F.
5. **High-impact rejection** — rejecting a proposed canonical
   doc or promotion in a way that shapes project direction.
6. **Roadmap or direction shift** — changing the target of the
   project, not just progress against it.

Automation may **propose**, **assemble evidence**, **render**
previews, and **surface** these acts to the user. Automation
may not **perform** them.

---

## The Proposal / Approval Seam

Automation is expected to reduce friction around human acts,
not to replace them. The standard pattern:

1. Background job detects a condition (e.g. enough evidence
   accumulated for a preview doc to be viable).
2. Job writes to Layer E (preview) and Layer D (journal event
   recording the proposal).
3. Job surfaces the proposal to the user through the UI.
4. User performs the project-shaping act explicitly.
5. Runtime writes the act to Layer A / C / F as appropriate,
   attributed to the contributor.

The seam is always the same: **automation proposes, human
approves, runtime records**.

---

## Attribution Rule

Every write to Layer A, Layer C, or Layer F MUST carry a
contributor identity. A write with no attributable human act
is an invariant violation and must be rejected at the writer.

Automation-authored Layer D events carry the automation's
identifier (`sage.s1`, `importer`, `scheduler`, etc.), never
a user identity.

---

## Scope of this Invariant

This invariant applies to:

- all scheduled tasks and cron jobs
- all Sage layers (S0 legacy advisory and S1+ typed)
- the importer pipeline
- all derivation writers (capsule builder, state builder,
  journal writer)
- all future background automation

This invariant does NOT constrain:

- UI-initiated actions where the user is synchronously present
  and performs the act explicitly
- local developer edits to `.zavod/` via file editor (these are
  the developer acting as the contributor, at their own risk)
- test fixtures and dev utilities operating on test projects

---

## Enforcement

This invariant is enforced by:

- writer matrix in `project_architecture_layers_v1.md`
  (layer-by-layer writer list)
- decision entry requirement in `project_decisions_v1.md`
- archival 3-write transaction in `project_archive_v1.md`
- attribution requirement on all Layer A / C / F writes

A background job that attempts a forbidden write must fail
loudly, not silently degrade to Layer E or Layer D.

---

## Status

Locked.

This is a foundational invariant. It may be extended (more
forbidden acts, more detail in the proposal seam) but its
central rule — *may work without the user, may not redefine
truth without the user* — is not subject to revision.
