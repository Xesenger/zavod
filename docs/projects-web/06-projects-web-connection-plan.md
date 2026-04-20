# Projects Web — Connection Plan

Дата: 2026-04-19  
Статус: план для реализации, выровнен с памяткой "shared engine".

---

## Главное правило

> **Projects UI = не новая система, а новый вид на тот же engine.**

Если для Projects появляется "своя" версия чего-то что уже есть в Chats — это red flag. Перед тем как создавать новый тип/сервис/файл — проверь, не нужно ли расширить существующий.

---

## Что уже готово (Chats → Projects переиспользует as-is)

### Conversation engine
| Тип | Файл | Что делает |
|---|---|---|
| `ConversationItemViewModel` | `UI/Rendering/Conversation/ConversationItemViewModel.cs` | typed item: Id + Revision (keyed), Kind, Text, RenderState, IsStreaming, Blocks |
| `ConversationItemKind` | там же | enum: User, Assistant, System, Status, Log, Artifact, Worker, Lead, Qc, Tool |
| `MessageRenderState` | там же | Raw / Streaming / Final |
| `IConversationAdapter` | `UI/Rendering/Conversation/IConversationAdapter.cs` | контракт адаптера (Items, Capabilities, SendAsync, CancelAsync, RetryAsync) |
| `ChatsAdapter` | `UI/Rendering/Conversation/ChatsAdapter.cs` | реализация для Chats — референс для `ProjectsAdapter` |
| `MessageRenderPipeline` | `UI/Rendering/Conversation/MessageRenderPipeline.cs` | стриминг буфер + парсинг + кеш |

### Markdown
| Тип | Файл |
|---|---|
| `MarkdownParserService` | `UI/Rendering/Markdown/MarkdownParserService.cs` |
| `MarkdownRenderCache` | там же (LRU 64) |
| `BlockRendererRegistry` | `UI/Rendering/Markdown/BlockRendererRegistry.cs` |
| `ConversationTypographyMode.Projects` | enum уже есть |

### Storage
| Тип | Файл | Зачем |
|---|---|---|
| `ConversationLogStorage` | `Persistence/ConversationLogStorage.cs` | `ForProjectConversation()` + `LoadLatestWindow(size)` для windowing |
| `ConversationArtifactStorage` | `Persistence/ConversationArtifactStorage.cs` | артефакты |
| `ConversationComposerDraftStore` | `Persistence/ConversationComposerDraftStore.cs` | pending attachments |
| `ConversationComposerSubmission` | там же | atomic submission record (text + attachments + projectId) |

### Локализация
- `AppText.Current.Get(key)` / `Format(key, args)` — `UI/Text/AppText.cs`
- Переключение: env `ZAVOD_UI_LANG=ru`
- Web: строки доставляются через `state_snapshot.text` (record `ChatsWebLocalizedText`)

### WebView2 host pattern
- `ChatsWebRendererView` (`UI/Modes/Chats/ChatsWebRendererView.xaml.cs`)
- Virtual host: `appassets.zavod` → `AppContext.BaseDirectory` (общий для всех modes)
- URL: `https://appassets.zavod/UI/Web/Chats/chats.surface.html`

---

## Bridge protocol (контракт уже валидирован в Chats)

### C# → JS — единый envelope

```json
{
  "type": "state_snapshot",
  "payload": {
    "mode": "chats" | "projects",
    "activeChatId": "...",
    "isEmpty": false,
    "hasOlder": true,
    "windowStartSeq": 1, "windowEndSeq": 12,
    "chats": [{ "id": "...", "title": "..." }],
    "messages": [{ "id", "revision", "role", "kind", "format", "text", "streamState", "label", "referenceId" }],
    "emptyState": { "headline", "subtitle" },
    "composer": { "placeholder", "pendingAttachments": [...] },
    "text": { ...localized strings... }
  }
}
```

C# тип: `ChatsWebEnvelope<ChatsWebStateSnapshot>`. **Projects использует тот же envelope**, но payload расширяется (см. ниже).

### JS → C# — текущие типы (Chats)

| type | payload | C# action |
|---|---|---|
| `dom_ready` / `renderer_ready` | `{}` | сигнал готовности |
| `send_message` | `{text}` | atomic submission |
| `new_chat` | `{}` | создать draft |
| `select_chat` | `{chatId}` | переключить |
| `request_older` | `{beforeSeq}` | prepend window |
| `request_attach_files` | `{sourceType: "file"\|"image"}` | FilePicker → stage |
| `remove_attachment` | `{draftId}` | удалить из draft |
| `stage_text_artifact` | `{text}` | большой paste → artifact |
| `render_complete` | `{}` | первый кадр готов |
| `toggle_mode` | `{mode: "projects"}` | переключить mode |

---

## Что Projects добавляет

### 1. Расширение state snapshot

Новый record-наследник или union-style payload `ProjectsWebStateSnapshot` (extends/wraps `ChatsWebStateSnapshot`):

```csharp
record ProjectsWebStateSnapshot(
    // base поля те же что в ChatsWebStateSnapshot — НЕ копируем, переиспользуем
    ChatsWebStateSnapshot Conversation, // или композиция
    string CurrentScreen,                // "list" | "home" | "work-cycle"
    ProjectsWebProjectList? List,
    ProjectsWebProjectHome? Home,
    ProjectsWebWorkCycle? WorkCycle);

record ProjectsWebProjectList(
    ProjectsWebProjectListItem[] Projects,
    bool CanImport, bool CanCreateNew);

record ProjectsWebProjectListItem(
    string ProjectId, string ProjectName, string ProjectRoot,
    string Description, string Status,
    string LastActivity, string[] StackTags,
    int FileCount, int AnchorCount);

record ProjectsWebProjectHome(
    string ProjectId, string ProjectName, string ProjectRoot,
    string DocumentStage,
    bool HasPreviewHtml, string? PreviewHtmlUrl, // virtual host URL
    ProjectsWebDocStatus[] CanonicalDocs,        // 5 docs со state
    ProjectsWebMaterialItem[] Materials,
    string Health,
    string? ActiveShiftId, string? ActiveTaskId,
    int FileCount, int AnchorCount);

record ProjectsWebDocStatus(
    string Kind,           // "project" | "direction" | "roadmap" | "canon" | "capsule"
    string FileName,
    bool Exists,
    string Stage);         // "ImportPreview" | "PreviewDocs" | "CanonicalDocs"

record ProjectsWebWorkCycle(
    string VisualPhase,    // "phase-1" | "phase-2" | "phase-3"
    bool ResultVisible,
    string SurfacePhase, string ExecutionSubphase, string ResultSubphase,
    bool ShowChat, bool ShowExecution, bool ShowResult,
    bool CanEnterWork, bool CanConfirmPreflight, bool CanSendMessage,
    bool ComposerEnabled,
    // Discussion items идут через base.Conversation.messages — НЕ дублируем
    ProjectsWebExecutionItem[] ExecutionItems,
    ProjectsWebPreflightTask[] PreflightTasks,
    string? ValidationSummary);

record ProjectsWebExecutionItem(...);  // agent-action / diff / status / log
record ProjectsWebPreflightTask(int Index, string Text, string Tag);
```

**Главное:** Discussion messages НЕ дублируются — идут через стандартное `messages[]` от conversation engine. Только Projects-специфичная execution/result визуализация — отдельные поля.

### 2. Локализация: расширение ChatsWebLocalizedText

```csharp
record ProjectsWebLocalizedText(
    // переиспользует все base поля из ChatsWebLocalizedText
    // плюс Projects-специфичные:
    string ProjectListTitle,         // "проекты"
    string ProjectListNew,           // "новый"
    string ProjectListImport,        // "импортировать проект"
    string HomeEnterWorkCycle,       // "войти в рабочий цикл"
    string PreflightConfirm,         // "запустить"
    string PreflightClarify,         // "уточнить"
    string PreflightBack,            // "назад"
    string ResultConfirm,            // "подтвердить"
    string ResultRevise,             // "на доработку"
    string ResultReject,             // "отклонить"
    // ... остальные Projects-специфичные ключи из project_concept_full.html
);
```

Все строки идут через `AppText.Current.Get("projects.home.enter_cycle")` etc. **Никаких хардкодов в html/js.**

### 3. Новые JS → C# message types для Projects

> **Полная привязка кнопок к core живёт в [`02-project-work-cycle-core-binding-ru.md`](02-project-work-cycle-core-binding-ru.md).**  
> Этот раздел даёт только wire-mapping между bridge messages и controller методами. Перед реализацией прочитать §6–§8 doc 02 — там правила деления controls на read-only / surface-navigation / core-driving + полный список core transitions.

| type | payload | категория (см. doc 02 §8) | действие |
|---|---|---|---|
| `navigate_screen` | `{screen: "list"\|"home"\|"work-cycle"}` | surface-navigation | UI navigation, **не двигает domain truth** |
| `select_project` | `{projectId}` | surface-navigation | открыть Home |
| `import_project` | `{}` | core-driving | FilePicker → import path |
| `create_project` | `{}` | core-driving | новый проект |
| `enter_work` | `{}` | core-driving | `WorkCycleActionController.EnterWorkAsync` (Discussion → Preflight) |
| `confirm_preflight` | `{}` | core-driving | `ConfirmPreflightAsync` (Preflight → Running) |
| `apply_clarification` | `{text}` | core-driving | `ApplyClarificationAsync` (preflight clarification) |
| `return_to_chat` | `{}` | core-driving | `ReturnToChatAsync` (Result → Discussion reopen) |
| `accept_result` | `{}` | core-driving | `AcceptResultAsync` — единственный apply-like mutation point |
| `request_revision` | `{text?}` | core-driving | `RequestRevisionAsync` (Result → Revision loop) |
| `reject_result` | `{}` | core-driving | `RejectResultAsync` |
| `cancel_execution` | `{}` | core-driving | `CancelExecution` (interrupt path) |
| `resume_execution` | `{}` | core-driving | `ResumeExecution` |

**send_message** переиспользуется — Projects Discussion идёт через тот же atomic `ConsumeComposerSubmissionAsync(text)`. Send в Discussion **сам по себе НЕ запускает execution** — это отдельный `enter_work`.

**Critical invariants (из doc 02 §12):**
- Repo mutation НЕ происходит через navigation
- Repo mutation НЕ происходит через visibility
- Repo mutation НЕ происходит при входе в Execution surface
- Repo mutation связана **только** с `accept_result` через owner path

### 4. VisualPhase mapping (computed в C#)

```csharp
static string ResolveVisualPhase(StepPhaseState state) => state.Phase switch
{
    SurfacePhase.Discussion => "phase-1",
    SurfacePhase.Execution when state.ExecutionSubphase == ExecutionSubphase.Preflight => "phase-2",
    SurfacePhase.Execution => "phase-3",
    SurfacePhase.Result => "phase-3", // + ResultVisible: true
    SurfacePhase.Completed => "phase-3", // + completed flag
    _ => "phase-1"
};
```

JS только читает `body.dataset.phase = state.workCycle.visualPhase` — **никакого second phase enum в JS**.

---

## Файловая структура (новые файлы)

```
UI/Web/Projects/
  projects.surface.html       — из docs/projects-web/project_concept_full.html
  projects.bridge.js          — паттерны из chats.bridge.js + projects-специфичные actions
  projects.css                — извлечь inline стили из прототипа

UI/Modes/Projects/
  ProjectsWebRendererView.xaml      — аналог ChatsWebRendererView
  ProjectsWebRendererView.xaml.cs   — WebView2 setup, postMessage handlers

UI/Modes/Projects/Bridge/
  ProjectsWebBridgeModels.cs        — records выше (расширение Chats)
  ProjectsWebSnapshotBuilder.cs     — собирает ProjectsWebStateSnapshot из projections
```

В `ProjectsRuntimeController` (если ещё нет — создать по аналогии с `ChatsRuntimeController`) добавить `BuildSnapshot()` метод который собирает full payload.

---

## Порядок реализации (Pass 1 — vertical slice)

```
1. Web host
   - UI/Web/Projects/ директория, файлы projects.surface.html/css/js
   - ProjectsWebRendererView.xaml + .cs (копия паттерна Chats)
   - virtual host appassets.zavod уже настроен (общий)
   - заменить XAML ProjectsHostView контент на WebView2

2. Snapshot models
   - ProjectsWebBridgeModels.cs — records выше
   - ProjectsWebSnapshotBuilder — собирает из ProjectsShellProjection + StepPhaseProjection
   - ProjectsWebLocalizedText — все строки через AppText

3. List → Home → WorkCycle navigation
   - JS отправляет navigate_screen / select_project
   - C# меняет _projectsScreen, шлёт обновлённый snapshot
   - JS применяет screen visibility (.screen.active)

4. Home rendering
   - applyHomeState(state.home) в JS
   - preview.html через iframe src = state.home.previewHtmlUrl (virtual host URL)
   - 5 doc cards с state из state.home.canonicalDocs

5. Work Cycle phase binding
   - body.dataset.phase = state.workCycle.visualPhase
   - body.dataset.resultVisible = state.workCycle.resultVisible
   - composer enabled/disabled из state.workCycle.composerEnabled

6. Discussion items (через shared engine)
   - используем state_snapshot.payload.messages
   - patchConversation паттерн из chats.bridge.js (keyed по id+revision)
   - markdown через тот же серверный pipeline

7. Actions wiring (JS → C#)
   - enter_work / confirm_preflight / send_message / accept_result / etc
   - C# обработчики в ProjectsWebRendererView вызывают WorkCycleActionController

8. Proof pass
   - Discussion → Preflight → Running через bridge работает
   - composer atomic submission intact (text + attachments через ConsumeComposerSubmissionAsync)
   - prepend older messages работает
   - локализация переключается RU↔EN через snapshot.text
```

Pass 2-4 (execution items rendering, result surface decision UI, full markdown polish) — после Pass 1.

---

## Что НЕЛЬЗЯ делать (red flags из памятки)

- `ProjectsWebConversationItem` или любой свой тип messages — используй `ConversationItemViewModel`
- Свой markdown renderer для Projects — расширяй `BlockRendererRegistry` через `ConversationTypographyMode.Projects`
- RU/EN строки в `projects.surface.html` или `projects.bridge.js` — всё через `AppText` → `ProjectsWebLocalizedText` → `state.text`
- Свой artifact pipeline — `ConversationArtifactStorage` общий
- Свой storage path — `ConversationLogStorage.ForProjectConversation()` уже есть
- Full rerender списка — keyed patching по id+revision (как Chats)
- Domain logic в `MainWindow` — `MainWindow` остаётся shell host, action wiring через `WorkCycleActionController` который уже есть
- Второй phase enum в JS — `VisualPhase` computed в C#

---

## Top-level switch (важно не задеть)

Switch "чаты | проекты" по центру — **WinUI**, в `MainWindow.xaml`. WebView2 живёт **внутри** Projects content area. Top-level переключение режимов остаётся в XAML, не уезжает в web.

При смене mode XAML просто показывает другой WebView2 host (Chats или Projects). Snapshot dispatch меняется на соответствующий controller.

---

## Готовые артефакты

- `docs/projects-web/project_concept_full.html` — полный UI прототип (List + Home + WorkCycle)
- `UI/Web/Chats/chats.surface.html` — bridge pattern референс (HTML)
- `UI/Web/Chats/chats.bridge.js` — bridge pattern референс (JS) — ~800 строк, изучить patchConversation, applyLocalizedText, scroll preservation
- `UI/Modes/Chats/ChatsWebRendererView.xaml.cs` — WebView2 setup референс
- `UI/Modes/Chats/ChatsRuntimeController.cs` — BuildSnapshot pattern референс
- `UI/Modes/Chats/ChatsWebBridgeModels.cs` — records pattern референс
- `UI/Rendering/Conversation/` — все shared типы
- `Flow/StepPhaseProjectionBuilder.cs` — visibility rules для Work Cycle surfaces
- `UI/Modes/Projects/WorkCycle/Actions/WorkCycleActionController.cs` — action entry points
- `UI/Modes/Projects/Projections/ProjectsShellProjection.cs` — Home + List state
- `Execution/WorkspaceEvidenceArtifactRuntimeService.cs` — preview.html генерация (всех 5 docs уже)
- `docs/projects-web/памятка.txt` — главный гайд по shared engine

---

## Контрольный чек перед каждым шагом

- [ ] Использую существующий тип/сервис вместо нового?
- [ ] Все строки через AppText?
- [ ] Truth идёт из C# в JS, не наоборот?
- [ ] Нет дубля паттерна который уже есть в Chats?
- [ ] Атомарность submission соблюдена?
- [ ] Keyed patching, не full rerender?
