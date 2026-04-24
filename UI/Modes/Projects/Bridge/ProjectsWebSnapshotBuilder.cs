using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using zavod.Execution;
using zavod.Flow;
using zavod.Orchestration;
using zavod.Persistence;
using zavod.UI.Modes.Projects.Projections;
using zavod.UI.Text;
using zavod.Welcoming;

namespace zavod.UI.Modes.Projects.Bridge;

/// <summary>
/// Builds <see cref="ProjectsWebStateSnapshot"/> envelopes for the Projects WebView host.
///
/// Pass 1 step 3a: structurally valid empty snapshot with the localized text dictionary
/// fully populated from <see cref="AppText"/>.
/// Pass 1 step 3b: stateful — tracks the current visible screen so navigation intents
/// from JS can flip it and push a new snapshot.
/// Pass 1 step 3c: composes the conversation snapshot from the existing
/// <see cref="ProjectsRuntimeController.BuildSnapshot"/>. Composer / attachments /
/// long-text-artifact paths now flow through the same shared engine that Chats uses.
/// Project list / home / work cycle payloads stay null until later steps.
/// </summary>
internal sealed class ProjectsWebSnapshotBuilder
{
    private readonly ProjectsRuntimeController _controller;

    public ProjectsWebSnapshotBuilder(ProjectsRuntimeController controller)
    {
        _controller = controller ?? throw new ArgumentNullException(nameof(controller));
        _selectedProjectId = ProjectRegistryStorage.Load().LastOpenedProjectId;
        if (!string.IsNullOrWhiteSpace(_selectedProjectId))
        {
            var entry = ProjectRegistryStorage.Load().Projects
                .FirstOrDefault(p => string.Equals(p.Id, _selectedProjectId, StringComparison.Ordinal));
            if (entry is not null && Directory.Exists(entry.RootPath))
            {
                ProjectWorkCycleQueryStateBuilder.RecoverCrashOrphan(entry.RootPath);
            }
        }
    }

    /// <summary>
    /// All <c>data-l10n</c> keys consumed by <c>projects.surface.html</c>.
    /// Keep in sync with new keys added to the HTML prototype.
    /// </summary>
    private static readonly string[] WebLocalizationKeys =
    {
        "projects.title",
        "projects.phase.list",
        "projects.phase.home",
        "projects.phase.workCycle",
        "projects.list.title",
        "projects.list.import",
        "projects.list.new",
        "projects.status.active",
        "projects.status.review",
        "projects.status.idle",
        "projects.nav.toList",
        "projects.nav.toHome",
        "projects.stats.files",
        "projects.stats.anchors",
        "projects.stats.tasks",
        "projects.stats.docs",
        "projects.home.currentLabel",
        "projects.home.enterWork",
        "projects.home.scannerAnalysis",
        "projects.home.truthDocs",
        "projects.home.nextActions",
        "projects.home.missingTruth",
        "projects.home.userDocs",
        "projects.welcome.action.review_preview_docs",
        "projects.welcome.action.promote_preview_to_canonical",
        "projects.welcome.action.author_canonical_doc",
        "projects.welcome.action.start_work_cycle",
        "projects.welcome.action.continue_work_cycle",
        "projects.welcome.action.review_project_audit",
        "projects.welcome.action.review_stale_sections",
        "projects.welcome.action.import_retry",
        "projects.welcome.action.reject_preview",
        "projects.welcome.action.open_roadmap",
        "projects.welcome.action.open_direction",
        "projects.welcome.detail.review_preview_docs",
        "projects.welcome.detail.promote_preview_to_canonical",
        "projects.welcome.detail.author_canonical_doc",
        "projects.welcome.detail.start_work_cycle",
        "projects.welcome.detail.continue_work_cycle",
        "projects.welcome.detail.review_project_audit",
        "projects.welcome.detail.review_stale_sections",
        "projects.welcome.detail.import_retry",
        "projects.welcome.detail.reject_preview",
        "projects.welcome.detail.open_roadmap",
        "projects.welcome.detail.open_direction",
        "projects.report.label",
        "projects.report.open",
        "projects.report.modal.label",
        "projects.report.modal.close",
        "projects.anchor.tag.file",
        "projects.anchor.tag.problem",
        "projects.anchor.tag.dependency",
        "projects.anchor.conf.confirmed",
        "projects.anchor.conf.likely",
        "projects.docs.category.text",
        "projects.docs.category.pdf",
        "projects.docs.category.html",
        "projects.docs.promote",
        "projects.docs.canonical",
        "projects.docs.absent",
        "projects.composer.placeholder",
        "projects.composer.intent",
        "projects.composer.intent.kbd",
        "projects.preflight.kicker",
        "projects.preflight.title",
        "projects.preflight.confirm",
        "projects.preflight.clarify",
        "projects.preflight.back",
        "projects.preflight.clarify.label",
        "projects.preflight.clarify.placeholder",
        "projects.preflight.clarify.send",
        "projects.worker.label",
        "projects.artifact.code",
        "projects.artifact.preview",
        "projects.artifact.waiting",
        "projects.artifact.refresh",
        "projects.artifact.share",
        "projects.action.tasks",
        "projects.action.files",
        "projects.action.errors",
        "projects.action.revise.placeholder",
        "projects.action.revise.send",
        "projects.action.revise",
        "projects.action.reject",
        "projects.action.confirm",
        "projects.newProject.title",
        "projects.newProject.nameLabel",
        "projects.newProject.namePlaceholder",
        "projects.newProject.kindLabel",
        "projects.newProject.pathLabel",
        "projects.newProject.pathHint",
        "projects.newProject.cancel",
        "projects.newProject.create",
        "projects.kind.generic",
        "projects.kind.webFrontend",
        "projects.kind.webBackend",
        "projects.kind.game",
        "projects.kind.library",
        "projects.kind.mobile",
        "projects.kind.cli",
        "projects.kind.docs"
    };

    /// <summary>Virtual host name registered in <c>ProjectsWebRendererView</c> for the
    /// currently selected project's root directory. Iframe <c>src</c> for preview.html
    /// is built against this host.</summary>
    public const string SelectedProjectVirtualHost = "projectfiles.zavod";

    private string _currentScreen = "list";
    private string? _selectedProjectId;

    /// <summary>Currently visible screen (<c>"list" | "home" | "work-cycle"</c>).</summary>
    public string CurrentScreen => _currentScreen;

    /// <summary>Identifier of the project the user last selected from the list.</summary>
    public string? SelectedProjectId => _selectedProjectId;

    /// <summary>Switches the current screen. Returns true when the value actually changed.</summary>
    public bool NavigateTo(string screen)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(screen);
        if (string.Equals(_currentScreen, screen, StringComparison.Ordinal))
        {
            return false;
        }

        _currentScreen = screen;
        return true;
    }

    /// <summary>
    /// Marks a project selection: stores the id so the next <see cref="Build"/> can attach
    /// real metadata (name / path / preview link) and switches the screen to <c>"home"</c>.
    /// Returns true when either the selection or the screen actually changed.
    /// </summary>
    public bool SelectProject(string projectId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(projectId);
        var idChanged = !string.Equals(_selectedProjectId, projectId, StringComparison.Ordinal);
        if (idChanged)
        {
            _selectedProjectId = projectId;
            var entry = ProjectRegistryStorage.Load().Projects
                .FirstOrDefault(p => string.Equals(p.Id, projectId, StringComparison.Ordinal));
            if (entry is not null && Directory.Exists(entry.RootPath))
            {
                ProjectWorkCycleQueryStateBuilder.RecoverCrashOrphan(entry.RootPath);
            }
        }
        var screenChanged = NavigateTo("home");
        return idChanged || screenChanged;
    }

    /// <summary>
    /// Builds a snapshot reflecting the current builder state and active <see cref="AppText"/>.
    /// The conversation portion is sourced from the shared
    /// <see cref="ProjectsRuntimeController.BuildSnapshot"/> so that messages, composer
    /// pending attachments, and chat windowing all flow through the same engine that
    /// powers Chats. Locale dictionary is rebuilt every call so runtime language switches
    /// surface without cache invalidation logic.
    /// </summary>
    public ProjectsWebStateSnapshot Build()
    {
        var conversation = _controller.BuildSnapshot();
        var registry = ProjectRegistryStorage.Load();
        var list = BuildProjectListPayload(registry);
        var selected = BuildSelectedProjectPayload(registry);

        return new ProjectsWebStateSnapshot(
            Conversation: conversation,
            CurrentScreen: _currentScreen,
            List: list,
            SelectedProject: selected,
            Home: null,
            WorkCycle: BuildWorkCyclePayload(),
            Text: BuildProjectsLocalizedDictionary());
    }

    /// <summary>
    /// Resolves the currently selected project to a minimal display payload. Stats stay
    /// at zero placeholders until a later slice wires real per-project scanning. The
    /// preview URL points at the selected project's <c>.zavod/preview.html</c> via the
    /// <see cref="SelectedProjectVirtualHost"/> mapping (registered by the renderer view
    /// when selection changes); null when the file is missing.
    /// </summary>
    private ProjectsWebSelectedProject? BuildSelectedProjectPayload(ProjectRegistry registry)
    {
        if (string.IsNullOrWhiteSpace(_selectedProjectId))
        {
            return null;
        }

        var entry = registry.Projects.FirstOrDefault(item =>
            string.Equals(item.Id, _selectedProjectId, StringComparison.Ordinal));
        if (entry is null)
        {
            return null;
        }

        var previewPath = Path.Combine(entry.RootPath, ".zavod", "preview.html");
        var previewUrl = File.Exists(previewPath)
            ? $"https://{SelectedProjectVirtualHost}/.zavod/preview.html"
            : null;

        var stats = ReadProjectStats(entry.RootPath);
        var snippets = ReadEvidenceSnippets(entry.RootPath);
        var welcome = BuildWelcomePayload(entry.RootPath);

        return new ProjectsWebSelectedProject(
            Id: entry.Id,
            Name: entry.Name,
            Description: entry.RootPath,
            PreviewUrl: previewUrl,
            Files: stats.Files,
            Anchors: stats.Anchors,
            Tasks: stats.Tasks,
            Docs: stats.Docs,
            AnchorRows: BuildAnchorRows(snippets),
            DocumentRows: BuildDocumentRows(snippets),
            CanonicalDocs: BuildCanonicalDocRows(entry.RootPath),
            WelcomeRule: welcome.Rule,
            CanonicalDocCount: welcome.CanonicalCount,
            PreviewDocCount: welcome.PreviewCount,
            WelcomeActions: welcome.Actions,
            MissingTruthWarnings: welcome.MissingTruthWarnings);
    }

    private sealed record WelcomePayload(
        string Rule,
        int CanonicalCount,
        int PreviewCount,
        IReadOnlyList<ProjectsWebWelcomeAction> Actions,
        IReadOnlyList<string> MissingTruthWarnings);

    private static WelcomePayload BuildWelcomePayload(string projectRoot)
    {
        var documentRuntime = new ProjectDocumentRuntimeService();
        var selection = documentRuntime.SelectSources(projectRoot);
        var status = WorkPacketBuilder.BuildCanonicalDocsStatus(selection);
        var preview = WorkPacketBuilder.BuildPreviewStatus(selection);
        var state = TryLoadProjectState(projectRoot);
        var actionSet = WelcomeSurfaceSelector.Select(new WelcomeStateInput(
            selection,
            HasActiveShift: state?.ActiveShiftId is not null,
            HasActiveTask: state?.ActiveTaskId is not null,
            HasStaleSections: false,
            HasImportFailure: false));

        return new WelcomePayload(
            Rule: actionSet.PrimaryRule.ToString(),
            CanonicalCount: status.CanonicalCount,
            PreviewCount: preview?.PreviewKinds.Count ?? 0,
            Actions: actionSet.Actions.Select(BuildWelcomeAction).ToArray(),
            MissingTruthWarnings: WorkPacketBuilder.BuildMissingTruthWarnings(status));
    }

    private static ProjectState? TryLoadProjectState(string projectRoot)
    {
        try
        {
            return ProjectStateStorage.Load(projectRoot);
        }
        catch (ZavodPersistenceException)
        {
            return null;
        }
    }

    private static ProjectsWebWelcomeAction BuildWelcomeAction(WelcomeAction action)
    {
        var key = GetWelcomeActionKey(action);
        return new ProjectsWebWelcomeAction(
            Action: key,
            Label: AppText.Current.Get($"projects.welcome.action.{key}"),
            Detail: AppText.Current.Get($"projects.welcome.detail.{key}"),
            IsWired: IsWelcomeActionWired(action));
    }

    private static bool IsWelcomeActionWired(WelcomeAction action)
    {
        return action is WelcomeAction.ReviewPreviewDocs
            or WelcomeAction.PromotePreviewToCanonical
            or WelcomeAction.StartWorkCycle
            or WelcomeAction.ContinueWorkCycle
            or WelcomeAction.ReviewProjectAudit
            or WelcomeAction.ImportRetry
            or WelcomeAction.RejectPreview;
    }

    private static string GetWelcomeActionKey(WelcomeAction action)
    {
        return action switch
        {
            WelcomeAction.ReviewPreviewDocs => "review_preview_docs",
            WelcomeAction.PromotePreviewToCanonical => "promote_preview_to_canonical",
            WelcomeAction.AuthorCanonicalDoc => "author_canonical_doc",
            WelcomeAction.StartWorkCycle => "start_work_cycle",
            WelcomeAction.ContinueWorkCycle => "continue_work_cycle",
            WelcomeAction.ReviewProjectAudit => "review_project_audit",
            WelcomeAction.ReviewStaleSections => "review_stale_sections",
            WelcomeAction.ImportRetry => "import_retry",
            WelcomeAction.RejectPreview => "reject_preview",
            WelcomeAction.OpenRoadmap => "open_roadmap",
            WelcomeAction.OpenDirection => "open_direction",
            _ => throw new InvalidOperationException($"Unhandled WelcomeAction: {action}.")
        };
    }

    private static IReadOnlyList<ProjectsWebDocStatus> BuildCanonicalDocRows(string projectRoot)
    {
        var projectDir = Path.Combine(projectRoot, ".zavod", "project");
        var previewDir = Path.Combine(projectRoot, ".zavod", "preview_docs");
        return new[]
        {
            BuildDocStatus("project", "project.md", Path.Combine(projectDir, "project.md"), Path.Combine(previewDir, "preview_project.md")),
            BuildDocStatus("direction", "direction.md", Path.Combine(projectDir, "direction.md"), Path.Combine(previewDir, "preview_direction.md")),
            BuildDocStatus("roadmap", "roadmap.md", Path.Combine(projectDir, "roadmap.md"), Path.Combine(previewDir, "preview_roadmap.md")),
            BuildDocStatus("canon", "canon.md", Path.Combine(projectDir, "canon.md"), Path.Combine(previewDir, "preview_canon.md")),
            BuildDocStatus("capsule", "capsule.md", Path.Combine(projectDir, "capsule.md"), Path.Combine(previewDir, "preview_capsule.md"))
        };
    }

    private static ProjectsWebDocStatus BuildDocStatus(string kind, string fileName, string canonicalPath, string previewPath)
    {
        var canonicalExists = File.Exists(canonicalPath);
        var previewExists = File.Exists(previewPath);
        var stage = canonicalExists
            ? "canonical"
            : previewExists
                ? "preview"
                : "absent";

        return new ProjectsWebDocStatus(
            kind,
            fileName,
            canonicalExists,
            stage,
            CanPromote: !canonicalExists && previewExists,
            CanReject: !canonicalExists && previewExists);
    }

    private static readonly string[] DocumentCategories =
    {
        "TextDocument",
        "PdfDocument",
        "OfficeDocument",
        "TextHumanContent"
    };

    private readonly record struct EvidenceSnippet(string RelativePath, string Category);

    /// <summary>
    /// Reads the importer's evidence_snippets.json, returning each entry as a tiny
    /// (path, category) pair. Defensive: malformed file → empty list, never throws.
    /// </summary>
    private static IReadOnlyList<EvidenceSnippet> ReadEvidenceSnippets(string projectRoot)
    {
        var path = Path.Combine(projectRoot, ".zavod", "import_evidence_bundle", "evidence_snippets.json");
        if (!File.Exists(path))
        {
            return Array.Empty<EvidenceSnippet>();
        }

        try
        {
            using var stream = File.OpenRead(path);
            using var document = JsonDocument.Parse(stream);
            if (document.RootElement.ValueKind != JsonValueKind.Array)
            {
                return Array.Empty<EvidenceSnippet>();
            }

            var snippets = new List<EvidenceSnippet>(document.RootElement.GetArrayLength());
            foreach (var item in document.RootElement.EnumerateArray())
            {
                if (item.ValueKind != JsonValueKind.Object)
                {
                    continue;
                }

                var rel = item.TryGetProperty("RelativePath", out var pathNode) && pathNode.ValueKind == JsonValueKind.String
                    ? pathNode.GetString()
                    : null;
                var category = item.TryGetProperty("Category", out var catNode) && catNode.ValueKind == JsonValueKind.String
                    ? catNode.GetString()
                    : null;

                if (string.IsNullOrWhiteSpace(rel) || string.IsNullOrWhiteSpace(category))
                {
                    continue;
                }

                snippets.Add(new EvidenceSnippet(rel, category));
            }

            return snippets;
        }
        catch
        {
            return Array.Empty<EvidenceSnippet>();
        }
    }

    /// <summary>
    /// Anchors = non-document evidence snippets (build configs, source roles,
    /// dependency hints). Top 12 by file order — enough for Home preview.
    /// </summary>
    private static IReadOnlyList<ProjectsWebHomeAnchor> BuildAnchorRows(IReadOnlyList<EvidenceSnippet> snippets)
    {
        return snippets
            .Where(static s => !DocumentCategories.Contains(s.Category, StringComparer.OrdinalIgnoreCase))
            .Take(12)
            .Select(static s => new ProjectsWebHomeAnchor(
                Tag: s.Category.ToLowerInvariant(),
                Value: s.RelativePath))
            .ToArray();
    }

    /// <summary>
    /// User documents = doc-flavoured evidence snippets (markdown, txt, pdf, office).
    /// Meta line is the relative directory the file lives in.
    /// </summary>
    private static IReadOnlyList<ProjectsWebHomeDocument> BuildDocumentRows(IReadOnlyList<EvidenceSnippet> snippets)
    {
        return snippets
            .Where(static s => DocumentCategories.Contains(s.Category, StringComparer.OrdinalIgnoreCase))
            .Take(12)
            .Select(static s =>
            {
                var dir = Path.GetDirectoryName(s.RelativePath) ?? string.Empty;
                var name = Path.GetFileName(s.RelativePath);
                return new ProjectsWebHomeDocument(
                    Name: string.IsNullOrWhiteSpace(name) ? s.RelativePath : name,
                    Meta: string.IsNullOrWhiteSpace(dir) ? "/" : dir.Replace('\\', '/'));
            })
            .ToArray();
    }

    private readonly struct ProjectStats
    {
        public int Files { get; init; }
        public int Anchors { get; init; }
        public int Tasks { get; init; }
        public int Docs { get; init; }
    }

    /// <summary>
    /// Reads the four headline numbers shown in Project Home stats from artifacts the
    /// importer already wrote to <c>&lt;projectRoot&gt;/.zavod/</c>. Defensive: any
    /// missing or malformed file yields zero for that metric, never throws.
    /// Files = source-only count (the asset-heavy total is less interesting at glance).
    /// Anchors = number of evidence snippets the scanner kept.
    /// Tasks = 0 (no shift/task tracking yet for imported projects — placeholder).
    /// Docs = canonical + preview markdown documents materialized so far.
    /// </summary>
    private static ProjectStats ReadProjectStats(string projectRoot)
    {
        var bundle = Path.Combine(projectRoot, ".zavod", "import_evidence_bundle");
        return new ProjectStats
        {
            Files = ReadIntProperty(Path.Combine(bundle, "project_profile.json"), "SourceFileCount"),
            Anchors = ReadJsonArrayLength(Path.Combine(bundle, "evidence_snippets.json")),
            Tasks = 0,
            Docs = CountMarkdownFiles(Path.Combine(projectRoot, ".zavod", "project"))
                 + CountMarkdownFiles(Path.Combine(projectRoot, ".zavod", "preview_docs"))
        };
    }

    private static int ReadIntProperty(string filePath, string propertyName)
    {
        if (!File.Exists(filePath))
        {
            return 0;
        }

        try
        {
            using var stream = File.OpenRead(filePath);
            using var document = JsonDocument.Parse(stream);
            if (document.RootElement.ValueKind == JsonValueKind.Object &&
                document.RootElement.TryGetProperty(propertyName, out var node) &&
                node.TryGetInt32(out var value))
            {
                return value;
            }
        }
        catch
        {
            // Defensive: malformed file should not crash UI snapshot building.
        }

        return 0;
    }

    private static int ReadJsonArrayLength(string filePath)
    {
        if (!File.Exists(filePath))
        {
            return 0;
        }

        try
        {
            using var stream = File.OpenRead(filePath);
            using var document = JsonDocument.Parse(stream);
            if (document.RootElement.ValueKind == JsonValueKind.Array)
            {
                return document.RootElement.GetArrayLength();
            }
        }
        catch
        {
        }

        return 0;
    }

    private static int CountMarkdownFiles(string directory)
    {
        if (!Directory.Exists(directory))
        {
            return 0;
        }

        try
        {
            return Directory.EnumerateFiles(directory, "*.md", SearchOption.TopDirectoryOnly).Count();
        }
        catch
        {
            return 0;
        }
    }

    /// <summary>
    /// Reads the cross-project registry and projects each entry into the web list payload.
    /// Display fields that require deeper project introspection (description, status,
    /// stack tags, file/anchor counts) stay as conservative placeholders until later
    /// passes wire real per-project scanning.
    /// </summary>
    private static ProjectsWebProjectList BuildProjectListPayload(ProjectRegistry registry)
    {
        var items = registry.Projects
            .OrderByDescending(static entry => entry.LastOpenedAt)
            .Select(entry =>
            {
                var stats = ReadProjectStats(entry.RootPath);
                return new ProjectsWebProjectListItem(
                    ProjectId: entry.Id,
                    ProjectName: entry.Name,
                    ProjectRoot: entry.RootPath,
                    Description: entry.RootPath,
                    Status: "idle",
                    LastActivity: entry.LastOpenedAt.ToLocalTime().ToString("yyyy-MM-dd HH:mm"),
                    StackTags: ReadStackTags(entry.RootPath),
                    FileCount: stats.Files,
                    AnchorCount: stats.Anchors);
            })
            .ToArray();

        return new ProjectsWebProjectList(
            Projects: items,
            CanImport: true,
            CanCreateNew: true);
    }

    /// <summary>
    /// Pulls a small flat list of "stack" tags from technical_passport.json so List
    /// cards can show language + build system + framework hints. Empty array when
    /// the passport file is missing or unreadable.
    /// </summary>
    private static IReadOnlyList<string> ReadStackTags(string projectRoot)
    {
        var passportPath = Path.Combine(projectRoot, ".zavod", "import_evidence_bundle", "technical_passport.json");
        if (!File.Exists(passportPath))
        {
            return Array.Empty<string>();
        }

        try
        {
            using var stream = File.OpenRead(passportPath);
            using var document = JsonDocument.Parse(stream);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                return Array.Empty<string>();
            }

            var tags = new List<string>(8);
            CollectStringArray(document.RootElement, "ObservedLanguages", tags);
            CollectStringArray(document.RootElement, "BuildSystems", tags);
            CollectStringArray(document.RootElement, "Frameworks", tags);
            return tags
                .Where(static tag => !string.IsNullOrWhiteSpace(tag))
                .Select(static tag => tag.ToLowerInvariant())
                .Distinct(StringComparer.Ordinal)
                .Take(5)
                .ToArray();
        }
        catch
        {
            return Array.Empty<string>();
        }
    }

    private static void CollectStringArray(JsonElement root, string propertyName, List<string> sink)
    {
        if (!root.TryGetProperty(propertyName, out var node) || node.ValueKind != JsonValueKind.Array)
        {
            return;
        }

        foreach (var item in node.EnumerateArray())
        {
            if (item.ValueKind == JsonValueKind.String)
            {
                var value = item.GetString();
                if (!string.IsNullOrWhiteSpace(value))
                {
                    sink.Add(value);
                }
            }
        }
    }

    /// <summary>
    /// Materializes every <c>data-l10n</c> key the Projects HTML prototype consumes.
    /// JS bridge looks up keys by their dotted form directly (no camelCase mapping).
    /// </summary>
    private static IReadOnlyDictionary<string, string> BuildProjectsLocalizedDictionary()
    {
        var dictionary = new Dictionary<string, string>(WebLocalizationKeys.Length, StringComparer.Ordinal);
        var catalog = AppText.Current;
        foreach (var key in WebLocalizationKeys)
        {
            dictionary[key] = catalog.Get(key);
        }

        return dictionary;
    }

    private ProjectsWebWorkCycle? BuildWorkCyclePayload()
    {
        if (string.IsNullOrWhiteSpace(_selectedProjectId))
        {
            return null;
        }

        var entry = ProjectRegistryStorage.Load().Projects.FirstOrDefault(p => string.Equals(p.Id, _selectedProjectId, StringComparison.Ordinal));
        if (entry is null || !Directory.Exists(entry.RootPath))
        {
            return null;
        }

        try
        {
            var queryState = ProjectWorkCycleQueryStateBuilder.Build(entry.RootPath);
            var shellProjection = ProjectsShellProjection.Build(queryState);
            var workCycle = ProjectWorkCycleProjection.Build(queryState, shellProjection);
            var phase = workCycle.PhaseState;
            var visualPhase = phase.Phase switch
            {
                SurfacePhase.Discussion => "phase-1",
                SurfacePhase.Execution => phase.ExecutionSubphase == ExecutionSubphase.Preflight ? "phase-2" : "phase-3",
                SurfacePhase.Result => "phase-3",
                SurfacePhase.Completed => "phase-3",
                _ => "phase-1"
            };
            var resultVisible = phase.Phase == SurfacePhase.Result || phase.Phase == SurfacePhase.Completed;
            return new ProjectsWebWorkCycle(
                VisualPhase: visualPhase,
                ResultVisible: resultVisible,
                SurfacePhase: phase.Phase.ToString(),
                ExecutionSubphase: phase.ExecutionSubphase.ToString(),
                ResultSubphase: phase.ResultSubphase.ToString(),
                ShowChat: phase.Phase == SurfacePhase.Discussion,
                ShowExecution: phase.Phase == SurfacePhase.Execution,
                ShowResult: resultVisible,
                CanEnterWork: workCycle.Projection.CanStartIntentValidation,
                CanConfirmPreflight: phase.Phase == SurfacePhase.Execution && phase.ExecutionSubphase == ExecutionSubphase.Preflight,
                CanSendMessage: phase.Phase == SurfacePhase.Discussion || (phase.Phase == SurfacePhase.Execution && phase.ExecutionSubphase == ExecutionSubphase.Revision),
                ComposerEnabled: true,
                ExecutionItems: Array.Empty<ProjectsWebExecutionItem>(),
                PreflightTasks: Array.Empty<ProjectsWebPreflightTask>(),
                ValidationSummary: string.IsNullOrWhiteSpace(workCycle.IntentSummary) ? null : workCycle.IntentSummary);
        }
        catch
        {
            return null;
        }
    }
}
