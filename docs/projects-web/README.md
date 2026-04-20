# Projects Web Migration

Documents and prototypes for migrating the Projects mode UI to a web renderer.

## Documents (read in order)

1. [01 — Projects UI Model](01-projects-ui-model-ru.md) — 3-level structure (List / Home / Work Cycle) and Work Cycle surfaces (Discussion / Execution / Result)
2. [02 — Work Cycle Core Binding](02-project-work-cycle-core-binding-ru.md) — How UI binds to StepPhaseState/StepPhaseProjection; typed phase visibility rules
3. [03 — Migration Plan](03-projects-web-migration-plan-ru.md) — 4-pass migration strategy with honesty passes

## HTML Prototypes

- `04-project-work-cycle-web-template.html` — Work Cycle surface template
- `05-project-work-cycle-web-template-clean.html` — Cleaned variant
- `html_lab_playground.html` — Rendering experiments
- `zavod_ui_prototype.html` — Full UI prototype

## Status

Active migration work. Documents reflect current code in `UI/Modes/Projects/`.
