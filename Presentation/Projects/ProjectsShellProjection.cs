using System;
using System.IO;
using System.Linq;
using zavod.Bootstrap;
using zavod.Execution;
using zavod.Persistence;
using zavod.Workspace;

namespace zavod.Presentation.Projects;

public sealed record ProjectsShellProjection(
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
        ArgumentException.ThrowIfNullOrWhiteSpace(projectRoot);

        var normalizedRoot = Path.GetFullPath(projectRoot);
        var state = LoadOrInitializeProjectState(normalizedRoot);
        var entry = ProjectEntryResolver.Resolve(state);
        var documentRuntime = new ProjectDocumentRuntimeService();
        var projectDocument = documentRuntime.Read(normalizedRoot, ProjectDocumentKind.Project);
        var capsuleDocument = documentRuntime.Read(normalizedRoot, ProjectDocumentKind.Capsule);
        var documentSelection = documentRuntime.SelectSources(normalizedRoot);
        var scan = WorkspaceScanner.Scan(new WorkspaceScanRequest(normalizedRoot));
        var preferredHtmlPath = ResolvePreferredProjectHtmlPath(normalizedRoot);

        return new ProjectsShellProjection(
            ProjectName: state.ProjectName,
            ProjectRoot: normalizedRoot,
            EntryStateText: $"Project entry: {entry.Mode}",
            StorageStateText: Directory.Exists(state.Paths.ZavodRoot)
                ? $".zavod initialized at {state.Paths.ZavodRoot}"
                : ".zavod storage root is missing",
            ActiveShiftText: $"Active shift: {state.ActiveShiftId ?? "none"}",
            ActiveTaskText: $"Active task: {state.ActiveTaskId ?? "none"}",
            DocumentStageText: BuildDocumentStageText(projectDocument, capsuleDocument),
            ProjectDocumentPathText: projectDocument.Exists
                ? projectDocument.Path
                : "No project document is currently materialized.",
            HasProjectDocument: projectDocument.Exists,
            ProjectDocumentPath: projectDocument.Exists ? projectDocument.Path : null,
            HasCapsuleDocument: capsuleDocument.Exists,
            CapsuleDocumentPath: capsuleDocument.Exists ? capsuleDocument.Path : null,
            ProjectListCurrentProjectText: $"{state.ProjectName}{Environment.NewLine}{normalizedRoot}",
            ProjectListCurrentStageText: $"Current stage: {documentSelection.ActiveStage}. Import kind: {scan.State.ImportKind}.",
            ProjectHomeStatusText: $"Health: {scan.State.Health}. Import kind: {scan.State.ImportKind}.",
            ProjectHomeStageText: $"Document stage: {documentSelection.ActiveStage}. project doc={(projectDocument.Exists ? projectDocument.Stage.ToString() : "missing")}, capsule={(capsuleDocument.Exists ? capsuleDocument.Stage.ToString() : "missing")}.",
            ProjectHomeActivityText: $"Active shift: {state.ActiveShiftId ?? "none"}. Active task: {state.ActiveTaskId ?? "none"}.",
            ProjectHomeMaterialsText: FormatMaterials(scan),
            HasProjectHtml: preferredHtmlPath is not null,
            ProjectHtmlPath: preferredHtmlPath);
    }

    private static ProjectState LoadOrInitializeProjectState(string normalizedRoot)
    {
        try
        {
            return ProjectStateStorage.Load(normalizedRoot);
        }
        catch (ZavodPersistenceException)
        {
            var directoryName = new DirectoryInfo(normalizedRoot).Name;
            var projectName = string.IsNullOrWhiteSpace(directoryName) ? "zavod" : directoryName;
            var projectId = projectName.ToLowerInvariant().Replace(' ', '-');
            return ProjectStateStorage.EnsureInitialized(normalizedRoot, projectId, projectName);
        }
    }

    private static string BuildDocumentStageText(ProjectDocumentReadResult projectDocument, ProjectDocumentReadResult capsuleDocument)
    {
        var projectState = projectDocument.Exists
            ? $"project={projectDocument.Stage}"
            : "project=missing";
        var capsuleState = capsuleDocument.Exists
            ? $"capsule={capsuleDocument.Stage}"
            : "capsule=missing";
        return $"Document state: {projectState}, {capsuleState}";
    }

    private static string FormatMaterials(WorkspaceScanResult scan)
    {
        if (scan.MaterialCandidates.Count == 0)
        {
            return "No user-facing materials were detected by the scanner yet.";
        }

        return string.Join(
            Environment.NewLine,
            scan.MaterialCandidates
                .Take(12)
                .Select(candidate => $"- [{candidate.Kind}] {candidate.RelativePath}"));
    }

    private static string? ResolvePreferredProjectHtmlPath(string projectRoot)
    {
        var preferredPaths = new[]
        {
            Path.Combine(projectRoot, ".zavod", "preview.html"),
            Path.Combine(projectRoot, ".zavod", "import_evidence_bundle", "preview.html")
        };

        return preferredPaths.FirstOrDefault(File.Exists);
    }
}
