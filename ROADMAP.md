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

### Canonical vocabulary

Role prompts, typed output schemas, and runtime routing must share a single canonical vocabulary. Synonyms (e.g. `complete` vs `success`, `Partial` vs `partial`) must be normalized at the classification layer so that model output cannot silently drift from system classification. A confident Worker response must never fall to the refused path because the prompt accepted a synonym the parser did not recognize.

This invariant belongs to the agent-contract boundary: when a role is swapped for a different model, the system vocabulary stays constant and the new model is normalized onto it — not the other way around.

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
| Scanner / Import / Evidence | Functional | Core flow works, accuracy and depth still evolving |
| Preview → Canonical pipeline | Functional | Stage-based truth pipeline active, still evolving |
| Runtime / Tool Layer | Functional | Unified execution layer with governance and routing |
| Role System (Lead / Worker / QC) | Functional | All three roles LLM-backed via OpenRouter with typed input/output contracts; QC decision is authoritative and drives phase + runtime transits (ACCEPT → Result/Ready, REVISE → Execution/Revision, REJECT → task abandoned); revision feedback loop passes prior QC rationale and user intake back to Worker |
| Acceptance / Apply Boundary | Functional | SHA256 hash-guarded atomic apply from staging sandbox to project on user Accept; quarantine on abandon preserves forensics under `.zavod.local/staging/_abandoned/<taskId>-<utc>/` |
| Dispatching / Router | Functional | ExecutionDispatcher + Bootstrap/ActiveShift/Idle subsystems + ProjectRouter |
| Advisory layer (Sage) | Functional (S1–S5a) | Two layers coexist: legacy `ProjectSageService` keyword advisory (S0) that still feeds Lead/Worker framing, and typed Sage pipeline (S1–S5a) emitting `SageObservation` records into sage_only JSONL at `.zavod/sage/observations.jsonl`. Pipeline hooks at AfterIntent / BeforeExecution / BeforeResult / AfterResult; core-enforced per-hook and per-task budgets; two field-verified emitters (`semantic_gap` on AfterIntent, `attention_miss` on BeforeExecution). Observations never enter role prompts (v2.1a isolation contract). Pattern memory (`pattern_repeat`), middle-truth correlation layer, and S3 deterministic rules are deferred until real field pain justifies them |

---

### Execution & Orchestration

| Area | Status | Notes |
|------|--------|------|
| Execution lifecycle | Functional | End-to-end cycle closes: intent → Lead validation → Preflight → Confirm → Worker (with real edits) → sandbox staging → QC decision → apply/abandon → commit. Not yet formalized as a declarative DSL |
| LLM orchestration (OpenRouter) | Functional | Lead / Worker / QC runtimes with typed contracts; per-call lab telemetry (`.zavod/lab/<UTC>-<role>-<taskId>/{request,response,parsed,meta}.json`); `max_tokens` plumbed through to upstream |
| Worker execution pipeline | Functional | Worker emits typed `edits` (write_full / insert_after with unique anchors); anchor discipline + revision feedback contract in prompt; output schema enforces real deliverable over plan-only responses |
| Staging sandbox & apply pipeline | Functional | `.zavod.local/staging/<taskId>/attempt-<N>/` isolated sandbox for Worker edits with `manifest.json`; SHA256 hash-guarded atomic apply on user Accept; quarantine (not delete) on abandon preserves staged artefacts under `_abandoned/<taskId>-<utc>/` |
| Execution verification pipeline | Partial | SHA256 origin-hash guard and staging manifest provide drift detection; mechanical verification layer (build / lint / test via TypedToolContracts) is planned (Slice 2.1b / 2.2) |
| Autonomous runtime planning | Not yet | No model-driven planning layer yet |

**Operational invariants (learned rules, enforced in pipeline):**

- **Infrastructure failure isolation.** Provider timeouts, LLM parse errors, and any upstream unavailability must be treated separately from Worker/QC task judgment. They must not synthesize fake results, trigger QC on empty input, or destructively abandon revision progress. Staged artefacts from prior attempts must survive transient infrastructure failures.
- **Revision cycles carry forward structured feedback.** A revision attempt must receive typed notes from its predecessors within the same task: user revision intake, prior QC rationale, and any staging skip reasons. Worker must see its own history within the task; blind retries are a regression.

---

### Environment & Tooling

| Area | Status | Notes |
|------|--------|------|
| Runtime profiles & isolation | Functional | Local/container/vm/remote profiles and isolation policies exist |
| Tool routing & governance | Functional | Policy-driven execution implemented |
| External tool orchestration (IDE/CLI) | Early | Initial integration paths exist, not end-to-end |
| Environment preparation (setup) | Early | Detection exists, guided setup not implemented |
| Run / Test inside ZAVOD | Partial | Execution capabilities exist, UX layer incomplete |

---

### User Surface & UX

| Area | Status | Notes |
|------|--------|------|
| Chats mode (web renderer) | Functional | WebView2 bridge with HTML/JS surface, streaming, markdown rendering |
| Projects mode (web renderer) | Functional | WebView2 bridge with phase-aware surfaces (Discussion / Preflight / Execution / Result); three-phase visualization with composer, action bar, and pf-card; full intent → validation → execution → apply cycle live in the web surface |
| Markdown rendering | Functional | Parser + WinUI 3 renderer, two typography modes (Chats/Projects) |
| Guided user flow | Early | Intent system exists, user journey not fully shaped |
| Internal editor | Not yet | No built-in editing surface |
| External change tracking | Not yet | No realtime file monitoring yet |
| Approval UX | Partial | User-authoritative Accept / Reject / Request revision buttons on Result phase; inspection of staged diffs before apply is still manual (filesystem). Planned: built-in staged diff inspection surface so user review does not depend on external file browsing |

---

### Summary

- Core architectural layers are in place and interacting
- Chats mode and Projects mode both run on the web renderer
- Full end-to-end execution cycle is live: user intent → Lead validation → Preflight → Worker with real typed edits → sandbox staging → QC adjudication (ACCEPT / REVISE / REJECT drives phase transits) → hash-guarded apply on user Accept → commit recorded
- Worker produces real execution artefacts (not plans): typed `edits` with `write_full` / `insert_after` operations, anchor-uniqueness guard, sandboxed staging under `.zavod.local/staging/`, atomic copy to project on Accept, quarantine on abandon
- Advisory layer runs in two coexisting modes: S0 (keyword-scored) still informs Lead/Worker framing; S1–S5a typed Sage pipeline emits `SageObservation` records (semantic_gap, attention_miss) into sage_only JSONL with zero prompt pollution. Field-verified on real tasks: `attention_miss` caught a missing-file reference ~1ms before Worker LLM dispatch, independently corroborated by Worker's own rationale
- Pattern memory (`pattern_repeat`), middle-truth correlation layer, deterministic S3 rules, and in-UI Sage surface are deferred until real use reveals the concrete pain that justifies them (grokking north star: observations should decrease over time, not proliferate)
- Mechanical verification (build / lint / test) via TypedToolContracts is deferred; current verification is SHA256 origin-hash drift detection + staging manifest
- External changes detected via scan/baseline/acceptance; realtime file watching not implemented

ZAVOD at this stage can be described as:

→ a structured and working system foundation with a closed execution loop
→ end-to-end code delivery works against real project files, with typed Sage advisory already observing the pipeline (fail-open, sage-only, zero prompt pollution)
→ not yet a complete end-user product; immediate direction is making 5/5 canonical document production (Project / Direction / Roadmap / Canon / Capsule) a product capability; longer-term: middle-truth correlation layer, mechanical verification, guided user flow
