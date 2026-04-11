# ZAVOD v1 - Anchor System

## Purpose

`Anchor System v1`
- defines explicit grounding references for execution
- reduces hallucination and drift
- binds prompts and execution to real project entities
- provides the anti-assumption layer for Context Builder, Prompt Assembly, execution, and verification

## Definition

`Anchor` is an explicit reference to a confirmed project fact.

Anchor is not:
- chat memory
- model assumption
- loose interpretation
- unstated background knowledge
- “probably this file / rule / symbol”

Anchor is a system-level grounding unit.

## Core Principle

```text
if a critical fact is not anchored,
it must not be treated as guaranteed truth

Why Anchors Exist

Anchors replace:

“the model probably remembers”
hidden assumptions
vague project references
unstable chat-derived interpretations

Anchors make execution:

explicit
bounded
auditable
reality-linked
Anchor Types
1. Code Anchor

References a real code entity.

Examples:

file path
class
method
function
property
symbol
signature

Example:

File: src/ui/IntentButton.xaml
Symbol: IntentButton
Property: IntentConfidence
2. Task Anchor

References the current executable task basis.

Examples:

validated intent
acceptance criteria
scope
exclusions
required output
3. Truth Anchor

References active project truth.

Examples:

canon rule
active direction
current roadmap constraint
project rule that must not be violated
4. Decision Anchor

References an accepted project decision.

Examples:

structural decision
architecture decision
naming decision
product-direction decision
5. Artifact Anchor

References execution artifacts.

Examples:

diff
result package
snapshot
review artifact
produced output
Anchor Resolution

Critical anchors must be resolved before execution where required.

That means:

the anchor must point to a real file / fact / decision / task basis
the system must not pretend an anchor exists when it was not resolved
unresolved critical anchors must block safe handoff
Resolution Rule
no fake anchors

If a file, symbol, rule, or decision cannot be confirmed,
it must not be passed as anchored truth.

Critical vs Non-Critical Anchors
Critical anchors

Missing critical anchors must block safe execution.

Typical examples:

current validated task basis
scope basis
primary file / symbol when mutation is concrete
canon or decision anchor when the task directly touches a project rule
Non-critical anchors

Missing non-critical anchors may allow execution to continue if grounding is still sufficient.

Typical examples:

secondary nearby symbol
optional supporting artifact
optional contextual reference
Anchor Usage

Anchors may be used in:

Context Builder
Prompt Assembly
Read Before Write Gate
Worker execution
Execution Verification Pipeline
QC review
Resume / restore context
Result explanation
Relation to Prompt Assembly

Prompt Assembly consumes anchors.

Prompt Assembly may place anchors inside:

Shift / Context Package
Task Block

But Prompt Assembly does not define anchor truth by itself.

Relation to Context Builder

Context Builder may:

discover anchors
filter anchors
package anchors

Typical flow:

validated intent
→ probing
→ anchor discovery
→ anchor selection
→ context package
→ prompt assembly

Context Builder prepares anchors.
Anchor System defines what counts as an anchor.

Relation to Read Before Write

Read Before Write Gate depends on anchors for safe mutation.

Examples:

task anchor confirms why the mutation exists
code anchor confirms where the mutation belongs
truth anchor confirms what must not be broken

Without anchor basis, write may be unsafe even if some files were read.

Relation to Execution

Execution must prefer anchored work over assumed work.

Examples:

anchored file path instead of “that UI file”
anchored symbol instead of “the button logic somewhere”
anchored canon rule instead of remembered wording
Relation to Verification

Verification should check result against anchors where applicable.

Examples:

task anchor → was requested outcome actually produced?
scope anchor → did execution stay inside bounds?
truth anchor → did the result violate project rules?
decision anchor → did the result break an accepted project choice?
Relation to Truth

Anchors do not create truth.

They reference truth or confirmed execution facts.

Anchor System:

does not mutate project truth
does not replace canon
does not replace decisions
does not replace validated intent

It only binds execution to confirmed reality.

Missing Anchor Behavior

If a non-critical anchor is missing:

execution may continue carefully if grounding is still sufficient

If a critical anchor is missing:

execution must stop
system must request clarification, probing, or resolution
execution must not continue on assumption alone
Minimal Anchor Set for Safe Execution

A safe execution handoff should normally include:

at least one task anchor
at least one scope basis
relevant code anchors when concrete mutation is involved
relevant truth anchors if the task touches project rules
relevant decision anchors if accepted decisions constrain this area
Rules
anchors must be explicit
anchors must correspond to real project entities
anchors must be resolved before execution where required
critical missing anchors must block safe execution
anchors are grounding references, not interpretation
prompt wording alone is not anchor truth
Canons
anchors are the anti-hallucination layer
anchors bind execution to reality
execution must prefer anchored facts over remembered impressions
unresolved critical anchors must block safe handoff
anchors reference truth; they do not create truth
Anchor System supports Context Builder, Prompt Assembly, Read Before Write, execution, and verification