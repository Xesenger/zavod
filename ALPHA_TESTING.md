# ZAVOD Alpha Testing

ZAVOD is in a very early alpha state.

This is not a polished product launch. It is a small field test for people who
like trying unfinished developer tools, importing real repositories, and sending
blunt feedback about what is useful, confusing, or wrong.

## What to Test

The most useful alpha test is a short project-import pass:

1. Start ZAVOD.
2. Import a local repository.
3. Open the project home surface.
4. Check the truth documents block.
5. Read the generated preview docs:
   - `project.md`
   - `direction.md`
   - `roadmap.md`
   - `canon.md`
   - `capsule.md`
6. Promote or reject one preview document.
7. Report what changed on screen and whether the generated text was useful.

## What Works Today

- Repository import from local folders.
- Evidence bundle generation from scanner/importer data.
- 5/5 preview document generation for Project, Direction, Roadmap, Canon, and Capsule.
- Projects Home truth-doc status display.
- Per-document preview promotion into canonical truth.
- Per-document preview rejection with journal attribution.
- Layer C decision records for canonical promotions.
- Layer D journal events for promotion and preview rejection.
- End-to-end role-based work cycle foundation: Lead, Worker, QC, staging, review, and apply.

## What Is Still Rough

- Generated project summaries can be too cautious or too coarse.
- Large monorepos and multi-language repositories can confuse entry-point ranking.
- `edit before promote` and `author from scratch` flows are not complete yet.
- Runtime 5/5 state awareness is still being wired into later work-cycle context.
- No realtime file watcher yet; external edits require rescan/refresh paths.
- UI has sharp edges and incomplete onboarding.
- Windows/.NET/WinUI setup may still produce environment-specific friction.

## What Not to Trust Yet

Preview docs are not truth.

They are candidate documents derived from scanner/importer evidence. Treat them
as review material. Canonical truth starts only after an explicit contributor
promotion or authoring action.

## Good Feedback

Useful feedback includes:

- repository type and size
- what ZAVOD got right
- what ZAVOD invented or overstated
- confusing UI moments
- missing information in generated docs
- whether the capsule helped you orient in the project
- screenshots of the truth-docs block before and after promote/reject

## Suggested Test Repositories

Start with a small or medium repository you understand well.

For stress testing:

- multi-language repositories test project/container ambiguity
- Rust Cargo workspaces test entry-point and module ranking
- documentation-heavy repos test whether ZAVOD over-trusts prose
- generated-code repos test whether noisy files are filtered correctly

## Current Alpha Goal

The current goal is not to prove that ZAVOD is always right.

The goal is to find where it is wrong, whether it says "unknown" honestly, and
whether its project-memory workflow feels useful enough to keep improving.
