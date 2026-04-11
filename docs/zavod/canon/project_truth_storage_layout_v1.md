# ZAVOD v1 - Project Truth Storage Layout

## Layout

```text
.zavod/
  project/
    project.md
    direction.md
    roadmap.md
    canon.md
    capsule.md

  decisions/
    DEC-0001-....md

  shifts/
    YYYY-MM-DD-shift-XX/
      snapshot.md
      handoff.md

  tasks/
    TASK-0001-....json

  journal/
    trace/
      TRACE-....jsonl

  cache/
    capsule.cache.json
    entrypack.cache.json

  meta/
    project.json
```

## project/

Initial materialization note:

- canonical project document locations are stable even before every file is materialized
- before the first confirmed project base, these locations may exist only as canonical targets
- imported foreign documents and preserved user materials must not be mixed into `project/`
- `project/` may contain only ZAVOD-owned active truth documents after explicit materialization
- imported materials may support preview and clarification, but their path or filename must not grant them truth authority
- unresolved meaning of imported materials is acceptable; preserved context may remain only partially interpreted before confirmation

Initial truth order:

- `project.md` is the first materialized project truth document
- `capsule.md` may appear only as a derived summary of confirmed `project.md`
- `direction.md` and `roadmap.md` may remain absent until later confirmed direction/path work exists
- imported roadmap-like, snapshot-like, and research-like files remain preserved context, not active truth

- назначение: active truth проекта
- тип: active
- изменяемость: mutable
- владелец слоя: project truth layer

## decisions/

- назначение: история решений
- тип: history
- изменяемость: append-only / records immutable after creation
- владелец слоя: decision layer

## shifts/

- назначение: история смен
- тип: history
- изменяемость: append-only / records immutable after creation
- владелец слоя: shift layer

## tasks/

- назначение: task intent records
- тип: active
- изменяемость: mutable
- владелец слоя: task layer

## journal/trace

- назначение: execution trace history
- тип: history
- изменяемость: append-only / records immutable after creation
- владелец слоя: observer-only trace layer

## cache/

- назначение: derived cache data
- тип: derived
- изменяемость: mutable
- владелец слоя: cache layer

## meta/

- назначение: technical project entry metadata
- тип: active
- изменяемость: mutable
- владелец слоя: project metadata layer

## Canons

- canonical truth paths may exist before every truth file is materialized
- imported user materials must remain outside `project/` truth ownership

- `.zavod/` живёт внутри репозитория проекта
- `project/` содержит только active truth
- `decisions/` содержит историю решений
- `shifts/` содержит историю смен
- `cache/` не является truth и может быть удалён
- `journal/trace` является observer-only слоем
- `meta/project.json` является технической точкой входа проекта
