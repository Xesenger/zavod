# ZAVOD Docs

This folder separates current project knowledge by authority level.

## Active Docs

| Folder | Purpose | Authority |
|---|---|---|
| `canon/` | System truth: lifecycle, storage, execution, state, prompt, and memory boundaries | Highest documentation authority |
| `roles/` | Role truth for Shift Lead, Worker, QC, and specialist roles | Role behavior authority |
| `plans/` | Active or recent production plans and debt maps | Planning, not truth |
| `operations/` | Current operational notes for implementation work | Guidance, not truth |
| `ui-shell-demo/` | Isolated UI shell demo work | Demo surface, not product truth |

## Archive

| Folder | Purpose |
|---|---|
| `_legacy/` | Historical plans, migrations, references, and closed audit material |

## Rules

- Canon files define system laws.
- Role files define role behavior.
- Plans and operations notes may guide work, but they do not override canon or roles.
- Legacy files are preserved for archaeology and may mention stale paths or old architecture.
- Generated or one-off audit dumps should not live in the active docs root.

## Current Scanner / Import Docs

- `plans/scanner-v2-followup-v1.md`
- `plans/5-of-5-production-plan-v1.md`
- `plans/integration-debt-v1.md`
