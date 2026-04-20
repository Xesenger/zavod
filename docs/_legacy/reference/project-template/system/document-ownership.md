# Document Ownership

## Purpose
Canonical document ownership contract.

## Rule
This document is an internal system contract.
It is not part of `/project`.

## Ownership Layers
- `/project` = only source of active truth
- `/decisions` = decision records
- `/tasks` = task intent records
- `/shifts` = shift execution records
- `/artifacts` = non-truth outputs
- `/archive` = inactive records
- `/system` = internal contracts

## Source of Truth Rules
- only `/project` defines active truth
- other layers must not override `/project`

## Write Boundaries
- decisions may trigger `/project` updates
- tasks must not define project truth
- shifts must not define project truth
- artifacts must not define project truth
- archive must not define active state

## Conflict Rules
- if a non-project document conflicts with `/project`, `/project` wins
- internal system contracts define behavior, not project truth
