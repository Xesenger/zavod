using System;
using System.Collections.Generic;
using zavod.Persistence;

namespace zavod.Orchestration;

// Builder for Work Packet status fields per project_work_packet_v1.md.
//
// Pure functions. No IO. Given a ProjectDocumentSourceSelection (from
// ProjectDocumentRuntimeService.SelectSources), produces:
//   - CanonicalDocsStatus: per-kind {Absent|Preview|Canonical} state
//   - PreviewStatus: present only when canonical < 5/5
//   - MissingTruthWarnings: honest gap list the model must see
//
// Staleness is not detected here (ProjectDocumentSourceSelection does
// not currently carry a stale flag). When staleness becomes available,
// caller may post-process CanonicalDocsStatus to replace Canonical with
// Stale for specific kinds.
public static class WorkPacketBuilder
{
    public static CanonicalDocsStatus BuildCanonicalDocsStatus(ProjectDocumentSourceSelection selection)
    {
        ArgumentNullException.ThrowIfNull(selection);
        return new CanonicalDocsStatus(
            Classify(selection.ProjectDocument),
            Classify(selection.DirectionDocument),
            Classify(selection.RoadmapDocument),
            Classify(selection.CanonDocument),
            Classify(selection.CapsuleDocument));
    }

    public static PreviewStatus? BuildPreviewStatus(ProjectDocumentSourceSelection selection)
    {
        ArgumentNullException.ThrowIfNull(selection);
        var kinds = new List<ProjectDocumentKind>();
        AppendIfPreview(kinds, ProjectDocumentKind.Project, selection.ProjectDocument);
        AppendIfPreview(kinds, ProjectDocumentKind.Direction, selection.DirectionDocument);
        AppendIfPreview(kinds, ProjectDocumentKind.Roadmap, selection.RoadmapDocument);
        AppendIfPreview(kinds, ProjectDocumentKind.Canon, selection.CanonDocument);
        AppendIfPreview(kinds, ProjectDocumentKind.Capsule, selection.CapsuleDocument);
        if (kinds.Count == 0)
        {
            return null;
        }
        return new PreviewStatus(kinds);
    }

    public static IReadOnlyList<string> BuildMissingTruthWarnings(CanonicalDocsStatus status)
    {
        ArgumentNullException.ThrowIfNull(status);
        var warnings = new List<string>();
        AddWarningIfMissing(warnings, "project.md", status.Project);
        AddWarningIfMissing(warnings, "direction.md", status.Direction);
        AddWarningIfMissing(warnings, "roadmap.md", status.Roadmap);
        AddWarningIfMissing(warnings, "canon.md", status.Canon);
        AddWarningIfMissing(warnings, "capsule.md", status.Capsule);
        return warnings;
    }

    private static DocumentCanonicalState Classify(ProjectDocumentSourceDescriptor? descriptor)
    {
        if (descriptor is null || !descriptor.Exists)
        {
            return DocumentCanonicalState.Absent;
        }
        return descriptor.Stage switch
        {
            ProjectDocumentStage.CanonicalDocs => DocumentCanonicalState.Canonical,
            ProjectDocumentStage.PreviewDocs => DocumentCanonicalState.Preview,
            ProjectDocumentStage.ImportPreview => DocumentCanonicalState.Preview,
            _ => DocumentCanonicalState.Absent
        };
    }

    private static void AppendIfPreview(
        List<ProjectDocumentKind> kinds,
        ProjectDocumentKind kind,
        ProjectDocumentSourceDescriptor? descriptor)
    {
        if (descriptor is null || !descriptor.Exists)
        {
            return;
        }
        if (descriptor.Stage != ProjectDocumentStage.CanonicalDocs)
        {
            kinds.Add(kind);
        }
    }

    private static void AddWarningIfMissing(List<string> warnings, string docName, DocumentCanonicalState state)
    {
        switch (state)
        {
            case DocumentCanonicalState.Absent:
                warnings.Add($"{docName} absent: do not invent content for this kind.");
                break;
            case DocumentCanonicalState.Preview:
                warnings.Add($"{docName} available as preview only, not canonical: treat as below-canonical.");
                break;
            case DocumentCanonicalState.Stale:
                warnings.Add($"{docName} marked stale: content may be falsified by recent changes.");
                break;
        }
    }
}
