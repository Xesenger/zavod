# Roadmap

ZAVOD is evolving from a project-aware system into a fully controlled execution environment for real work.

This roadmap focuses on **what the system is becoming**, not only what already exists in code.

---

## Execution Model

Move from implicit agent behavior to structured, controllable execution.

Planned:

- minimal execution DSL  
- explicit execution plans (`ExecutionPlan`)  
- bounded steps (`ExecutionStep`)  
- observable results (`ExecutionResult`)  

Initial step types:

- read / edit files  
- search  
- run commands  
- verify results  

Goal:

- predictable behavior  
- safe automation  
- replayable workflows  

---

## Agent-Agnostic Role System

ZAVOD is designed so that external agents can be replaced without changing the core workflow.

The system should not depend on one specific model, provider, or runtime.
A new agent should enter the process like a new shift on a factory floor: different capabilities, same rules, same structure, same expectations.

### Direction

ZAVOD is moving toward a model where:

- agents are replaceable
- roles remain stable
- workflow remains stable
- contracts remain stable
- acceptance rules remain stable

### Meaning

A stronger or weaker model may affect:

- reasoning quality
- speed
- accuracy
- depth of analysis

But it should not change:

- system boundaries
- role semantics
- execution flow
- review requirements
- evidence requirements
- acceptance logic

### Planned model

External agents will be integrated through role-based contracts.

The same agent family should be able to operate in different roles:

- Lead
- Worker
- QC

What changes between roles is not only the model selection, but the system contract around it:

- permission scope
- tool access
- expected output shape
- execution boundaries
- success criteria
- escalation rules

### Goal

The long-term goal is to make agents interchangeable while keeping the system itself consistent.

In practical terms:

- models may change
- providers may change
- runtimes may change
- the process should remain the same

This allows ZAVOD to treat agents as execution resources inside the system, not as the source of process truth.

## Environment Preparation

Make environment setup part of the system instead of a manual prerequisite.

Planned:

- automatic detection of missing tools  
- guided installation (Qt, .NET, Git, CMake, etc.)  
- verification of dependencies  
- environment readiness checks  

Rules:

- user-approved  
- transparent  
- recoverable  

Goal:

- reduce setup friction  
- support non-expert users  
- ensure reproducible environments  

---

## Runtime & Tool Layer

Introduce a unified execution layer independent of any single tool or model.

Planned:

- controlled filesystem access  
- process execution layer  
- tool abstraction (CLI, scripts, services)  
- sandboxed operations  

Examples:

- running builds  
- executing scripts  
- interacting with local tools  
- controlled network access  

Goal:

- capabilities belong to the system, not the model  
- consistent execution across different environments  

---

## External Tool Orchestration

Integrate existing tools as part of the execution system.

Targets:

- IDEs (Qt Creator, Visual Studio, VS Code)  
- CLI tools (git, cmake, dotnet, compilers)  
- external agent runtimes (Codex, Claude, Google ecosystems)  

Model:

- tools act as execution workers  
- ZAVOD coordinates and observes  
- outputs are captured and integrated  

Goal:

- no tool fragmentation  
- no loss of context  
- unified control over execution  

---

## Internal Editing Surface

Provide a minimal built-in editor to maintain system awareness.

Planned:

- file editing and navigation  
- diff awareness  
- change tracking  
- integration with scanner  

Goal:

- keep system in sync with file state  
- reduce dependency on external editors  
- enable controlled edits inside ZAVOD  

---

## External Change Awareness

Ensure the system never loses track of real project state.

Planned:

- file watchers  
- detection of external edits  
- project invalidation and refresh  
- safe resync flow  

Goal:

- maintain consistency  
- prevent silent desynchronization  
- support hybrid workflows  

---

## Run & Test Inside ZAVOD

Allow validation without leaving the system.

Planned:

- project run profiles  
- build / test execution  
- output streaming  
- result capture  

Goal:

- close the feedback loop  
- reduce tool switching  
- integrate execution into review flow  

---

## Safe Execution Workspaces

Isolate agent work from the real repository.

Planned:

- temporary workspaces  
- patch generation  
- diff-first workflow  
- acceptance-based apply  

Goal:

- prevent accidental damage  
- enable safe experimentation  
- support external agents and contractors  

---

## Guided User Mode

Make ZAVOD usable without deep technical knowledge.

Planned:

- intention-driven entry  
- minimal questioning  
- step-by-step guidance  
- progressive disclosure  

Example:

> "I want to build a project"

System:

- prepares environment  
- creates structure  
- guides next steps  

Goal:

- lower entry barrier  
- make complex workflows accessible  
- keep control without overwhelming the user  

---

## Direction

ZAVOD is moving toward a model where:

- project truth lives outside the LLM  
- execution is structured and observable  
- environment setup is part of the system  
- tools are orchestrated, not manually managed  
- users are guided from idea to result  

---

## Status

Core foundations are in progress.

This roadmap reflects the intended system design, including components not yet implemented.

---

## Note

Long-term direction includes reducing reliance on VM-based execution in favor of more native and direct runtime approaches where it improves control, performance, and system transparency.

---

## Current System Snapshot (Code Reality)

This section reflects the current state of the system based on the codebase.
It is a factual snapshot, not a claim of completeness.

---

### Core Foundation

| Area | Status | Notes |
|------|--------|------|
| Scanner / Import / Evidence | Functional | Core flow works, but accuracy and depth are still evolving |
| Preview → Canonical pipeline | Functional | Stage-based truth pipeline exists and is actively used, but still evolving |
| Runtime / Tool Layer | Functional | Unified execution layer with governance and routing |
| Role System (Lead / Worker / QC) | Functional | Roles and boundaries exist, further refinement expected |
| Acceptance / Apply Boundary | Functional | Controlled result application boundary exists and is separated from execution |

---

### Execution & Orchestration

| Area | Status | Notes |
|------|--------|------|
| Execution lifecycle | Partial | Core flow exists, not yet formalized as a full DSL |
| LLM orchestration | Partial | Lead/Importer are active, full loop not yet complete |
| Worker execution pipeline | Partial | Concept and fragments exist, not fully connected |
| Autonomous runtime planning | Not yet | No model-driven planning layer yet |

---

### Environment & Tooling

| Area | Status | Notes |
|------|--------|------|
| Runtime profiles & isolation | Functional | Runtime profiles and isolation policies exist for local/container/vm/remote paths |
| Tool routing & governance | Functional | Policy-driven execution is implemented |
| External tool orchestration (IDE/CLI) | Early | Initial integration paths exist, not end-to-end |
| Environment preparation (setup) | Early | Detection exists, guided setup not implemented |
| Run / Test inside ZAVOD | Partial | Execution capabilities exist, UX layer incomplete |

---

### User Surface & UX

| Area | Status | Notes |
|------|--------|------|
| Guided user flow | Early | Intent system exists, user journey not fully shaped |
| Internal editor | Not yet | No built-in editing surface |
| External change tracking | Not yet | No realtime file monitoring yet |
| Approval UX | Partial | Policy exists, product-level UX is not finalized |

---

### Summary

- Core architectural layers are in place and already interacting
- Several subsystems are functional but still evolving
- Product-level experience and UI are currently under active reconstruction
- External changes are detected via scan/baseline/acceptance checks; realtime file watching is not implemented yet.

ZAVOD at this stage can be described as:

→ a structured and working system foundation  
→ not yet a complete end-user product
