# Decisions

## Role
Decision storage layer.

## Contains
- decision records
- decision rationale
- decision review outcomes

## Rule
Decisions are not project truth.
Decision = trigger for project truth update.
Accepted decisions must be materialized into all affected `/project` documents.
Affected documents may include `/project/project.md`, `/project/direction.md`, `/project/roadmap.md`, and `/project/canon.md`.
`/project/capsule.md` must be updated if derived active context changed.
Accepted decision MUST be materialized into `/project` before shift completion.
Shift cannot be marked as completed if decision is not applied to `/project`.
Decisions must not replace `/project`.
Unapplied decision = invalid system state.
