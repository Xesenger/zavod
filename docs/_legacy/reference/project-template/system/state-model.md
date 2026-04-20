# State Model

## Purpose
Canonical execution state contract.

## Rule
This document is an internal system contract.
It is not part of `/project`.
State is owned by shift lifecycle.
State transitions occur only within shift execution.
State is not stored in UI.
State is not stored in `/project`.
`/project` reflects result, not lifecycle.

## States
- prepared
- running
- stopping
- stopped
- result_ready
- accepted
- rejected
- rolled_back

## Transitions
- prepared -> running
- prepared -> stopped
- running -> stopping
- running -> result_ready
- stopping -> stopped
- result_ready -> accepted
- result_ready -> rejected
- accepted -> rolled_back

## Entry Conditions
- `prepared`: task intent is defined and execution has not started
- `running`: sandbox execution has started
- `result_ready`: execution finished and reviewable diff exists
- `accepted`: review approved apply and apply finished
- `rejected`: review rejected result and apply did not happen
- `rolled_back`: applied result was reverted

## Exit Conditions
- `prepared`: exit on execution start or stop before execution
- `running`: exit on stop request or reviewable result creation
- `result_ready`: exit on accept or reject
- `accepted`: exit on rollback only
- `rejected`: terminal unless a new execution starts from a new shift cycle
- `rolled_back`: terminal
