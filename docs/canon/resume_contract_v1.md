# ZAVOD v1 — Resume Contract

## Purpose

`Resume Contract v1`
- defines what the system may honestly restore after restart
- establishes the strict conditions for canonical resume
- separates truthful restore from fake continuity

---

## Core Principle

Resume in ZAVOD is truth-based, not memory-based.

The system does not try to reconstruct what "probably was happening".
It loads persisted truth and validates it strictly.

Formula:

```
resume = load persisted truth + validate all refs match
```

---

## Allowed Resume Sources

Resume may rely only on:

- `ProjectState` (persisted)
- `ActiveShiftId` from ProjectState
- `ActiveTaskId` from ProjectState
- `ShiftState` loaded from shift storage
- `TaskState` from the loaded ShiftState

No other sources are canonical resume inputs.

---

## Conditions for Canonical Resume

All of the following must be true:

1. `ProjectState.ActiveShiftId` is present and non-empty
2. `ProjectState.ActiveTaskId` is present and non-empty
3. Loaded `ShiftState.ShiftId` matches `ActiveShiftId`
4. Loaded `ShiftState.Status` is `Active`
5. `ShiftState.CurrentTaskId` matches `ActiveTaskId`
6. `TaskState` with `ActiveTaskId` exists inside the loaded ShiftState

If any condition fails, resume throws `InvalidOperationException`.
There is no degraded fallback — the system surfaces the inconsistency.

---

## Resume Cases

### Case 1 — No Active Shift

If `ActiveShiftId` is null, the system enters idle / bootstrap-capable state.
Cold start without active shift is valid.

### Case 2 — Active Shift Exists

If `ActiveShiftId` is set, the system attempts canonical resume via `ActiveShiftResume.Resume()`.
All conditions above must be satisfied.

---

## Forbidden Sources

Resume must not use:

- UI-local memory
- model memory
- guessed last state
- runtime-only temporary state

---

## Honest Behavior

Resume reconstructs entry from persisted truth only.
It does not pretend to remember.

Correct behavior:
- Load ProjectState
- Validate active refs
- Load and cross-check ShiftState + TaskState
- Return `ActiveShiftResumeResult` with the verified state triple

---

## Canons

- resume is truth-based, not memory-based
- all active refs must match across ProjectState and ShiftState
- inconsistent persisted state surfaces as an error, not a silent fallback
- UI-local memory is not a valid resume source
