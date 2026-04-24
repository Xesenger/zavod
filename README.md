## Documentation

- [Roadmap](./ROADMAP.md)
- [Alpha testing guide](./ALPHA_TESTING.md)
- [License](./LICENSE)

# ZAVOD

ZAVOD is a system for controlled project work with LLM agents.

It replaces chaotic prompting with a structured workflow: read → interpret → execute → review.

A project is first grounded in real data, then interpreted with explicit uncertainty, and only after that — modified.

Agents are not owners of the project.  
They operate inside a defined process as roles: Lead, Worker, QC.

Models may change. The process remains stable.

**A system that turns chaotic LLM-driven development
into a structured, controlled workflow.**

---

## What it is

ZAVOD is not a chat and not just an AI assistant.

It is a working environment where:

* a project is first **read and grounded in facts**
* then **interpreted honestly (with uncertainty)**
* and only after that - modified

---

## How it works

1. Project Import (Scanner v2 Evidence Cartographer)
Builds cold evidence from files, manifests, entry points, topology, run profiles, uncertainty, and scan budgets.
Produces cold evidence data rather than project-purpose claims.

2. Interpretation (Importer)
Preserves scanner topology boundaries while explaining what the project might be.
Explicitly marks confidence (Confirmed / Likely / Unknown) and keeps preview docs below canonical truth until reviewed.
The system derives and maintains documentation based on this interpretation.

3. Documentation (System-derived)
The system derives project documentation from evidence and interpretation across five canonical kinds: Project, Direction, Roadmap, Canon, Capsule.
Documents flow through stages (ImportPreview → PreviewDocs → CanonicalDocs). Import can produce 5/5 preview docs; per-document promote/reject is wired, while edit-before-promote and author-from-scratch remain under construction.

4. Role-based work
Lead - understands intent and builds a plan
Worker - executes changes
QC - validates the result

5. Runtime execution
All operations are executed in a controlled environment

6. UI as projection
The interface shows execution, but does not define logic

---

## Why this is different

Typical LLM workflows:

* use chat as context
* rely on memory and guessing
* easily break scope boundaries

ZAVOD:

* does **not use chat as truth**
* builds structured requests
* operates on **anchors (verified facts)**

```
Stable Base
+ Shift Context
+ Anchor Pack
= Request Packet
```

If something is not anchored, it is not treated as real.

---

## Execution as a process

ZAVOD does not “return answers”.

It shows execution:

* output is streamed
* UI updates in logical blocks
* results go through validation (QC)

```
Streaming → InProgress → Result → Finalized
```

---

## User experience

The user:

* selects a project
* sees what the system could understand
* describes what they want in plain language

ZAVOD:

* translates this into structured intent
* executes
* shows the result

---

## Direction

ZAVOD is not being designed as a standalone chat interface only.

The broader direction is to support integration with external developer tools:

* CLI-based agent workflows
* IDE-assisted editing and execution
* multiple model backends

The goal is not to replace those tools,
but to orchestrate them within a structured project workflow.

These integrations are part of the intended architecture
and are not yet implemented as stable features.

---

## Current status

ZAVOD is a **prototype and ongoing research**, not a finished product.

It is ready only for a small friendly alpha with people who understand that
generated preview documents are candidates, not truth. See
[Alpha testing guide](./ALPHA_TESTING.md) for the current test script and known
limits.

### What exists

* Scanner v2 MVP evidence cartographer: structural inventory, manifests, symbols, edges, entrypoints, project units, run profiles, topology, budgets, uncertainty, and cold scan summaries
* Importer alignment with scanner topology for MaterialOnly, Container / MultipleIndependentProjects, MixedSourceRelease, Decompilation, Legacy, Ambiguous, and ReleaseBundle modes
* Document pipeline: ImportPreview → PreviewDocs → CanonicalDocs, with 5/5 preview generation for Project / Direction / Roadmap / Canon / Capsule
* Projects Home truth-doc status block for the five canonical document kinds
* Per-document preview promotion into canonical docs, with Layer C decision records and Layer D journal events
* Per-document preview rejection, with Layer D journal attribution
* Role system: Lead, Worker, QC — all live via typed LLM contracts
* End-to-end execution cycle: intent → Lead framing → Preflight → Worker with real typed edits → sandbox staging → QC adjudication (ACCEPT / REVISE / REJECT) → SHA256 drift-blocking staged apply on user Accept
* Revision loop carries forward structured feedback: prior QC rationale, user revision intake, staging skip reasons
* Quarantine on abandon preserves staged artefacts under `.zavod.local/staging/_abandoned/<taskId>-<utc>/` rather than deleting
* Sage advisory pipeline (S1–S5a): typed `SageObservation` records in sage-only JSONL, pipeline hooks at four stages, core-enforced budgets, two field-verified emitters (`semantic_gap`, `attention_miss`) — zero prompt pollution
* Web-rendered Projects and Chats modes with phase-aware surfaces

### What is incomplete

* 5/5 canonical creation is not yet product-complete: per-kind promote / reject exist, but edit-before-promote, author-from-scratch, and runtime 5/5 state awareness are still next work
* Mechanical verification (build / lint / test) via typed tool contracts is deferred; current drift detection is SHA256 origin-hash + staging manifest
* No internal editor or realtime file watchers — external edits surface only on scan/baseline
* No in-UI Sage surface yet; observations live in `.zavod.local/sage/observations.jsonl` for manual inspection
* Pattern memory, deterministic Sage rules, and middle-truth correlation layer are deferred until real use justifies them
* Scanner v2 is an MVP review candidate, not a production-grade full architecture map
* Ambiguous layouts can still expose wording gaps, especially around "main entry" versus candidate entry surfaces
* Decompilation and legacy repositories can still produce broad active-root candidates that need review
* Canonical promotion has not yet been re-verified end-to-end after the Scanner v2 topology/importer alignment work

### Important note

* Some architectural ideas have already evolved
* Parts of the system exist as design, not full implementation
* Local API credentials are machine-local, not repository truth. OpenRouter local config is read from `Documents/ZAVOD/openrouter.local.json` by default, or from `OPENROUTER_CONFIG_FILE`; secret files must not live in the source tree or clean exports.
* Sage is designed to observe, not adjudicate — role boundaries (Sage observes, QC adjudicates) are enforced by construction

---

## Goal

To build a system where:

* entering a project takes minutes, not hours
* environment setup is not a user burden
* work becomes structured and observable
* LLM is no longer a “black box”

---

## Contributing

ZAVOD is an early-stage project.

If you find the idea interesting, feel free to explore, experiment,
and open discussions or pull requests.

The system is still evolving, so contributions are more about shaping direction
than polishing details.

---

ZAVOD is not necessarily a final product name.

It is a concept - a system for managing project work
in a structured and controlled way.

---

## In short

ZAVOD is an attempt to turn software development
into a controlled, observable system.

---

## More

Architecture and internal concepts → `/docs/`
