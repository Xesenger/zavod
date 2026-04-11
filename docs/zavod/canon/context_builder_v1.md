# ZAVOD v1 - Context Builder

## Purpose

`Context Builder v1`
- defines the system layer that prepares execution context
- prevents full-project and full-chat overload
- ensures Worker receives bounded, relevant, grounded context

## Definition

`Context Builder` is the derived system layer that constructs the execution input for Worker based on the current validated intent, scope, and project truth.

Context is not requested by the model.  
Context is prepared by the system.

## Core Principle

```text
context is curated, not accumulated

The system must not dump the whole chat or the whole project into execution by default.

Inputs

Context Builder may use:

validated intent
active task
scope / allowed paths
anchors
active truth
relevant decisions
current shift state
current execution state if relevant
observations as hints only
cache as acceleration only
Forbidden Inputs

Context Builder must not treat these as default execution truth:

raw full chat history
hidden model memory
unrelated archive content
UI-local state
cache as source of truth
observations as canon or decision substitutes
Output

Context Builder produces a bounded context package.

It may contain:

primary target files
related files
anchor summary
relevant canon excerpts
relevant decisions
execution constraints
scope limits
known risks or local observations
short current task state
Construction Goal

The output must be:

sufficient for safe execution
small enough to stay bounded
explicit enough to be auditable
grounded enough to reduce drift
Probing

Probing is the fast discovery step inside Context Builder.

Its purpose is to:

inspect local structure
inspect naming
inspect usage
inspect immediate dependencies
inspect likely touch-points
discover anchors before execution

Probing is:

bounded by scope
pre-execution
system-driven
fast and selective

Probing is not:

full-project loading
execution itself
hidden model intuition
uncontrolled browsing
Construction Flow
validated intent
→ scope seed
→ probing
→ primary target selection
→ related-file expansion
→ anchor selection
→ truth selection
→ bounded context package
Primary Target Selection

Context Builder must first identify the minimum primary targets.

Examples:

the file to edit
the symbol to inspect
the config to verify
the result artifact to review

Primary targets are preferred over broad context dumps.

Related File Expansion

After primary targets are selected, Context Builder may expand to related files only when justified.

Valid reasons include:

direct usage
direct dependency
required interface / contract
required truth boundary
verification relevance

Invalid reasons include:

“maybe useful”
full-folder loading by habit
whole-project safety blanket
Truth Selection

Context Builder may include only truth that is relevant to the current task.

Examples:

canon rule touched by this task
current direction constraint
decision affecting this area
scope prohibition relevant to the edit

Truth inclusion must remain selective.

Scope Rule

Context Builder must remain bounded by current task scope.

That means:

no automatic expansion to the whole project
no unrelated neighboring systems by default
no cross-layer drift without explicit basis
Relation to Read Before Write

Read Before Write Gate uses the prepared context package, but does not replace it.

Context Builder prepares working context
Read Before Write Gate blocks mutation until minimum grounding is actually satisfied
Relation to Prompt Assembly

Prompt Assembly consumes the output of Context Builder.

Context Builder decides what bounded context enters execution
Prompt Assembly decides how that context is structured in the final request
Relation to Anchors

Context Builder may:

discover anchors
filter anchors
package anchors

It does not redefine anchor truth.
It only prepares anchored execution input.

Relation to Observations

Observations may be included only as hints.

They:

may warn about quirks
may warn about risks
may help avoid repeated mistakes

They must not:

override canon
override decisions
become hidden truth
Relation to Cache

Cache may accelerate context preparation, but cache does not define reality.

If cache is stale:

it must be discarded or rebuilt
it must not silently override truth
Minimal Safe Package

A safe context package should normally include:

current task basis
scope / allowed paths
primary target
relevant anchors
minimal related context
relevant truth constraints
Rules
context is prepared by the system, not by the model
context must be bounded
context must stay task-relevant
context must not default to full chat or full project
probing belongs to context construction
cache and observations are helpers only
Canons
Context Builder is a derived layer
context is curated, not accumulated
probing is part of context preparation
execution receives bounded working context, not raw project mass
truth remains above context