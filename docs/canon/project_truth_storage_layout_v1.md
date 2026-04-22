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
    SHIFT-NNN.json

  journal/
    trace/
      YYYY-MM-DD.jsonl

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

- назначение: история смен с вложенной историей задач (task records are embedded in the shift JSON, not stored in a separate `tasks/` directory)
- формат: `shifts/SHIFT-NNN.json` — one JSON per shift, containing `ShiftId`, `Goal`, `Status`, `CurrentTaskId`, and `Tasks` array
- тип: mixed (active while shift is open, historical once closed)
- изменяемость: mutable while open; append-only semantics for closed shifts
- владелец слоя: shift layer
- boundary with historical events: detailed lifecycle events live in `journal/trace/` (see `project_journal_v1.md`); shift JSON itself is a state snapshot, not an event log

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
- писатель: **гибрид**. Identity fields (`projectId`, `projectName`, `layoutVersion`, `entryMode`) are set at import or migration and treated as human-stable identity. Linkage fields (`activeShiftId`, `activeTaskId`) are runtime-written as a byproduct of shift/task lifecycle and do not require contributor promotion.
- invariant: `meta/project.json` must never carry semantic project truth (see `project_meta_contract_v1.md`); its scope is identity + runtime operational linkage only

## Canons

- canonical truth paths may exist before every truth file is materialized
- imported user materials must remain outside `project/` truth ownership

- `.zavod/` живёт внутри репозитория проекта
- `project/` содержит только active truth
- `decisions/` содержит историю решений
- `shifts/` содержит историю смен; задачи вложены в JSON смены, отдельной `tasks/` папки нет
- `cache/` не является truth и может быть удалён
- `journal/trace` является observer-only слоем
- `meta/project.json` является технической точкой входа проекта; identity fields контрибьютор-стабильны, linkage fields пишутся runtime'ом
