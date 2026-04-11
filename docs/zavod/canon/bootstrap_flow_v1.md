# ZAVOD v1 — Bootstrap Flow

## Purpose

`Bootstrap Flow v1`

- описывает поведение системы после сборки `ProjectState`
- применяется, когда `activeShiftId == null`
- определяет переход от idle состояния к первой смене
- фиксирует bootstrap как entry-boundary, а не как общий документ про intent-систему

---

## Input

- `ProjectEntryResult`
- `ProjectState`

Bootstrap активируется только после того,
как система уже честно собрала entry result и active project view.

---

## Activation Condition

Bootstrap режим активен, если:

- `ProjectState.ActiveShiftId == null`

Следствие:

- активной смены нет
- task truth ещё не материализована
- execution отсутствует
- idle является допустимым состоянием

---

## Bootstrap Mode

Bootstrap — это допустимый стартовый режим проекта.

В этом режиме система:

- не симулирует работу
- не делает вид, что что-то уже выполняется
- не создаёт shift автоматически
- не создаёт task автоматически
- не стартует execution

Пока validated intent не достигнут,
система имеет право оставаться в idle сколь угодно долго.

---

## Lead Behavior

Lead в bootstrap режиме:

- работает только на уровне обсуждения и интерпретации намерения
- не инициирует execution
- не создаёт shift автоматически
- не создаёт task напрямую
- анализирует пользовательский ввод только как candidate intent
- может вести диалог, уточнять и помогать сформулировать задачу

Lead в bootstrap не переводит систему в work state без validated intent.

---

## Flow

### Step 1

Пользователь вводит сообщение.

### Step 2

Lead определяет:

- есть ли candidate intent
- относится ли он к текущему проектному контексту
- достаточно ли у системы оснований продолжать intent-loop

### Step 3

Если intent отсутствует или недостаточен:

- система остаётся в bootstrap режиме
- shift не создаётся
- task не создаётся
- execution не запускается

### Step 4

Если candidate intent появился:

- он продолжает жить внутри interaction / intent layer
- bootstrap сам по себе не создаёт shift на этом шаге

### Step 5

Если intent достигает validated state:

- создаётся первая смена
- `activeShiftId` обновляется
- bootstrap режим завершается
- система переходит в shift-based lifecycle

---

## Output

Результат bootstrap flow может быть только двух типов:

### 1. Idle preserved

- `ProjectState` остаётся без `activeShiftId`
- система честно остаётся в bootstrap режиме

### 2. First shift created

- `activeShiftId` появляется
- bootstrap режим завершается
- начинается обычный shift lifecycle

Других side-effects bootstrap flow не производит.

---

## Single Allowed Side-Effect

Единственный допустимый side-effect bootstrap flow:

- materialization of the first shift after validated intent

Bootstrap flow не имеет права:

- создавать execution
- создавать accepted result
- создавать closure
- обновлять project truth documents содержательно

---

## Relation to Project Truth

Bootstrap не меняет project-level truth,
кроме появления первой активной смены в active linkage.

Он не обновляет содержательно:

- `project.md`
- `direction.md`
- `roadmap.md`
- `canon.md`
- decisions

Bootstrap may coexist with derived import understanding and preserved foreign materials that remain below truth.
Bootstrap does not promote such derived understanding into project truth by itself.
Bootstrap only opens the path toward the first shift-bound work cycle.

---

## Canons

- bootstrap режим является валидным состоянием
- отсутствие active shift является нормальным состоянием
- idle допустим и не считается ошибкой
- validated intent — единственная точка входа в materialization первой смены
- bootstrap не инициирует execution
- bootstrap не создаёт task напрямую
- bootstrap создаёт только первую смену
- bootstrap не должен выглядеть как “система уже что-то помнит и делает”

---

## Boundaries

Bootstrap flow не включает:

- worker
- QC
- execution pipeline
- closure
- snapshot creation
- result surfaces
- task history
- decisions as active mutation path
- local cache / journal как owner поведения

Bootstrap заканчивается там,
где появляется первая активная смена.

---

## Relation to Adjacent Documents

Следующие смыслы не являются хозяевами этого документа и должны жить отдельно:

- intent states / invalidation / readiness → `intent_system_v1.md`
- interaction / validation path → `interaction_validation_flow_v1.md`
- entry object / resume routing → `cold_start_behavior_v1.md` и entry-layer docs
- resume reconstruction → `resume_contract_v1.md` и `resume_intelligence_v1.md`

Bootstrap Flow фиксирует только entry-boundary:

нет active shift → идём через bootstrap → validated intent → first shift.

---

## Exclusions

В этот документ не входят:

- полный intent state machine
- readiness mapping
- primary action presentation
- interaction session model
- retrieval system
- prompt pipeline
- scoring logic
- async orchestration details
- upper-layer UI presentation rules beyond bootstrap boundary

Если эти механики нужны,
они должны описываться в своих собственных canon-документах,
а не расширять bootstrap flow.
