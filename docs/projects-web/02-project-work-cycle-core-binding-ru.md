# Project Work Cycle — 3-фазовый UI и привязка к core

Дата: 2026-04-16
Статус: рабочий reference-документ для Projects Web migration

## 1. Назначение документа

Этот документ фиксирует, как должен работать `Project Work Cycle`:

- какие у него есть поверхности
- как они завязаны на `StepPhaseState` / `StepPhaseProjection`
- какие controls являются только UI navigation
- какие controls реально двигают core

Цель документа:

- не дать UI снова изобрести локальный lifecycle
- не дать web migration превратиться в новую симуляцию backend loop

## 2. Базовая модель

`Project Work Cycle` состоит из трех внутренних surfaces:

- `Discussion`
- `Execution`
- `Result`

Эти поверхности не живут отдельно друг от друга как три независимых экрана.
Они являются одной фазовой проекцией typed core state.

## 3. Typed core, к которому должен быть привязан UI

Ключевые core types:

- `SurfacePhase`
- `DiscussionSubphase`
- `ExecutionSubphase`
- `ResultSubphase`
- `StepPhaseState`
- `StepPhaseProjection`

См.:

- [StepPhaseContracts.cs](/C:/Users/Boris/Documents/Dev/zavod/Flow/StepPhaseContracts.cs:1)
- [StepPhaseMachine.cs](/C:/Users/Boris/Documents/Dev/zavod/Flow/StepPhaseMachine.cs:1)
- [StepPhaseProjectionBuilder.cs](/C:/Users/Boris/Documents/Dev/zavod/Flow/StepPhaseProjectionBuilder.cs:1)

## 4. Фазовая truth-модель

### 4.1. SurfacePhase

На верхнем уровне `Work Cycle` phase truth выглядит так:

- `Discussion`
- `Execution`
- `Result`
- `Completed`

Для web migration практически значимы первые три.

### 4.2. DiscussionSubphase

Стадии discussion:

- `Idle`
- `Forming`
- `Ready`
- `Reopened`

Смысл:

- `Idle/Forming` — обсуждение еще не доведено до запуска работы
- `Ready` — можно переходить к preflight/work entry
- `Reopened` — discussion reopened после уже существующего workflow path

### 4.3. ExecutionSubphase

Стадии execution:

- `Preflight`
- `Running`
- `Qc`
- `Revision`
- `Interrupted`

Смысл:

- `Preflight` — подтверждение перед входом в работу
- `Running` — активная execution стадия
- `Qc` — QC review state
- `Revision` — повторный заход на доработку
- `Interrupted` — execution path был прерван честно, без фейкового continuation

### 4.4. ResultSubphase

Стадии result:

- `Ready`
- `RevisionRequested`

Смысл:

- `Ready` — результат готов к финальному решению
- `RevisionRequested` — результат вернул execution path обратно в revision loop

## 5. Как UI получает видимость surfaces

Видимость surfaces должна идти из `StepPhaseProjection`, а не из локальных UI-флагов.

Текущее правило видно в [StepPhaseProjectionBuilder.cs](/C:/Users/Boris/Documents/Dev/zavod/Flow/StepPhaseProjectionBuilder.cs:21):

- `ShowChat`
- `ShowExecution`
- `ShowResult`

Текущее базовое поведение:

- `Discussion`:
  - chat visible
  - execution hidden
  - result hidden

- `Execution`:
  - chat still exists as context/reference
  - execution visible
  - result hidden

- `Result`:
  - execution visible
  - result visible
  - chat remains bounded/frozen context

Важно:

- `Execution` не является отдельным вторым приложением
- `Result` не заменяет `Execution`
- `Revision` не создает новый UI-world, а возвращает в тот же bounded loop

## 6. Текущее поведение core transitions

### 6.1. Discussion -> Preflight

Переход в preflight делается отдельным work-entry действием, а не обычным send.

Ключевые переходы:

- `EnterPreflight`
- `EnterActiveShiftPreflight`
- `EnterReopenedPreflight`

### 6.2. Preflight -> Running

Подтверждение preflight:

- `ConfirmPreflight`

### 6.3. Running/Revision -> QC

- `MoveToQc`

### 6.4. QC -> Result

- `AcceptQc`

### 6.5. Result -> Revision

- `ReturnForRevision`

### 6.6. Result -> Discussion reopen

- `ReturnToLead`

### 6.7. Interrupted path

- `CancelExecution`
- `ResumeExecution`
- `OpenInterruptedDiscussion`

### 6.8. Revision cycle continuation

- `StartRevisionCycle`
- `ReturnToResultFromRevisionIntake`

Все это уже существует в core и не должно переписываться ради UI.

## 7. Как это выражается в текущем UI owner path

Текущий owner для Work Cycle actions:

- [WorkCycleActionController.cs](/C:/Users/Boris/Documents/Dev/zavod/UI/Modes/Projects/WorkCycle/Actions/WorkCycleActionController.cs:1)

Ключевые action entry points:

- `SendProjectsMessageAsync`
- `EnterWorkAsync`
- `ConfirmPreflightAsync`
- `ApplyClarificationAsync`
- `ReturnToChatAsync`
- `AcceptResultAsync`
- `RequestRevisionAsync`
- `RejectResultAsync`

Текущее правило:

- UI вызывает owner path
- owner path двигает typed state / resume snapshot / adapter items
- UI перечитывает snapshot/projection

UI не должен делать lifecycle transition сам по себе.

## 8. Как делить controls в web UI

### 8.1. Read-only projection controls

Это controls, которые только показывают typed state:

- phase label
- discussion summary
- execution summary
- result summary
- evidence/status/doc links

Они не двигают lifecycle.

### 8.2. Surface-navigation controls

Это controls, которые меняют то, что пользователь видит, но не должны автоматически мутировать truth.

Примеры:

- focus on discussion
- focus on execution
- focus on result
- return to visible surface

Они должны быть отделены от core-driving actions.

### 8.3. Core-driving controls

Это controls, которые реально вызывают owner path и меняют bounded workflow truth.

Примеры:

- enter work
- confirm preflight
- clarify/apply clarification
- return for revision
- accept result
- reject/abandon result

## 9. Composer boundary inside Work Cycle

Composer внутри `Discussion` — это не свободный top-level chat.

Его правила:

- он привязан к project conversation
- он работает через atomic submission
- attachments принадлежат тому же submission, что и text
- он может влиять на `StepPhaseState`, но только через owner path

Важно:

- send discussion message сам по себе не равен “start execution”
- work entry — отдельное action

## 10. Execution surface semantics

`Execution` — это одна постоянная surface.

Это значит:

- `Preflight` живет внутри нее
- `Running` живет внутри нее
- `Qc` живет внутри нее
- `Revision` возвращается в нее же

То есть web migration не должна рисовать отдельный special screen для каждой execution subphase.
Нужно иметь один `Execution` surface с typed internal state.

## 11. Result surface semantics

`Result` — это тоже не отдельный lifecycle outside loop.

Его правило:

- `Result` появляется, когда core говорит, что результат готов
- `Execution` при этом не исчезает как reference/context surface
- `Accept` является единственной apply-like mutation decision
- `Revise` возвращает path обратно в execution/revision loop

## 12. Repo mutation rule

Критическое invariant:

- repo mutation не должна происходить через navigation
- repo mutation не должна происходить через visibility
- repo mutation не должна происходить при простом входе в `Execution`
- repo mutation должна быть связана только с результатным owner path уровня `Accept`

## 13. Что это значит для web migration

Web UI для `Project Work Cycle` должен строиться так:

1. брать `ProjectWorkCycleProjection`
2. читать `StepPhaseState` и `StepPhaseProjection`
3. рендерить:
   - `Discussion`
   - `Execution`
   - `Result`
   как typed surfaces
4. делить controls на:
   - read-only
   - navigation
   - core-driving
5. никогда не заменять core truth локальной UI-логикой

## 14. Hard rules

- не вводить second local phase enum в web layer
- не читать lifecycle из display strings
- не хранить UI-only flags как замену `StepPhaseProjection`
- не смешивать `Chats` и `Work Cycle discussion`
- не превращать `MainWindow` в новый workflow brain
- не делать synthetic shortcut из `Discussion` прямо в `Result`

## 15. Практический минимальный target

Для первого честного web vertical slice достаточно:

- `Discussion` surface читает реальные items и real phase state
- `Execution` surface читает реальные execution/result-adjacent items и real phase state
- `Result` surface читает реальные result/apply state и real phase state
- visibility surfaces идет от `StepPhaseProjection`
- actions wired only through owner path
