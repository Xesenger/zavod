# ZAVOD — Integration Debt Snapshot (v1)

Status: **active tracking**
Created: 2026-04-22
Scope: runtime / integration gaps carried over from the Welcome Surface + Work Packet canon foundation work

---

## Purpose

This document exists so the debt from iteration N is not lost when
iteration N+1 focuses on a different track.

It is **not** canon. It is **not** a plan of record. It is a tracked
snapshot of integration gaps that must close before the system runs
end-to-end.

When a gap closes, mark it resolved (keep the entry with resolution
note; do not delete). When a new gap appears, append it.

---

## Current State (as of 2026-04-25)

The original Welcome Surface and Work Packet primitives no longer exist only
in isolation:

- Welcome Surface selector (pure function, covered by tests)
- Work Packet types: `DocumentCanonicalState`, `CanonicalDocsStatus`,
  `PreviewStatus`
- `WorkPacketBuilder` — pure-function mapper from
  `ProjectDocumentSourceSelection` to Work Packet fields
- `PromptRequestInput` extended with optional Work Packet fields
  (backward-compatible)
- Projects web snapshot calls `WorkPacketBuilder` and
  `WelcomeSurfaceSelector` to render project-home next actions and missing
  truth warnings
- Lead prompt assembly receives Work Packet truth-status fields
  (`canonical_docs_status`, `preview_status`, missing truth warnings, and
  first-cycle marker) through `LeadAgentInput`
- Lead first-cycle prompt now includes a state-derived guidance/guardrail
  when `IsFirstCycle=true`
- Worker prompt assembly receives the same Work Packet truth-status fields
  through `WorkerAgentInput`
- QC prompt assembly receives the same Work Packet truth-status fields
  through `QcAgentInput`

Remaining debt is now narrower:

- first-cycle prompt assembly is now proven through the unified
  `PromptRequestPipeline` for the Shift Lead first-cycle packet, but
  production Lead execution still uses `LeadAgentRuntime` directly
- Lead role production execution still uses `LeadAgentRuntime` directly
  rather than the unified prompt pipeline, though its prompt now carries
  Work Packet truth-status
- Worker role production execution still uses `WorkerAgentRuntime` directly
  rather than the unified prompt pipeline, though its prompt now carries
  Work Packet truth-status
- QC role production execution still uses `QcAgentRuntime` directly rather
  than the unified prompt pipeline, though its prompt now carries Work Packet
  truth-status
- some Welcome actions are projected but not fully wired as product flows

---

## Unresolved Slices

### 1. B3 — First-Cycle Path (Pipeline Gap)

**Gap:** `PromptRequestPipeline.Execute` previously hard-required
`IntentState == Validated` and a `TaskState` that belongs to the
current `ShiftState`. First-cycle open needs a runtime packet before
canonical task truth exists.

**Required change:**

- introduce a first-cycle mode in the pipeline
- relax validation rules for initial entry (no task, no validated
  intent yet)
- allow packet assembly without a pre-existing task so Lead can
  receive the first-cycle instruction

**Canon reference:** `project_work_packet_v1.md` First-Cycle Variant

**Status:** IMPLEMENTED for unified Shift Lead prompt assembly on
2026-04-25. The pipeline now accepts `ShiftLead + IsFirstCycle` with a
synthetic bounded task placeholder, keeps Worker first-cycle gated, and
emits Work Packet first-cycle state / guardrail lines into the assembled
prompt.
**Remaining:** production Lead execution still bypasses
`PromptRequestPipeline`, so runtime call-site wiring is tracked under §2.
**Risk tier when picked up:** HIGH (touching protocol contracts)

---

### 2. Runtime Wiring Gap

**Gap:** the original zero-consumer state is partially resolved. Projects web
snapshot now consumes `WorkPacketBuilder` and `WelcomeSurfaceSelector`, but
model-facing runtime wiring is still incomplete:

- `WorkPacketBuilder` is called from
  `UI/Modes/Projects/Bridge/ProjectsWebSnapshotBuilder.cs`
- `WelcomeSurfaceSelector` is called from
  `UI/Modes/Projects/Bridge/ProjectsWebSnapshotBuilder.cs`
- Lead role currently bypasses `PromptRequestPipeline` entirely
  (direct OpenRouter call, per `docs/_legacy/projects-web-migration/07-pass1-handoff.md`)
- Lead, Worker, and QC model-facing prompts now receive Work Packet
  truth-status, but no production call site is proven to assemble a full Work
  Packet through `PromptRequestPipeline`

**Required:**

- identify actual execution entry points (Projects-Web action handlers,
  chat runtime controllers, any Lead-initiating path)
- wire `WorkPacketBuilder` into `PromptRequestInput` creation
- ensure `PromptRequestPipeline` is the **single** path from project
  memory to the model (no bypass by any role)

**Canon reference:** `project_work_packet_v1.md` — "Work Packet is
the only authorized channel from project memory to the model"

**Status:** PARTIAL — project-home projection is wired; Lead, Worker, and QC
prompts carry Work Packet truth-status; unified `PromptRequestPipeline` wiring
remains open
**Risk tier when picked up:** HIGH (touching protocol contracts and
  existing Lead bypass, multi-file change)

---

### 3. UI Gap — Welcome Surface

**Gap:** the original empty project-home state is partially resolved.
Projects web snapshot renders Welcome actions and missing truth warnings, but
not every projected action is fully wired as a product flow.

**Required:**

- keep project-home welcome/start surface wired to current project state
- show project state (canonical_docs_status, preview_status, 2–4
  next actions from `WelcomeSurfaceSelector`)
- honor `source_stage` marker on capsule_snapshot (preview-sourced
  capsule must render as below-canonical)
- finish remaining action flows such as author-from-scratch and open
  roadmap/direction actions

**Canon reference:** `project_welcome_surface_v1.md`

**Status:** PARTIAL — selector is production-consumed by Projects web snapshot;
remaining work is action-flow completion and visual/product hardening
**Risk tier when picked up:** HIGH (WinUI 3 + Projects-Web integration,
  user-visible surface)

---

### 4. Prompt File Drift (Orthogonal)

**Gap:** ~10 tests failing with messages like
"Prompt system file 'worker.system.md' must contain 'Role:'".
Unrelated to Work Packet / Welcome / 5/5 docs work.

**Required:**

- locate current `worker.system.md` / other role prompt files
- determine what changed (prompt format drift? file relocation?
  `Role:` marker removed by a previous edit?)
- restore tests to green without compromising prompt content

**Impact:** pre-existing since before the canon foundation work.
Blocks full integration testing of `PromptRequestPipeline` from tests.

**Status:** RESOLVED 2026-04-23
**Risk tier when picked up:** LOW-MEDIUM (file hygiene, reversible)

---

## Track Separation

Two parallel tracks must not be mixed:

- **5/5 canonical docs production** (`S1–S7`, see 5/5 plan when
  persisted) — defines how project memory is **built**
- **Integration debt** (this file, items 1–4) — defines how the
  system **runs and surfaces** that memory

A slice in one track must not silently close a gap in the other.
When closing a 5/5 slice, check this file for any gap that becomes
newly blocking. When closing an integration slice, check the 5/5
plan for any section that becomes newly feasible.

---

## Execution Priority Guidance

**Primary track now:**

→ proceed with 5/5 canonical docs (S1–S7)

**Secondary tracked gaps (addressed after or between slices):**

→ B3 first-cycle path (§1)
→ runtime wiring (§2)
→ UI welcome surface (§3)

**Optional quick win:**

→ fix prompt file drift (§4) to restore green tests

---

## 2026-04-25 Priority Update

- Primary: verify Scanner v2 canonical promotion path, then continue the
  remaining 5/5 canonical docs product capabilities.
- Secondary: model-facing Work Packet runtime wiring (§2) and Welcome
  action-flow completion (§3).
- Closed: prompt file drift (§4).

---

## Resolution Log

*(Entries appended as gaps close. Keep resolved items for audit.)*

- 2026-04-23 — Resolved Prompt File Drift (§4): Worker and QC prompt
  files now expose machine-readable `Role`, `Stack`, `Style`,
  `[Rules]`, `[Response Contract]`, and `[Constraints]` blocks while
  preserving their richer markdown guidance. Prompt loader tests pass.
- 2026-04-25 — Partially resolved Runtime Wiring / Welcome Surface
  (§2-§3): `ProjectsWebSnapshotBuilder` now calls `WorkPacketBuilder`
  and `WelcomeSurfaceSelector`, so project-home next actions and missing
  truth warnings are production-projected. Remaining debt is model-facing
  Work Packet assembly through `PromptRequestPipeline` and completion of
  projected Welcome action flows.
- 2026-04-25 — Partially resolved model-facing Work Packet visibility
  (§2): `LeadAgentInput` now carries canonical docs status, preview status,
  missing truth warnings, and first-cycle marker into the Lead prompt.
- 2026-04-25 — Extended model-facing Work Packet visibility (§2):
  `WorkerAgentInput` now carries the same truth-status fields into the Worker
  prompt. Remaining debt is still the unified `PromptRequestPipeline` path for
  Lead/Worker/QC.
- 2026-04-25 — Extended model-facing Work Packet visibility (§2):
  `QcAgentInput` now carries the same truth-status fields into the QC prompt.
  Remaining debt is still the unified `PromptRequestPipeline` path for
  Lead/Worker/QC.
- 2026-04-25 — Partially mitigated First-Cycle Path (§1): Lead direct prompt
  now receives state-derived first-cycle guidance/guardrail when
  `IsFirstCycle=true`. Remaining debt is still the unified
  `PromptRequestPipeline` first-cycle mode.
- 2026-04-25 — Resolved unified First-Cycle Path (§1): `PromptRequestPipeline`
  now accepts a Shift Lead first-cycle packet with a synthetic bounded task
  placeholder, emits first-cycle Work Packet state and honesty guardrails, and
  keeps Worker first-cycle requests gated. Remaining debt moved to runtime
  call-site wiring (§2), because production Lead execution still bypasses the
  unified pipeline.

## Maintenance Rule

This file is updated when:

- a gap listed above closes → add resolution note and move to the
  Resolution Log section; do not delete the original entry
- a new integration gap is identified → append under Unresolved Slices
  with the same structure (Gap / Required / Canon reference / Status /
  Risk tier)

The file itself is Layer D-adjacent (tracking state), not canon.
Updates do not require the decision ceremony of Layer C.
