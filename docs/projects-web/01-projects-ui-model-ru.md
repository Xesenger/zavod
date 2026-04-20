# Projects UI Model — текущая рабочая модель

Дата: 2026-04-16
Статус: рабочий reference-документ для migration stage

## 1. Назначение документа

Этот документ фиксирует **текущую модель UI режима Projects** после стабилизации foundation:

- atomic submission
- artifact -> execution/model
- UTF-8 end-to-end
- typed work-cycle truth
- snapshot-store-first semantics
- keyed incremental rendering

Документ не описывает старый recovery-подход и не является архивной заметкой.
Его задача — зафиксировать, **что именно мы переносим в web**, не изобретая новый продуктовый смысл заново.

## 2. Верхний уровень продукта

На верхнем уровне приложение делится на два режима:

- `Chats`
- `Projects`

Это не просто две темы интерфейса и не две визуальные оболочки одной и той же модели.
Это два разных пользовательских режима с разными правилами.

### 2.1. Chats

`Chats` — это свободный диалоговый режим.

Он:

- не является project lifecycle
- не создает project truth сам по себе
- не открывает execution/result loop автоматически
- может использовать attachments и model execution, но не является bounded project work cycle

### 2.2. Projects

`Projects` — это основной режим ZAVOD, внутри которого живет bounded project workflow.

Внутри `Projects` есть три внутренних UI-уровня:

- `Project List`
- `Project Home`
- `Project Work Cycle`

Именно эта трехуровневая структура должна перейти в web.

## 3. Внутренняя структура Projects

### 3.1. Project List

`Project List` — это уровень выбора и входа.

Его роль:

- импорт существующего проекта
- создание нового проекта
- выбор текущего проекта

`Project List` не должен сам по себе:

- создавать lifecycle truth
- запускать execution
- писать в repo

### 3.2. Project Home

`Project Home` — это обзорный слой.

Его роль:

- показать текущее состояние проекта
- показать документы и материалы
- дать переход в `Work Cycle`

`Project Home` не является:

- chat surface
- execution surface
- result surface

`Project Home` должен оставаться легким обзорным экраном, а не второй версией `Work Cycle`.

### 3.3. Project Work Cycle

`Project Work Cycle` — это внутреннее ядро bounded project workflow.

Только внутри него живут три рабочие поверхности:

- `Discussion`
- `Execution`
- `Result`

Важно:

- top-level `Chats` != `Discussion` внутри `Work Cycle`
- `Work Cycle` — это не весь app-level UI
- `Work Cycle` не должен использовать локальную выдуманную lifecycle-модель

## 4. Что такое 3-фазовый UI

Когда мы говорим “3-фазовый UI”, мы говорим не о трех окнах и не о трех экранах приложения.

Мы говорим о трех **внутренних surface inside Project Work Cycle**:

- `Discussion`
- `Execution`
- `Result`

Это фазовый UI, потому что его видимость и доступные действия зависят от typed phase state из core.

## 5. Откуда UI получает правду

Правда для `Projects UI` должна идти только из typed state и typed projections.

Текущие ключевые источники:

- `ProjectWorkCycleQueryState`
- `ProjectWorkCycleProjection`
- `StepPhaseState`
- `StepPhaseProjection`
- conversation snapshots / typed items

UI не должен:

- парсить display strings
- выводить truth обратно из текста интерфейса
- хранить отдельный lifecycle в локальных bool/string flags
- принимать навигацию за доменную правду

## 6. Навигация и truth

Навигация внутри `Projects`:

- `List -> Home -> Work Cycle`
- `Work Cycle -> Home`
- `Home -> List`

должна трактоваться как **UI navigation only**.

Навигация не должна:

- создавать shift/task truth
- мутировать repo truth
- симулировать execution progress

## 7. Mutation rule

Реальные изменения repo и apply-путь должны появляться только через bounded result decision.

На текущей модели это означает:

- repo mutation не происходит при навигации
- repo mutation не происходит при простом входе в `Work Cycle`
- repo mutation не происходит при показе `Execution` или `Result`
- реальный apply связан с `Accept` в `Result`

## 8. Attachments / artifacts в Projects UI

Composer submission внутри `Projects` должен рассматриваться как одна typed submission unit:

- `text`
- `attachments`
- `conversationId`
- `projectId` при наличии

Attachment не является декоративным timeline append после send.
Он принадлежит тому же submission, что и текст сообщения.

UI должен показывать attachments как typed items/typed attachments, а не как неявный markdown side-effect.

## 9. Текущее направление migration в web

В web мы переносим не “красивую картинку recovery shell”, а именно эту модель:

1. top-level split:
   - `Chats`
   - `Projects`

2. inside `Projects`:
   - `List`
   - `Home`
   - `Work Cycle`

3. inside `Work Cycle`:
   - `Discussion`
   - `Execution`
   - `Result`

4. все это питается typed state / projections / snapshots

## 10. Что нельзя делать в следующем этапе

- нельзя заново смешивать `Chats` и `Project Work Cycle discussion`
- нельзя трактовать `Project Home` как execution shell
- нельзя вводить string-driven shortcuts
- нельзя превращать `MainWindow` в новый lifecycle brain
- нельзя рисовать новый UI раньше, чем зафиксирован payload/state contract

## 11. Практический вывод

Для следующего этапа нужно считать, что web migration Projects — это:

- **не редизайн продукта**
- **не новый lifecycle**
- **не переписывание foundation**
- **а перенос уже подтвержденной typed модели в web surface**
