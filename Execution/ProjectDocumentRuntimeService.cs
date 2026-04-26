using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using zavod.Persistence;
using zavod.Workspace;

namespace zavod.Execution;

public sealed class ProjectDocumentRuntimeService(GitRoadmapHistoryReader? roadmapHistoryReader = null)
{
    private const string DefaultContributorId = "local-contributor";

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

    public ProjectCanonicalMaterializationResult ConfirmPreviewDocs(
        string projectRootPath,
        string contributorId = DefaultContributorId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(projectRootPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(contributorId);

        var normalizedProjectRoot = ResolveEffectiveDocumentRoot(projectRootPath);
        var promotions = new List<ProjectDocumentPromotionResult>
        {
            PromotePreviewDoc(normalizedProjectRoot, ProjectDocumentKind.Project, contributorId),
            PromotePreviewDoc(normalizedProjectRoot, ProjectDocumentKind.Direction, contributorId),
            PromotePreviewDoc(normalizedProjectRoot, ProjectDocumentKind.Roadmap, contributorId),
            PromotePreviewDoc(normalizedProjectRoot, ProjectDocumentKind.Canon, contributorId),
            PromotePreviewDoc(normalizedProjectRoot, ProjectDocumentKind.Capsule, contributorId)
        };
        var projectState = EnsureProjectState(normalizedProjectRoot);

        return new ProjectCanonicalMaterializationResult(
            projectState.TruthPointers.ProjectDocumentPath,
            projectState.TruthPointers.DirectionDocumentPath,
            projectState.TruthPointers.RoadmapDocumentPath,
            projectState.TruthPointers.CanonDocumentPath,
            projectState.TruthPointers.CapsuleDocumentPath,
            promotions);
    }

    public ProjectDocumentPromotionResult PromotePreviewDoc(
        string projectRootPath,
        ProjectDocumentKind kind,
        string contributorId = DefaultContributorId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(projectRootPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(contributorId);

        var normalizedProjectRoot = ResolveEffectiveDocumentRoot(projectRootPath);
        var projectState = EnsureProjectState(normalizedProjectRoot);
        var previewPath = GetPreviewPath(normalizedProjectRoot, kind);
        if (!File.Exists(previewPath))
        {
            throw new InvalidOperationException($"Preview {GetCanonicalFileName(kind)} document must exist before canonical promotion.");
        }

        var previewMarkdown = File.ReadAllText(previewPath, Encoding.UTF8);
        if (RequiresProjectRootSelectionBeforeCanonicalPromotion(previewMarkdown))
        {
            throw new InvalidOperationException("Container preview requires selecting a specific active project root before canonical promotion.");
        }

        var canonicalPath = GetCanonicalPath(projectState, kind);
        var canonicalMarkdown = kind switch
        {
            ProjectDocumentKind.Project => BuildCanonicalProjectMarkdown(previewMarkdown),
            ProjectDocumentKind.Capsule => BuildCanonicalCapsuleMarkdown(projectState, normalizedProjectRoot),
            _ => BuildCanonicalMarkdown(kind, previewMarkdown)
        };

        File.WriteAllText(canonicalPath, canonicalMarkdown, Encoding.UTF8);
        if (kind != ProjectDocumentKind.Capsule)
        {
            TryRegenerateCanonicalCapsule(projectState, normalizedProjectRoot);
        }

        var previewHash = ComputeSha256(previewMarkdown);
        var attribution = WritePromotionAttribution(
            projectState,
            kind,
            contributorId,
            previewPath,
            previewHash,
            canonicalPath);

        return new ProjectDocumentPromotionResult(
            kind,
            previewPath,
            canonicalPath,
            previewHash,
            attribution.DecisionId,
            attribution.DecisionPath,
            attribution.DecisionRecordedEventId,
            attribution.CanonicalPromotedEventId,
            attribution.JournalPath);
    }

    public ProjectDocumentRejectionResult RejectPreviewDoc(
        string projectRootPath,
        ProjectDocumentKind kind,
        string contributorId = DefaultContributorId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(projectRootPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(contributorId);

        var normalizedProjectRoot = ResolveEffectiveDocumentRoot(projectRootPath);
        var projectState = EnsureProjectState(normalizedProjectRoot);
        var previewPath = GetPreviewPath(normalizedProjectRoot, kind);
        if (!File.Exists(previewPath))
        {
            throw new InvalidOperationException($"Preview {GetCanonicalFileName(kind)} document must exist before preview rejection.");
        }

        var previewMarkdown = File.ReadAllText(previewPath, Encoding.UTF8);
        var previewHash = ComputeSha256(previewMarkdown);
        var journal = WritePreviewRejectedJournalEvent(
            projectState,
            kind,
            contributorId,
            previewPath,
            previewHash);

        File.Delete(previewPath);

        return new ProjectDocumentRejectionResult(
            kind,
            previewPath,
            previewHash,
            journal.EventId,
            journal.JournalPath);
    }

    public ProjectDocumentReadResult RegenerateCapsule(string projectRootPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(projectRootPath);

        var normalizedProjectRoot = ResolveEffectiveDocumentRoot(projectRootPath);
        var canonicalProjectPath = Path.Combine(normalizedProjectRoot, ".zavod", "project", "project.md");
        if (File.Exists(canonicalProjectPath))
        {
            var projectState = EnsureProjectState(normalizedProjectRoot);
            var canonicalCapsuleMarkdown = BuildCanonicalCapsuleMarkdown(projectState, normalizedProjectRoot);
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
        var topology = pack?.Topology;
        var topologyKind = topology?.Kind ?? "Unknown";
        var safeImportMode = topology?.SafeImportMode ?? "Unknown";
        var language = WorkspaceDocumentationLanguagePolicy.ResolveCurrent();
        var isStandardSingleProjectTopology = IsStandardSingleProjectTopology(topologyKind);
        var isNonStandardTopology = !isStandardSingleProjectTopology && !string.Equals(topologyKind, "Unknown", StringComparison.OrdinalIgnoreCase);
        var topMaterials = materials
            .Where(static material => material.PossibleUsefulness != WorkspaceMaterialContextUsefulness.Unknown || !string.IsNullOrWhiteSpace(material.Summary))
            .Take(5)
            .ToArray();
        var builder = new StringBuilder();

        builder.AppendLine(PreviewHeading(language, ProjectDocumentKind.Project));
        builder.AppendLine();
        AppendPreviewDocumentStatus(builder, runResult, ProjectDocumentKind.Project);
        builder.AppendLine();

        builder.AppendLine(SectionHeading(language, "section.project.identity", "Identity"));
        builder.AppendLine();
        AppendSectionConfidence(builder, language, WorkspaceEvidenceConfidenceLevel.Confirmed, "note.identity_boundary", "Workspace root and derived identity are filesystem-observed; purpose remains preview.");
        builder.AppendLine($"- {L(language, "label.project_id", "Project Id")}: `{projectId}`");
        builder.AppendLine($"- {L(language, "label.project_name", "Project Name")}: `{projectName}`");
        builder.AppendLine($"- {L(language, "label.workspace_root", "Workspace Root")}: `{workspaceRoot}`");
        builder.AppendLine($"- {L(language, "label.import_kind", "Import Kind")}: `{interpretation.ImportKind}`");
        builder.AppendLine($"- {L(language, "label.interpretation_mode", "Interpretation Mode")}: `{interpretationMode}`");
        builder.AppendLine($"- {L(language, "label.scanner_topology", "Scanner Topology")}: `{topologyKind}`");
        builder.AppendLine($"- {L(language, "label.safe_import_mode", "Safe Import Mode")}: `{safeImportMode}`");
        builder.AppendLine($"- {L(language, "label.scan_health", "Scan health")}: `{pack?.ProjectProfile.Health.ToString() ?? "Unknown"}`");
        builder.AppendLine($"- {L(language, "label.truth_status", "Truth Status")}: `{L(language, "value.preview_only_not_canonical", "Preview only / not canonical yet")}`");
        builder.AppendLine();

        builder.AppendLine(SectionHeading(language, "section.project.topology_scope", "Topology and scope"));
        builder.AppendLine();
        AppendSectionConfidence(builder, language, WorkspaceEvidenceConfidenceLevel.Confirmed, "note.topology_scope_boundary", "Topology/mode is produced by scanner/importer evidence, not contributor-confirmed architecture.");
        if (sourceRoots.Count > 0)
        {
            var sourceRootsLabel = isNonStandardTopology
                ? L(language, "label.observed_source_like_zones", "Observed source-like zones")
                : L(language, "label.source_roots", "Source Roots");
            builder.AppendLine($"- {sourceRootsLabel}: {string.Join(", ", sourceRoots.Select(static root => $"`{root}`"))}");
        }
        else
        {
            builder.AppendLine($"- {L(language, "label.source_roots", "Source Roots")}: {L(language, "value.unknown", "Unknown")}");
        }

        AppendInline(builder, L(language, "label.build_roots", "Build Roots"), pack?.ProjectProfile.BuildRoots);
        AppendInline(builder, L(language, "label.structural_anomalies", "Structural Anomalies"), pack?.ProjectProfile.StructuralAnomalies);
        if (topology is not null)
        {
            AppendInline(builder, L(language, "label.topology_uncertainty", "Topology Uncertainty"), topology.UncertaintyReasons);
            AppendInline(builder, L(language, "label.release_output_zones", "Release / Output Zones"), topology.ReleaseOutputZones);
            AppendInline(builder, L(language, "label.ignored_noise_zones", "Ignored / Noise Zones"), topology.IgnoredNoiseZones);
        }

        if (interpretationMode == ProjectInterpretationMode.SingleProject)
        {
            if (isStandardSingleProjectTopology)
            {
                builder.AppendLine($"- {L(language, "label.topology_status", "Topology Status")}: {L(language, "value.single_project_interpretation", "Single project interpretation")}.");
            }
            else
            {
                builder.AppendLine($"- {L(language, "label.topology_status", "Topology Status")}: `{topologyKind}`; {L(language, "sentence.normal_app_not_confirmed", "normal single-application assumptions are not confirmed")}.");
                builder.AppendLine($"- {L(language, "sentence.safe_mode_visible", "Safe mode must remain visible until a contributor selects or confirms the active project shape")}.");
            }
        }
        else
        {
            builder.AppendLine($"- {L(language, "label.interpretation_status", "Interpretation Status")}: `{interpretationMode}`.");
            if (isNonStandardTopology)
            {
                builder.AppendLine($"- {L(language, "label.topology_status", "Topology Status")}: `{topologyKind}`; {L(language, "sentence.normal_app_not_confirmed", "normal single-application assumptions are not confirmed")}.");
                builder.AppendLine($"- {L(language, "sentence.unified_arch_not_confirmed", "Unified architecture across the whole folder is not confirmed")}.");
                builder.AppendLine($"- {L(language, "sentence.safe_mode_visible", "Safe mode must remain visible until a contributor selects or confirms the active project shape")}.");
            }
            else
            {
                builder.AppendLine($"- {L(language, "sentence.unified_arch_not_confirmed", "Unified architecture across the whole folder is not confirmed")}.");
                builder.AppendLine($"- {L(language, "sentence.signals_not_shared_arch", "Any technical signals below describe observed folder evidence, not a shared project architecture")}.");
            }
        }
        builder.AppendLine();

        builder.AppendLine(SectionHeading(language, "section.project.description", "What this project appears to be"));
        builder.AppendLine();
        AppendSectionConfidence(builder, language, confirmedSignals.Count > 0 ? WorkspaceEvidenceConfidenceLevel.Likely : WorkspaceEvidenceConfidenceLevel.Unknown, "note.importer_summary_boundary", "Importer-owned summary; contributor confirmation is still required.");
        builder.AppendLine($"- {interpretation.SummaryLine}");
        foreach (var detail in interpretation.ProjectDetails.Take(4))
        {
            builder.AppendLine($"- {detail}");
        }
        builder.AppendLine();

        builder.AppendLine(SectionHeading(language, "section.project.observed_structure", "Observed structure"));
        builder.AppendLine();
        AppendSectionConfidence(builder, language, entryPoints.Count > 0 || modules.Count > 0 ? WorkspaceEvidenceConfidenceLevel.Likely : WorkspaceEvidenceConfidenceLevel.Unknown, "note.observed_structure_boundary", "Entry points and modules are interpreted from scanner/importer evidence.");

        if (entryPoints.Count > 0)
        {
            var primary = entryPoints[0];
            var confirmedMain = entryPoints.FirstOrDefault(IsConfirmedExecutableOrCodeMainEntry);
            if (confirmedMain is null)
            {
                builder.AppendLine($"- {L(language, "label.confirmed_main_entry", "Confirmed main entry")}: {L(language, "value.unknown", "Unknown")}");
            }

            builder.AppendLine(IsPackageSurfaceEntry(primary)
                ? $"- {L(language, "label.selected_package_surface", "Selected package surface")}: `{primary.RelativePath}` [{primary.Confidence}]"
                : ShouldUseCandidateEntrySurface(topologyKind, primary)
                    ? $"- {L(language, "label.candidate_entry_surface", "Candidate entry surface")}: `{primary.RelativePath}` [{primary.Confidence}]"
                : $"- {L(language, "label.main_entry", "Main Entry")}: `{primary.RelativePath}` [{primary.Confidence}]");
            if (entryPoints.Count > 1)
            {
                builder.AppendLine($"- {L(language, "label.likely_entries", "Likely Entries")}: {string.Join(", ", entryPoints.Skip(1).Take(3).Select(entry => $"`{entry.RelativePath}` [{entry.Confidence}]"))}");
            }
        }
        else
        {
            builder.AppendLine($"- {L(language, "label.confirmed_main_entry", "Confirmed main entry")}: {L(language, "value.unknown", "Unknown")}");
        }

        if (interpretationMode == ProjectInterpretationMode.SingleProject && modules.Count > 0)
        {
            builder.AppendLine($"- {L(language, "label.key_modules", "Key Modules")}: {string.Join(", ", modules.Take(5).Select(module => $"`{module.Name}` [{module.Confidence}]"))}");
        }
        else if (interpretationMode != ProjectInterpretationMode.SingleProject)
        {
            var suppressionKey = IsContainerTopology(topologyKind)
                ? "sentence.module_map_suppressed_container"
                : "sentence.module_map_suppressed";
            var suppressionFallback = IsContainerTopology(topologyKind)
                ? "Unified module map is suppressed for this container"
                : "Unified module map is suppressed for this topology";
            builder.AppendLine($"- {L(language, "label.key_modules", "Key Modules")}: {L(language, suppressionKey, suppressionFallback)}.");
        }
        else
        {
            builder.AppendLine($"- {L(language, "label.key_modules", "Key Modules")}: {L(language, "value.unknown", "Unknown")}");
        }
        builder.AppendLine();

        builder.AppendLine(SectionHeading(language, "section.project.runtime_signals", "Runtime / stack signals"));
        builder.AppendLine();
        AppendSectionConfidence(builder, language, HasAnyValues(pack?.TechnicalPassport.ObservedLanguages, pack?.TechnicalPassport.Frameworks, pack?.TechnicalPassport.BuildSystems, pack?.TechnicalPassport.Toolchains)
            ? WorkspaceEvidenceConfidenceLevel.Likely
            : WorkspaceEvidenceConfidenceLevel.Unknown,
            "note.runtime_signals_boundary",
            interpretationMode == ProjectInterpretationMode.SingleProject
                ? "Technical passport values are observed signals, not architectural rules."
                : "Technical passport values are observed across the container and do not prove one unified stack.");
        AppendInline(builder, L(language, "label.languages", "Languages"), pack?.TechnicalPassport.ObservedLanguages);
        AppendInline(builder, L(language, "label.frameworks", "Frameworks"), pack?.TechnicalPassport.Frameworks);
        AppendInline(builder, L(language, "label.build_systems", "Build Systems"), pack?.TechnicalPassport.BuildSystems);
        AppendInline(builder, L(language, "label.toolchains", "Toolchains"), pack?.TechnicalPassport.Toolchains);
        AppendInline(builder, L(language, "label.target_platforms", "Target Platforms"), pack?.TechnicalPassport.TargetPlatforms);
        AppendInline(builder, L(language, "label.runtime_surfaces", "Runtime Surfaces"), pack?.TechnicalPassport.RuntimeSurfaces);
        AppendInline(builder, L(language, "label.version_hints", "Version Hints"), pack?.TechnicalPassport.VersionHints);
        AppendInline(builder, L(language, "label.build_variants", "Build Variants"), pack?.TechnicalPassport.BuildVariants);
        AppendInline(builder, L(language, "label.notable_options", "Notable Options"), pack?.TechnicalPassport.NotableOptions);
        if (!HasAnyValues(pack?.TechnicalPassport.ObservedLanguages, pack?.TechnicalPassport.Frameworks, pack?.TechnicalPassport.BuildSystems, pack?.TechnicalPassport.Toolchains))
        {
            builder.AppendLine($"- {L(language, "sentence.runtime_signals_unknown", "Runtime / stack signals remain coarse or unknown")}.");
        }
        builder.AppendLine();

        builder.AppendLine(SectionHeading(language, "section.project.confidence_split", "What is confirmed / likely / unknown"));
        builder.AppendLine();
        AppendSectionConfidence(builder, language, WorkspaceEvidenceConfidenceLevel.Confirmed, "note.confidence_split_boundary", "This section preserves the importer's explicit confidence split.");
        AppendInline(builder, L(language, "label.confirmed", "Confirmed"), confirmedSignals);
        AppendInline(builder, L(language, "label.likely", "Likely"), likelySignals);
        AppendInline(builder, L(language, "label.unknown", "Unknown"), unknownSignals);
        if (!confirmedSignals.Any() && !likelySignals.Any() && !unknownSignals.Any())
        {
            builder.AppendLine($"- {L(language, "sentence.confidence_split_coarse", "Confidence split is still coarse")}.");
        }
        builder.AppendLine();

        builder.AppendLine(SectionHeading(language, "section.project.materials", "Materials worth reading"));
        builder.AppendLine();
        AppendSectionConfidence(builder, language, topMaterials.Length > 0 ? WorkspaceEvidenceConfidenceLevel.Likely : WorkspaceEvidenceConfidenceLevel.Unknown, "note.materials_boundary", "Imported materials are context candidates, not canonical truth.");
        if (topMaterials.Length > 0)
        {
            foreach (var material in topMaterials)
            {
                builder.AppendLine($"- `{material.RelativePath}` [{material.Confidence}] kind={material.Kind} usefulness={material.PossibleUsefulness} temporal={material.TemporalStatus} context_only={material.ContextOnly}: {material.Summary}");
            }
        }
        else
        {
            builder.AppendLine($"- {L(language, "sentence.no_materials", "No clearly useful materials are stabilized yet")}.");
        }
        builder.AppendLine();

        builder.AppendLine(SectionHeading(language, "section.project.open_uncertainty", "Open uncertainty"));
        builder.AppendLine();
        AppendSectionConfidence(builder, language, WorkspaceEvidenceConfidenceLevel.Unknown, "note.open_uncertainty_boundary", "Unknowns are explicit gaps, not hidden facts.");
        if (unknownSignals.Count > 0)
        {
            foreach (var item in unknownSignals.Take(5))
            {
                builder.AppendLine($"- {item}");
            }
        }
        else if (interpretationMode != ProjectInterpretationMode.SingleProject)
        {
            builder.AppendLine($"- {L(language, "sentence.shared_arch_not_confirmed", "Shared architecture across the whole folder is not confirmed")}.");
        }
        else
        {
            builder.AppendLine($"- {L(language, "sentence.remaining_uncertainty_low", "Remaining uncertainty is low or still coarse")}.");
        }
        builder.AppendLine();

        builder.AppendLine(SectionHeading(language, "section.project.canonical_readiness", "Canonical readiness"));
        builder.AppendLine();
        var readinessConfidence = interpretationMode == ProjectInterpretationMode.SingleProject && isStandardSingleProjectTopology
            ? WorkspaceEvidenceConfidenceLevel.Likely
            : WorkspaceEvidenceConfidenceLevel.Unknown;
        AppendSectionConfidence(builder, language, readinessConfidence, "note.canonical_readiness_boundary", "Promotion is a contributor act; preview evidence alone is not truth.");
        builder.AppendLine($"- {L(language, "label.first_confirm_target", "First confirm target")}: `project.md`");
        builder.AppendLine($"- {L(language, "label.derived_companion", "Derived companion after confirm")}: `capsule.md`");
        builder.AppendLine(interpretationMode == ProjectInterpretationMode.SingleProject && isStandardSingleProjectTopology
            ? $"- {L(language, "sentence.preview_bounded_not_truth", "Current preview looks bounded enough for explicit confirm, but it is still not truth until confirmed")}."
            : $"- `{topologyKind}` {L(language, "sentence.evidence_too_coarse", "evidence remains too coarse for a strong unified truth claim without contributor review")}.");

        return builder.ToString().TrimEnd();
    }

    private static string BuildPreviewCapsuleMarkdown(
        WorkspaceImportMaterialInterpreterRunResult runResult,
        string previewProjectMarkdown,
        string previewDirectionMarkdown,
        string previewRoadmapMarkdown,
        string previewCanonMarkdown)
    {
        var markdown = BuildPreviewCapsuleMarkdown(
            Path.GetFullPath(runResult.PreviewPacket.WorkspaceRoot),
            previewProjectMarkdown,
            previewDirectionMarkdown,
            previewRoadmapMarkdown,
            previewCanonMarkdown);
        return InsertPreviewCapsuleStatus(markdown, runResult);
    }

    private static void AppendPreviewDocumentStatus(
        StringBuilder builder,
        WorkspaceImportMaterialInterpreterRunResult runResult,
        ProjectDocumentKind kind,
        bool includeHeading = true)
    {
        var language = WorkspaceDocumentationLanguagePolicy.ResolveCurrent();
        var topology = runResult.PreviewPacket.EvidencePack?.Topology;
        var topologyKind = topology?.Kind ?? "Unknown";
        var safeImportMode = topology?.SafeImportMode ?? "unknown";
        var localPath = Path.GetFullPath(runResult.PreviewPacket.WorkspaceRoot);

        builder.AppendLine(includeHeading
            ? SectionHeading(language, "section.preview_status", "Preview status")
            : $"**{L(language, "section.preview_status", "Preview status")}**");
        builder.AppendLine();
        builder.AppendLine($"- {L(language, "label.document", "Document")}: `{GetCanonicalFileName(kind)}`");
        builder.AppendLine($"- {L(language, "label.status", "Status")}: `Preview, not canonical`.");
        builder.AppendLine($"- {L(language, "sentence.document_not_canonical", "This document is not canonical truth yet")}.");
        builder.AppendLine($"- {L(language, "label.topology", "Topology")}: `{topologyKind}`.");
        builder.AppendLine($"- {L(language, "label.safe_import_mode", "Safe import mode")}: `{safeImportMode}`.");
        builder.AppendLine($"- {L(language, "label.evidence_boundary", "Evidence boundary")}: {L(language, "note.preview_status_boundary", "scanner/importer evidence projection; contributor must confirm or replace this document before canon")}.");
        builder.AppendLine($"- {L(language, "label.local_import_path", "Local import path")}: `{localPath}` ({L(language, "note.local_import_path", "local import metadata, not project truth")}).");
    }

    private static string InsertPreviewCapsuleStatus(
        string markdown,
        WorkspaceImportMaterialInterpreterRunResult runResult)
    {
        var status = new StringBuilder();
        AppendPreviewDocumentStatus(status, runResult, ProjectDocumentKind.Capsule, includeHeading: false);
        var normalized = markdown.Replace("\r\n", "\n");
        var headingIndex = normalized.IndexOf("\n# ", StringComparison.Ordinal);
        if (headingIndex >= 0)
        {
            headingIndex += 1;
        }
        else if (normalized.StartsWith("# ", StringComparison.Ordinal))
        {
            headingIndex = 0;
        }

        if (headingIndex < 0)
        {
            return markdown;
        }

        var firstBreak = normalized.IndexOf("\n\n", headingIndex, StringComparison.Ordinal);
        if (firstBreak < 0)
        {
            return markdown;
        }

        var insertAt = firstBreak + 2;
        return normalized.Insert(insertAt, status.ToString().TrimEnd() + "\n\n");
    }

    private static string BuildPreviewCapsuleMarkdown(
        string projectRootPath,
        string previewProjectMarkdown,
        string previewDirectionMarkdown,
        string previewRoadmapMarkdown,
        string previewCanonMarkdown)
    {
        var projectName = new DirectoryInfo(Path.GetFullPath(projectRootPath)).Name;
        var language = WorkspaceDocumentationLanguagePolicy.ResolveCurrent();
        var sources = new CapsuleLayerSources(
            new CapsuleSourceDocument(ProjectDocumentKind.Project, ProjectDocumentStage.PreviewDocs, "preview_project.md", previewProjectMarkdown, Exists: true),
            new CapsuleSourceDocument(ProjectDocumentKind.Direction, ProjectDocumentStage.PreviewDocs, "preview_direction.md", previewDirectionMarkdown, !string.IsNullOrWhiteSpace(previewDirectionMarkdown)),
            new CapsuleSourceDocument(ProjectDocumentKind.Roadmap, ProjectDocumentStage.PreviewDocs, "preview_roadmap.md", previewRoadmapMarkdown, !string.IsNullOrWhiteSpace(previewRoadmapMarkdown)),
            new CapsuleSourceDocument(ProjectDocumentKind.Canon, ProjectDocumentStage.PreviewDocs, "preview_canon.md", previewCanonMarkdown, !string.IsNullOrWhiteSpace(previewCanonMarkdown)));

        return BuildCapsuleV2Markdown(
            PreviewHeading(language, ProjectDocumentKind.Capsule),
            projectName,
            sources,
            activeShiftId: null,
            activeTaskId: null,
            outputStage: ProjectDocumentStage.PreviewDocs);
    }

    private static string BuildPreviewDirectionMarkdown(WorkspaceImportMaterialInterpreterRunResult runResult)
    {
        var direction = DirectionSignalInterpreter.Interpret(runResult);
        var language = WorkspaceDocumentationLanguagePolicy.ResolveCurrent();
        var builder = new StringBuilder();

        builder.AppendLine(PreviewHeading(language, ProjectDocumentKind.Direction));
        builder.AppendLine();
        AppendPreviewDocumentStatus(builder, runResult, ProjectDocumentKind.Direction);
        builder.AppendLine(L(language, "sentence.direction_review", "Contributor may reject, rewrite, or author direction from scratch before promotion."));
        builder.AppendLine();

        if (!direction.HasDirectionEvidence)
        {
            builder.AppendLine(SectionHeading(language, "section.unknown_not_established", "Unknown / not-yet-established"));
            builder.AppendLine();
            foreach (var unknown in direction.Unknowns)
            {
                builder.AppendLine($"- {unknown}");
            }

            return builder.ToString().TrimEnd();
        }

        builder.AppendLine(SectionHeading(language, "section.direction.confirmed", "Confirmed direction"));
        builder.AppendLine();
        builder.AppendLine($"- {L(language, "sentence.no_confirmed_direction", "No confirmed direction statement is derived automatically")}.");
        builder.AppendLine();

        builder.AppendLine(SectionHeading(language, "section.direction.candidates", "Likely / candidate direction signals"));
        builder.AppendLine();
        foreach (var candidate in direction.Candidates)
        {
            builder.AppendLine($"- [{candidate.Confidence}] {candidate.Text} Evidence: {candidate.Evidence}.");
        }
        builder.AppendLine();

        builder.AppendLine(SectionHeading(language, "section.unknown_not_established", "Unknown / not-yet-established"));
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
        var language = WorkspaceDocumentationLanguagePolicy.ResolveCurrent();
        var builder = new StringBuilder();

        builder.AppendLine(PreviewHeading(language, ProjectDocumentKind.Roadmap));
        builder.AppendLine();
        AppendPreviewDocumentStatus(builder, runResult, ProjectDocumentKind.Roadmap);
        builder.AppendLine(L(language, "sentence.roadmap_review", "Every phase below is candidate-level only; contributor must confirm or replace it."));
        builder.AppendLine();

        if (!roadmap.HasCandidateEvidence)
        {
            builder.AppendLine(SectionHeading(language, "section.unknown_not_established", "Unknown / not-yet-established"));
            builder.AppendLine();
            foreach (var unknown in roadmap.Unknowns)
            {
                builder.AppendLine($"- {unknown}");
            }

            return builder.ToString().TrimEnd();
        }

        builder.AppendLine(SectionHeading(language, "section.roadmap.candidates", "Candidate phases"));
        builder.AppendLine();
        foreach (var candidate in roadmap.Candidates)
        {
            builder.AppendLine($"- {L(language, "sentence.candidate_phase_from", "Candidate phase from")} {candidate.Evidence}. {L(language, "sentence.contributor_confirm_replace", "Contributor must confirm or replace")}. {candidate.Label}");
        }
        builder.AppendLine();

        builder.AppendLine(SectionHeading(language, "section.unknown_not_established", "Unknown / not-yet-established"));
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
        var language = WorkspaceDocumentationLanguagePolicy.ResolveCurrent();
        var builder = new StringBuilder();

        builder.AppendLine(PreviewHeading(language, ProjectDocumentKind.Canon));
        builder.AppendLine();
        AppendPreviewDocumentStatus(builder, runResult, ProjectDocumentKind.Canon);
        builder.AppendLine(L(language, "sentence.canon_preview_boundary", "It contains observed technical facts only; contributor-authored rules remain empty."));
        builder.AppendLine();

        builder.AppendLine(SectionHeading(language, "section.canon.observed_signals", "Observed technical signals"));
        builder.AppendLine();
        builder.AppendLine($"- {L(language, "label.confidence", "Confidence")}: `{L(language, "value.confirmed_observations", "Confirmed for listed scanner/importer observations")}`");
        builder.AppendLine($"- {L(language, "label.evidence_boundary", "Evidence Boundary")}: {L(language, "note.canon_signals_boundary", "Derived from TechnicalPassport, interpreted modules, and interpreted entry points only; these are not canon rules")}.");
        AppendObservedCanonValues(builder, L(language, "label.observed_languages", "Observed Languages"), passport?.ObservedLanguages);
        AppendObservedCanonValues(builder, L(language, "label.frameworks", "Frameworks"), passport?.Frameworks);
        AppendObservedCanonValues(builder, L(language, "label.build_systems", "Build Systems"), passport?.BuildSystems);
        AppendObservedCanonValues(builder, L(language, "label.toolchains", "Toolchains"), passport?.Toolchains);
        AppendObservedCanonValues(builder, L(language, "label.target_platforms", "Target Platforms"), passport?.TargetPlatforms);
        AppendObservedCanonValues(builder, L(language, "label.runtime_surfaces", "Runtime Surfaces"), passport?.RuntimeSurfaces);
        AppendObservedCanonValues(builder, L(language, "label.version_hints", "Version Hints"), passport?.VersionHints);
        AppendObservedCanonValues(builder, L(language, "label.config_markers", "Config Markers"), passport?.ConfigMarkers);
        AppendObservedCanonValues(builder, L(language, "label.build_variants", "Build Variants"), passport?.BuildVariants);
        AppendObservedCanonValues(builder, L(language, "label.notable_options", "Notable Options"), passport?.NotableOptions);
        if (modules.Count > 0)
        {
            builder.AppendLine($"- {L(language, "label.observed_modules", "Observed Modules")}: {string.Join(", ", modules.Take(8).Select(static module => $"`{module.Name}` [{module.Confidence}]"))}");
        }

        if (entryPoints.Count > 0)
        {
            builder.AppendLine($"- {L(language, "label.observed_entry_points", "Observed Entry Points")}: {string.Join(", ", entryPoints.Take(8).Select(static entry => $"`{entry.RelativePath}` [{entry.Confidence}]"))}");
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
            builder.AppendLine($"- {L(language, "sentence.no_technical_signals", "No technical signals are confirmed by the current import evidence")}.");
        }
        builder.AppendLine();

        builder.AppendLine(SectionHeading(language, "section.canon.contributor_rules", "Contributor-authored rules"));
        builder.AppendLine();
        builder.AppendLine($"- {L(language, "label.confidence", "Confidence")}: `{L(language, "value.none_contributor_owned", "None / contributor-owned")}`");
        builder.AppendLine($"- {L(language, "sentence.no_authored_rules", "No authored rules yet. Contributor must add review rules / execution rules / intent rules here")}.");
        builder.AppendLine($"- {L(language, "sentence.importer_must_not_derive_rules", "The importer must not derive these rules from observed frameworks, modules, filenames, README prose, or code layout")}.");
        builder.AppendLine();

        builder.AppendLine(SectionHeading(language, "section.unknown_not_established", "Unknown / not-yet-established"));
        builder.AppendLine();
        builder.AppendLine($"- {L(language, "label.confidence", "Confidence")}: `Unknown`");
        builder.AppendLine($"- {L(language, "label.not_yet_canonical", "What is not yet canonical")}: {L(language, "value.review_workflow", "review workflow")}.");
        builder.AppendLine($"- {L(language, "label.not_yet_canonical", "What is not yet canonical")}: {L(language, "value.execution_boundaries", "execution boundaries")}.");
        builder.AppendLine($"- {L(language, "label.not_yet_canonical", "What is not yet canonical")}: {L(language, "value.refusal_rules", "refusal rules")}.");
        builder.AppendLine($"- {L(language, "label.not_yet_canonical", "What is not yet canonical")}: {L(language, "value.truth_mutation_limits", "truth mutation limits")}.");
        builder.AppendLine($"- {L(language, "label.not_yet_canonical", "What is not yet canonical")}: {L(language, "value.scope_discipline", "scope discipline")}.");
        builder.AppendLine($"- {L(language, "sentence.contributor_author_rules", "Contributor must confirm, replace, or author these rules before promotion")}.");

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

    private static string BuildCanonicalMarkdown(ProjectDocumentKind kind, string previewMarkdown)
    {
        var lines = previewMarkdown.Replace("\r\n", "\n").Split('\n');
        var body = lines.SkipWhile(static line => !line.StartsWith("## ", StringComparison.Ordinal)).ToArray();
        var builder = new StringBuilder();

        builder.AppendLine($"# {GetCanonicalTitle(kind)}");
        builder.AppendLine();
        builder.AppendLine($"Confirmed canonical {GetCanonicalFileName(kind)} materialized from `{GetPreviewFileName(kind)}`.");
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
            builder.AppendLine("- Preview content had no parseable sections. Contributor confirmed the document boundary, but body remains coarse.");
        }

        return builder.ToString().TrimEnd();
    }

    private static string BuildCanonicalCapsuleMarkdown(
        ProjectState projectState,
        string projectRootPath)
    {
        var sources = new CapsuleLayerSources(
            ReadLayerSource(projectState.TruthPointers.ProjectDocumentPath, ProjectDocumentKind.Project, "project.md", ProjectDocumentStage.CanonicalDocs, ProjectDocumentPathResolver.GetPreviewProjectPath(projectRootPath), "preview_project.md"),
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
        var language = WorkspaceDocumentationLanguagePolicy.ResolveCurrent();
        var builder = new StringBuilder();

        builder.AppendLine("---");
        builder.AppendLine($"source_stage: {sourceStage}");
        builder.AppendLine("---");
        builder.AppendLine();
        builder.AppendLine(heading);
        builder.AppendLine();
        builder.AppendLine(L(language, "sentence.capsule_intro", "Derived capsule v2. This document is a compressed view over Layer A sources, not an independent truth layer."));
        if (sourceStage == "preview")
        {
            builder.AppendLine(L(language, "sentence.capsule_preview_obligation", "Reader obligation: source_stage preview is below canonical truth."));
        }
        builder.AppendLine();

        builder.AppendLine(SectionHeading(language, "section.capsule.project_identity", "Project identity"));
        builder.AppendLine();
        AppendSectionSource(builder, language, sources.Project);
        builder.AppendLine($"- {L(language, "label.project", "Project")}: `{projectName}`");
        AppendBulletsOrNone(builder, ExtractBulletsUnderSections(sources.Project.Markdown, SectionAliases(language, "section.project.identity", "Identity")).Take(3), L(language, "sentence.no_project_identity", "No project identity details are available."));
        builder.AppendLine();

        builder.AppendLine(SectionHeading(language, "section.capsule.project_description", "What this project is"));
        builder.AppendLine();
        AppendSectionSource(builder, language, sources.Project);
        AppendBulletsOrNone(
            builder,
            ExtractBulletsUnderSections(sources.Project.Markdown, SectionAliases(language, "section.project.description", "What this project appears to be", "What this looks like"))
                .Take(4),
            L(language, "sentence.none_established", "None established."));
        builder.AppendLine();

        builder.AppendLine(SectionHeading(language, "section.capsule.current_direction", "Current direction"));
        builder.AppendLine();
        AppendSectionSource(builder, language, sources.Direction);
        AppendBulletsOrNone(
            builder,
            ExtractBulletsUnderSections(sources.Direction.Markdown, SectionAliases(language, "section.direction.confirmed", "Confirmed direction"))
                .Concat(ExtractBulletsUnderSections(sources.Direction.Markdown, SectionAliases(language, "section.direction.candidates", "Likely / candidate direction signals")))
                .Concat(ExtractBulletsUnderSections(sources.Direction.Markdown, SectionAliases(language, "section.unknown_not_established", "Unknown / not-yet-established")))
                .Take(4),
            L(language, "sentence.none_established", "None established."));
        builder.AppendLine();

        builder.AppendLine(SectionHeading(language, "section.capsule.current_roadmap", "Current roadmap phase"));
        builder.AppendLine();
        AppendSectionSource(builder, language, sources.Roadmap);
        AppendBulletsOrNone(
            builder,
            ExtractBulletsUnderSections(sources.Roadmap.Markdown, SectionAliases(language, "section.roadmap.candidates", "Candidate phases"))
                .Concat(ExtractBulletsUnderSections(sources.Roadmap.Markdown, SectionAliases(language, "section.unknown_not_established", "Unknown / not-yet-established")))
                .Take(2),
            L(language, "sentence.none_established", "None established."));
        builder.AppendLine();

        builder.AppendLine(SectionHeading(language, "section.capsule.core_canon_rules", "Core canon rules"));
        builder.AppendLine();
        AppendSectionSource(builder, language, sources.Canon);
        AppendBulletsOrNone(
            builder,
            ExtractBulletsUnderSections(sources.Canon.Markdown, SectionAliases(language, "section.canon.contributor_rules", "Contributor-authored rules"))
                .Take(6),
            L(language, "sentence.none_established", "None established."));
        builder.AppendLine();

        builder.AppendLine(SectionHeading(language, "section.capsule.current_focus", "Current focus"));
        builder.AppendLine();
        builder.AppendLine($"- {L(language, "label.source", "Source")}: runtime overlay [runtime]");
        builder.AppendLine($"- {L(language, "label.overlay_boundary", "Overlay boundary")}: {L(language, "sentence.runtime_overlay_boundary", "this section is runtime state only, not Layer A truth")}.");
        if (string.IsNullOrWhiteSpace(activeShiftId) && string.IsNullOrWhiteSpace(activeTaskId))
        {
            builder.AppendLine($"- {L(language, "label.active_shift", "Active shift")}: none.");
            builder.AppendLine($"- {L(language, "label.active_task", "Active task")}: none.");
        }
        else
        {
            builder.AppendLine($"- {L(language, "label.active_shift", "Active shift")}: `{activeShiftId ?? "none"}`");
            builder.AppendLine($"- {L(language, "label.active_task", "Active task")}: `{activeTaskId ?? "none"}`");
        }
        builder.AppendLine();

        builder.AppendLine(SectionHeading(language, "section.capsule.open_risks", "Open risks / unresolved items"));
        builder.AppendLine();
        builder.AppendLine($"- {L(language, "label.source", "Source")}: compressed Layer A unknowns [mixed]");
        AppendBulletsOrNone(
            builder,
            ExtractBulletsUnderSections(sources.Project.Markdown, SectionAliases(language, "section.project.open_uncertainty", "Open uncertainty"))
                .Concat(ExtractBulletsUnderSections(sources.Direction.Markdown, SectionAliases(language, "section.unknown_not_established", "Unknown / not-yet-established")))
                .Concat(ExtractBulletsUnderSections(sources.Roadmap.Markdown, SectionAliases(language, "section.unknown_not_established", "Unknown / not-yet-established")))
                .Concat(ExtractBulletsUnderSections(sources.Canon.Markdown, SectionAliases(language, "section.unknown_not_established", "Unknown / not-yet-established")))
                .Distinct(StringComparer.Ordinal)
                .Take(5),
            L(language, "sentence.none_listed", "None listed."));
        builder.AppendLine();

        builder.AppendLine(SectionHeading(language, "section.capsule.completeness", "Canon completeness status"));
        builder.AppendLine();
        builder.AppendLine($"- {L(language, "label.source", "Source")}: derived from 5/5 document state [derived]");
        builder.AppendLine($"- source_stage: `{sourceStage}`");
        builder.AppendLine($"- {L(language, "label.status", "Status")}: {BuildCompletenessStatus(sources, outputStage)}");
        builder.AppendLine($"- {L(language, "label.project_identity_source", "Project identity source")}: {FormatStage(sources.Project)}");
        builder.AppendLine($"- {L(language, "label.project_description_source", "What this project is source")}: {FormatStage(sources.Project)}");
        builder.AppendLine($"- {L(language, "label.current_direction_source", "Current direction source")}: {FormatStage(sources.Direction)}");
        builder.AppendLine($"- {L(language, "label.current_roadmap_source", "Current roadmap phase source")}: {FormatStage(sources.Roadmap)}");
        builder.AppendLine($"- {L(language, "label.core_canon_source", "Core canon rules source")}: {FormatStage(sources.Canon)}");
        builder.AppendLine($"- {L(language, "label.current_focus_source", "Current focus source")}: runtime overlay.");
        builder.AppendLine($"- {L(language, "label.open_risks_source", "Open risks / unresolved items source")}: mixed Layer A unknowns.");

        return builder.ToString().TrimEnd();
    }

    private static ProjectDocumentSourceDescriptor CreateDescriptor(ProjectDocumentKind kind, ProjectDocumentStage stage, string path)
    {
        return new ProjectDocumentSourceDescriptor(kind, stage, path, File.Exists(path));
    }

    private static string GetPreviewPath(string projectRootPath, ProjectDocumentKind kind)
    {
        return kind switch
        {
            ProjectDocumentKind.Project => ProjectDocumentPathResolver.GetPreviewProjectPath(projectRootPath),
            ProjectDocumentKind.Direction => ProjectDocumentPathResolver.GetPreviewDirectionPath(projectRootPath),
            ProjectDocumentKind.Roadmap => ProjectDocumentPathResolver.GetPreviewRoadmapPath(projectRootPath),
            ProjectDocumentKind.Canon => ProjectDocumentPathResolver.GetPreviewCanonPath(projectRootPath),
            ProjectDocumentKind.Capsule => ProjectDocumentPathResolver.GetPreviewCapsulePath(projectRootPath),
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown project document kind.")
        };
    }

    private static string GetCanonicalPath(ProjectState projectState, ProjectDocumentKind kind)
    {
        return kind switch
        {
            ProjectDocumentKind.Project => projectState.TruthPointers.ProjectDocumentPath,
            ProjectDocumentKind.Direction => projectState.TruthPointers.DirectionDocumentPath,
            ProjectDocumentKind.Roadmap => projectState.TruthPointers.RoadmapDocumentPath,
            ProjectDocumentKind.Canon => projectState.TruthPointers.CanonDocumentPath,
            ProjectDocumentKind.Capsule => projectState.TruthPointers.CapsuleDocumentPath,
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown project document kind.")
        };
    }

    private static string GetCanonicalTitle(ProjectDocumentKind kind)
    {
        return kind switch
        {
            ProjectDocumentKind.Project => "Project",
            ProjectDocumentKind.Direction => "Direction",
            ProjectDocumentKind.Roadmap => "Roadmap",
            ProjectDocumentKind.Canon => "Canon",
            ProjectDocumentKind.Capsule => "Capsule",
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown project document kind.")
        };
    }

    private static string GetCanonicalFileName(ProjectDocumentKind kind)
    {
        return kind switch
        {
            ProjectDocumentKind.Project => "project.md",
            ProjectDocumentKind.Direction => "direction.md",
            ProjectDocumentKind.Roadmap => "roadmap.md",
            ProjectDocumentKind.Canon => "canon.md",
            ProjectDocumentKind.Capsule => "capsule.md",
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown project document kind.")
        };
    }

    private static string GetPreviewFileName(ProjectDocumentKind kind)
    {
        return kind switch
        {
            ProjectDocumentKind.Project => "preview_project.md",
            ProjectDocumentKind.Direction => "preview_direction.md",
            ProjectDocumentKind.Roadmap => "preview_roadmap.md",
            ProjectDocumentKind.Canon => "preview_canon.md",
            ProjectDocumentKind.Capsule => "preview_capsule.md",
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown project document kind.")
        };
    }

    private static string ReadRequired(string path, ProjectDocumentKind kind)
    {
        if (!File.Exists(path))
        {
            throw new InvalidOperationException($"Preview {GetCanonicalFileName(kind)} document must exist before canonical promotion.");
        }

        return File.ReadAllText(path, Encoding.UTF8);
    }

    private static void TryRegenerateCanonicalCapsule(ProjectState projectState, string projectRootPath)
    {
        if (!File.Exists(projectState.TruthPointers.CapsuleDocumentPath) ||
            !File.Exists(projectState.TruthPointers.ProjectDocumentPath))
        {
            return;
        }

        var canonicalCapsuleMarkdown = BuildCanonicalCapsuleMarkdown(projectState, projectRootPath);
        File.WriteAllText(projectState.TruthPointers.CapsuleDocumentPath, canonicalCapsuleMarkdown, Encoding.UTF8);
    }

    private static PromotionAttribution WritePromotionAttribution(
        ProjectState projectState,
        ProjectDocumentKind kind,
        string contributorId,
        string previewPath,
        string previewHash,
        string canonicalPath)
    {
        var timestamp = DateTimeOffset.UtcNow;
        var decisionsRoot = Path.Combine(projectState.Paths.ZavodRoot, "decisions");
        var traceRoot = Path.Combine(projectState.Paths.ZavodRoot, "journal", "trace");
        Directory.CreateDirectory(decisionsRoot);
        Directory.CreateDirectory(traceRoot);

        var decisionNumber = ResolveNextDecisionNumber(decisionsRoot);
        var decisionId = $"DEC-{decisionNumber:0000}";
        var kindFileName = GetCanonicalFileName(kind);
        var decisionPath = Path.Combine(decisionsRoot, $"{decisionId}-promote-{kindFileName.Replace(".", "-", StringComparison.OrdinalIgnoreCase)}.md");
        var canonicalEventId = BuildJournalEventId(timestamp, $"canonical_promoted|{decisionId}|{kind}|{previewHash}");
        var decisionEventId = BuildJournalEventId(timestamp, $"decision_recorded|{decisionId}|canonical_promotion");
        var journalPath = Path.Combine(traceRoot, $"{timestamp.UtcDateTime:yyyy-MM-dd}.jsonl");

        File.WriteAllText(
            decisionPath,
            BuildPromotionDecisionMarkdown(
                decisionId,
                kind,
                timestamp,
                contributorId,
                canonicalEventId,
                projectState.Paths.ProjectRoot,
                previewPath,
                previewHash,
                canonicalPath),
            Encoding.UTF8);

        AppendJournalEvent(journalPath, new
        {
            event_type = "decision_recorded",
            timestamp = timestamp.UtcDateTime.ToString("O"),
            event_id = decisionEventId,
            decision_id = decisionId,
            payload = new
            {
                decision_id = decisionId,
                type = "canonical_promotion"
            }
        });
        AppendJournalEvent(journalPath, new
        {
            event_type = "canonical_promoted",
            timestamp = timestamp.UtcDateTime.ToString("O"),
            event_id = canonicalEventId,
            decision_id = decisionId,
            payload = new
            {
                kind = kind.ToString().ToLowerInvariant(),
                from_preview_ref = new
                {
                    path = NormalizeRelativePath(projectState.Paths.ProjectRoot, previewPath),
                    sha256 = previewHash
                },
                canonical_path = NormalizeRelativePath(projectState.Paths.ProjectRoot, canonicalPath)
            }
        });

        return new PromotionAttribution(decisionId, decisionPath, decisionEventId, canonicalEventId, journalPath);
    }

    private static PreviewRejectionJournalEvent WritePreviewRejectedJournalEvent(
        ProjectState projectState,
        ProjectDocumentKind kind,
        string contributorId,
        string previewPath,
        string previewHash)
    {
        var timestamp = DateTimeOffset.UtcNow;
        var traceRoot = Path.Combine(projectState.Paths.ZavodRoot, "journal", "trace");
        Directory.CreateDirectory(traceRoot);

        var eventId = BuildJournalEventId(timestamp, $"preview_rejected|{kind}|{previewHash}|{contributorId}");
        var journalPath = Path.Combine(traceRoot, $"{timestamp.UtcDateTime:yyyy-MM-dd}.jsonl");

        AppendJournalEvent(journalPath, new
        {
            event_type = "preview_rejected",
            timestamp = timestamp.UtcDateTime.ToString("O"),
            event_id = eventId,
            payload = new
            {
                kind = kind.ToString().ToLowerInvariant(),
                contributor = SanitizeFrontmatterScalar(contributorId),
                preview_ref = new
                {
                    path = NormalizeRelativePath(projectState.Paths.ProjectRoot, previewPath),
                    sha256 = previewHash
                }
            }
        });

        return new PreviewRejectionJournalEvent(eventId, journalPath);
    }

    private static string BuildPromotionDecisionMarkdown(
        string decisionId,
        ProjectDocumentKind kind,
        DateTimeOffset timestamp,
        string contributorId,
        string relatedJournalEventId,
        string projectRootPath,
        string previewPath,
        string previewHash,
        string canonicalPath)
    {
        var builder = new StringBuilder();
        var kindFileName = GetCanonicalFileName(kind);

        builder.AppendLine("---");
        builder.AppendLine($"id: {decisionId}");
        builder.AppendLine("type: canonical_promotion");
        builder.AppendLine($"timestamp: {timestamp.UtcDateTime:O}");
        builder.AppendLine($"contributor: {SanitizeFrontmatterScalar(contributorId)}");
        builder.AppendLine("supersedes: []");
        builder.AppendLine("superseded_by: null");
        builder.AppendLine("related_shift: null");
        builder.AppendLine("related_task: null");
        builder.AppendLine($"related_journal: {relatedJournalEventId}");
        builder.AppendLine("---");
        builder.AppendLine();
        builder.AppendLine($"# Promote {kindFileName}");
        builder.AppendLine();
        builder.AppendLine("## Context");
        builder.AppendLine();
        builder.AppendLine($"- `{GetPreviewFileName(kind)}` existed as PreviewDocs material and was explicitly promoted into `{kindFileName}`.");
        builder.AppendLine($"- Preview reference: `{NormalizeRelativePath(projectRootPath, previewPath)}`.");
        builder.AppendLine($"- Preview SHA-256: `{previewHash}`.");
        builder.AppendLine($"- Canonical target: `{NormalizeRelativePath(projectRootPath, canonicalPath)}`.");
        builder.AppendLine();
        builder.AppendLine("## Options considered");
        builder.AppendLine();
        builder.AppendLine("- Do nothing: leave the document in preview only, keeping canonical coverage incomplete.");
        builder.AppendLine($"- Promote `{GetPreviewFileName(kind)}` now: accept the reviewed preview content as the current canonical `{kindFileName}`.");
        builder.AppendLine();
        builder.AppendLine("## Chosen option");
        builder.AppendLine();
        builder.AppendLine($"- Promote `{GetPreviewFileName(kind)}` into `{kindFileName}`.");
        builder.AppendLine();
        builder.AppendLine("## Rationale");
        builder.AppendLine();
        builder.AppendLine("- Contributor confirmation approved this preview document as canonical project truth at this moment.");
        builder.AppendLine("- The promotion records the preview hash so future readers can identify the exact preview source.");
        builder.AppendLine();
        builder.AppendLine("## Invalidation criteria");
        builder.AppendLine();
        builder.AppendLine("- Revisit this decision if later scanner/importer evidence contradicts a promoted section.");
        builder.AppendLine("- Revisit this decision if a contributor replaces the canonical document through a later promotion or authoring decision.");

        return builder.ToString().TrimEnd();
    }

    private static int ResolveNextDecisionNumber(string decisionsRoot)
    {
        var max = Directory.EnumerateFiles(decisionsRoot, "DEC-*.md", SearchOption.TopDirectoryOnly)
            .Select(static path => Path.GetFileNameWithoutExtension(path))
            .Select(static name => name.Length >= 8 && int.TryParse(name.Substring(4, 4), out var value) ? value : 0)
            .DefaultIfEmpty(0)
            .Max();
        return max + 1;
    }

    private static void AppendJournalEvent(string journalPath, object journalEvent)
    {
        var serialized = JsonSerializer.Serialize(journalEvent);
        File.AppendAllText(journalPath, serialized + Environment.NewLine, Encoding.UTF8);
    }

    private static string BuildJournalEventId(DateTimeOffset timestamp, string seed)
    {
        var hash = ComputeSha256(seed)[..8].ToUpperInvariant();
        return $"EVT-{timestamp.UtcDateTime:yyyyMMddHHmmssfff}-{hash}";
    }

    private static string ComputeSha256(string value)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    private static string NormalizeRelativePath(string projectRootPath, string path)
    {
        return Path.GetRelativePath(projectRootPath, path).Replace('\\', '/');
    }

    private static string SanitizeFrontmatterScalar(string value)
    {
        return value.Replace("\r", string.Empty, StringComparison.Ordinal).Replace("\n", string.Empty, StringComparison.Ordinal).Trim();
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

    private static IReadOnlyList<string> ExtractBulletsUnderSections(string markdown, IEnumerable<string> headings)
    {
        return headings
            .SelectMany(heading => ExtractBulletsUnderSection(markdown, heading))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
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
               bullet.StartsWith("Уверенность:", StringComparison.OrdinalIgnoreCase) ||
               bullet.StartsWith("Граница evidence:", StringComparison.OrdinalIgnoreCase) ||
               bullet.StartsWith("Source:", StringComparison.OrdinalIgnoreCase);
    }

    private static void AppendSectionSource(StringBuilder builder, WorkspaceDocumentationLanguagePolicy language, CapsuleSourceDocument source)
    {
        builder.AppendLine(source.Exists
            ? $"- {L(language, "label.source", "Source")}: `{source.FileName}` [{FormatStage(source)}]"
            : $"- {L(language, "label.source", "Source")}: `{source.FileName}` [absent]");
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

    private static void AppendSectionConfidence(
        StringBuilder builder,
        WorkspaceDocumentationLanguagePolicy language,
        WorkspaceEvidenceConfidenceLevel confidence,
        string noteKey,
        string note)
    {
        builder.AppendLine($"- {L(language, "label.confidence", "Confidence")}: `{confidence}`");
        builder.AppendLine($"- {L(language, "label.evidence_boundary", "Evidence Boundary")}: {L(language, noteKey, note)}");
    }

    private static string PreviewHeading(WorkspaceDocumentationLanguagePolicy language, ProjectDocumentKind kind)
    {
        return $"# {L(language, $"doc.{kind.ToString().ToLowerInvariant()}", GetCanonicalTitle(kind))} ({L(language, "label.preview", "Preview")})";
    }

    private static string SectionHeading(WorkspaceDocumentationLanguagePolicy language, string key, string english)
    {
        return $"## {L(language, key, english)}";
    }

    private static IEnumerable<string> SectionAliases(WorkspaceDocumentationLanguagePolicy language, string key, params string[] englishAliases)
    {
        var aliases = new List<string>();
        foreach (var english in englishAliases)
        {
            aliases.Add($"## {english}");
        }

        var localized = SectionHeading(language, key, englishAliases.FirstOrDefault() ?? key);
        if (!aliases.Contains(localized, StringComparer.OrdinalIgnoreCase))
        {
            aliases.Add(localized);
        }

        return aliases;
    }

    private static string L(WorkspaceDocumentationLanguagePolicy language, string key, string english)
    {
        var code = language.TwoLetterIsoCode.ToLowerInvariant();
        return DocumentText.TryGetValue(code, out var catalog) && catalog.TryGetValue(key, out var localized)
            ? localized
            : english;
    }

    private static readonly IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> DocumentText =
        new Dictionary<string, IReadOnlyDictionary<string, string>>(StringComparer.OrdinalIgnoreCase)
        {
            ["ru"] = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["doc.project"] = "Проект",
                ["doc.direction"] = "Направление",
                ["doc.roadmap"] = "Roadmap",
                ["doc.canon"] = "Канон",
                ["doc.capsule"] = "Capsule",
                ["label.preview"] = "Превью",
                ["section.preview_status"] = "Статус превью",
                ["section.project.identity"] = "Идентичность",
                ["section.project.topology_scope"] = "Топология и область",
                ["section.project.description"] = "Что это за проект",
                ["section.project.observed_structure"] = "Наблюдаемая структура",
                ["section.project.runtime_signals"] = "Runtime / stack сигналы",
                ["section.project.confidence_split"] = "Что подтверждено / вероятно / неизвестно",
                ["section.project.materials"] = "Материалы для чтения",
                ["section.project.open_uncertainty"] = "Открытая неопределённость",
                ["section.project.canonical_readiness"] = "Готовность к канону",
                ["section.unknown_not_established"] = "Неизвестно / ещё не установлено",
                ["section.direction.confirmed"] = "Подтверждённое направление",
                ["section.direction.candidates"] = "Вероятные / кандидатные сигналы направления",
                ["section.roadmap.candidates"] = "Кандидатные фазы",
                ["section.canon.observed_signals"] = "Наблюдаемые технические сигналы",
                ["section.canon.contributor_rules"] = "Правила, заданные contributor",
                ["section.capsule.project_identity"] = "Идентичность проекта",
                ["section.capsule.project_description"] = "Что это за проект",
                ["section.capsule.current_direction"] = "Текущее направление",
                ["section.capsule.current_roadmap"] = "Текущая roadmap-фаза",
                ["section.capsule.core_canon_rules"] = "Основные правила канона",
                ["section.capsule.current_focus"] = "Текущий фокус",
                ["section.capsule.open_risks"] = "Открытые риски / нерешённые пункты",
                ["section.capsule.completeness"] = "Статус полноты канона",
                ["label.document"] = "Документ",
                ["label.status"] = "Статус",
                ["label.topology"] = "Топология",
                ["label.safe_import_mode"] = "Safe import mode",
                ["label.evidence_boundary"] = "Граница evidence",
                ["label.local_import_path"] = "Локальный путь импорта",
                ["label.confidence"] = "Уверенность",
                ["label.project_id"] = "Project Id",
                ["label.project_name"] = "Название проекта",
                ["label.workspace_root"] = "Workspace root",
                ["label.import_kind"] = "Тип импорта",
                ["label.interpretation_mode"] = "Режим интерпретации",
                ["label.scanner_topology"] = "Топология scanner",
                ["label.scan_health"] = "Состояние скана",
                ["label.truth_status"] = "Статус truth",
                ["label.source_roots"] = "Source roots",
                ["label.observed_source_like_zones"] = "Наблюдаемые source-like зоны",
                ["label.build_roots"] = "Build roots",
                ["label.structural_anomalies"] = "Структурные аномалии",
                ["label.topology_uncertainty"] = "Неопределённость топологии",
                ["label.release_output_zones"] = "Release / output зоны",
                ["label.ignored_noise_zones"] = "Игнорируемые / noise зоны",
                ["label.topology_status"] = "Статус топологии",
                ["label.interpretation_status"] = "Статус интерпретации",
                ["label.confirmed_main_entry"] = "Подтверждённый main entry",
                ["label.selected_package_surface"] = "Выбранная package surface",
                ["label.candidate_entry_surface"] = "Кандидатная entry surface",
                ["label.main_entry"] = "Main Entry",
                ["label.likely_entries"] = "Вероятные entries",
                ["label.key_modules"] = "Ключевые модули",
                ["label.languages"] = "Языки",
                ["label.observed_languages"] = "Наблюдаемые языки",
                ["label.frameworks"] = "Frameworks",
                ["label.build_systems"] = "Build systems",
                ["label.toolchains"] = "Toolchains",
                ["label.target_platforms"] = "Target platforms",
                ["label.runtime_surfaces"] = "Runtime surfaces",
                ["label.version_hints"] = "Version hints",
                ["label.config_markers"] = "Config markers",
                ["label.build_variants"] = "Build variants",
                ["label.notable_options"] = "Notable options",
                ["label.confirmed"] = "Подтверждено",
                ["label.likely"] = "Вероятно",
                ["label.unknown"] = "Неизвестно",
                ["label.first_confirm_target"] = "Первый документ для подтверждения",
                ["label.derived_companion"] = "Производный companion после подтверждения",
                ["label.observed_modules"] = "Наблюдаемые модули",
                ["label.observed_entry_points"] = "Наблюдаемые entry points",
                ["label.not_yet_canonical"] = "Ещё не канонично",
                ["label.source"] = "Источник",
                ["label.project"] = "Проект",
                ["label.overlay_boundary"] = "Граница overlay",
                ["label.active_shift"] = "Активный shift",
                ["label.active_task"] = "Активная task",
                ["label.project_identity_source"] = "Источник идентичности проекта",
                ["label.project_description_source"] = "Источник описания проекта",
                ["label.current_direction_source"] = "Источник текущего направления",
                ["label.current_roadmap_source"] = "Источник текущей roadmap-фазы",
                ["label.core_canon_source"] = "Источник правил канона",
                ["label.current_focus_source"] = "Источник текущего фокуса",
                ["label.open_risks_source"] = "Источник открытых рисков",
                ["value.unknown"] = "Unknown",
                ["value.preview_only_not_canonical"] = "Preview only / not canonical yet",
                ["value.single_project_interpretation"] = "single project interpretation",
                ["value.confirmed_observations"] = "Confirmed for listed scanner/importer observations",
                ["value.none_contributor_owned"] = "None / contributor-owned",
                ["value.review_workflow"] = "review workflow",
                ["value.execution_boundaries"] = "execution boundaries",
                ["value.refusal_rules"] = "refusal rules",
                ["value.truth_mutation_limits"] = "truth mutation limits",
                ["value.scope_discipline"] = "scope discipline",
                ["sentence.document_not_canonical"] = "Этот документ ещё не является канонической правдой проекта",
                ["sentence.normal_app_not_confirmed"] = "обычные single-application предположения не подтверждены",
                ["sentence.safe_mode_visible"] = "Safe mode должен оставаться видимым, пока contributor не выберет или не подтвердит active project shape",
                ["sentence.unified_arch_not_confirmed"] = "Единая архитектура всей папки не подтверждена",
                ["sentence.signals_not_shared_arch"] = "Технические сигналы ниже описывают наблюдаемую папку, а не общую архитектуру проекта",
                ["sentence.module_map_suppressed"] = "Единая карта модулей подавлена для этой топологии",
                ["sentence.module_map_suppressed_container"] = "Единая карта модулей подавлена для этого container",
                ["sentence.runtime_signals_unknown"] = "Runtime / stack сигналы пока грубые или неизвестные",
                ["sentence.confidence_split_coarse"] = "Confidence split пока грубый",
                ["sentence.no_materials"] = "Стабилизированных полезных материалов пока нет",
                ["sentence.shared_arch_not_confirmed"] = "Общая архитектура всей папки не подтверждена",
                ["sentence.remaining_uncertainty_low"] = "Оставшаяся неопределённость низкая или пока грубая",
                ["sentence.preview_bounded_not_truth"] = "Текущее превью выглядит достаточно ограниченным для явного подтверждения, но не является truth до подтверждения",
                ["sentence.evidence_too_coarse"] = "evidence слишком грубое для сильного утверждения о единой truth без review contributor",
                ["sentence.direction_review"] = "Contributor может отклонить, переписать или создать direction с нуля перед promotion.",
                ["sentence.no_confirmed_direction"] = "Подтверждённое направление автоматически не выводится",
                ["sentence.roadmap_review"] = "Все фазы ниже являются candidate-level; contributor должен подтвердить или заменить их.",
                ["sentence.candidate_phase_from"] = "Кандидатная фаза из",
                ["sentence.contributor_confirm_replace"] = "Contributor должен подтвердить или заменить",
                ["sentence.canon_preview_boundary"] = "Документ содержит только наблюдаемые технические факты; contributor-authored rules остаются пустыми.",
                ["sentence.no_technical_signals"] = "Текущий import evidence не подтверждает технические сигналы",
                ["sentence.no_authored_rules"] = "Авторских правил пока нет. Contributor должен добавить review rules / execution rules / intent rules здесь",
                ["sentence.importer_must_not_derive_rules"] = "Importer не должен выводить эти правила из framework, модулей, имён файлов, README prose или code layout",
                ["sentence.contributor_author_rules"] = "Contributor должен подтвердить, заменить или написать эти правила перед promotion",
                ["sentence.capsule_intro"] = "Derived capsule v2. Это сжатый вид Layer A sources, а не независимый truth layer.",
                ["sentence.capsule_preview_obligation"] = "Reader obligation: source_stage preview находится ниже canonical truth.",
                ["sentence.no_project_identity"] = "Детали идентичности проекта недоступны.",
                ["sentence.none_established"] = "Ничего не установлено.",
                ["sentence.runtime_overlay_boundary"] = "эта секция является только runtime state, не Layer A truth",
                ["sentence.none_listed"] = "Ничего не перечислено.",
                ["note.preview_status_boundary"] = "это проекция scanner/importer evidence; contributor должен подтвердить или заменить документ перед каноном",
                ["note.local_import_path"] = "локальная метка импорта, не project truth",
                ["note.identity_boundary"] = "Workspace root и derived identity наблюдаются из файловой системы; purpose остаётся preview.",
                ["note.topology_scope_boundary"] = "Топология/режим получены из scanner/importer evidence, а не из contributor-confirmed architecture.",
                ["note.importer_summary_boundary"] = "Importer-owned summary; confirmation contributor всё ещё требуется.",
                ["note.observed_structure_boundary"] = "Entry points и modules интерпретируются из scanner/importer evidence.",
                ["note.runtime_signals_boundary"] = "Technical passport values являются наблюдаемыми сигналами и не доказывают единую архитектуру.",
                ["note.confidence_split_boundary"] = "Эта секция сохраняет explicit confidence split импортера.",
                ["note.materials_boundary"] = "Imported materials являются context candidates, не canonical truth.",
                ["note.open_uncertainty_boundary"] = "Unknowns являются явными gaps, а не скрытыми фактами.",
                ["note.canonical_readiness_boundary"] = "Promotion является действием contributor; preview evidence само по себе не truth.",
                ["note.canon_signals_boundary"] = "Derived from TechnicalPassport, interpreted modules, and interpreted entry points only; these are not canon rules"
            }
        };

    private static bool IsPackageSurfaceEntry(WorkspaceImportMaterialEntryPointInterpretation entry)
    {
        return entry.Role.Equals("package-surface", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsStandardSingleProjectTopology(string topologyKind)
    {
        return string.Equals(topologyKind, "SingleProject", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsContainerTopology(string topologyKind)
    {
        return topologyKind.Equals("Container", StringComparison.OrdinalIgnoreCase) ||
               topologyKind.Equals("MultipleIndependentProjects", StringComparison.OrdinalIgnoreCase) ||
               topologyKind.Equals("AmbiguousContainer", StringComparison.OrdinalIgnoreCase);
    }

    private static bool RequiresProjectRootSelectionBeforeCanonicalPromotion(string previewMarkdown)
    {
        if (string.IsNullOrWhiteSpace(previewMarkdown))
        {
            return false;
        }

        return previewMarkdown.Contains("Scanner Topology: `Container`", StringComparison.OrdinalIgnoreCase) ||
               previewMarkdown.Contains("Топология: `Container`", StringComparison.OrdinalIgnoreCase) ||
               previewMarkdown.Contains("Interpretation Mode: `MultipleIndependentProjects`", StringComparison.OrdinalIgnoreCase) ||
               previewMarkdown.Contains("Режим интерпретации: `MultipleIndependentProjects`", StringComparison.OrdinalIgnoreCase) ||
               previewMarkdown.Contains("Safe Import Mode: `container-review", StringComparison.OrdinalIgnoreCase) ||
               previewMarkdown.Contains("Safe import mode: `container-review", StringComparison.OrdinalIgnoreCase);
    }

    private static bool ShouldUseCandidateEntrySurface(string topologyKind, WorkspaceImportMaterialEntryPointInterpretation entry)
    {
        if (topologyKind.Equals("Decompilation", StringComparison.OrdinalIgnoreCase) ||
            topologyKind.Equals("Legacy", StringComparison.OrdinalIgnoreCase) ||
            topologyKind.Equals("MaterialOnly", StringComparison.OrdinalIgnoreCase) ||
            topologyKind.Equals("ReleaseBundle", StringComparison.OrdinalIgnoreCase) ||
            topologyKind.Equals("Ambiguous", StringComparison.OrdinalIgnoreCase) ||
            topologyKind.Equals("Container", StringComparison.OrdinalIgnoreCase) ||
            topologyKind.Equals("MultipleIndependentProjects", StringComparison.OrdinalIgnoreCase))
        {
            return entry.Confidence != WorkspaceEvidenceConfidenceLevel.Confirmed;
        }

        return (topologyKind.Equals("Mixed", StringComparison.OrdinalIgnoreCase) ||
                topologyKind.Equals("MixedSourceRelease", StringComparison.OrdinalIgnoreCase)) &&
               entry.Confidence != WorkspaceEvidenceConfidenceLevel.Confirmed;
    }

    private static bool IsConfirmedExecutableOrCodeMainEntry(WorkspaceImportMaterialEntryPointInterpretation entry)
    {
        if (entry.Confidence != WorkspaceEvidenceConfidenceLevel.Confirmed)
        {
            return false;
        }

        return entry.Role.Equals("main", StringComparison.OrdinalIgnoreCase) ||
               entry.Role.Equals("entry", StringComparison.OrdinalIgnoreCase) ||
               entry.Role.Equals("cli", StringComparison.OrdinalIgnoreCase) ||
               entry.Role.Equals("service", StringComparison.OrdinalIgnoreCase) ||
               entry.Role.Equals("bootstrap", StringComparison.OrdinalIgnoreCase) ||
               entry.Role.Equals("ui", StringComparison.OrdinalIgnoreCase);
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
    string DirectionDocumentPath,
    string RoadmapDocumentPath,
    string CanonDocumentPath,
    string CapsuleDocumentPath,
    IReadOnlyList<ProjectDocumentPromotionResult> Promotions);

public sealed record ProjectDocumentPromotionResult(
    ProjectDocumentKind Kind,
    string PreviewDocumentPath,
    string CanonicalDocumentPath,
    string PreviewSha256,
    string DecisionId,
    string DecisionPath,
    string DecisionRecordedEventId,
    string CanonicalPromotedEventId,
    string JournalPath);

public sealed record ProjectDocumentRejectionResult(
    ProjectDocumentKind Kind,
    string PreviewDocumentPath,
    string PreviewSha256,
    string PreviewRejectedEventId,
    string JournalPath);

internal sealed record PromotionAttribution(
    string DecisionId,
    string DecisionPath,
    string DecisionRecordedEventId,
    string CanonicalPromotedEventId,
    string JournalPath);

internal sealed record PreviewRejectionJournalEvent(
    string EventId,
    string JournalPath);
