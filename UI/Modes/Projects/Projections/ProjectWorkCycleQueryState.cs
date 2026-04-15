using System;
using System.IO;
using System.Linq;
using zavod.Bootstrap;
using zavod.Execution;
using zavod.Persistence;
using zavod.Workspace;

namespace zavod.UI.Modes.Projects.Projections;

public sealed record ProjectWorkCycleQueryState(
    ProjectEntryResult Entry,
    ProjectDocumentSourceSelection DocumentSelection,
    ProjectDocumentReadResult ProjectDocument,
    ProjectDocumentReadResult CapsuleDocument,
    ResumeStageSnapshot? ResumeSnapshot,
    WorkspaceScanResult Scan,
    string? PreferredHtmlPath)
{
    public ProjectState ProjectState => Entry.ProjectState;
    public string ProjectId => Entry.ProjectState.ProjectId;
    public string ProjectName => Entry.ProjectState.ProjectName;
    public string ProjectRoot => Entry.ProjectState.Paths.ProjectRoot;
    public string? ActiveShiftId => Entry.ProjectState.ActiveShiftId;
    public string? ActiveTaskId => Entry.ProjectState.ActiveTaskId;
    public bool HasActiveShift => ActiveShiftId is not null;
    public bool HasActiveTask => ActiveTaskId is not null;
    public bool HasProjectDocument => ProjectDocument.Exists;
    public bool HasCapsuleDocument => CapsuleDocument.Exists;
    public bool RuntimeStatePresent => ResumeSnapshot?.RuntimeState is not null;
    public ProjectDocumentStage DocumentStage => DocumentSelection.ActiveStage;
}

public static class ProjectWorkCycleQueryStateBuilder
{
    public static ProjectWorkCycleQueryState Build(string projectRoot)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(projectRoot);

        var normalizedRoot = Path.GetFullPath(projectRoot);
        var state = LoadOrInitializeProjectState(normalizedRoot);
        var entry = ProjectEntryResolver.Resolve(state);
        var documentRuntime = new ProjectDocumentRuntimeService();
        var documentSelection = documentRuntime.SelectSources(normalizedRoot);
        var projectDocument = documentRuntime.Read(normalizedRoot, ProjectDocumentKind.Project);
        var capsuleDocument = documentRuntime.Read(normalizedRoot, ProjectDocumentKind.Capsule);
        var normalizedResume = ResumeStageNormalizer.Normalize(
            ResumeStageStorage.Load(normalizedRoot),
            hasActiveWork: state.ActiveTaskId is not null,
            preserveLiveRuntimePhase: true,
            hasActiveShift: state.ActiveShiftId is not null);
        var scan = WorkspaceScanner.Scan(new WorkspaceScanRequest(normalizedRoot));

        return new ProjectWorkCycleQueryState(
            entry,
            documentSelection,
            projectDocument,
            capsuleDocument,
            normalizedResume,
            scan,
            ResolvePreferredProjectHtmlPath(normalizedRoot));
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
