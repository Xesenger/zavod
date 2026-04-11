using System;

namespace zavod.Persistence;

public enum ProjectDocumentStage
{
    ImportPreview = 0,
    PreviewDocs = 1,
    CanonicalDocs = 2
}

public enum ProjectDocumentKind
{
    Project = 0,
    Direction = 1,
    Roadmap = 2,
    Canon = 3,
    Capsule = 4
}

public sealed record ProjectDocumentSourceDescriptor(
    ProjectDocumentKind Kind,
    ProjectDocumentStage Stage,
    string Path,
    bool Exists);

public sealed record ProjectDocumentSourceSelection(
    ProjectDocumentStage ActiveStage,
    ProjectDocumentSourceDescriptor? ProjectDocument,
    ProjectDocumentSourceDescriptor? DirectionDocument,
    ProjectDocumentSourceDescriptor? RoadmapDocument,
    ProjectDocumentSourceDescriptor? CanonDocument,
    ProjectDocumentSourceDescriptor? CapsuleDocument);

public sealed record ProjectDocumentReadResult(
    ProjectDocumentKind Kind,
    ProjectDocumentStage Stage,
    string Path,
    bool Exists,
    string Markdown);
