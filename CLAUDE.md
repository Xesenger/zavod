Work in anti-confirmation mode.
Never claim success from code edits alone.

Every change must be followed by the nearest honest verification (lint, build, typecheck, test, run, or UI check).
If verification was not run — say it.
If a tool is broken or unavailable — say it and treat confidence as reduced.

Assume the change may be wrong until evidence proves otherwise.
Do not invent coverage.
Do not generalize from partial checks.

Separate clearly:
Hypothesis / Change / Verification / Evidence / Risks / Unverified

UI is not verified until actually observed.
Passing static checks ≠ runtime correctness.

Write clean, minimal, compiler-safe code.
No unused imports, no avoidable warnings.
Prefer real signals over confident narration.

Orchestrate work dynamically. Do not create subagents by default.
Spawn subagents only when explicitly requested or when the task explicitly calls for parallel delegation.

Before acting, evaluate:
* whether the task can be split into independent parts
* whether there is overlap in files or responsibilities
* whether parallelization reduces or increases merge complexity

Use a single agent for narrow, tightly coupled tasks.
Use 2–3 subagents only for read-heavy analysis with clearly separated scopes.
For write-heavy tasks, use subagents only if changes are independent and do not touch the same areas.

Each subagent must have a clear contract:
goal, scope, constraints, expected output.

Always maintain one leading agent that:
* integrates results
* checks invariants
* does not treat subagent outputs as truth without verification

Do not parallelize for speed if it increases coordination cost or risk.

Additional constraints:
- Make surgical changes only. Every modified line must directly relate to the task.
- Do not introduce abstractions or generalizations unless required for the current task.
- Before coding, identify the smallest possible fix and understand the failure mechanism.

---

Reasoning tier protocol:
- Default = medium on Opus 4.7. Low only when full blast radius fits in one line (one file, no branching, no I/O, no state).
- Auto-escalate to high/max when any trigger fires:
  * touching state transitions, persistence format, or protocol contracts
  * fixing a bug whose mechanism is not yet understood
  * multi-file change without a clear call graph in mind
  * wording a plan as "думаю, должно сработать" (guess smell — name it, go up)
- Before starting, state chosen tier + one-line reason ("medium — изолировано в bridge.js, state machine не трогаю"). User may override before work begins.
- Mid-task tripwire: if the task turns out wider than the chosen tier assumed — stop, report, re-ask for tier. Do not carry low reasoning into a grown task.

Compact reminder protocol:
- Remind about /compact at slice boundaries (cycle closed, build green, git status clean), not by context %.
- If context >40% AND a logical slice just finished — surface one short reminder. Otherwise stay silent.
- Never suggest /compact mid-slice; summary would lose the middle of the reasoning and the next session starts with a blind spot.

---

Project orientation:
- Layout & concept: README.md
- Status snapshot: ROADMAP.md
- Shared engine principle (applies to any UI/runtime work): docs/projects-web/памятка.txt
- Build (always with --no-incremental for verification): dotnet build zavod.csproj -p:Platform=x64 -p:Configuration=Debug --no-incremental