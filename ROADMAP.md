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
