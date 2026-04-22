## Documentation

- [Roadmap](./ROADMAP.md)
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

1. Project Import (Scanner)
Reads files, structure, dependencies → produces cold, evidence-based data

2. Interpretation (Importer)
Explains what the project might be → explicitly marks confidence (Confirmed / Likely / Unknown)
The system derives and maintains documentation based on this interpretation.

3. Documentation (System-derived)
The system derives project documentation from evidence and interpretation across five canonical kinds: Project, Direction, Roadmap, Canon, Capsule.
Documents flow through stages (ImportPreview → PreviewDocs → CanonicalDocs). Making 5/5 canonical production a dependable product capability is the next direction — today not every document reaches CanonicalDocs automatically.

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

### What exists

* Scanner and Importer for evidence-based project grounding
* Document pipeline: ImportPreview → PreviewDocs → CanonicalDocs
* Role system: Lead, Worker, QC — all live via typed LLM contracts
* End-to-end execution cycle: intent → Lead framing → Preflight → Worker with real typed edits → sandbox staging → QC adjudication (ACCEPT / REVISE / REJECT) → SHA256 hash-guarded apply on user Accept
* Revision loop carries forward structured feedback: prior QC rationale, user revision intake, staging skip reasons
* Quarantine on abandon preserves staged artefacts under `.zavod.local/staging/_abandoned/<taskId>-<utc>/` rather than deleting
* Sage advisory pipeline (S1–S5a): typed `SageObservation` records in sage-only JSONL, pipeline hooks at four stages, core-enforced budgets, two field-verified emitters (`semantic_gap`, `attention_miss`) — zero prompt pollution
* Web-rendered Projects and Chats modes with phase-aware surfaces

### What is incomplete

* Producing all five canonical documents (Project / Direction / Roadmap / Canon / Capsule) is not yet a product capability — derivation exists, but reaching 5/5 consistently is the next direction
* Mechanical verification (build / lint / test) via typed tool contracts is deferred; current drift detection is SHA256 origin-hash + staging manifest
* No internal editor or realtime file watchers — external edits surface only on scan/baseline
* No in-UI Sage surface yet; observations live in `.zavod/sage/observations.jsonl` for manual inspection
* Pattern memory, deterministic Sage rules, and middle-truth correlation layer are deferred until real use justifies them
* Importer may over-interpret or produce inaccurate explanations

### Important note

* Some architectural ideas have already evolved
* Parts of the system exist as design, not full implementation
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
