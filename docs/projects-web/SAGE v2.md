ZAVOD — SAGE (COMPACT SPEC, v2.1 hardened)

ROLE
-----
Sage = backend semantic integrity layer.
Ensures system does not lose meaning, continuity, or invariant alignment.

Sage DOES:
- detect invariant violations
- detect intent ↔ execution mismatch
- detect structural/boundary leaks
- detect repeated failure patterns
- detect semantic gaps ("holes")
- detect attention issues
- enforce trust hierarchy

Sage DOES NOT:
- make decisions
- generate plans
- execute actions
- replace Lead / Worker / QC
- act as chat participant by default
- write observations into conversation JSONL or any role's prompt


CORE PRINCIPLES
---------------
1. Continuity:
   No unresolved contradictions ("no semantic holes")

2. Attention:
   Adjust attention, not decisions

3. Trust hierarchy:
   canon > current truth > accepted history > discussion > raw logs

4. Non-autonomy:
   advisory only, explicit invocation for suggestions

5. Convergence:
   system must stabilize across iterations

6. Isolation (hardened in v2.1):
   Sage observations live in a dedicated channel. They MUST NOT leak into
   Lead/Worker/QC prompts as free-text hints. If a role needs a Sage signal,
   it consumes a typed field, not a string note. This prevents the
   pollution loop where a prior observation gets re-ingested by the next
   LLM turn and amplified into new clarification questions.


POLLUTION CONTRACT (NEW IN v2.1)
--------------------------------
Observed failure mode (pre-Sage): Worker refusals and QC REVISE rationales
were keyword-scored and fed back as ADVISORY NOTES / RECENT CONVERSATION
to Lead, which then parroted downstream concerns at the user, forcing
clarification loops.

Rules:
- Sage output goes to a structured channel (e.g. .zavod/sage/observations.jsonl
  or a state field), never to conversation.jsonl.
- Role prompts receive ONLY: (a) typed observations explicitly selected by
  the pipeline stage, (b) flattened into role-specific fields, (c) with
  severity gate (hint-level dropped by default).
- Raw conversation JSONL is NEVER a Sage input source for framing advisory;
  framing advisory is built strictly from the Middle Truth Layer (S4).
- Sage reads raw JSONL only for pattern detection (S5), never for
  per-turn hinting.


OBSERVATION MODEL
-----------------
SageObservation:
- Type
- Severity (hint | warning)
- Message
- Stage (intent | plan | execution | result)
- AffectedLayer (intent | plan | execution | result | truth)
- Channel (sage_only | role_input)   — default sage_only
- EvidenceRef?
- AnchorRef?
- ExpiresAt?                          — used by pattern-sourced observations

Types — MVP set (S1, ship first):
- semantic_gap          — intent references a concept with no anchor
- pattern_repeat        — structurally identical failure observed before
- attention_miss        — critical anchor not in pack

Types — Extended set (S3+, add by evidence):
- invariant_violation
- intent_mismatch
- structural_risk
- attention_overload
- trust_violation
- unresolved_conflict
- chain_break_risk
- noise_overload

Starting with 3 types keeps false-positive surface small and lets each type
earn expansion by demonstrated signal.


PIPELINE HOOKS
--------------
after_intent:
- validate intent integrity
- detect semantic gaps early

before_execution:
- validate plan vs intent
- check invariants + boundaries
- check missing anchors

during_execution (lightweight):
- detect drift
- detect pattern repeats
- detect chain break risk

before_result:
- validate result vs intent
- detect false success
- detect unresolved conflicts


FOUNDATION — EXECUTION ORDER (v2.1 reordered)
---------------------------------------------
S1 — Typed Observation Layer:
- implement SageObservation record (incl. Channel + ExpiresAt)
- define MVP types + severity + stage

S2 — Pipeline Integration:
- inject Sage as silent observer at the 4 hook points
- Sage observations routed to sage channel ONLY (no conversation writes)

S4 (BEFORE S3) — Middle Truth Layer:
- validated_intent
- active_constraints
- accepted_paths           (task → applied modifications)
- rejected_paths           (task → explicit reject with reason)
- accepted_outcomes, split by OutcomeKind:
    * applied_with_modifications    (real edits landed)
    * accepted_without_execution    (formally accepted refusal / no-op)
    * reverted
- Goal: S3 rules run on structured truth, not raw conversation noise

S3 — Deterministic Rules:
- invariant enforcement
- intent vs execution checks
- structural checks
- history-based pattern detection (read from S4, not raw JSONL)

Rationale for reorder: rules built on raw logs are rules on noise. A small
middle-truth layer costs 1 slice and turns every subsequent rule into a
stable check against a versioned record.


PATTERN MEMORY (S5) — SAFETY RULES
----------------------------------
Store compressed patterns:
- boundary leaks
- duplicated logic
- unstable paths
- reverted directions

Goal: enable "this already failed" signals

Safety rules (hardened):
- Patterns are bound to an invariant id, NOT to a text snippet. A stored
  pattern reads "invariant_X violated twice in last N tasks", never
  "last time LLM said phrase Y".
- Every pattern carries ExpiresAt (default: 20 tasks or 30 days).
- Patterns are versioned against codebase revision; a pattern from before
  a structural refactor is auto-demoted.
- Pattern retrieval requires scope match (same subsystem / module), not
  keyword overlap with the current user message.
- A pattern older than its ExpiresAt is GC'd, not merely down-weighted.
Without these, pattern memory becomes confirmation-bias amplifier:
a prior refusal coaches the next role into the same refusal.


ROLE BOUNDARY — SAGE vs QC
--------------------------
QC: reviews a single concrete Worker result against stated acceptance
criteria. Output: ACCEPT | REVISE | REJECT on THIS result.

Sage: observes the system's semantic state across stages and history.
Output: typed observations about continuity, patterns, and attention.
Never adjudicates a specific result.

Worked example:
- Worker produced FPS counter edits.
- QC: "ACCEPT — modifications match acceptance criteria."
- Sage at same moment: "pattern_repeat (hint) — 3rd FPS-related task in this
  shift; prior two accepted without execution; consider invariant check on
  HUD module ownership before next similar intent."

Both observations are valid and non-overlapping. QC rules on the artifact;
Sage rules on the trajectory. Pipeline never asks Sage to override QC or
vice versa.


STAGE-AWARE SAGE (S6)
---------------------
Contextualize observations by stage:
- intent stage → understanding errors
- planning stage → structural risks
- execution stage → drift
- result stage → false alignment


LLM AUGMENTATION (S7, LATER)
----------------------------
Input:
- structured truth (not raw text)
- stage
- current observations
- diffs / risks

Output (STRICT):
- type
- severity
- message
- evidence

LLM role:
- enhancer, not source of truth
- hard latency budget (must not double cycle time)
- not a second QC — enhancer emits only Sage-typed observations,
  never ACCEPT/REVISE/REJECT verdicts


ATTENTION INTEGRATION
---------------------
Inputs:
- anchor pack
- anchor priorities

Detect:
- missing critical anchors → attention_miss
- irrelevant dominance → attention_overload

Tie-in to existing Worker anchor pack (Slice B in current roadmap):
keyword-scored snippet picker is the 0-gen attention heuristic. Sage
replaces the heuristic with a principled score over anchor priorities
once Middle Truth records which paths were truly critical for accepted
tasks.

Goal: stabilize decision context.


TRUTH INPUT LAYERS (for Sage itself)
------------------------------------
1. Canon:
   project.md, direction.md, roadmap.md, canon.md, capsule.md, invariants

2. Current Reality:
   task/shift state, execution state, QC/apply state

3. Structured History (Middle Truth — S4):
   accepted_paths / rejected_paths / accepted_outcomes (with OutcomeKind)

4. Pattern Memory (S5, invariant-scoped with expiry):
   failure patterns, regressions, anti-patterns

5. Raw History (diagnostics only, never framing source):
   JSONL conversations


ANTI-NOISE POLICY
-----------------
- do not rely on raw JSONL as primary source
- ignore outdated states
- prefer compressed structured memory
- respect Isolation principle: observations do not enter role prompts
  as free text

Signal:
- noise_overload


SUCCESS CRITERIA
----------------
Early (S1 → S2 → S4 → S3):
- short, correct warnings influence decisions
- zero observed pollution of role prompts with Sage text
- accepted_outcomes correctly split (applied vs accepted-without-execution)

Mid (S5):
- system remembers failures by invariant, not by phrase
- fewer repeated mistakes
- pattern retrieval precision measurable (matched scope, not keyword)

Late (S6–S7):
- fewer observations needed
- shorter messages
- stable behavior
- LLM augmentation adds < 500ms per cycle

Final:
- decreasing semantic gaps
- decreasing intent mismatches
- convergence toward consistent system state
- role prompts stay clean under long history (anti-pollution holds)
