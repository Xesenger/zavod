# Review Contract

## Purpose
Canonical review gate contract.

## Rule
This document is an internal system contract.
It is not part of `/project`.

## Inputs
- diff
- report
- review decision

## Review Surface
- review happens after result_ready
- review inspects sandbox result before apply
- execution -> diff -> review -> apply -> update `/project`

## Decision Actions
- accept
- reject
- request revision

## Post-Review Rules
- review is required before apply
- if `Requires Review = yes`, apply without review is forbidden
- no direct mutation of `/project` is allowed outside apply step
- accepted review permits apply
- accepted decision MUST be materialized into `/project` before shift completion
- shift cannot be marked as completed if decision is not applied to `/project`
- rejected review forbids apply
