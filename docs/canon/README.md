# ZAVOD — Canon Layer

Status: locked

## Purpose

This folder contains system-truth documents for ZAVOD.

These files define:
- system laws
- lifecycle rules
- truth boundaries
- execution boundaries
- persistence and resume boundaries
- prompting and grounding system layers

Canon truth lives here.

---

## Rule

```
canon defines system laws
roles define role behavior

docs/canon/   = system truth
docs/roles/   = role truth

These layers must not compete with each other.
```

---

## Canon Areas

### 1. Project Truth and Storage

`project_architecture_layers_v1.md` *(root architectural canon)*
`project_truth_storage_layout_v1.md`
`project_truth_documents_v1.md`
`project_state_model_v1.md`
`project_state_persistence_v1.md`
`project_meta_contract_v1.md`
`project_state_builder_v1.md`

### 2. Intent / Interaction Boundary

`intent_system_v1.md`
`interaction_validation_flow_v1.md`
`bootstrap_flow_v1.md`
`cold_start_behavior_v1.md`

### 3. Shift / Task / Snapshot Lifecycle

`first_shift_creation_v1.md`
`shift_lifecycle_v1.md`
`task_model_v1.md`
`shift_closure_review_v1.md`
`snapshot_model_v1.md`
`current_shift_trace_v1.md`
`implicit_shift_segmentation_v1.md`

### 4. Execution Layer

`execution_loop_work_cycle_v1.md`
`read_before_write_v1.md`
`worker_execution_strategy_v1.md`
`execution_verification_pipeline_v1.md` *(planned — not yet implemented)*
`runtime_execution_extensions_v1.md`

### 5. Resume Layer

`resume_contract_v1.md`

### 6. Prompting / Grounding Layer

`prompt_assembly_v1.md`
`anchor_system_v1.md`
`context_builder_v1.md`

### 7. Meta Support Layers

`observation_layer_v1.md`
`project_state_builder_v1.md`

---

## Boundaries

**Canon is above UI.**
UI may project canon truth and reflect runtime state. UI may not invent truth or redefine project laws.

**Canon is above runtime.**
Runtime may execute, verify, signal, and optimize. Runtime may not redefine truth or bypass closure.

**Derived layers remain below truth.**
Cache, observations, tool memory, runtime signals, and entry packs may support the system but must not replace truth.

---

## Merge Rule

Canon files must be merged by system concept, not by filename alone.

If two canon files compete for the same truth, they must be merged or one must be removed.

---

## Promotion Rule

New material may enter canon only if:
- it defines a real system boundary or law
- it does not duplicate existing canon
- it has a clear place in the layer map
- it does not compete with roles/

---

## Canons

- `docs/canon/` is the system-truth layer
- canon defines laws, boundaries, and system structure
- roles define role behavior in `docs/roles/`
- UI, runtime, and derived layers remain below canon truth
- canon must stay explicit, layered, and non-competing
