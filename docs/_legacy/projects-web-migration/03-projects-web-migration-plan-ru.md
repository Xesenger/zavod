# Projects Web Migration Plan — рабочий план

Дата: 2026-04-16
Статус: текущий план миграции

## 1. Цель этапа

Перенести `Projects UI` в web без повторного разрушения foundation.

Foundation на этом этапе считается замороженным:

- atomic submission
- artifact -> execution/model
- UTF-8 path
- typed work-cycle truth
- snapshot-store-first semantics
- keyed incremental rendering

Если следующий UI-pass требует менять что-то из этого, сначала нужен отдельный mini-audit.

## 2. Что уже считается доказанным

На момент старта migration stage уже доказано:

- attachment принадлежит тому же submission, что и text
- artifact content доходит до реального model path
- кириллица проходит live path end-to-end
- `Projects` уже сидит на typed backend/projection loop

Safe return point:

- tag `projects-web-pre-migration-safe-point`

## 3. Основной принцип migration

Мы не переносим сразу “весь Projects”.
Мы переносим **один честный vertical slice** поверх уже подтвержденной модели.

Главная цель:

- доказать, что `Projects Web UI` — это настоящая проекция backend loop
- а не новая локальная симуляция workflow

## 4. Что не делать

На текущем этапе нельзя:

- делать broad refactor foundation
- строить сразу весь `Projects UI`
- расширять markdown platform сверх минимального scope
- смешивать layout polish с domain rewiring
- возвращать string-driven logic
- докладывать новую доменную логику в `MainWindow`

## 5. UI guardrails

### 5.1. Truth rules

UI не должен:

- парсить текст интерфейса
- создавать truth
- заменять `StepPhaseState` локальным lifecycle

UI должен:

- читать typed state
- читать typed projections
- читать typed conversation/result items
- вызывать owner path для core-driving actions

### 5.2. MainWindow rules

`MainWindow` должен оставаться:

- shell host
- mode switch owner
- high-level router

`MainWindow` не должен становиться:

- lifecycle brain
- phase owner
- domain state machine

### 5.3. Renderer rules

Нужно закрепить typed renderer contract:

- `message -> message renderer`
- `status -> status renderer`
- `log -> log renderer`
- `artifact -> artifact renderer`

Нельзя пытаться “превратить все в markdown”.

## 6. Payload / snapshot contract перед UI

До активной отрисовки `Projects Web UI` нужно считать базовым payload contract:

### 6.1. Shell-level

- current app mode
- current projects screen
- top-level title/summary

### 6.2. Projects level

- `Project List` state
- `Project Home` state
- `Project Work Cycle` state

### 6.3. Work Cycle level

- `StepPhaseState`
- `StepPhaseProjection`
- discussion items
- execution items
- result items
- available actions
- composer state

Именно после фиксации этого contract имеет смысл рисовать экран.

## 7. Migration order

### Pass 1 — Projects Web skeleton

Цель:

- построить web shell для `Projects`
- не влезать в сложные mutation paths

Что делаем:

- web host for `Projects`
- internal routing:
  - `List`
  - `Home`
  - `Work Cycle`
- внутри `Work Cycle`:
  - 3 pane structure:
    - `Discussion`
    - `Execution`
    - `Result`
- только real typed state binding

Что не делаем:

- fancy interaction
- rich markdown
- “умные” shortcut transitions

### Pass 2 — Work Cycle honesty pass

Цель:

- доказать, что 3 surface реально привязаны к core

Что проверяем:

- visibility идет от `StepPhaseProjection`
- work entry не равен обычному send
- preflight / running / QC / revision / result читаются как typed state
- accept / revise / reject wired только через owner path

### Pass 3 — Artifact presence in Projects UI

Цель:

- сделать artifacts/logs/statuses видимыми как typed items

Что делаем:

- attached items visible in discussion/execution context
- result artifacts visible in result context
- log/status/artifact renderers живут отдельно

### Pass 4 — Markdown/document polish

Только после стабилизации структуры.

Минимальный scope:

- paragraphs
- headings
- lists
- quotes
- code fences
- inline code
- links

Позже можно рассмотреть:

- tables
- checklists

Отложить:

- diagrams
- LaTeX
- complex embeds
- advanced widgets

## 8. Work Cycle migration order внутри одного vertical slice

Даже внутри `Work Cycle` лучше идти слоями:

### Layer A — layout only

- 3 pane layout
- sticky zones
- scrolling
- width/rail structure

Без сложного поведения.

### Layer B — real binding

- discussion pane читает реальные items/state
- execution pane читает реальные execution/result-adjacent items/state
- result pane читает реальные result/apply state

### Layer C — polish

- spacing
- density
- transitions
- nicer artifact cards
- markdown refinement

## 9. Verification after every pass

После каждого meaningful pass делать короткий proof pass.

Минимальный checklist:

- `Projects` still reads typed truth
- no string seams reintroduced
- atomic submission still intact
- artifacts still reach model
- no full rerender regression
- no mojibake in web path

Короткий smoke check после каждого pass лучше, чем большой recovery потом.

## 10. Legacy strategy

После migration не надо удалять старый XAML shell мгновенно.

Нужно держать три категории:

### 10.1. Keep as foundation

- typed backend
- projections
- runtime/action controllers
- storage/contracts
- web host/runtime

### 10.2. Transitional

- current `ProjectsHostView` / XAML screens
- старые shell surfaces, пока web cutover не завершен

### 10.3. Becomes legacy after web cutover

- XAML `Projects` screens
- старые visual-only recovery scaffolds

## 11. Documentation rule

После стабилизации каждого куска нужно обновлять документы сразу.

Минимум:

- current architecture snapshot
- runtime truth loop note
- storage semantics note
- Projects UI ownership note

Иначе docs снова начнут описывать прошлое состояние.

## 12. Practical next step

Самый безопасный следующий шаг:

1. закрепить текущий checkpoint как baseline
2. зафиксировать payload/snapshot contract для `Projects Web`
3. сделать `Pass 1`:
   - Projects web shell
   - List/Home/Work Cycle routing
   - 3-pane Work Cycle skeleton
   - typed binding only

Только после этого переходить к execution/result honesty pass.
