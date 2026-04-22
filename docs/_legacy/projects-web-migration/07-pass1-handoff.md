# Pass 2 Handoff — Lead + Worker live, лаборатория на cssDOOM работает

Дата обновления: 2026-04-20
Статус: Pass 1 (UI shell) + Pass 2 срезы A/B/C-1 завершены. Срез C-2 (QC LLM) — следующий.

---

## Текущее состояние: что работает end-to-end

**User → Lead (LLM) → Preflight → Worker (LLM) → QC auto-accept → Result** — проходится в web UI на cssDOOM:

1. User шлёт сообщение в Discussion
2. `LeadAgentRuntime` (gpt-4.1-mini) читает:
   - `RECENT CONVERSATION` — последние до 7 реплик user/lead/worker/qc
   - `PROJECT CONTEXT` (name/kind/root)
   - `PROJECT STACK` из `import_evidence_bundle/technical_passport.json` + `project_profile.json` (languages, source_roots, file counts, config_markers)
   - `ADVISORY NOTES` от `ProjectSageService`
   - `ORIENTATION MODE` инструкции если `OrientationIntentDetector.IsOrientationRequest` сработал
3. Lead возвращает strict JSON `{intent_state, reply, scope_notes, task_brief, warnings}`
4. `TryMapLeadIntentState` переводит Lead's intent_state в `ContextIntentState` — **Lead авторитет**, переопределяет `ProductIntentClassifier` (regex fast pre-pass)
5. На `ready_for_validation`: `task_brief` становится `TaskState.Description` (НЕ сырой user text — эта поправка убрала Worker-refused-by-garbage-input)
6. `tab to work` кнопка загорается (`workCycle.canEnterWork`), user жмёт → `EnterWorkAsync` → Preflight phase
7. Preflight card выскакивает (`pfCard.open`), user жмёт Confirm → `ConfirmPreflightAsync` → `WorkerAgentRuntime` (deepseek-chat-v3) → структурированный результат
8. QC auto-accept → Result phase → phase-3 UI с Accept/Reject/Revise кнопками (все 3 wired)

---

## Инфраструктура (что реально зафиксировано в коде)

### Runtime

- `Execution/RolesConfiguration.cs` — record `RoleProfile(Model, Temperature, TimeoutSeconds, MaxTokens)`, loader из `app/config/roles.json` с defaults fallback
- `Execution/LabTelemetryWriter.cs` — пишет `<projectRoot>/.zavod/lab/<UTC>-<role>-<callId>/{request,response,parsed,meta}.json` по каждому LLM-вызову
- `Lead/LeadAgentRuntime.cs` — direct OpenRouter, JSON parse + code-fence strip, structured `LeadAgentResult`
- `Worker/WorkerAgentRuntime.cs` — то же для Worker, возвращает `WorkerAgentParsedResult` с plan/actions/modifications/blockers/risks/warnings

### Controller wiring

- `WorkCycleActionController`:
  - `SetProjectRoot(string)` — mutable projectRoot для re-anchoring per-project
  - `SetProgressCallback(Func<Task>)` — intermediate UI pushes между шагами (User bubble сразу, Lead/Worker по мере готовности)
  - `BuildLeadRecentTurns` — собирает контекст из `ProjectsAdapter.Items`
  - `BuildProjectStackSummary` — читает evidence bundle
  - `ReadProjectKind` — из `.zavod/meta/project_kind.txt` (fallback `unknown`)
  - `TryMapLeadIntentState` — Lead's JSON → ContextIntentState override
  - `MapWorkerStatus` — Worker's status string → WorkerExecutionStatus enum
  - `ConfirmPreflightAsync` — заменил dummy `ProduceResult` на реальный Worker LLM через `Task.Run`, с graceful fallback на dummy при сбое LLM

### MainWindow wiring (web intents)

Все 7 "красных" intents из старого handoff теперь подключены через диспетчер `HandleProjectsWebWorkCycleActionAsync`:
- `enter_work` → `EnterWorkAsync`
- `confirm_preflight` → `ConfirmPreflightAsync` (**→ Worker LLM**)
- `accept_result` → `AcceptResultAsync`
- `reject_result` → `RejectResultAsync`
- `request_revision` → `RequestRevisionAsync`
- `apply_clarification` → `ApplyClarificationAsync(text)`
- `return_to_chat` → `ReturnToChatAsync`

Каждый action после выполнения пушит snapshot.

### Re-anchor per project

- `MainWindow` конструктор: `_workCycleActions.SetProgressCallback(PushProjectsWebSnapshot)`
- `select_project` intent handler: `_workCycleActions.SetProjectRoot(entry.RootPath)` + `HandleProjectsWebSelectProjectAsync` (ReanchorToAsync) — controller переключает `_projectsController` под выбранный проект
- `ProjectsRuntimeController.IsInitialized` public getter + guard в `EnsureProjectsWebReadyAndPushAsync`: "не перезатирать уже reanchor'нутое состояние"
- `ProjectsWebSnapshotBuilder` constructor: авто-подхватывает `registry.LastOpenedProjectId` — при рестарте app контекст продолжается, а не сбрасывается

### Snapshot payload

- `ProjectsWebSnapshotBuilder.BuildWorkCyclePayload` — раньше `null`, теперь строит `ProjectsWebWorkCycle` из активного проекта:
  - `VisualPhase` (phase-1/2/3), `SurfacePhase`, `ExecutionSubphase`, `ResultSubphase`
  - `CanEnterWork` (из `projection.CanStartIntentValidation`)
  - `CanConfirmPreflight`, `CanSendMessage`, `ComposerEnabled`
  - `ResultVisible` (для phase-3 рендера)
  - `ValidationSummary` (текущий intentSummary = task_brief от Lead)
  - `ExecutionItems` / `PreflightTasks` — пока `Array.Empty<>` (срез C-3)

### JS rendering (UI/Web/Projects/projects.bridge.js)

- `renderMessages(state.conversation.messages)` — полный rerender feed'а:
  - `role=user` → bubble-wrap (справа)
  - `role=assistant/lead/worker/qc` → doc-block (слева)
  - status → doc-block тоже
- `intentBtn.className = 'intent-btn lvl3'` когда `canEnterWork === true` — кнопка горит ярко
- `pfCard.classList.toggle('open', inPreflight)` — preflight card показывается в Execution/Preflight
- `phase3.classList.toggle('active', resultVisible)` + `phase1.display = 'none'` — Result UI с Accept/Reject/Revise
- `phase1Overlay.classList.toggle('active', inPreflight)` — dim feed в preflight

### Prompts

- `app/prompts/lead.system.md` — расширен с правилами против over-refining:
  - Trust go-signals (погнали/поехали/go/ship it)
  - No repeat: не задавать тот же вопрос дважды
  - One-shot clarification для small bounded tasks
  - Trust PROJECT STACK (не спрашивать про язык если уже виден)
  - Output contract с task_brief (обязательно при ready_for_validation)
- `app/prompts/worker.system.md` — без изменений в этой сессии, минимально содержательный (TODO: довести до уровня import.system.md)

### Config

- `app/config/roles.json` (gitignored):
  - Lead: `openai/gpt-4.1-mini` (0.3, 60s, 800t)
  - Worker: `deepseek/deepseek-chat-v3` (0.2, 120s, 2000t)
  - QC: `anthropic/claude-haiku-4.5` (0.0, 45s, 800t) — пока не используется
- `app/config/openrouter.local.json` — API key + default model

---

## Лаб-валидация

На cssDOOM прогнаны сценарии 1-3 (orientation, vague intent, confirmation dialogue):

- OK Orientation: "кто я? где я?" → Lead корректно идентифицирует ZAVOD + cssDOOM-main + роль Shift Lead
- OK Диалог с уточнениями работает, Lead помнит предыдущие реплики через RECENT CONVERSATION
- OK Go-signal detection: "погнали уже в работу" → ready_for_validation (после того как добавили trust-go rule)
- OK Lead intent override на classifier работает — кнопка tab to work разблокируется
- OK Phase transitions через state machine корректны
- OK Worker первый реальный вызов прошёл (deepseek-v3, 5s, 200 OK) — выявил баг с raw-text TaskDescription → пофикшено через task_brief
- TODO Сценарий 4 (Worker happy-path на нормальной задаче) — не проверен после фикса task_brief

Lab telemetry накапливается в `<projectRoot>/.zavod/lab/<UTC>-<role>-<callId>/` — полные входные пакеты, raw response, parsed JSON, meta с latency/model/diagnostics.

---

## Что НЕ сделано (приоритизация)

### Срез C-2 — QC LLM runtime

- `QcAgentRuntime.cs` — direct OpenRouter с parsed `{status: ACCEPT|REVISE|REJECT, verified, issues, reason, next_action}`
- Заменить `ExecutionRuntimeController.AcceptQcReview` (auto-accept) на wire через QC runtime
- Map ACCEPT → ProduceProvidedResult + AcceptQcReview, REVISE → RestartCompletedResultForRevision, REJECT → abandon
- QC bubble `ConversationItemKind.Qc` с metadata `lab.qc.status/latency/model`
- Upgrade `app/prompts/qc.system.md` до уровня `import.system.md` с JSON schema

### Срез C-3 — phase-3 detail rendering

- JS: рендер `state.workCycle.executionItems` в `#agent-feed` (Worker actions / logs)
- JS: рендер `state.workCycle.preflightTasks` в `#pf-rows` (preflight details)
- JS: артефакт-карточка в `#artifact-body` из Worker's modifications (diff preview)
- C#: заполнение этих полей из Worker's `ExecutionRuntimeState.Result`

### Срез 2.1 — minimal DSL + линтеры

- Records: `ExecutionPlan`, `ExecutionStep`, `ExecutionResult`
- 3 typed `TypedToolContract`: `code.lint`, `project.build`, `project.test`
- Worker's `modifications` + `plan` становятся executable steps
- Mechanical QC layer наполняется из tool evidence (сейчас заглушка/auto)

### Срез 2.3 — streaming

- Async variant `OpenRouterExecutionClient` с SSE
- Интеграция с `ProjectsAdapter.AppendStreamingAsync` / `CompleteStreamingAsync`
- Live прогресс Worker вместо 5-30s паузы до ответа

### Partial re-anchor fix

- `ProjectsRuntimeController._projectRoot` всё ещё `readonly` — conversation logs + artifacts пишутся в ZAVOD's `.zavod.local/`, не в per-project. Работает (бабллы в UI ok, сообщения персистятся), но **НЕ в правильном месте на диске**.
- Фикс: сделать `_projectRoot` mutable + добавить в `ResetState` обновление; вызывать при reanchor.

### Senior Specialist

Отложен до после C-3 и DSL. Infra (`SeniorSpecialistRuntime`) не создана.

### Worker prompt upgrade

`worker.system.md` нужно довести до уровня `import.system.md` (full structured с JSON schema inline + few-shot примеры refusal / partial / success).

### Phase-3 detail gap в UI

Сейчас в Result user видит карточку с Accept/Reject/Revise, но:
- `#agent-feed` (left col) пустой
- `#artifact-body` (right col) пустой
- `#task-strip` пустой

Кнопки работают, но UX слеповат — user видит status bubble в feed (phase-1 скрыт) и голый grid.

---

## Критичные архитектурные заметки

1. **Lead bypass pipeline**: `PromptRequestPipeline.Execute` требует `IntentState == Validated`, что несовместимо с Lead (работает в Candidate/Refining). Lead использует direct OpenRouter call с raw `lead.system.md` как system prompt. Pipeline-fix для Lead — отдельная задача, не блокер.

2. **Worker bypass pipeline too**: WorkerAgentRuntime тоже не использует PromptRequestPipeline (direct call). Значит 4-part packet (ROLE CORE + SHIFT CONTEXT + TASK BLOCK + ANCHORS) в Worker не используется. Это снижает anti-hallucination гарантии канона. Когда Worker будет делать реальные мутации (после DSL) — нужно будет подключить pipeline.

3. **Classifier stays as fast pre-pass**: `ProductIntentClassifier` (regex) держим до Lead call для дешёвой pre-classification. Lead override имеет приоритет.

4. **State persistence**: `.zavod/` в проекте — shared/commit-friendly (meta, shifts, snapshots, lab). `.zavod.local/` — app-local runtime state (resume-stage, conversations, artifacts, cache). Эти два разделены умышленно.

5. **Lab telemetry путь зависит от выбранного projectRoot**: Lead пишет в `<project>/.zavod/lab/<UTC>-lead-send-message/`, Worker — `<project>/.zavod/lab/<UTC>-worker-<TASK-ID>/`. Для фазы QC — зарезервировано `-qc-<RESULT-ID>`.

---

## Hints для нового чата / новой сессии

**Файлы которые будут чаще всего трогаться:**
- `Worker/WorkerAgentRuntime.cs` — полировать prompt, structured output parsing
- `Qc/QcAgentRuntime.cs` (создать) — по шаблону Lead/Worker
- `UI/Modes/Projects/WorkCycle/Actions/WorkCycleActionController.cs` — wire QC, extend phase-3 payloads
- `UI/Modes/Projects/Bridge/ProjectsWebSnapshotBuilder.cs` — расширить WorkCycle payload под phase-3 detail
- `UI/Web/Projects/projects.bridge.js` — рендер agent-feed / task-strip / artifact-body

**Полезные ссылки:**
- `agent/ZAVOD — Execution Engine & Prompt Assembly (current).md` — main текущая архитектура
- `docs/zavod/roles/{shift_lead,worker,qc}.md` — канон ролей
- Lab examples: `zavod import test/cssDOOM-main/.zavod/lab/` — примеры Lead/Worker пакетов + ответов для reference
- `app/prompts/import.system.md` — эталон structured prompt

**Тестовый сценарий на следующей сессии:**
1. cssDOOM: запрос "добавь FPS counter в правый верхний угол"
2. Lead (обновлённый с task_brief) → ready_for_validation с чистой формулировкой
3. tab to work → Confirm preflight
4. Worker → должен выдать non-refused результат (success/partial с планом) теперь когда TaskDescription корректный
5. Result UI (C-3 gap: agent-feed пустой) → Accept/Reject/Revise — проверить что accept_result действительно промотит cycle
