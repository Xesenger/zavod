using System;
using System.IO;
using System.Linq;
using zavod.Bootstrap;
using zavod.Persistence;
using zavod.UI.Text;
using zavod.Workspace;

namespace zavod.UI.Modes.Projects.Projections;

public sealed record ProjectsShellProjection(
    string ProjectId,
    string ProjectName,
    string ProjectRoot,
    string EntryStateText,
    string StorageStateText,
    string ActiveShiftText,
    string ActiveTaskText,
    string DocumentStageText,
    string ProjectDocumentPathText,
    bool HasProjectDocument,
    string? ProjectDocumentPath,
    bool HasCapsuleDocument,
    string? CapsuleDocumentPath,
    string ProjectListCurrentProjectText,
    string ProjectListCurrentStageText,
    string ProjectHomeStatusText,
    string ProjectHomeStageText,
    string ProjectHomeActivityText,
    string ProjectHomeMaterialsText,
    bool HasProjectHtml,
    string? ProjectHtmlPath)
{
    public static ProjectsShellProjection Build(string projectRoot)
    {
        return Build(ProjectWorkCycleQueryStateBuilder.Build(projectRoot));
    }

    public static ProjectsShellProjection Build(ProjectWorkCycleQueryState queryState)
    {
        ArgumentNullException.ThrowIfNull(queryState);

        var state = queryState.ProjectState;
        var entry = queryState.Entry;
        var projectDocument = queryState.ProjectDocument;
        var capsuleDocument = queryState.CapsuleDocument;
        var documentSelection = queryState.DocumentSelection;
        var scan = queryState.Scan;
        var normalizedRoot = queryState.ProjectRoot;
        var activeStage = DisplayDocumentStage(documentSelection.ActiveStage);
        var importKind = DisplayImportKind(scan.State.ImportKind);
        var health = DisplayHealth(scan.State.Health);
        var projectDocumentStage = projectDocument.Exists ? DisplayDocumentStage(projectDocument.Stage) : AppText.Current.Get("projects.token.missing");
        var capsuleDocumentStage = capsuleDocument.Exists ? DisplayDocumentStage(capsuleDocument.Stage) : AppText.Current.Get("projects.token.missing");

        return new ProjectsShellProjection(
            ProjectId: state.ProjectId,
            ProjectName: state.ProjectName,
            ProjectRoot: normalizedRoot,
            EntryStateText: AppText.Current.Format("projects.shell.entry_state", DisplayEntryMode(entry.Mode)),
            StorageStateText: Directory.Exists(state.Paths.ZavodRoot)
                ? AppText.Current.Format("projects.shell.storage_initialized", state.Paths.ZavodRoot)
                : AppText.Current.Get("projects.shell.storage_missing"),
            ActiveShiftText: AppText.Current.Format("projects.shell.active_shift", state.ActiveShiftId ?? AppText.Current.Get("projects.token.none")),
            ActiveTaskText: AppText.Current.Format("projects.shell.active_task", state.ActiveTaskId ?? AppText.Current.Get("projects.token.none")),
            DocumentStageText: BuildDocumentStageText(projectDocument, capsuleDocument),
            ProjectDocumentPathText: projectDocument.Exists
                ? projectDocument.Path
                : AppText.Current.Get("projects.shell.no_project_document"),
            HasProjectDocument: projectDocument.Exists,
            ProjectDocumentPath: projectDocument.Exists ? projectDocument.Path : null,
            HasCapsuleDocument: capsuleDocument.Exists,
            CapsuleDocumentPath: capsuleDocument.Exists ? capsuleDocument.Path : null,
            ProjectListCurrentProjectText: $"{state.ProjectName}{Environment.NewLine}{normalizedRoot}",
            ProjectListCurrentStageText: AppText.Current.Format("projects.shell.current_stage", activeStage, importKind),
            ProjectHomeStatusText: AppText.Current.Format("projects.shell.health", health, importKind),
            ProjectHomeStageText: AppText.Current.Format("projects.shell.home_stage", activeStage, projectDocumentStage, capsuleDocumentStage),
            ProjectHomeActivityText: AppText.Current.Format("projects.shell.home_activity", state.ActiveShiftId ?? AppText.Current.Get("projects.token.none"), state.ActiveTaskId ?? AppText.Current.Get("projects.token.none")),
            ProjectHomeMaterialsText: FormatMaterials(scan),
            HasProjectHtml: queryState.PreferredHtmlPath is not null,
            ProjectHtmlPath: queryState.PreferredHtmlPath);
    }

    private static string BuildDocumentStageText(ProjectDocumentReadResult projectDocument, ProjectDocumentReadResult capsuleDocument)
    {
        var projectState = projectDocument.Exists
            ? $"project={projectDocument.Stage}"
            : $"project={AppText.Current.Get("projects.token.missing")}";
        var capsuleState = capsuleDocument.Exists
            ? $"capsule={capsuleDocument.Stage}"
            : $"capsule={AppText.Current.Get("projects.token.missing")}";
        return AppText.Current.Format("projects.shell.document_state", projectState, capsuleState);
    }

    private static string FormatMaterials(WorkspaceScanResult scan)
    {
        if (scan.MaterialCandidates.Count == 0)
        {
            return AppText.Current.Get("projects.shell.materials_empty");
        }

        return string.Join(
            Environment.NewLine,
            scan.MaterialCandidates
                .Take(12)
                .Select(candidate => $"- [{DisplayMaterialKind(candidate.Kind)}] {candidate.RelativePath}"));
    }

    private static string DisplayImportKind(WorkspaceImportKind importKind)
    {
        return importKind switch
        {
            WorkspaceImportKind.Empty => AppText.Current.Get("projects.enum.import.empty"),
            WorkspaceImportKind.SourceProject => AppText.Current.Get("projects.enum.import.source_project"),
            WorkspaceImportKind.NonSourceImport => AppText.Current.Get("projects.enum.import.non_source"),
            WorkspaceImportKind.MixedImport => AppText.Current.Get("projects.enum.import.mixed"),
            _ => importKind.ToString()
        };
    }

    private static string DisplayHealth(WorkspaceHealthStatus health)
    {
        return health switch
        {
            WorkspaceHealthStatus.Healthy => AppText.Current.Get("projects.enum.health.healthy"),
            WorkspaceHealthStatus.Missing => AppText.Current.Get("projects.enum.health.missing"),
            WorkspaceHealthStatus.Unavailable => AppText.Current.Get("projects.enum.health.unavailable"),
            WorkspaceHealthStatus.BrokenStructure => AppText.Current.Get("projects.enum.health.broken_structure"),
            WorkspaceHealthStatus.ScanPending => AppText.Current.Get("projects.enum.health.scan_pending"),
            WorkspaceHealthStatus.ScanFailed => AppText.Current.Get("projects.enum.health.scan_failed"),
            _ => health.ToString()
        };
    }

    private static string DisplayDocumentStage(ProjectDocumentStage stage)
    {
        return stage switch
        {
            ProjectDocumentStage.ImportPreview => AppText.Current.Get("projects.enum.document_stage.import_preview"),
            ProjectDocumentStage.PreviewDocs => AppText.Current.Get("projects.enum.document_stage.preview_docs"),
            ProjectDocumentStage.CanonicalDocs => AppText.Current.Get("projects.enum.document_stage.canonical_docs"),
            _ => stage.ToString()
        };
    }

    private static string DisplayMaterialKind(WorkspaceMaterialKind kind)
    {
        return kind switch
        {
            WorkspaceMaterialKind.TextDocument => AppText.Current.Get("projects.enum.material.text_document"),
            WorkspaceMaterialKind.PdfDocument => AppText.Current.Get("projects.enum.material.pdf_document"),
            WorkspaceMaterialKind.OfficeDocument => AppText.Current.Get("projects.enum.material.office_document"),
            WorkspaceMaterialKind.Spreadsheet => AppText.Current.Get("projects.enum.material.spreadsheet"),
            WorkspaceMaterialKind.Presentation => AppText.Current.Get("projects.enum.material.presentation"),
            WorkspaceMaterialKind.ImageAsset => AppText.Current.Get("projects.enum.material.image_asset"),
            WorkspaceMaterialKind.ArchiveArtifact => AppText.Current.Get("projects.enum.material.archive_artifact"),
            WorkspaceMaterialKind.Multimedia => AppText.Current.Get("projects.enum.material.multimedia"),
            _ => kind.ToString()
        };
    }

    private static string DisplayEntryMode(ProjectEntryMode mode)
    {
        return mode switch
        {
            ProjectEntryMode.Bootstrap => AppText.Current.Get("projects.enum.entry_mode.bootstrap"),
            ProjectEntryMode.Resume => AppText.Current.Get("projects.enum.entry_mode.resume"),
            _ => mode.ToString()
        };
    }
}
