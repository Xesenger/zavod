# Sandbox Contract

## Purpose
Canonical sandbox execution contract.

## Rule
This document is an internal system contract.
It is not part of `/project`.

## Scope
- sandbox is the only execution environment
- direct project changes are forbidden

## Allowed Operations
- create sandbox state
- apply changes inside sandbox
- produce diff against current project state

## Apply Rules
- execution must not write directly to active project truth
- execution result = diff between sandbox and current project state
- execution -> diff -> review -> apply -> update `/project`
- no direct mutation of `/project` is allowed outside apply step
- apply to project requires explicit accept step

## Cleanup Rules
- rejected work clears sandbox
- rolled back work clears applied sandbox state
