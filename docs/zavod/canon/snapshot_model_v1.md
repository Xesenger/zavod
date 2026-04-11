# ZAVOD v1 — Snapshot Model / History

## Purpose

`Snapshot Model v1`

- описывает, как ZAVOD фиксирует исторические точки проекта при closure смены
- отделяет snapshot от active project truth и runtime state
- задаёт snapshot как immutable history artifact, а не как копию всего живого состояния проекта

---

## Definition

`Snapshot` — это неизменяемый исторический артефакт,
создаваемый при завершении смены из подтверждённых следов работы.

`Snapshot` не является простой immutable copy of `ProjectState`.

Он фиксирует:

- итог closure данной смены
- принятые результаты
- затронутые задачи
- релевантные решения
- незавершённые элементы
- контекст handoff для следующей смены

---

## Nature

- snapshot является immutable
- snapshot принадлежит истории, а не active state
- snapshot фиксирует итог смены, а не весь процесс её выполнения
- snapshot должен быть воспроизводим из closure inputs

---

## Creation

Snapshot создаётся только при shift closure.

Pipeline:

- freeze shift state
- collect accepted results
- collect touched tasks
- collect relevant decisions
- collect unresolved items
- build structured snapshot
- persist snapshot
- close shift

Snapshot создаётся после closure outcome,
но не заменяет сам closure.

---

## Sources

Snapshot собирается из структурированных владельцев truth и подтверждённых derived inputs:

- shift state
- accepted results
- touched tasks
- relevant decisions
- unresolved items
- closure summary / handoff summary
- при необходимости refs на active project documents эпохи closure

Запрещённые источники:

- raw chat history
- UI state
- “память модели”
- неподтверждённые промежуточные данные
- local cache как источник истины

---

## Content (v1)

Минимально snapshot может содержать:

- `snapshotId`
- `shiftId`
- `createdAt`
- closure outcome
- summary of what was done
- summary of what was completed
- accepted results refs
- touched task refs
- relevant decision refs
- unresolved items
- handoff / next-shift context
- optional refs на active project docs на момент closure

Snapshot может включать краткие derived summaries,
но не должен тащить внутрь полный body чужих truth-documents без необходимости.

---

## Relation to ProjectState

- `ProjectState` — active derived project view
- `Snapshot` — historical closure artifact
- snapshot может ссылаться на `ProjectState` эпохи closure,
  но не является его полной immutable копией по определению
- `ProjectState` не равен “последнему snapshot”

Если нужно восстановить историю,
используются snapshots.
Если нужно собрать текущее состояние,
используется актуальный `ProjectState`, пересобранный из truth owners.

---

## Relation to Shift

- snapshot принадлежит смене
- одна закрытая смена может породить один snapshot
- snapshot фиксирует итог именно shift-level work,
  а не абстрактное глобальное состояние без контекста

---

## Relation to Closure

- snapshot создаётся только после подтверждённого closure path
- closure определяет, какие подтверждённые результаты и unresolved items попадут в snapshot
- без closure snapshot не создаётся

Формула:

closure applies
snapshot records

---

## Relation to Trace

- snapshot не хранит raw trace
- trace не является содержимым snapshot
- trace может служить evidence/input для closure и handoff summary
- после closure trace остаётся отдельным слоем и не подменяет snapshot

Формула:

trace informs
snapshot records outcome

---

## Relation to Decisions

- если в рамках смены были приняты решения,
  snapshot может ссылаться на них
- snapshot не заменяет decision layer
- decision остаётся владельцем объяснения поворота или изменения курса
- snapshot лишь фиксирует, что данная смена затронула / породила соответствующее решение

---

## Usage

Snapshot используется для:

- просмотра истории смен
- понимания, что реально было сделано в конкретной смене
- handoff между сменами
- анализа эволюции проекта
- будущего исторического diff / timeline views

Snapshot не используется как active control surface текущей работы.

---

## Canons

- snapshot создаётся только через closure
- snapshot является immutable
- snapshot отражает итог смены, а не весь runtime process
- snapshot не является полной копией `ProjectState` по определению
- snapshot не содержит raw chat / UI / live execution state
- snapshot должен опираться только на подтверждённые источники

---

## Boundaries

- snapshot не управляет текущей работой
- snapshot не подменяет active project truth
- snapshot не является decision log
- snapshot не является trace archive
- snapshot не является runtime state container
- snapshot не должен становиться монолитным дампом всего проекта

---

## Exclusions

Исключаются:

- редактирование snapshot после создания
- частичный snapshot без closure
- snapshot как immutable copy of whole `ProjectState` by default
- хранение полного процесса выполнения
- хранение полного чата
- автоматическое создание snapshot вне closure path
