using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using zavod.Persistence;
using zavod.Workspace;

namespace zavod.Execution;

public sealed class ProjectDocumentRuntimeService
{
    public ProjectPreviewDocsArtifacts WritePreviewDocs(
        WorkspaceImportMaterialInterpreterRunResult runResult,
        string projectRootPath)
    {
        ArgumentNullException.ThrowIfNull(runResult);
        ArgumentException.ThrowIfNullOrWhiteSpace(projectRootPath);

        var normalizedProjectRoot = Path.GetFullPath(projectRootPath);
        ProjectDocumentPathResolver.EnsurePreviewDocsRoot(normalizedProjectRoot);

        var previewProjectPath = ProjectDocumentPathResolver.GetPreviewProjectPath(normalizedProjectRoot);
        var previewCapsulePath = ProjectDocumentPathResolver.GetPreviewCapsulePath(normalizedProjectRoot);

        var previewProjectMarkdown = BuildPreviewProjectMarkdown(runResult);
        File.WriteAllText(previewProjectPath, previewProjectMarkdown, Encoding.UTF8);

        var previewCapsuleMarkdown = BuildPreviewCapsuleMarkdown(runResult, previewProjectMarkdown);
        File.WriteAllText(previewCapsulePath, previewCapsuleMarkdown, Encoding.UTF8);

        return new ProjectPreviewDocsArtifacts(
            ProjectDocumentPathResolver.GetPreviewDocsRoot(normalizedProjectRoot),
            previewProjectPath,
            previewCapsulePath);
    }

    public ProjectCanonicalMaterializationResult ConfirmPreviewDocs(string projectRootPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(projectRootPath);

        var normalizedProjectRoot = ResolveEffectiveDocumentRoot(projectRootPath);
        var projectState = EnsureProjectState(normalizedProjectRoot);
        var previewProjectPath = ProjectDocumentPathResolver.GetPreviewProjectPath(normalizedProjectRoot);
        var previewCapsulePath = ProjectDocumentPathResolver.GetPreviewCapsulePath(normalizedProjectRoot);
        if (!File.Exists(previewProjectPath))
        {
            throw new InvalidOperationException("Preview project document must exist before canonical materialization.");
        }

        var previewProjectMarkdown = File.ReadAllText(previewProjectPath, Encoding.UTF8);
        var canonicalProjectMarkdown = BuildCanonicalProjectMarkdown(previewProjectMarkdown);
        File.WriteAllText(projectState.TruthPointers.ProjectDocumentPath, canonicalProjectMarkdown, Encoding.UTF8);

        var previewCapsuleMarkdown = File.Exists(previewCapsulePath)
            ? File.ReadAllText(previewCapsulePath, Encoding.UTF8)
            : string.Empty;
        var canonicalCapsuleMarkdown = BuildCanonicalCapsuleMarkdown(projectState, canonicalProjectMarkdown, previewCapsuleMarkdown);
        File.WriteAllText(projectState.TruthPointers.CapsuleDocumentPath, canonicalCapsuleMarkdown, Encoding.UTF8);

        return new ProjectCanonicalMaterializationResult(
            projectState.TruthPointers.ProjectDocumentPath,
            projectState.TruthPointers.CapsuleDocumentPath);
    }

    public ProjectDocumentSourceSelection SelectSources(string projectRootPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(projectRootPath);

        var normalizedProjectRoot = ResolveEffectiveDocumentRoot(projectRootPath);
        var canonicalProjectPath = Path.Combine(normalizedProjectRoot, ".zavod", "project", "project.md");
        var canonicalDirectionPath = Path.Combine(normalizedProjectRoot, ".zavod", "project", "direction.md");
        var canonicalRoadmapPath = Path.Combine(normalizedProjectRoot, ".zavod", "project", "roadmap.md");
        var canonicalCanonPath = Path.Combine(normalizedProjectRoot, ".zavod", "project", "canon.md");
        var canonicalCapsulePath = Path.Combine(normalizedProjectRoot, ".zavod", "project", "capsule.md");
        var previewProjectPath = ProjectDocumentPathResolver.GetPreviewProjectPath(normalizedProjectRoot);
        var previewDirectionPath = ProjectDocumentPathResolver.GetPreviewDirectionPath(normalizedProjectRoot);
        var previewRoadmapPath = ProjectDocumentPathResolver.GetPreviewRoadmapPath(normalizedProjectRoot);
        var previewCanonPath = ProjectDocumentPathResolver.GetPreviewCanonPath(normalizedProjectRoot);
        var previewCapsulePath = ProjectDocumentPathResolver.GetPreviewCapsulePath(normalizedProjectRoot);
        var importReportPath = ProjectDocumentPathResolver.GetImportReportPath(normalizedProjectRoot);

        if (File.Exists(canonicalProjectPath))
        {
            return new ProjectDocumentSourceSelection(
                ProjectDocumentStage.CanonicalDocs,
                new ProjectDocumentSourceDescriptor(ProjectDocumentKind.Project, ProjectDocumentStage.CanonicalDocs, canonicalProjectPath, Exists: true),
                CreateDescriptor(ProjectDocumentKind.Direction, ProjectDocumentStage.CanonicalDocs, canonicalDirectionPath),
                CreateDescriptor(ProjectDocumentKind.Roadmap, ProjectDocumentStage.CanonicalDocs, canonicalRoadmapPath),
                CreateDescriptor(ProjectDocumentKind.Canon, ProjectDocumentStage.CanonicalDocs, canonicalCanonPath),
                CreateDescriptor(ProjectDocumentKind.Capsule, ProjectDocumentStage.CanonicalDocs, canonicalCapsulePath));
        }

        if (File.Exists(previewProjectPath))
        {
            return new ProjectDocumentSourceSelection(
                ProjectDocumentStage.PreviewDocs,
                new ProjectDocumentSourceDescriptor(ProjectDocumentKind.Project, ProjectDocumentStage.PreviewDocs, previewProjectPath, Exists: true),
                CreateDescriptor(ProjectDocumentKind.Direction, ProjectDocumentStage.PreviewDocs, previewDirectionPath),
                CreateDescriptor(ProjectDocumentKind.Roadmap, ProjectDocumentStage.PreviewDocs, previewRoadmapPath),
                CreateDescriptor(ProjectDocumentKind.Canon, ProjectDocumentStage.PreviewDocs, previewCanonPath),
                CreateDescriptor(ProjectDocumentKind.Capsule, ProjectDocumentStage.PreviewDocs, previewCapsulePath));
        }

        return new ProjectDocumentSourceSelection(
            ProjectDocumentStage.ImportPreview,
            CreateDescriptor(ProjectDocumentKind.Project, ProjectDocumentStage.ImportPreview, importReportPath),
            null,
            null,
            null,
            null);
    }

    public ProjectDocumentReadResult Read(string projectRootPath, ProjectDocumentKind kind)
    {
        var selection = SelectSources(projectRootPath);
        var descriptor = kind switch
        {
            ProjectDocumentKind.Project => selection.ProjectDocument,
            ProjectDocumentKind.Direction => selection.DirectionDocument,
            ProjectDocumentKind.Roadmap => selection.RoadmapDocument,
            ProjectDocumentKind.Canon => selection.CanonDocument,
            ProjectDocumentKind.Capsule => selection.CapsuleDocument,
            _ => null
        };

        if (descriptor is null)
        {
            return new ProjectDocumentReadResult(kind, selection.ActiveStage, string.Empty, Exists: false, string.Empty);
        }

        var markdown = descriptor.Exists ? File.ReadAllText(descriptor.Path, Encoding.UTF8) : string.Empty;
        return new ProjectDocumentReadResult(kind, descriptor.Stage, descriptor.Path, descriptor.Exists, markdown);
    }

    private static ProjectState EnsureProjectState(string projectRootPath)
    {
        var metaPath = Path.Combine(projectRootPath, ".zavod", "meta", "project.json");
        if (File.Exists(metaPath))
        {
            return ProjectStateStorage.Load(projectRootPath);
        }

        var directoryName = new DirectoryInfo(projectRootPath).Name;
        var projectName = string.IsNullOrWhiteSpace(directoryName) ? "ZAVOD Imported Project" : directoryName;
        var projectId = BuildProjectId(projectName);
        return ProjectStateStorage.EnsureInitialized(projectRootPath, projectId, projectName);
    }

    private static string ResolveEffectiveDocumentRoot(string projectRootPath)
    {
        var normalizedRoot = Path.GetFullPath(projectRootPath);
        if (HasAnyLocalDocumentMarker(normalizedRoot))
        {
            return normalizedRoot;
        }

        var nestedPreviewProject = FindNestedDocumentRoot(normalizedRoot, "preview_project.md", "preview_docs");
        if (nestedPreviewProject is not null)
        {
            return nestedPreviewProject;
        }

        var nestedCanonicalProject = FindNestedDocumentRoot(normalizedRoot, "project.md", "project");
        if (nestedCanonicalProject is not null)
        {
            return nestedCanonicalProject;
        }

        var nestedImportReport = FindNestedDocumentRoot(normalizedRoot, "project_report.md", "import_evidence_bundle");
        if (nestedImportReport is not null)
        {
            return nestedImportReport;
        }

        return normalizedRoot;
    }

    private static bool HasAnyLocalDocumentMarker(string projectRootPath)
    {
        return File.Exists(ProjectDocumentPathResolver.GetPreviewProjectPath(projectRootPath)) ||
               File.Exists(ProjectDocumentPathResolver.GetImportReportPath(projectRootPath)) ||
               File.Exists(Path.Combine(projectRootPath, ".zavod", "project", "project.md"));
    }

    private static string? FindNestedDocumentRoot(string projectRootPath, string fileName, string ownerDirectoryName)
    {
        string? found = null;
        foreach (var candidate in Directory.EnumerateFiles(projectRootPath, fileName, SearchOption.AllDirectories))
        {
            var parent = Path.GetDirectoryName(candidate);
            if (parent is null || !string.Equals(Path.GetFileName(parent), ownerDirectoryName, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var zavodRoot = Directory.GetParent(parent)?.FullName;
            if (zavodRoot is null || !string.Equals(Path.GetFileName(zavodRoot), ".zavod", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var candidateRoot = Directory.GetParent(zavodRoot)?.FullName;
            if (string.IsNullOrWhiteSpace(candidateRoot))
            {
                continue;
            }

            if (found is not null)
            {
                // More than one matching nested root found — ambiguous container.
                // Do not silently pick the first; return null so caller falls back to the explicit root.
                return null;
            }

            found = Path.GetFullPath(candidateRoot);
        }

        return found;
    }

    private static string BuildProjectId(string projectName)
    {
        var builder = new StringBuilder();
        foreach (var ch in projectName)
        {
            if (char.IsLetterOrDigit(ch))
            {
                builder.Append(char.ToLowerInvariant(ch));
            }
            else if (builder.Length == 0 || builder[^1] != '-')
            {
                builder.Append('-');
            }
        }

        var value = builder.ToString().Trim('-');
        return string.IsNullOrWhiteSpace(value) ? "zavod-imported-project" : value;
    }

    private static string BuildPreviewProjectMarkdown(WorkspaceImportMaterialInterpreterRunResult runResult)
    {
        var interpretation = runResult.Interpretation;
        var pack = runResult.PreviewPacket.EvidencePack;
        var materials = interpretation.Materials ?? Array.Empty<WorkspaceMaterialPreviewInterpretation>();
        var sourceRoots = runResult.PreviewPacket.SourceRoots ?? Array.Empty<string>();
        var entryPoints = interpretation.EntryPoints ?? Array.Empty<WorkspaceImportMaterialEntryPointInterpretation>();
        var modules = interpretation.Modules ?? Array.Empty<WorkspaceImportMaterialModuleInterpretation>();
        var confirmedSignals = interpretation.ConfirmedSignals ?? Array.Empty<string>();
        var likelySignals = interpretation.LikelySignals ?? Array.Empty<string>();
        var unknownSignals = interpretation.UnknownSignals ?? Array.Empty<string>();
        var title = new DirectoryInfo(Path.GetFullPath(runResult.PreviewPacket.WorkspaceRoot)).Name;
        var topMaterials = materials
            .Where(static material => material.PossibleUsefulness != WorkspaceMaterialContextUsefulness.Unknown || !string.IsNullOrWhiteSpace(material.Summary))
            .Take(5)
            .ToArray();
        var builder = new StringBuilder();

        builder.AppendLine("# Project (Preview)");
        builder.AppendLine();
        builder.AppendLine("Candidate project base from the current import understanding.");
        builder.AppendLine("This document is not canonical truth yet.");
        builder.AppendLine();

        builder.AppendLine("## Identity");
        builder.AppendLine();
        builder.AppendLine($"- Project Title: `{title}`");
        builder.AppendLine($"- Import Kind: `{interpretation.ImportKind}`");
        builder.AppendLine($"- Interpretation Mode: `{interpretation.InterpretationMode}`");
        builder.AppendLine($"- Health: `{pack?.ProjectProfile.Health.ToString() ?? "Unknown"}`");
        builder.AppendLine($"- Truth Status: `Preview only / not canonical yet`");
        builder.AppendLine();

        builder.AppendLine("## What this looks like");
        builder.AppendLine();
        builder.AppendLine($"- {interpretation.SummaryLine}");
        foreach (var detail in interpretation.ProjectDetails.Take(4))
        {
            builder.AppendLine($"- {detail}");
        }
        builder.AppendLine();

        builder.AppendLine("## Observed structure");
        builder.AppendLine();
        if (sourceRoots.Count > 0)
        {
            builder.AppendLine($"- Source Roots: {string.Join(", ", sourceRoots.Select(static root => $"`{root}`"))}");
        }
        else
        {
            builder.AppendLine("- Source Roots: Unknown");
        }

        if (entryPoints.Count > 0)
        {
            var primary = entryPoints[0];
            builder.AppendLine($"- Main Entry: `{primary.RelativePath}` [{primary.Confidence}]");
            if (entryPoints.Count > 1)
            {
                builder.AppendLine($"- Likely Entries: {string.Join(", ", entryPoints.Skip(1).Take(3).Select(entry => $"`{entry.RelativePath}` [{entry.Confidence}]"))}");
            }
        }
        else
        {
            builder.AppendLine("- Main Entry: Unknown");
        }

        if (interpretation.InterpretationMode == ProjectInterpretationMode.SingleProject && modules.Count > 0)
        {
            builder.AppendLine($"- Key Modules: {string.Join(", ", modules.Take(5).Select(module => $"`{module.Name}` [{module.Confidence}]"))}");
        }
        else if (interpretation.InterpretationMode != ProjectInterpretationMode.SingleProject)
        {
            builder.AppendLine("- Key Modules: Unified module map is suppressed for this container.");
        }
        else
        {
            builder.AppendLine("- Key Modules: Unknown");
        }
        builder.AppendLine();

        builder.AppendLine("## Runtime / stack signals");
        builder.AppendLine();
        AppendInline(builder, "Languages", pack?.TechnicalPassport.ObservedLanguages);
        AppendInline(builder, "Frameworks", pack?.TechnicalPassport.Frameworks);
        AppendInline(builder, "Build Systems", pack?.TechnicalPassport.BuildSystems);
        AppendInline(builder, "Toolchains", pack?.TechnicalPassport.Toolchains);
        if (!HasAnyValues(pack?.TechnicalPassport.ObservedLanguages, pack?.TechnicalPassport.Frameworks, pack?.TechnicalPassport.BuildSystems, pack?.TechnicalPassport.Toolchains))
        {
            builder.AppendLine("- Runtime / stack signals remain coarse or unknown.");
        }
        builder.AppendLine();

        builder.AppendLine("## What is confirmed / likely / unknown");
        builder.AppendLine();
        AppendInline(builder, "Confirmed", confirmedSignals);
        AppendInline(builder, "Likely", likelySignals);
        AppendInline(builder, "Unknown", unknownSignals);
        if (!confirmedSignals.Any() && !likelySignals.Any() && !unknownSignals.Any())
        {
            builder.AppendLine("- Confidence split is still coarse.");
        }
        builder.AppendLine();

        builder.AppendLine("## Materials worth reading");
        builder.AppendLine();
        if (topMaterials.Length > 0)
        {
            foreach (var material in topMaterials)
            {
                builder.AppendLine($"- `{material.RelativePath}` [{material.Confidence}] usefulness={material.PossibleUsefulness}: {material.Summary}");
            }
        }
        else
        {
            builder.AppendLine("- No clearly useful materials are stabilized yet.");
        }
        builder.AppendLine();

        builder.AppendLine("## Open uncertainty");
        builder.AppendLine();
        if (unknownSignals.Count > 0)
        {
            foreach (var item in unknownSignals.Take(5))
            {
                builder.AppendLine($"- {item}");
            }
        }
        else if (interpretation.InterpretationMode != ProjectInterpretationMode.SingleProject)
        {
            builder.AppendLine("- Shared architecture across the whole folder is not confirmed.");
        }
        else
        {
            builder.AppendLine("- Remaining uncertainty is low or still coarse.");
        }
        builder.AppendLine();

        builder.AppendLine("## Canonical readiness");
        builder.AppendLine();
        builder.AppendLine("- First confirm target: `project.md`");
        builder.AppendLine("- Derived companion after confirm: `capsule.md`");
        builder.AppendLine(interpretation.InterpretationMode == ProjectInterpretationMode.SingleProject
            ? "- Current preview looks bounded enough for explicit confirm, but it is still not truth until confirmed."
            : "- Container/mixed evidence remains too coarse for a strong unified truth claim.");

        return builder.ToString().TrimEnd();
    }

    private static string BuildPreviewCapsuleMarkdown(
        WorkspaceImportMaterialInterpreterRunResult runResult,
        string previewProjectMarkdown)
    {
        var interpretation = runResult.Interpretation;
        var entryPoints = interpretation.EntryPoints ?? Array.Empty<WorkspaceImportMaterialEntryPointInterpretation>();
        var confirmedSignals = interpretation.ConfirmedSignals ?? Array.Empty<string>();
        var likelySignals = interpretation.LikelySignals ?? Array.Empty<string>();
        var unknownSignals = interpretation.UnknownSignals ?? Array.Empty<string>();
        var title = new DirectoryInfo(Path.GetFullPath(runResult.PreviewPacket.WorkspaceRoot)).Name;
        var firstEntry = entryPoints.FirstOrDefault();
        var builder = new StringBuilder();

        builder.AppendLine("# Capsule (Preview)");
        builder.AppendLine();
        builder.AppendLine("Derived candidate capsule from `preview_project.md`.");
        builder.AppendLine("This document is not canonical truth yet.");
        builder.AppendLine();
        builder.AppendLine("## Project");
        builder.AppendLine();
        builder.AppendLine($"- `{title}`");
        builder.AppendLine($"- Interpretation Mode: `{interpretation.InterpretationMode}`");
        if (firstEntry is not null)
        {
            builder.AppendLine($"- Primary Entry Candidate: `{firstEntry.RelativePath}` [{firstEntry.Confidence}]");
        }
        builder.AppendLine();

        builder.AppendLine("## Current understanding");
        builder.AppendLine();
        builder.AppendLine($"- {interpretation.SummaryLine}");
        builder.AppendLine($"- Confidence Slice: {BuildConfidenceSummary(confirmedSignals, likelySignals, unknownSignals)}");
        builder.AppendLine();

        builder.AppendLine("## Key constraints");
        builder.AppendLine();
        builder.AppendLine("- Derived from `preview_project.md` and current importer-owned interpretation only.");
        builder.AppendLine("- No additional truth is invented beyond the current import bundle.");
        if (interpretation.InterpretationMode != ProjectInterpretationMode.SingleProject)
        {
            builder.AppendLine("- Unified architecture is not assumed for this folder.");
        }
        builder.AppendLine();

        builder.AppendLine("## Top risks / unknowns");
        builder.AppendLine();
        if (unknownSignals.Count > 0)
        {
            foreach (var item in unknownSignals.Take(3))
            {
                builder.AppendLine($"- {item}");
            }
        }
        else
        {
            builder.AppendLine("- Remaining uncertainty is still bounded and may need explicit confirmation.");
        }
        builder.AppendLine();

        builder.AppendLine("## Next confirmable step");
        builder.AppendLine();
        builder.AppendLine("- Confirm `preview_project.md` into `project.md` if this understanding is acceptable.");
        builder.AppendLine("- Keep `capsule.md` derived from the confirmed project base only.");
        builder.AppendLine($"- Preview project digest length: {previewProjectMarkdown.Length}");

        return builder.ToString().TrimEnd();
    }

    private static string BuildCanonicalProjectMarkdown(string previewProjectMarkdown)
    {
        var lines = previewProjectMarkdown.Replace("\r\n", "\n").Split('\n');
        var body = lines.SkipWhile(static line => !line.StartsWith("## ", StringComparison.Ordinal)).ToArray();
        var builder = new StringBuilder();

        builder.AppendLine("# Project");
        builder.AppendLine();
        builder.AppendLine("Confirmed canonical project base materialized from `preview_project.md`.");
        builder.AppendLine();

        if (body.Length > 0)
        {
            foreach (var line in body)
            {
                builder.AppendLine(line);
            }
        }
        else
        {
            builder.AppendLine("- Preview content had no parseable sections. Project identity is confirmed but body remains coarse.");
        }

        return builder.ToString().TrimEnd();
    }

    private static string BuildCanonicalCapsuleMarkdown(
        ProjectState projectState,
        string canonicalProjectMarkdown,
        string previewCapsuleMarkdown)
    {
        var summary = ExtractFirstBulletUnderSection(canonicalProjectMarkdown, "## What this looks like")
                      ?? ExtractFirstBulletUnderSection(canonicalProjectMarkdown, "## What is confirmed / likely / unknown")
                      ?? ExtractFirstBullet(previewCapsuleMarkdown)
                      ?? "Project identity is confirmed, but the capsule remains coarse.";
        var builder = new StringBuilder();

        builder.AppendLine("# Capsule");
        builder.AppendLine();
        builder.AppendLine("Derived companion of confirmed `project.md`.");
        builder.AppendLine();
        builder.AppendLine($"- Project: `{projectState.ProjectName}`");
        builder.AppendLine($"- Summary: {summary}");
        builder.AppendLine($"- Source: `{Path.GetFileName(projectState.TruthPointers.ProjectDocumentPath)}`");
        builder.AppendLine("- This capsule is derived and non-authoritative.");

        return builder.ToString().TrimEnd();
    }

    private static ProjectDocumentSourceDescriptor CreateDescriptor(ProjectDocumentKind kind, ProjectDocumentStage stage, string path)
    {
        return new ProjectDocumentSourceDescriptor(kind, stage, path, File.Exists(path));
    }

    private static string BuildConfidenceSummary(
        IReadOnlyList<string> confirmed,
        IReadOnlyList<string> likely,
        IReadOnlyList<string> unknown)
    {
        return $"Confirmed={confirmed.Count}, Likely={likely.Count}, Unknown={unknown.Count}";
    }

    private static string? ExtractFirstBulletUnderSection(string markdown, string heading)
    {
        var lines = markdown.Replace("\r\n", "\n").Split('\n');
        var headingIndex = Array.FindIndex(lines, line => string.Equals(line.Trim(), heading, StringComparison.OrdinalIgnoreCase));
        if (headingIndex < 0)
        {
            return null;
        }

        for (var index = headingIndex + 1; index < lines.Length; index++)
        {
            var line = lines[index].Trim();
            if (line.StartsWith("## ", StringComparison.Ordinal))
            {
                break;
            }

            if (line.StartsWith("- ", StringComparison.Ordinal))
            {
                return line[2..].Trim();
            }
        }

        return null;
    }

    private static string? ExtractFirstBullet(string markdown)
    {
        return markdown.Replace("\r\n", "\n")
            .Split('\n')
            .Select(static line => line.Trim())
            .FirstOrDefault(static line => line.StartsWith("- ", StringComparison.Ordinal))?[2..].Trim();
    }

    private static void AppendInline(StringBuilder builder, string label, IReadOnlyList<string>? values)
    {
        var items = values?.Where(static value => !string.IsNullOrWhiteSpace(value)).ToArray() ?? Array.Empty<string>();
        if (items.Length == 0)
        {
            return;
        }

        builder.AppendLine($"- **{label}**: {string.Join(", ", items)}");
    }

    private static bool HasAnyValues(params IReadOnlyList<string>?[] values)
    {
        return values.Any(static list => list is { Count: > 0 });
    }
}

public sealed record ProjectPreviewDocsArtifacts(
    string PreviewDocsRoot,
    string PreviewProjectPath,
    string PreviewCapsulePath);

public sealed record ProjectCanonicalMaterializationResult(
    string ProjectDocumentPath,
    string CapsuleDocumentPath);
