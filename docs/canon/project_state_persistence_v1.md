# ZAVOD v1 — Project State / Persistence

## Purpose

`Project State / Persistence v1`

- определяет, как ZAVOD хранит project-level truth и служебную meta
- фиксирует, что `ProjectState` не является монолитным документом проекта
- задаёт границу между persisted truth, derived state и runtime

---

## Definition

`ProjectState` — это типизированное актуальное представление проекта,
собираемое системой из persisted project truth и minimal meta.

`ProjectState` не является отдельной "главной книгой проекта".
Он является собранным active view поверх:

- `.zavod/meta/project.json`
- active truth documents
- truth pointers / references
- active shift linkage

---

## Nature

- `ProjectState` — derived typed state, а не самостоятельный содержательный документ
- `ProjectState` собирается детерминированно
- `ProjectState` не хранит trace / history / raw execution data
- потеря `ProjectState` как runtime object не должна означать потерю project truth

---

## Persisted Truth Sources

Persisted truth проекта живёт не в одном объекте `ProjectState`, а в нескольких владельцах:

- `.zavod/meta/project.json`
- `.zavod/project/project.md`
- `.zavod/project/direction.md`
- `.zavod/project/roadmap.md`
- `.zavod/project/canon.md`
- `.zavod/decisions/*`
- `.zavod/shifts/*`

`ProjectState` читает эти источники, но не заменяет их.

---

## Minimal Meta Contract

Persisted meta должна содержать только то, что нужно для сборки active project view.

Минимально допустимые поля:

- `projectId`
- `activeShiftId`
- `activeTaskId` (если применимо)
- pointers / refs на active project documents
- системные поля версии формата

Допустимы дополнительные служебные refs,
но не содержательные дубли `direction`, `roadmap`, `canon` и других project docs.

---

## What ProjectState Contains

`ProjectState` может содержать собранные typed fields, например:

- `ProjectId`
- `ActiveShiftId`
- `ActiveTaskId`
- `ProjectDocumentRef`
- `DirectionDocumentRef`
- `RoadmapDocumentRef`
- `CanonDocumentRef`
- `CapsuleDocumentRef`
- статус доступности / валидности required truth inputs

Допустимо наличие derived summaries,
но только как runtime/read model,
не как новый источник истины.

---

## What ProjectState Does Not Contain

`ProjectState` не должен содержать как собственное persisted body:

- полный текст `direction`
- полный текст `roadmap`
- полный текст `canon`
- trace
- raw execution data
- UI state
- chat history
- snapshot history

Если это нужно системе,
оно хранится у своего прямого владельца,
а не дублируется в `ProjectState`.

---

## Truth Ownership

Правило:

- `project.md` владеет текущей идентичностью проекта
- `direction.md` владеет текущим направлением
- `roadmap.md` владеет текущим маршрутом
- `canon.md` владеет системными и архитектурными правилами
- `decisions/*` владеют зафиксированными поворотами и объяснением изменений
- `shifts/*` владеют историей выполнения смен
- `.zavod/meta/project.json` владеет минимальной активной meta и linkage

`ProjectState` ничем из этого не владеет содержательно.
Он только собирает текущее состояние из владельцев truth.

---

## Update Mechanism

`ProjectState` пересобирается после truth-changing событий,
а не редактируется как самостоятельный смысловой объект.

Источники обновления:

- accepted decision
- shift closure
- изменение active linkage
- изменение active document refs
- migration / repair meta

Правило:

- сначала обновляется владелец truth
- затем обновляется minimal meta / refs
- затем пересобирается `ProjectState`

`ProjectState` не является местом прямого смыслового редактирования.

---

## Relation to Shift

- active shift существует поверх project truth
- активная смена может менять runtime state, но не должна напрямую переписывать `ProjectState` как truth-body
- shift closure может обновить owners of truth и после этого привести к пересборке `ProjectState`

---

## Relation to Snapshot

`snapshot` не является просто immutable copy of `ProjectState`.

`snapshot` — это shift-level итог,
собранный из:

- accepted results
- touched tasks
- relevant decisions
- unresolved items
- closure outcome

`snapshot` может ссылаться на `ProjectState` эпохи closure,
но не заменяет и не копирует его полностью как основной смысловой объект.

---

## Relation to Trace

- trace не хранится в `ProjectState`
- trace не является владельцем truth
- trace может использоваться как evidence/input для decisions и closure
- после closure trace может быть архивирован отдельно

Формула:

trace informs
closure applies
ProjectState reflects

---

## Canons

- `ProjectState` — derived active project view
- persisted truth распределена по своим владельцам
- `.zavod/meta/project.json` должен оставаться минимальным
- содержательные project docs не должны дублироваться внутрь `ProjectState`
- `ProjectState` должен быть детерминированно пересобираем
- потеря read model не должна означать потерю project truth

---

## Boundaries

- `ProjectState` не хранит trace или историю выполнения
- `ProjectState` не является decision log
- `ProjectState` не управляет execution
- `ProjectState` не описывает UI presentation
- `ProjectState` не подменяет project documents

---

## Exclusions

Исключаются:

- monolithic persisted object со всем смыслом проекта внутри
- хранение полных текстов active docs внутри `ProjectState`
- автоматические truth updates без closure / accepted path
- параллельные конфликтующие версии active `ProjectState`
- использование `ProjectState` как substitute для canon / direction / roadmap
