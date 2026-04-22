using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using zavod.Persistence;
using zavod.Workspace;

namespace zavod.Execution;

public sealed class ProjectDocumentRuntimeService(GitRoadmapHistoryReader? roadmapHistoryReader = null)
{
    private readonly GitRoadmapHistoryReader _roadmapHistoryReader = roadmapHistoryReader ?? new GitRoadmapHistoryReader();

    public ProjectPreviewDocsArtifacts WritePreviewDocs(
        WorkspaceImportMaterialInterpreterRunResult runResult,
        string projectRootPath)
    {
        ArgumentNullException.ThrowIfNull(runResult);
        ArgumentException.ThrowIfNullOrWhiteSpace(projectRootPath);

        var normalizedProjectRoot = Path.GetFullPath(projectRootPath);
        ProjectDocumentPathResolver.EnsurePreviewDocsRoot(normalizedProjectRoot);

        var previewProjectPath = ProjectDocumentPathResolver.GetPreviewProjectPath(normalizedProjectRoot);
        var previewDirectionPath = ProjectDocumentPathResolver.GetPreviewDirectionPath(normalizedProjectRoot);
        var previewRoadmapPath = ProjectDocumentPathResolver.GetPreviewRoadmapPath(normalizedProjectRoot);
        var previewCanonPath = ProjectDocumentPathResolver.GetPreviewCanonPath(normalizedProjectRoot);
        var previewCapsulePath = ProjectDocumentPathResolver.GetPreviewCapsulePath(normalizedProjectRoot);

        var previewProjectMarkdown = BuildPreviewProjectMarkdown(runResult);
        File.WriteAllText(previewProjectPath, previewProjectMarkdown, Encoding.UTF8);

        var previewDirectionMarkdown = BuildPreviewDirectionMarkdown(runResult);
        File.WriteAllText(previewDirectionPath, previewDirectionMarkdown, Encoding.UTF8);

        var previewRoadmapMarkdown = BuildPreviewRoadmapMarkdown(runResult, normalizedProjectRoot);
        File.WriteAllText(previewRoadmapPath, previewRoadmapMarkdown, Encoding.UTF8);

        var previewCanonMarkdown = BuildPreviewCanonMarkdown(runResult);
        File.WriteAllText(previewCanonPath, previewCanonMarkdown, Encoding.UTF8);

        var previewCapsuleMarkdown = BuildPreviewCapsuleMarkdown(
            runResult,
            previewProjectMarkdown,
            previewDirectionMarkdown,
            previewRoadmapMarkdown,
            previewCanonMarkdown);
        File.WriteAllText(previewCapsulePath, previewCapsuleMarkdown, Encoding.UTF8);

        return new ProjectPreviewDocsArtifacts(
            ProjectDocumentPathResolver.GetPreviewDocsRoot(normalizedProjectRoot),
            previewProjectPath,
            previewDirectionPath,
            previewRoadmapPath,
            previewCanonPath,
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

        var canonicalCapsuleMarkdown = BuildCanonicalCapsuleMarkdown(projectState, normalizedProjectRoot, canonicalProjectMarkdown);
        File.WriteAllText(projectState.TruthPointers.CapsuleDocumentPath, canonicalCapsuleMarkdown, Encoding.UTF8);

        return new ProjectCanonicalMaterializationResult(
            projectState.TruthPointers.ProjectDocumentPath,
            projectState.TruthPointers.CapsuleDocumentPath);
    }

    public ProjectDocumentReadResult RegenerateCapsule(string projectRootPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(projectRootPath);

        var normalizedProjectRoot = ResolveEffectiveDocumentRoot(projectRootPath);
        var canonicalProjectPath = Path.Combine(normalizedProjectRoot, ".zavod", "project", "project.md");
        if (File.Exists(canonicalProjectPath))
        {
            var projectState = EnsureProjectState(normalizedProjectRoot);
            var canonicalProjectMarkdown = File.ReadAllText(canonicalProjectPath, Encoding.UTF8);
            var canonicalCapsuleMarkdown = BuildCanonicalCapsuleMarkdown(projectState, normalizedProjectRoot, canonicalProjectMarkdown);
            File.WriteAllText(projectState.TruthPointers.CapsuleDocumentPath, canonicalCapsuleMarkdown, Encoding.UTF8);

            return new ProjectDocumentReadResult(
                ProjectDocumentKind.Capsule,
                ProjectDocumentStage.CanonicalDocs,
                projectState.TruthPointers.CapsuleDocumentPath,
                Exists: true,
                canonicalCapsuleMarkdown);
        }

        var previewProjectPath = ProjectDocumentPathResolver.GetPreviewProjectPath(normalizedProjectRoot);
        if (!File.Exists(previewProjectPath))
        {
            throw new InvalidOperationException("Project document must exist before capsule regeneration.");
        }

        var previewCapsulePath = ProjectDocumentPathResolver.GetPreviewCapsulePath(normalizedProjectRoot);
        var previewCapsuleMarkdown = BuildPreviewCapsuleMarkdown(
            projectRootPath: normalizedProjectRoot,
            previewProjectMarkdown: File.ReadAllText(previewProjectPath, Encoding.UTF8),
            previewDirectionMarkdown: ReadOptional(ProjectDocumentPathResolver.GetPreviewDirectionPath(normalizedProjectRoot)),
            previewRoadmapMarkdown: ReadOptional(ProjectDocumentPathResolver.GetPreviewRoadmapPath(normalizedProjectRoot)),
            previewCanonMarkdown: ReadOptional(ProjectDocumentPathResolver.GetPreviewCanonPath(normalizedProjectRoot)));
        File.WriteAllText(previewCapsulePath, previewCapsuleMarkdown, Encoding.UTF8);

        return new ProjectDocumentReadResult(
            ProjectDocumentKind.Capsule,
            ProjectDocumentStage.PreviewDocs,
            previewCapsulePath,
            Exists: true,
            previewCapsuleMarkdown);
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
        var workspaceRoot = Path.GetFullPath(runResult.PreviewPacket.WorkspaceRoot);
        var title = new DirectoryInfo(workspaceRoot).Name;
        var projectName = string.IsNullOrWhiteSpace(title) ? "ZAVOD Imported Project" : title;
        var projectId = BuildProjectId(projectName);
        var interpretationMode = interpretation.InterpretationMode;
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
        AppendSectionConfidence(builder, WorkspaceEvidenceConfidenceLevel.Confirmed, "Workspace root and derived identity are filesystem-observed; purpose remains preview.");
        builder.AppendLine($"- Project Id: `{projectId}`");
        builder.AppendLine($"- Project Name: `{projectName}`");
        builder.AppendLine($"- Workspace Root: `{workspaceRoot}`");
        builder.AppendLine($"- Import Kind: `{interpretation.ImportKind}`");
        builder.AppendLine($"- Interpretation Mode: `{interpretationMode}`");
        builder.AppendLine($"- Health: `{pack?.ProjectProfile.Health.ToString() ?? "Unknown"}`");
        builder.AppendLine($"- Truth Status: `Preview only / not canonical yet`");
        builder.AppendLine();

        builder.AppendLine("## Scope and container mode");
        builder.AppendLine();
        AppendSectionConfidence(builder, WorkspaceEvidenceConfidenceLevel.Confirmed, "Container mode is produced by the importer/scanner interpretation.");
        if (sourceRoots.Count > 0)
        {
            builder.AppendLine($"- Source Roots: {string.Join(", ", sourceRoots.Select(static root => $"`{root}`"))}");
        }
        else
        {
            builder.AppendLine("- Source Roots: Unknown");
        }

        AppendInline(builder, "Build Roots", pack?.ProjectProfile.BuildRoots);
        AppendInline(builder, "Structural Anomalies", pack?.ProjectProfile.StructuralAnomalies);
        if (interpretationMode == ProjectInterpretationMode.SingleProject)
        {
            builder.AppendLine("- Container Status: Single project interpretation.");
        }
        else
        {
            builder.AppendLine($"- Container Status: `{interpretationMode}`.");
            builder.AppendLine("- Unified architecture across the whole folder is not confirmed.");
            builder.AppendLine("- Any technical signals below describe observed folder evidence, not a shared project architecture.");
        }
        builder.AppendLine();

        builder.AppendLine("## What this project appears to be");
        builder.AppendLine();
        AppendSectionConfidence(builder, confirmedSignals.Count > 0 ? WorkspaceEvidenceConfidenceLevel.Likely : WorkspaceEvidenceConfidenceLevel.Unknown, "Importer-owned summary; contributor confirmation is still required.");
        builder.AppendLine($"- {interpretation.SummaryLine}");
        foreach (var detail in interpretation.ProjectDetails.Take(4))
        {
            builder.AppendLine($"- {detail}");
        }
        builder.AppendLine();

        builder.AppendLine("## Observed structure");
        builder.AppendLine();
        AppendSectionConfidence(builder, entryPoints.Count > 0 || modules.Count > 0 ? WorkspaceEvidenceConfidenceLevel.Likely : WorkspaceEvidenceConfidenceLevel.Unknown, "Entry points and modules are interpreted from scanner/importer evidence.");

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

        if (interpretationMode == ProjectInterpretationMode.SingleProject && modules.Count > 0)
        {
            builder.AppendLine($"- Key Modules: {string.Join(", ", modules.Take(5).Select(module => $"`{module.Name}` [{module.Confidence}]"))}");
        }
        else if (interpretationMode != ProjectInterpretationMode.SingleProject)
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
        AppendSectionConfidence(builder, HasAnyValues(pack?.TechnicalPassport.ObservedLanguages, pack?.TechnicalPassport.Frameworks, pack?.TechnicalPassport.BuildSystems, pack?.TechnicalPassport.Toolchains)
            ? WorkspaceEvidenceConfidenceLevel.Likely
            : WorkspaceEvidenceConfidenceLevel.Unknown,
            interpretationMode == ProjectInterpretationMode.SingleProject
                ? "Technical passport values are observed signals, not architectural rules."
                : "Technical passport values are observed across the container and do not prove one unified stack.");
        AppendInline(builder, "Languages", pack?.TechnicalPassport.ObservedLanguages);
        AppendInline(builder, "Frameworks", pack?.TechnicalPassport.Frameworks);
        AppendInline(builder, "Build Systems", pack?.TechnicalPassport.BuildSystems);
        AppendInline(builder, "Toolchains", pack?.TechnicalPassport.Toolchains);
        AppendInline(builder, "Target Platforms", pack?.TechnicalPassport.TargetPlatforms);
        AppendInline(builder, "Runtime Surfaces", pack?.TechnicalPassport.RuntimeSurfaces);
        AppendInline(builder, "Version Hints", pack?.TechnicalPassport.VersionHints);
        AppendInline(builder, "Build Variants", pack?.TechnicalPassport.BuildVariants);
        AppendInline(builder, "Notable Options", pack?.TechnicalPassport.NotableOptions);
        if (!HasAnyValues(pack?.TechnicalPassport.ObservedLanguages, pack?.TechnicalPassport.Frameworks, pack?.TechnicalPassport.BuildSystems, pack?.TechnicalPassport.Toolchains))
        {
            builder.AppendLine("- Runtime / stack signals remain coarse or unknown.");
        }
        builder.AppendLine();

        builder.AppendLine("## What is confirmed / likely / unknown");
        builder.AppendLine();
        AppendSectionConfidence(builder, WorkspaceEvidenceConfidenceLevel.Confirmed, "This section preserves the importer's explicit confidence split.");
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
        AppendSectionConfidence(builder, topMaterials.Length > 0 ? WorkspaceEvidenceConfidenceLevel.Likely : WorkspaceEvidenceConfidenceLevel.Unknown, "Imported materials are context candidates, not canonical truth.");
        if (topMaterials.Length > 0)
        {
            foreach (var material in topMaterials)
            {
                builder.AppendLine($"- `{material.RelativePath}` [{material.Confidence}] kind={material.Kind} usefulness={material.PossibleUsefulness} temporal={material.TemporalStatus} context_only={material.ContextOnly}: {material.Summary}");
            }
        }
        else
        {
            builder.AppendLine("- No clearly useful materials are stabilized yet.");
        }
        builder.AppendLine();

        builder.AppendLine("## Open uncertainty");
        builder.AppendLine();
        AppendSectionConfidence(builder, WorkspaceEvidenceConfidenceLevel.Unknown, "Unknowns are explicit gaps, not hidden facts.");
        if (unknownSignals.Count > 0)
        {
            foreach (var item in unknownSignals.Take(5))
            {
                builder.AppendLine($"- {item}");
            }
        }
        else if (interpretationMode != ProjectInterpretationMode.SingleProject)
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
        AppendSectionConfidence(builder, interpretationMode == ProjectInterpretationMode.SingleProject ? WorkspaceEvidenceConfidenceLevel.Likely : WorkspaceEvidenceConfidenceLevel.Unknown, "Promotion is a contributor act; preview evidence alone is not truth.");
        builder.AppendLine("- First confirm target: `project.md`");
        builder.AppendLine("- Derived companion after confirm: `capsule.md`");
        builder.AppendLine(interpretationMode == ProjectInterpretationMode.SingleProject
            ? "- Current preview looks bounded enough for explicit confirm, but it is still not truth until confirmed."
            : "- Container/mixed evidence remains too coarse for a strong unified truth claim.");

        return builder.ToString().TrimEnd();
    }

    private static string BuildPreviewCapsuleMarkdown(
        WorkspaceImportMaterialInterpreterRunResult runResult,
        string previewProjectMarkdown,
        string previewDirectionMarkdown,
        string previewRoadmapMarkdown,
        string previewCanonMarkdown)
    {
        return BuildPreviewCapsuleMarkdown(
            Path.GetFullPath(runResult.PreviewPacket.WorkspaceRoot),
            previewProjectMarkdown,
            previewDirectionMarkdown,
            previewRoadmapMarkdown,
            previewCanonMarkdown);
    }

    private static string BuildPreviewCapsuleMarkdown(
        string projectRootPath,
        string previewProjectMarkdown,
        string previewDirectionMarkdown,
        string previewRoadmapMarkdown,
        string previewCanonMarkdown)
    {
        var projectName = new DirectoryInfo(Path.GetFullPath(projectRootPath)).Name;
        var sources = new CapsuleLayerSources(
            new CapsuleSourceDocument(ProjectDocumentKind.Project, ProjectDocumentStage.PreviewDocs, "preview_project.md", previewProjectMarkdown, Exists: true),
            new CapsuleSourceDocument(ProjectDocumentKind.Direction, ProjectDocumentStage.PreviewDocs, "preview_direction.md", previewDirectionMarkdown, !string.IsNullOrWhiteSpace(previewDirectionMarkdown)),
            new CapsuleSourceDocument(ProjectDocumentKind.Roadmap, ProjectDocumentStage.PreviewDocs, "preview_roadmap.md", previewRoadmapMarkdown, !string.IsNullOrWhiteSpace(previewRoadmapMarkdown)),
            new CapsuleSourceDocument(ProjectDocumentKind.Canon, ProjectDocumentStage.PreviewDocs, "preview_canon.md", previewCanonMarkdown, !string.IsNullOrWhiteSpace(previewCanonMarkdown)));

        return BuildCapsuleV2Markdown(
            "# Capsule (Preview)",
            projectName,
            sources,
            activeShiftId: null,
            activeTaskId: null,
            outputStage: ProjectDocumentStage.PreviewDocs);
    }

    private static string BuildPreviewDirectionMarkdown(WorkspaceImportMaterialInterpreterRunResult runResult)
    {
        var direction = DirectionSignalInterpreter.Interpret(runResult);
        var builder = new StringBuilder();

        builder.AppendLine("# Direction (Preview)");
        builder.AppendLine();
        builder.AppendLine("Candidate direction surface from current import evidence.");
        builder.AppendLine("This document is not canonical truth yet.");
        builder.AppendLine("Contributor may reject, rewrite, or author direction from scratch before promotion.");
        builder.AppendLine();

        if (!direction.HasDirectionEvidence)
        {
            builder.AppendLine("## Unknown / not-yet-established");
            builder.AppendLine();
            foreach (var unknown in direction.Unknowns)
            {
                builder.AppendLine($"- {unknown}");
            }

            return builder.ToString().TrimEnd();
        }

        builder.AppendLine("## Confirmed direction");
        builder.AppendLine();
        builder.AppendLine("- No confirmed direction statement is derived automatically.");
        builder.AppendLine();

        builder.AppendLine("## Likely / candidate direction signals");
        builder.AppendLine();
        foreach (var candidate in direction.Candidates)
        {
            builder.AppendLine($"- [{candidate.Confidence}] {candidate.Text} Evidence: {candidate.Evidence}.");
        }
        builder.AppendLine();

        builder.AppendLine("## Unknown / not-yet-established");
        builder.AppendLine();
        foreach (var unknown in direction.Unknowns)
        {
            builder.AppendLine($"- {unknown}");
        }

        return builder.ToString().TrimEnd();
    }

    private string BuildPreviewRoadmapMarkdown(WorkspaceImportMaterialInterpreterRunResult runResult, string projectRootPath)
    {
        var history = _roadmapHistoryReader.Read(projectRootPath);
        var roadmap = RoadmapSignalInterpreter.Interpret(history, runResult);
        var builder = new StringBuilder();

        builder.AppendLine("# Roadmap (Preview)");
        builder.AppendLine();
        builder.AppendLine("Candidate roadmap surface from observable evidence.");
        builder.AppendLine("This document is not canonical truth yet.");
        builder.AppendLine("Every phase below is candidate-level only; contributor must confirm or replace it.");
        builder.AppendLine();

        if (!roadmap.HasCandidateEvidence)
        {
            builder.AppendLine("## Unknown / not-yet-established");
            builder.AppendLine();
            foreach (var unknown in roadmap.Unknowns)
            {
                builder.AppendLine($"- {unknown}");
            }

            return builder.ToString().TrimEnd();
        }

        builder.AppendLine("## Candidate phases");
        builder.AppendLine();
        foreach (var candidate in roadmap.Candidates)
        {
            builder.AppendLine($"- Candidate phase from {candidate.Evidence}. Contributor must confirm or replace. {candidate.Label}");
        }
        builder.AppendLine();

        builder.AppendLine("## Unknown / not-yet-established");
        builder.AppendLine();
        foreach (var unknown in roadmap.Unknowns)
        {
            builder.AppendLine($"- {unknown}");
        }

        return builder.ToString().TrimEnd();
    }

    private static string BuildPreviewCanonMarkdown(WorkspaceImportMaterialInterpreterRunResult runResult)
    {
        var interpretation = runResult.Interpretation;
        var pack = runResult.PreviewPacket.EvidencePack;
        var passport = pack?.TechnicalPassport;
        var modules = interpretation.Modules ?? Array.Empty<WorkspaceImportMaterialModuleInterpretation>();
        var entryPoints = interpretation.EntryPoints ?? Array.Empty<WorkspaceImportMaterialEntryPointInterpretation>();
        var builder = new StringBuilder();

        builder.AppendLine("# Canon (Preview)");
        builder.AppendLine();
        builder.AppendLine("Observed technical canon candidate from the current import understanding.");
        builder.AppendLine("This document is not canonical truth yet.");
        builder.AppendLine("It contains observed technical facts only; contributor-authored rules remain empty.");
        builder.AppendLine();

        builder.AppendLine("## Observed technical invariants");
        builder.AppendLine();
        builder.AppendLine("- Confidence: `Confirmed for listed scanner/importer observations`");
        builder.AppendLine("- Evidence Boundary: Derived from TechnicalPassport, interpreted modules, and interpreted entry points only.");
        AppendObservedCanonValues(builder, "Observed Languages", passport?.ObservedLanguages);
        AppendObservedCanonValues(builder, "Frameworks", passport?.Frameworks);
        AppendObservedCanonValues(builder, "Build Systems", passport?.BuildSystems);
        AppendObservedCanonValues(builder, "Toolchains", passport?.Toolchains);
        AppendObservedCanonValues(builder, "Target Platforms", passport?.TargetPlatforms);
        AppendObservedCanonValues(builder, "Runtime Surfaces", passport?.RuntimeSurfaces);
        AppendObservedCanonValues(builder, "Version Hints", passport?.VersionHints);
        AppendObservedCanonValues(builder, "Config Markers", passport?.ConfigMarkers);
        AppendObservedCanonValues(builder, "Build Variants", passport?.BuildVariants);
        AppendObservedCanonValues(builder, "Notable Options", passport?.NotableOptions);
        if (modules.Count > 0)
        {
            builder.AppendLine($"- Observed Modules: {string.Join(", ", modules.Take(8).Select(static module => $"`{module.Name}` [{module.Confidence}]"))}");
        }

        if (entryPoints.Count > 0)
        {
            builder.AppendLine($"- Observed Entry Points: {string.Join(", ", entryPoints.Take(8).Select(static entry => $"`{entry.RelativePath}` [{entry.Confidence}]"))}");
        }

        if (!HasAnyValues(
                passport?.ObservedLanguages,
                passport?.Frameworks,
                passport?.BuildSystems,
                passport?.Toolchains,
                passport?.TargetPlatforms,
                passport?.RuntimeSurfaces,
                passport?.VersionHints,
                passport?.ConfigMarkers,
                passport?.BuildVariants,
                passport?.NotableOptions) &&
            modules.Count == 0 &&
            entryPoints.Count == 0)
        {
            builder.AppendLine("- No technical invariants are confirmed by the current import evidence.");
        }
        builder.AppendLine();

        builder.AppendLine("## Contributor-authored rules");
        builder.AppendLine();
        builder.AppendLine("- Confidence: `None / contributor-owned`");
        builder.AppendLine("- No authored rules yet. Contributor must add review rules / execution rules / intent rules here.");
        builder.AppendLine("- The importer must not derive these rules from observed frameworks, modules, filenames, README prose, or code layout.");
        builder.AppendLine();

        builder.AppendLine("## Unknown / not-yet-established");
        builder.AppendLine();
        builder.AppendLine("- Confidence: `Unknown`");
        builder.AppendLine("- What is not yet canonical: review workflow.");
        builder.AppendLine("- What is not yet canonical: execution boundaries.");
        builder.AppendLine("- What is not yet canonical: refusal rules.");
        builder.AppendLine("- What is not yet canonical: truth mutation limits.");
        builder.AppendLine("- What is not yet canonical: scope discipline.");
        builder.AppendLine("- Contributor must confirm, replace, or author these rules before promotion.");

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
        string projectRootPath,
        string canonicalProjectMarkdown)
    {
        var sources = new CapsuleLayerSources(
            new CapsuleSourceDocument(ProjectDocumentKind.Project, ProjectDocumentStage.CanonicalDocs, "project.md", canonicalProjectMarkdown, Exists: true),
            ReadLayerSource(projectState.TruthPointers.DirectionDocumentPath, ProjectDocumentKind.Direction, "direction.md", ProjectDocumentStage.CanonicalDocs, ProjectDocumentPathResolver.GetPreviewDirectionPath(projectRootPath), "preview_direction.md"),
            ReadLayerSource(projectState.TruthPointers.RoadmapDocumentPath, ProjectDocumentKind.Roadmap, "roadmap.md", ProjectDocumentStage.CanonicalDocs, ProjectDocumentPathResolver.GetPreviewRoadmapPath(projectRootPath), "preview_roadmap.md"),
            ReadLayerSource(projectState.TruthPointers.CanonDocumentPath, ProjectDocumentKind.Canon, "canon.md", ProjectDocumentStage.CanonicalDocs, ProjectDocumentPathResolver.GetPreviewCanonPath(projectRootPath), "preview_canon.md"));

        return BuildCapsuleV2Markdown(
            "# Capsule",
            projectState.ProjectName,
            sources,
            projectState.ActiveShiftId,
            projectState.ActiveTaskId,
            outputStage: ProjectDocumentStage.CanonicalDocs);
    }

    private static string BuildCapsuleV2Markdown(
        string heading,
        string projectName,
        CapsuleLayerSources sources,
        string? activeShiftId,
        string? activeTaskId,
        ProjectDocumentStage outputStage)
    {
        var sourceStage = ResolveCapsuleSourceStage(sources, outputStage);
        var builder = new StringBuilder();

        builder.AppendLine("---");
        builder.AppendLine($"source_stage: {sourceStage}");
        builder.AppendLine("---");
        builder.AppendLine();
        builder.AppendLine(heading);
        builder.AppendLine();
        builder.AppendLine("Derived capsule v2. This document is a compressed view over Layer A sources, not an independent truth layer.");
        if (sourceStage == "preview")
        {
            builder.AppendLine("Reader obligation: source_stage preview is below canonical truth.");
        }
        builder.AppendLine();

        builder.AppendLine("## Project identity");
        builder.AppendLine();
        AppendSectionSource(builder, sources.Project);
        builder.AppendLine($"- Project: `{projectName}`");
        AppendBulletsOrNone(builder, ExtractBulletsUnderSection(sources.Project.Markdown, "## Identity").Take(3), "No project identity details are available.");
        builder.AppendLine();

        builder.AppendLine("## What this project is");
        builder.AppendLine();
        AppendSectionSource(builder, sources.Project);
        AppendBulletsOrNone(
            builder,
            ExtractBulletsUnderSection(sources.Project.Markdown, "## What this project appears to be")
                .Concat(ExtractBulletsUnderSection(sources.Project.Markdown, "## What this looks like"))
                .Take(4),
            "None established.");
        builder.AppendLine();

        builder.AppendLine("## Current direction");
        builder.AppendLine();
        AppendSectionSource(builder, sources.Direction);
        AppendBulletsOrNone(
            builder,
            ExtractBulletsUnderSection(sources.Direction.Markdown, "## Confirmed direction")
                .Concat(ExtractBulletsUnderSection(sources.Direction.Markdown, "## Likely / candidate direction signals"))
                .Concat(ExtractBulletsUnderSection(sources.Direction.Markdown, "## Unknown / not-yet-established"))
                .Take(4),
            "None established.");
        builder.AppendLine();

        builder.AppendLine("## Current roadmap phase");
        builder.AppendLine();
        AppendSectionSource(builder, sources.Roadmap);
        AppendBulletsOrNone(
            builder,
            ExtractBulletsUnderSection(sources.Roadmap.Markdown, "## Candidate phases")
                .Concat(ExtractBulletsUnderSection(sources.Roadmap.Markdown, "## Unknown / not-yet-established"))
                .Take(2),
            "None established.");
        builder.AppendLine();

        builder.AppendLine("## Core canon rules");
        builder.AppendLine();
        AppendSectionSource(builder, sources.Canon);
        AppendBulletsOrNone(
            builder,
            ExtractBulletsUnderSection(sources.Canon.Markdown, "## Contributor-authored rules")
                .Take(6),
            "None established.");
        builder.AppendLine();

        builder.AppendLine("## Current focus");
        builder.AppendLine();
        builder.AppendLine("- Source: runtime overlay [runtime]");
        builder.AppendLine("- Overlay boundary: this section is runtime state only, not Layer A truth.");
        if (string.IsNullOrWhiteSpace(activeShiftId) && string.IsNullOrWhiteSpace(activeTaskId))
        {
            builder.AppendLine("- Active shift: none.");
            builder.AppendLine("- Active task: none.");
        }
        else
        {
            builder.AppendLine($"- Active shift: `{activeShiftId ?? "none"}`");
            builder.AppendLine($"- Active task: `{activeTaskId ?? "none"}`");
        }
        builder.AppendLine();

        builder.AppendLine("## Open risks / unresolved items");
        builder.AppendLine();
        builder.AppendLine("- Source: compressed Layer A unknowns [mixed]");
        AppendBulletsOrNone(
            builder,
            ExtractBulletsUnderSection(sources.Project.Markdown, "## Open uncertainty")
                .Concat(ExtractBulletsUnderSection(sources.Direction.Markdown, "## Unknown / not-yet-established"))
                .Concat(ExtractBulletsUnderSection(sources.Roadmap.Markdown, "## Unknown / not-yet-established"))
                .Concat(ExtractBulletsUnderSection(sources.Canon.Markdown, "## Unknown / not-yet-established"))
                .Distinct(StringComparer.Ordinal)
                .Take(5),
            "None listed.");
        builder.AppendLine();

        builder.AppendLine("## Canon completeness status");
        builder.AppendLine();
        builder.AppendLine("- Source: derived from 5/5 document state [derived]");
        builder.AppendLine($"- source_stage: `{sourceStage}`");
        builder.AppendLine($"- Status: {BuildCompletenessStatus(sources, outputStage)}");
        builder.AppendLine($"- Project identity source: {FormatStage(sources.Project)}");
        builder.AppendLine($"- What this project is source: {FormatStage(sources.Project)}");
        builder.AppendLine($"- Current direction source: {FormatStage(sources.Direction)}");
        builder.AppendLine($"- Current roadmap phase source: {FormatStage(sources.Roadmap)}");
        builder.AppendLine($"- Core canon rules source: {FormatStage(sources.Canon)}");
        builder.AppendLine("- Current focus source: runtime overlay.");
        builder.AppendLine("- Open risks / unresolved items source: mixed Layer A unknowns.");

        return builder.ToString().TrimEnd();
    }

    private static ProjectDocumentSourceDescriptor CreateDescriptor(ProjectDocumentKind kind, ProjectDocumentStage stage, string path)
    {
        return new ProjectDocumentSourceDescriptor(kind, stage, path, File.Exists(path));
    }

    private static CapsuleSourceDocument ReadLayerSource(
        string canonicalPath,
        ProjectDocumentKind kind,
        string canonicalFileName,
        ProjectDocumentStage canonicalStage,
        string previewPath,
        string previewFileName)
    {
        if (File.Exists(canonicalPath))
        {
            return new CapsuleSourceDocument(kind, canonicalStage, canonicalFileName, File.ReadAllText(canonicalPath, Encoding.UTF8), Exists: true);
        }

        if (File.Exists(previewPath))
        {
            return new CapsuleSourceDocument(kind, ProjectDocumentStage.PreviewDocs, previewFileName, File.ReadAllText(previewPath, Encoding.UTF8), Exists: true);
        }

        return new CapsuleSourceDocument(kind, ProjectDocumentStage.ImportPreview, canonicalFileName, string.Empty, Exists: false);
    }

    private static string ReadOptional(string path)
    {
        return File.Exists(path) ? File.ReadAllText(path, Encoding.UTF8) : string.Empty;
    }

    private static IReadOnlyList<string> ExtractBulletsUnderSection(string markdown, string heading)
    {
        var lines = markdown.Replace("\r\n", "\n").Split('\n');
        var headingIndex = Array.FindIndex(lines, line => string.Equals(line.Trim(), heading, StringComparison.OrdinalIgnoreCase));
        if (headingIndex < 0)
        {
            return Array.Empty<string>();
        }

        var bullets = new List<string>();
        for (var index = headingIndex + 1; index < lines.Length; index++)
        {
            var line = lines[index].Trim();
            if (line.StartsWith("## ", StringComparison.Ordinal))
            {
                break;
            }

            if (line.StartsWith("- ", StringComparison.Ordinal))
            {
                var bullet = line[2..].Trim();
                if (ShouldSkipCapsuleBullet(bullet))
                {
                    continue;
                }

                bullets.Add(bullet);
            }
        }

        return bullets;
    }

    private static bool ShouldSkipCapsuleBullet(string bullet)
    {
        return bullet.StartsWith("Confidence:", StringComparison.OrdinalIgnoreCase) ||
               bullet.StartsWith("Evidence Boundary:", StringComparison.OrdinalIgnoreCase) ||
               bullet.StartsWith("Source:", StringComparison.OrdinalIgnoreCase);
    }

    private static void AppendSectionSource(StringBuilder builder, CapsuleSourceDocument source)
    {
        builder.AppendLine(source.Exists
            ? $"- Source: `{source.FileName}` [{FormatStage(source)}]"
            : $"- Source: `{source.FileName}` [absent]");
    }

    private static void AppendBulletsOrNone(StringBuilder builder, IEnumerable<string> bullets, string noneText)
    {
        var items = bullets.Where(static bullet => !string.IsNullOrWhiteSpace(bullet)).ToArray();
        if (items.Length == 0)
        {
            builder.AppendLine($"- {noneText}");
            return;
        }

        foreach (var item in items)
        {
            builder.AppendLine($"- {item}");
        }
    }

    private static string ResolveCapsuleSourceStage(CapsuleLayerSources sources, ProjectDocumentStage outputStage)
    {
        var layerSources = sources.All.ToArray();
        if (outputStage == ProjectDocumentStage.CanonicalDocs &&
            layerSources.All(static source => source.Exists && source.Stage == ProjectDocumentStage.CanonicalDocs))
        {
            return "canonical";
        }

        if (layerSources.Where(static source => source.Exists).All(static source => source.Stage == ProjectDocumentStage.PreviewDocs))
        {
            return "preview";
        }

        return "mixed";
    }

    private static string BuildCompletenessStatus(CapsuleLayerSources sources, ProjectDocumentStage outputStage)
    {
        var canonicalCount = sources.All.Count(static source => source.Exists && source.Stage == ProjectDocumentStage.CanonicalDocs) +
                             (outputStage == ProjectDocumentStage.CanonicalDocs ? 1 : 0);
        var previewCount = sources.All.Count(static source => source.Exists && source.Stage == ProjectDocumentStage.PreviewDocs) +
                           (outputStage == ProjectDocumentStage.PreviewDocs ? 1 : 0);
        var absentCount = 5 - canonicalCount - previewCount;

        return $"canonical {canonicalCount}/5, preview {previewCount}/5, absent {absentCount}/5";
    }

    private static string FormatStage(CapsuleSourceDocument source)
    {
        if (!source.Exists)
        {
            return "absent";
        }

        return source.Stage switch
        {
            ProjectDocumentStage.CanonicalDocs => "canonical",
            ProjectDocumentStage.PreviewDocs => "preview",
            _ => "import_preview"
        };
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

    private static void AppendObservedCanonValues(StringBuilder builder, string label, IReadOnlyList<string>? values)
    {
        var items = values?.Where(static value => !string.IsNullOrWhiteSpace(value)).ToArray() ?? Array.Empty<string>();
        if (items.Length == 0)
        {
            return;
        }

        builder.AppendLine($"- {label}: {string.Join(", ", items.Select(static item => $"`{item}`"))}");
    }

    private static void AppendSectionConfidence(StringBuilder builder, WorkspaceEvidenceConfidenceLevel confidence, string note)
    {
        builder.AppendLine($"- Confidence: `{confidence}`");
        builder.AppendLine($"- Evidence Boundary: {note}");
    }

    private static bool HasAnyValues(params IReadOnlyList<string>?[] values)
    {
        return values.Any(static list => list is { Count: > 0 });
    }
}

internal sealed record CapsuleSourceDocument(
    ProjectDocumentKind Kind,
    ProjectDocumentStage Stage,
    string FileName,
    string Markdown,
    bool Exists);

internal sealed record CapsuleLayerSources(
    CapsuleSourceDocument Project,
    CapsuleSourceDocument Direction,
    CapsuleSourceDocument Roadmap,
    CapsuleSourceDocument Canon)
{
    public IReadOnlyList<CapsuleSourceDocument> All => new[] { Project, Direction, Roadmap, Canon };
}

public sealed record ProjectPreviewDocsArtifacts(
    string PreviewDocsRoot,
    string PreviewProjectPath,
    string PreviewDirectionPath,
    string PreviewRoadmapPath,
    string PreviewCanonPath,
    string PreviewCapsulePath);

public sealed record ProjectCanonicalMaterializationResult(
    string ProjectDocumentPath,
    string CapsuleDocumentPath);
