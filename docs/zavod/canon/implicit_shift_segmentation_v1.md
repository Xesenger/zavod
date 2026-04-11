\# implicit\_shift\_segmentation\_v1.md



\## Context



The current system models work as explicit shifts that are manually opened and closed by the user.



While this provides clear structural boundaries, it introduces unnecessary responsibility on the user to decide when a shift should end.



In practice, this leads to:



\* overly long shifts

\* loss of meaningful segmentation

\* increased cognitive load



\## Addition



This document introduces an additive behavior layer:



Shift boundaries may be determined implicitly by the system,

based on checkpoint detection,

without requiring explicit user action.



This does not replace the concept of shifts,

but changes how and when they are closed.



\## Core Rule



A shift is closed automatically when a meaningful checkpoint is detected.



A checkpoint is defined as:



\* completion of a coherent segment of work, or

\* a material change in project direction



\## Dual Update Model



Two distinct update modes are introduced:



\### 1. Step-level (cheap)



Executed after each accepted step.



Effects:



\* update derived projections (ProjectHome, summaries, activity)

\* update lightweight state only



Does NOT:



\* close shift

\* build snapshot

\* update canonical documents



\### 2. Checkpoint-level (heavy)



Executed only when a checkpoint is detected.



Effects:



\* close current shift

\* build snapshot

\* assign final shift name

\* update derived documents if required

\* open new active shift internally



\## Checkpoint Detection (v1)



Initial detection is intentionally simple and deterministic.



A checkpoint is triggered when:



\* an accepted result is followed by a new intent of different nature, or

\* a decision is recorded that affects structure or direction



\## System Behavior



\* Shift lifecycle remains unchanged internally

\* All existing mechanisms (snapshots, decisions, documents) remain valid

\* The system operates shifts as an internal construct



User-facing flow becomes continuous and does not require manual shift management



\## Non-Breaking Guarantee



This addition does NOT modify:



\* shift lifecycle semantics

\* snapshot structure

\* decision model

\* canonical document definitions



It only changes the trigger conditions for shift closure



\## Determinism Requirement



Checkpoint detection must be:



\* deterministic

\* reproducible

\* based on observable state transitions



No implicit or non-traceable heuristics are allowed in v1



\## Summary



Shifts remain a core structural unit of the system.



However, their boundaries are no longer controlled manually,

but are inferred automatically from meaningful progress in the project.



This allows the system to maintain structure,

while presenting the user with a continuous working flow.



