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

3. Documentation (System-managed)
The system continuously builds and maintains project documentation based on evidence and interpretation.
Documentation is not written manually — it is derived, updated, and kept consistent with the project state.

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

* basic Scanner (import)
* early Importer (interpretation)
* document pipeline (preview → canonical)
* core architectural model (roles, anchors, runtime separation)

### What is incomplete

* Worker execution is not yet fully explored or stabilized
* Execution pipeline is not end-to-end
* QC is only partially implemented
* UI is currently unstable (recent regressions during development)
* Importer may over-interpret or produce inaccurate explanations

### Important note

* some architectural ideas have already evolved
* `canon` documentation may lag behind current thinking
* parts of the system exist as design, not full implementation

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
