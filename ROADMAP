# ZAVOD

**Desktop orchestration system for real project work**

ZAVOD helps people move from idea to working result through guided collaboration with agents — without losing control over project truth, execution, or review.

---

## Overview

ZAVOD is not just a chat, not just an AI IDE, and not an autonomous agent playground.

It is a controlled work system where:

- project truth is preserved outside the LLM
- agent work is bounded and reviewable
- execution is structured
- users are guided step by step

---

## Why ZAVOD

Modern development tools often assume that the user:

- already knows the toolchain
- keeps the whole project in their head
- manually coordinates execution
- notices when context is lost
- trusts agents too early

ZAVOD changes this.

It acts as:

- project memory
- execution boundary
- review system
- guided workflow

---

## Core Principles

### Project truth first

ZAVOD keeps project understanding outside the model itself.

Important state lives in the system:

- project documents
- snapshots
- logs
- execution history
- derived runtime state

This makes work:

- recoverable
- inspectable
- stable across sessions
- less dependent on one specific model

### Diff-first workflow

Agents should not directly own or mutate the real project by default.

The intended flow is:

1. work in an isolated environment
2. propose changes
3. show the diff
4. require explicit acceptance
5. only then apply to the real repository

### Controlled execution

Agent behavior should be structured, not improvised.

ZAVOD moves toward:

- bounded execution steps
- explicit plans
- safe operations
- observable results
- review before promotion

### Guided experience

ZAVOD is designed not only for experienced developers.

The long-term direction is to help users start from intention, not from tool complexity.

The system should:

- ask only the minimum necessary questions
- prepare the environment when needed
- guide work step by step
- hide complexity until it becomes useful

---

## High-Level Flow

`Idea → Import → Understanding → Plan → Execution → Review → Accept → Result`

In practice this means:

- import a project or start a new one
- scan code and structure
- build preview understanding
- confirm canonical project truth
- generate a bounded execution plan
- execute work in a controlled environment
- review changes before acceptance
- apply accepted results safely

---

## Roadmap

### Phase 1 — Stable project truth foundation

**Status:** In progress

**Goals**

- import project materials
- scan code and structure
- build preview understanding
- confirm canonical documents
- persist logs and snapshots
- support recovery after interruption

**Expected outcomes**

- evidence bundle
- preview docs pipeline
- canonical docs pipeline
- conversation logs
- snapshot system
- resume-ready state model

---

### Phase 2 — Guided execution model

**Status:** Partial foundation exists

This phase moves ZAVOD from understanding the project to coordinating real work.

**Goals**

- introduce explicit execution flow
- keep agent work bounded
- prevent uncontrolled refactoring
- prepare the system for safer multi-step work

**Expected outcomes**

- Lead / Worker / QC model
- execution boundaries
- task framing
- diff-first workflow
- acceptance gate before repo apply

---

### Phase 3 — DSL / execution contract

**Status:** Planned

This phase introduces a minimal execution language so agent work becomes structured instead of loose and unpredictable.

Without DSL, agents tend to:

- overreach
- modify unrelated areas
- blur task boundaries
- become expensive and hard to trust

With DSL, ZAVOD can move toward:

- explicit steps
- bounded execution
- stable review
- replayable work
- testable workflows

**Possible step types**

- `read_file`
- `edit_file`
- `search`
- `run_command`
- `verify`
- `open_project_target`
- `run_project_profile`

**Expected outcomes**

- `ExecutionPlan`
- `ExecutionStep`
- `ExecutionResult`
- allowed step registry
- planner → executor contract
- rejection of unsafe or unknown steps

---

### Phase 4 — Environment preparation

**Status:** Planned

This phase covers preparing project environments at the user's request.

**Goal**

Help users install and verify the software required for a project without forcing them to do everything manually.

**Examples**

- install Qt / Qt Creator
- install .NET SDK
- install Git
- install CMake / compilers
- install project-specific tools
- verify required dependencies

**Rules**

Environment preparation must be:

- explicit
- user-approved
- logged
- recoverable

**Expected outcomes**

- environment requirements detection
- setup suggestions
- setup execution plan
- installation progress reporting
- post-install verification

This is especially important for users who should be able to say:

> I want to work on this project

and then be guided through missing setup safely.

---

### Phase 5 — Internal lightweight code editor

**Status:** Planned

ZAVOD should include its own basic source editor.

Not because it must replace full IDEs forever, but because it needs a stable internal editing surface under its own control.

**Why this matters**

A built-in editor helps with:

- quick edits without leaving ZAVOD
- diff review
- patch application
- awareness of local file changes
- reduced confusion when project state changes outside the current agent flow

**Design direction**

The first editor should be:

- simple
- reliable
- tightly integrated with scanner/state tracking
- replaceable later if needed

**Expected outcomes**

- file open / edit / save
- syntax highlighting
- diff awareness
- file change tracking
- scanner refresh integration
- external change detection

---

### Phase 6 — External change awareness

**Status:** Planned

This phase ensures that ZAVOD does not lose track of reality when files change outside its own controlled flow.

**Goal**

Detect and react to changes made:

- in external editors
- by IDEs
- by build systems
- by the user directly
- by tools running outside ZAVOD

**Expected outcomes**

- file watchers
- project invalidation and refresh signals
- scanner-aware resync
- external change notifications
- safe reload and comparison flow

This is critical if the user moves between ZAVOD and other tools during real work.

---

### Phase 7 — Run and test inside ZAVOD

**Status:** Planned

ZAVOD should be able to validate changes without forcing the user to leave the application.

**Goal**

Run project-specific build, test, and launch flows from inside ZAVOD using known project profiles.

**Examples**

- Qt projects
- CMake projects
- .NET projects
- script-driven projects

**Expected outcomes**

- project run profiles
- build / run / test commands
- execution output streaming
- result capture
- failure reporting
- integration into the review loop

---

### Phase 8 — Safer execution workspaces

**Status:** Conceptual foundation exists

This phase hardens the model where agent work happens away from the real repository.

**Goals**

- keep agent work isolated
- support throwaway workspaces
- enable diff-before-accept
- prevent accidental repo damage

**Expected outcomes**

- workspace cloning or preparation
- patch generation
- reviewed apply
- cleanup and retention policy
- acceptance-based promotion to real repo

This becomes especially important when external contractors, external runtimes, or non-default agents are involved.

---

### Phase 9 — Guided user mode

**Status:** Long-term direction

This is the ordinary-user entry point.

**Goal**

Let people start from intention, not from tools.

Example entry:

> Let's make a Sonic game

ZAVOD should then:

- ask only the minimum clarifying questions
- prepare the environment if needed
- create the first safe project structure
- guide the user one step at a time
- keep complexity hidden unless needed

**Expected outcomes**

- simple mode
- guided next-step flow
- low-jargon UX
- progressive disclosure
- safe defaults

This is where ZAVOD becomes a real hand-holding project system, not just a developer shell.

---

## Future Integration with IDEs and CLI Tools

ZAVOD is not meant to deny the existence of external tools.

It is meant to orchestrate them.

### Philosophy

External tools should become workers inside the system, not the source of truth.

ZAVOD should be able to integrate with:

- IDEs such as Qt Creator, Visual Studio, and VS Code
- CLI tools such as `git`, `cmake`, `dotnet`, compilers, test runners, and build systems
- external agent runtimes and contractor-style tools
- future third-party development environments

### Intended model

Even when external IDEs or CLI tools are used:

- ZAVOD remains the source of project truth
- execution is still tracked
- outputs are still captured
- changes are still reviewable
- acceptance still matters before promotion to the real repo

### Why this matters

People already work across multiple tools.

The goal is not to force everyone into one editor window.

The goal is to let ZAVOD coordinate real-world tools without losing context, control, or recoverability.

---

## Supporting Systems

### Logging and replay

Everything important should remain inspectable:

- actions
- diffs
- decisions
- tool usage
- run and test output
- acceptance history

### Approval model

Potentially dangerous actions should be gated by:

- policy
- role
- user approval
- trust level

### Recovery and continuity

After interruption, ZAVOD should restore:

- where work stopped
- what changed
- what was accepted
- what remains pending

---

## Near-Term Priorities

### Priority A — Bounded execution

- diff-first flow
- no direct repo mutation by agents
- minimal execution contract

### Priority B — Honest project state

- scanner refresh
- external change detection
- truth / preview separation

### Priority C — Reduced dependency on IDE context

- internal lightweight editor
- run / test inside ZAVOD
- project profile awareness

### Priority D — Real onboarding

- environment preparation
- missing tool detection
- user-approved setup flow

---

## Example Future Experience

User says:

> Let's make a Sonic game

ZAVOD responds by:

- asking only the minimum useful questions
- preparing missing tools and environment
- creating a safe starting structure
- guiding the user step by step
- keeping execution controlled and reviewable

---

## Current Status

ZAVOD is under active development.

The product direction is already clear:

- project truth should live outside the LLM
- agent work should be isolated and reviewable
- execution should be structured
- users should be guided toward real results

Core foundations are being built first so the future UX rests on something stable.

---

## Direction

ZAVOD is moving toward a model where:

- project truth lives outside the LLM
- agent work is isolated and reviewable
- execution is structured, not improvised
- users are guided step by step toward a real result
- external IDEs and CLI tools can be integrated without becoming the source of truth

---

## License

TBD
