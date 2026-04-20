using System.Collections.Generic;
using zavod.UI.Modes.Chats;

namespace zavod.UI.Modes.Projects.Bridge;

public sealed record ProjectsWebStateSnapshot(
    ChatsWebStateSnapshot Conversation,
    string CurrentScreen,
    ProjectsWebProjectList? List,
    ProjectsWebSelectedProject? SelectedProject,
    ProjectsWebProjectHome? Home,
    ProjectsWebWorkCycle? WorkCycle,
    IReadOnlyDictionary<string, string>? Text);

public sealed record ProjectsWebSelectedProject(
    string Id,
    string Name,
    string Description,
    string? PreviewUrl,
    int Files,
    int Anchors,
    int Tasks,
    int Docs,
    IReadOnlyList<ProjectsWebHomeAnchor> AnchorRows,
    IReadOnlyList<ProjectsWebHomeDocument> DocumentRows);

public sealed record ProjectsWebHomeAnchor(
    string Tag,
    string Value);

public sealed record ProjectsWebHomeDocument(
    string Name,
    string Meta);

public sealed record ProjectsWebProjectList(
    IReadOnlyList<ProjectsWebProjectListItem> Projects,
    bool CanImport,
    bool CanCreateNew);

public sealed record ProjectsWebProjectListItem(
    string ProjectId,
    string ProjectName,
    string ProjectRoot,
    string Description,
    string Status,
    string LastActivity,
    IReadOnlyList<string> StackTags,
    int FileCount,
    int AnchorCount);

public sealed record ProjectsWebProjectHome(
    string ProjectId,
    string ProjectName,
    string ProjectRoot,
    string DocumentStage,
    bool HasPreviewHtml,
    string? PreviewHtmlUrl,
    IReadOnlyList<ProjectsWebDocStatus> CanonicalDocs,
    IReadOnlyList<ProjectsWebMaterialItem> Materials,
    string Health,
    string? ActiveShiftId,
    string? ActiveTaskId,
    int FileCount,
    int AnchorCount);

public sealed record ProjectsWebDocStatus(
    string Kind,
    string FileName,
    bool Exists,
    string Stage);

public sealed record ProjectsWebMaterialItem(
    string RelativePath,
    string Kind,
    string? Summary);

public sealed record ProjectsWebWorkCycle(
    string VisualPhase,
    bool ResultVisible,
    string SurfacePhase,
    string ExecutionSubphase,
    string ResultSubphase,
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

public sealed record ProjectsWebExecutionItem(
    string Id,
    string Kind,
    string Label,
    string? Detail,
    bool IsHighlighted,
    string? DiffFile,
    string? DiffTag,
    IReadOnlyList<ProjectsWebDiffLine>? DiffLines,
    string? Caption);

public sealed record ProjectsWebDiffLine(
    string Type,
    int? LineNumber,
    string Content);

public sealed record ProjectsWebPreflightTask(
    int Index,
    string Text,
    string Tag);
