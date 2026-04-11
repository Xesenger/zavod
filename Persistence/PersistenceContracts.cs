using System;

namespace zavod.Persistence;

public sealed record ProjectPaths(
    string ProjectRoot,
    string ZavodRoot,
    string MetaFilePath,
    string ProjectTruthRoot);

public sealed record TruthPointers(
    string ProjectDocumentPath,
    string DirectionDocumentPath,
    string RoadmapDocumentPath,
    string CanonDocumentPath,
    string CapsuleDocumentPath);

public sealed record ProjectState(
    string Version,
    string ProjectId,
    string ProjectName,
    string LayoutVersion,
    string EntryMode,
    string? ActiveShiftId,
    string? ActiveTaskId,
    ProjectPaths Paths,
    TruthPointers TruthPointers)
{
    public bool IsColdStart => ActiveShiftId is null;
}
