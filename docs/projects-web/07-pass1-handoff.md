# Pass 1 Handoff — UI готов, дальше core actions wiring

Дата: 2026-04-19
Статус: Pass 1 (UI shell) завершён. Pass 2 (core-driving actions) — следующий этап.

---

## Что сделано в Pass 1

**Полный переход Projects mode на WebView2 с настоящими данными:**

- `UI/Web/Projects/projects.surface.html|css|bridge.js` — web shell поверх прототипа `project_concept_full.html`
- `UI/Modes/Projects/ProjectsWebRendererView.xaml(.cs)` — WebView2 host, паттерн зеркалит `ChatsWebRendererView`
- `UI/Modes/Projects/Bridge/ProjectsWebBridgeModels.cs` — typed records snapshot envelope
- `UI/Modes/Projects/Bridge/ProjectsWebSnapshotBuilder.cs` — stateful builder, читает реестр + per-project артефакты с диска
- `Persistence/ProjectRegistryStorage.cs` — реестр в `~/Documents/ZAVOD/projects.json`
- `Bootstrap/ProjectBootstrap.cs` — добавлен overload `Initialize(rootPath, projectName)` для нового потока с явным именем
- `MainWindow.xaml.cs` — feature flag `UseProjectsWebRenderer = true`, parallel хост рядом с XAML, intent dispatch

### Реально работает end-to-end

- **Mode switch** Chats ↔ Projects (top WinUI, не трогать)
- **Project List** — рендерится из registry. Stack tags + file count + anchor count читаются из per-project `.zavod/import_evidence_bundle/*.json`
- **New project modal** — name + kind dropdown + live path preview. Создаёт уникальную папку в `~/Documents/ZAVOD/<slug>/`, Bootstrap с явным именем, kind в `.zavod/meta/project_kind.txt`
- **Import project** — FolderPicker → Bootstrap + WorkspaceScanner.Scan + WorkspaceImportMaterialInterpreterRuntime.Interpret (gpt-4.1-nano) → preview.html + 20 JSON артефактов → registry → snapshot push → preview.html открывается в браузере
- **Project Home** — реальное имя/путь/stats/preview iframe (через второй virtual host `projectfiles.zavod`). Scanner analysis секция (top 12 non-doc snippets) + User documents секция (doc-categorized snippets)
- **Composer** — paperclip → C# FilePicker → staged file chip. Long paste (>4000 chars / >40 lines) → text artifact chip. × на chip → unstage. Send → atomic submission в conversation log (без LLM)
- **Локализация** RU/EN — `ZAVOD_UI_LANG` env. Все ключи из `AppText.cs` идут в snapshot.text dict, JS применяет по `data-l10n` атрибутам

### Ключевые решения

- **Folder vs Name decoupled**: folder — физический адрес, фиксируется при create. Name — метаданные в `.zavod/meta/project.json`, редактируется свободно. Convention из VSCode workspace / JetBrains
- **Single source of truth**: snapshot envelope содержит всё что JS видит. JS не хранит локального state кроме UI-only toggle'ов (modal open/close, clarify expand)
- **Shared engine**: conversation portion snapshot'а строится через `_projectsController.BuildSnapshot()` который возвращает `ChatsWebStateSnapshot` — тот же тип что у Chats. Composer/attachments/long-text идут через `ConsumeComposerSubmissionAsync`, `StageFiles`, `StageLongTextArtifact` — общие методы Chats и Projects контроллеров
- **L10n keys = AppText keys** (dotted format) вместо camelCase record fields — для масштаба 70+ строк проще Dictionary<string,string> чем строго типизированный record

---

## Bridge contract (актуальное состояние)

### C# → JS

`state_snapshot` envelope с payload:
```json
{
  "conversation": ChatsWebStateSnapshot,    // messages, composer, pendingAttachments, text-en
  "currentScreen": "list" | "home" | "work-cycle",
  "list": { "projects": [...], "canImport": true, "canCreateNew": true },
  "selectedProject": {
    "id", "name", "description", "previewUrl",
    "files", "anchors", "tasks", "docs",
    "anchorRows": [{ "tag", "value" }],
    "documentRows": [{ "name", "meta" }]
  },
  "home": null,         // зарезервировано для richer Home payload
  "workCycle": null,    // зарезервировано для phase/preflight/execution payload
  "text": { "projects.list.title": "...", ... }
}
```

### JS → C# intents

| Intent | Wired? | C# handler |
|---|---|---|
| `dom_ready` / `renderer_ready` | trace only | — |
| `render_complete` | hide overlay | `FirstFrameReady` event |
| `navigate_screen { screen }` | ✅ | `_projectsWebSnapshotBuilder.NavigateTo` + push |
| `select_project { projectId }` | ✅ | `SetSelectedProjectFolder` + `Touch` + `SelectProject` + push |
| `create_project { name, kind }` | ✅ | `HandleProjectsWebCreateAsync` |
| `import_project` | ✅ | `HandleProjectsWebImportAsync` (FolderPicker + Scan + Interpret + registry + push) |
| `send_message { text }` | partial | `ConsumeComposerSubmissionAsync` + `AddMessageAsync(User)` + commit + push (НО не дёргает Lead) |
| `request_attach_files { sourceType }` | ✅ | `StageComposerFilesAsync(projectsMode: true)` + push |
| `remove_attachment { draftId }` | ✅ | `RemovePendingComposerInput` + push |
| `stage_text_artifact { text }` | ✅ | `StageLongTextArtifact` + push |
| **`enter_work`** | ❌ trace only | должен → `_workCycleActions.EnterWorkAsync()` |
| **`confirm_preflight`** | ❌ trace only | должен → `_workCycleActions.ConfirmPreflightAsync()` |
| **`return_to_chat`** | ❌ trace only | должен → `_workCycleActions.ReturnToChatAsync()` |
| **`apply_clarification` { text }** | ❌ trace only | должен → `_workCycleActions.ApplyClarificationAsync(text)` |
| **`accept_result`** | ❌ trace only | должен → `_workCycleActions.AcceptResultAsync()` |
| **`reject_result`** | ❌ trace only | должен → `_workCycleActions.RejectResultAsync()` |
| **`request_revision` { text }** | ❌ trace only | должен → `_workCycleActions.RequestRevisionAsync(text)` |

**7 жирных красных** — это task для Pass 2.

---

## Pass 2 — что нужно сделать

Цель: сделать возможным live-тестирование Lead/Worker/QC ролей на импортированном репозитории.

### 1. Расширить `ProjectsWebStateSnapshot.WorkCycle` payload

Сейчас `WorkCycle: null`. Должно содержать (из доки 06):

```csharp
record ProjectsWebWorkCycle(
    string VisualPhase,            // "phase-1" | "phase-2" | "phase-3"
    bool ResultVisible,
    string SurfacePhase,           // "Discussion" | "Execution" | "Result" | "Completed"
    string ExecutionSubphase,      // "Preflight" | "Running" | "Qc" | "Revision" | "Interrupted" | ""
    string ResultSubphase,         // "Ready" | "RevisionRequested" | ""
    bool ShowChat,
    bool ShowExecution,
    bool ShowResult,
    bool CanEnterWork,
    bool CanConfirmPreflight,
    bool CanSendMessage,
    bool ComposerEnabled,
    IReadOnlyList<ProjectsWebExecutionItem> ExecutionItems,
    IReadOnlyList<ProjectsWebPreflightTask> PreflightTasks,
    string? ValidationSummary);
```

Builder читает `_workCycleActions` / `StepPhaseProjection` для активного проекта.

### 2. VisualPhase mapping (computed в C#)

```
Discussion + любая subphase     → "phase-1"
Execution + Preflight           → "phase-2"
Execution + Running/Qc/Revision → "phase-3"
Result + любая subphase         → "phase-3" + ResultVisible: true
Completed + —                   → "phase-3" + completed flag
```

JS просто читает `body.dataset.phase = state.workCycle.visualPhase`. Никакого second phase enum в JS.

### 3. Wire 7 core-driving intent handlers

Все идут через существующий `WorkCycleActionController` (он уже есть в MainWindow и работает в XAML). Просто перенаправить из IntentReceived switch.

**Важно**: до этого нужно убедиться что `_workCycleActions` инициализирован для **выбранного проекта** (сейчас он привязан к ZAVOD repo через `_projectRoot`). Это значит при `select_project` нужно либо:
- A) Re-anchor `_projectsController` + `_workCycleActions` к новому projectRoot (большой рефакторинг — controllers пишут в свой projectRoot)
- B) Создавать новые instances controllers для выбранного project — проще и чище

### 4. JS render для preflight rows + execution items + task strip

Сейчас контейнеры пустые (`#pf-rows`, `#feed`, `#agent-feed`, `#task-strip`, `#artifact-body`):

- `state.workCycle.preflightTasks[]` → render `.pf-row` элементов в `#pf-rows`
- `state.conversation.messages[]` → render `.bubble-wrap`/`.doc-block` в `#feed` (Discussion view)
- `state.workCycle.executionItems[]` → render agent action blocks + diff cards в `#agent-feed` + `#artifact-body`
- Task strip: derived from preflight tasks статус (running/done/pending)
- Action bar stats: `#ab-stat-tasks`, `#ab-stat-files`, `#ab-stat-errors` обновляются из state

### 5. Re-anchorable controllers

Сейчас `_chatsController` / `_projectsController` / `_workCycleActions` создаются в конструкторе `MainWindow` для `_projectRoot` (resolved by ProjectRootResolver = ZAVOD repo). Для multi-project работы:

Вариант B (см. п.3): когда `select_project` приходит — пересоздавать controllers под новый projectRoot. Старые dispose'ятся (или просто GC'ятся). Snapshot пересобирается.

Это критично потому что `WorkCycleActionController` пишет в `<projectRoot>/.zavod/shifts/`. Если мы оставим его привязанным к ZAVOD repo, любая попытка enter_work на cssDOOM-main создаст shift в ZAVOD's `.zavod/shifts/`, не в cssDOOM. То самое "core должен крутить ZAVOD на ZAVOD" — анти-паттерн.

---

## Carry-over hints для нового чата

**Не трогать:**
- Top-level switch Chats|Projects — это WinUI в `MainWindow.xaml` (`<shell:ModeSwitchView>`)
- XAML `<projects:ProjectsHostView>` — fallback за feature flag, оставить пока Pass 2 не доказан полностью
- `_chatsController` flow и Chats web — отдельная вселенная

**Файлы которые ты будешь чаще всего трогать в Pass 2:**
- `MainWindow.xaml.cs` — IntentReceived switch (line ~1380 area), контроллеры
- `UI/Modes/Projects/Bridge/ProjectsWebSnapshotBuilder.cs` — расширение Build() для WorkCycle payload
- `UI/Modes/Projects/Bridge/ProjectsWebBridgeModels.cs` — добавление полей в WorkCycle
- `UI/Web/Projects/projects.bridge.js` — render для preflight/execution из state
- `UI/Modes/Projects/WorkCycle/Actions/WorkCycleActionController.cs` — уже работает, не переписывать

**Полезные ссылки:**
- `docs/projects-web/06-projects-web-connection-plan.md` — оригинальный план Pass 1+2 c bridge contract
- `docs/projects-web/02-project-work-cycle-core-binding-ru.md` — какие state transitions есть в core (EnterPreflight, ConfirmPreflight, MoveToQc, AcceptQc, ReturnForRevision, ReturnToLead, CancelExecution, ResumeExecution, OpenInterruptedDiscussion, StartRevisionCycle)
- `docs/projects-web/памятка.txt` — главное правило: shared engine, не плодить параллельные сущности

**OpenRouter / LLM:**
- `app/config/openrouter.local.json` содержит API key + `modelId: "openai/gpt-4.1-nano"` — это активный default для Importer role
- Lead и Worker могут использовать другую модель — смотри как они инициализируются в коде
- `OpenRouterExecutionClient.Execute` синхронный, всегда оборачивай в `Task.Run` чтобы не лочить UI

**Тестовый сценарий который ты будешь гонять:**
1. Запустить app
2. Click "новый проект" → ввести имя → создать (или import готовый репо)
3. Click на карточку → Home
4. Click "ENTER WORK CYCLE" → Discussion
5. Написать запрос в composer → Send
6. **Сейчас ничего не отвечает.** В Pass 2: должен ответить Lead с предложением intent
7. Кнопка "В работу" / Tab → preflight card с задачами от Lead
8. Click "Запустить" → Worker стартует execution
9. Diff cards + agent actions появляются в правой колонке
10. Action bar visible с Accept/Revise/Reject

Главные вопросы для тестирования:
- Адекватно ли Lead формирует intent на разных типах проектов (cssDOOM = JS game vs Rust CLI vs C# library)?
- Откажется ли Worker делать противоречивую задачу?
- Корректно ли QC принимает/отклоняет результат?
- Не сваливается ли flow в loop при revision?
