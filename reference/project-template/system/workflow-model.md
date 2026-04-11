# Workflow Model

## Purpose
Canonical workflow phase contract.

## Rule
This document is an internal system contract.
It is not part of `/project`.
Workflow state is process state.
Workflow state is not execution state.

## States
- chat
- validation
- execution_running
- review
- completed
- cancelled

## Transitions
- chat -> validation
- validation -> chat
- validation -> execution_running
- execution_running -> review
- execution_running -> cancelled
- review -> completed
- review -> chat
- review -> cancelled

## Ownership
- workflow state may drive UI
- workflow state must not replace execution state
- `/project` reflects accepted result only
- workflow must not enter `completed` until apply is finished
- required `/project` updates must be materialized before completion
