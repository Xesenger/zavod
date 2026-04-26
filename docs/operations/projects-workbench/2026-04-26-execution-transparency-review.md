# Execution Transparency Review Packet - 2026-04-26

Purpose: compact reviewer packet for the post-Scanner v2 execution work.
This is an operational note, not canon.

## Context

After Scanner v2 MVP closure, field testing moved from import honesty into the
first real work loop:

- cssDOOM: weak-model Worker attempts to add and move an FPS counter
- sm64: novice build/play requests on Windows against a Makefile/Docker-style
  decompilation project
- opencode: build/run requests against a mixed TypeScript/Rust/container-like
  repository

The useful failure pattern: ZAVOD is now safer than before, but the work loop
still feels like a black box when Lead/Worker/QC transitions are not in the
same conversational timeline.

## Implemented Since Scanner v2 Freeze

- Scanner v2 evidence artifacts and topology-aware import previews are live.
- Importer preserves nonstandard topology boundaries better than the old
  narrative path.
- Model routing is centralized in `model-routing.defaults.json` with local
  override support outside the repo.
- Chats mode streams ordinary assistant replies through OpenRouter SSE.
- Projects execution evidence is being kept in the main conversation feed
  instead of relying on the lower execution panel.
- Run/build profile execution can run scanner-discovered commands and returns
  failed commands as revision-ready evidence.
- Missing host executable diagnostics are explicit: for example `npm` missing
  on PATH should surface as a missing tool, not opaque Win32 noise.
- Worker edit DSL v0 exists for file edits: `insert_at_slot` can use
  deterministic edit slots instead of exact raw anchors.
- Revision cycles pass prior QC rationale, user revision intake, and staging
  skip reasons back to Worker.
- Infrastructure failures are separated from task judgment; provider timeout or
  parse failure must not synthesize a fake result.

## Field Results

| Target | Result | Meaning |
|---|---|---|
| cssDOOM FPS add | Worker sometimes staged partial edits; QC correctly rejected skipped anchors / missing FPS logic | QC honesty is working; weak Worker grounding remains a stress target |
| cssDOOM move follow-up | Fresh test still asked which element to move | Recent task context alone is not enough; needs deterministic continuation binding |
| sm64 build/play | `make` missing on Windows became process output / revision evidence | Better than softlock, but user-facing next steps remain raw |
| opencode build | `npm run build` failed to start when `npm` was unavailable | Now covered by missing-tool diagnostic path |
| opencode execution UI | old execution panel could appear stuck / interrupted | Product direction should keep evidence in the conversation feed |

## Verified

- Backend invariants: `All 562 backend invariants passed`
- Main build: `dotnet build zavod.csproj -p:Platform=x64 -p:Configuration=Debug --no-incremental`
- WorkspaceProbe build: `dotnet build tools\WorkspaceProbe\WorkspaceProbe.csproj -p:Configuration=Debug --no-incremental`
- Hygiene: `git diff --check` passed with CRLF warnings only

## Still Open

1. Deterministic continuation resolver
   - Bind "it / this / that element / left corner" to the active or recent task
     when exactly one target exists.
   - If multiple targets exist, ask one focused question.

2. Execution transparency surface
   - Prefer one conversation timeline for user, Lead, Worker, QC, process
     output, and result review.
   - Avoid a separate execution panel until it has clear actions and an obvious
     escape hatch.

3. Tool setup UX
   - Missing `npm`, `make`, or compiler should become a plain next-step message:
     what was attempted, what is missing, and what the user can install/configure.

4. Weak-model Worker hardening
   - Edit slots reduce anchor fragility, but do not solve intent binding,
     file-selection, or plan-only outputs by themselves.

5. UI visual verification
   - Static/build checks are green, but the latest Projects UI changes have not
     been visually verified as final UX.

## Suggested Next Slice

Build a small deterministic `TaskContinuationResolver` before Lead/Worker
handoff:

- inputs: current user message, active task, latest abandoned/revision task,
  recent task context, conversation turns
- output: resolved task brief candidate or one clarification question
- rules: bind only when exactly one recent concrete target exists; never invent
  a target; record the binding as evidence in Lead prompt / conversation

This should come before more UI polish or broader DSL expansion.
