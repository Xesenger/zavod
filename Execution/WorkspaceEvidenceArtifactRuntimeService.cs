using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.RegularExpressions;
using zavod.Persistence;
using zavod.Workspace;

namespace zavod.Execution;

public sealed class WorkspaceEvidenceArtifactRuntimeService(
    ArchitectureDiagramRuntimeService? diagramRuntime = null)
{
    private readonly ArchitectureDiagramRuntimeService _diagramRuntime = diagramRuntime ?? new ArchitectureDiagramRuntimeService();
    private readonly ProjectDocumentRuntimeService _projectDocumentRuntime = new();

    public WorkspaceEvidenceArtifactBundle WriteBundle(
        WorkspaceImportMaterialInterpreterRunResult runResult,
        string? outputDirectory = null,
        bool writePdf = false)
    {
        ArgumentNullException.ThrowIfNull(runResult);

        var directory = outputDirectory ?? Path.Combine(ResolveArtifactRoot(runResult), ".zavod", "import_evidence_bundle");
        Directory.CreateDirectory(directory);

        var scanRunPath = Path.Combine(directory, "scanrun.json");
        var projectProfilePath = Path.Combine(directory, "project_profile.json");
        var projectReportPath = Path.Combine(directory, "project_report.md");
        var scanSummaryPath = Path.Combine(directory, "scan_summary.md");
        var filesIndexPath = Path.Combine(directory, "files.index.json");
        var manifestsIndexPath = Path.Combine(directory, "manifests.index.json");
        var symbolsIndexPath = Path.Combine(directory, "symbols.index.json");
        var edgesIndexPath = Path.Combine(directory, "edges.index.json");
        var entryPointsIndexPath = Path.Combine(directory, "entrypoints.index.json");
        var modulesMapPath = Path.Combine(directory, "modules.map.json");
        var projectUnitsIndexPath = Path.Combine(directory, "project_units.index.json");
        var runProfilesIndexPath = Path.Combine(directory, "runprofiles.index.json");
        var topologyIndexPath = Path.Combine(directory, "topology.index.json");
        var predicateRegistryPath = Path.Combine(directory, "predicate_registry.json");
        var scanBudgetPath = Path.Combine(directory, "scan_budget.json");
        var uncertaintyReportPath = Path.Combine(directory, "uncertainty_report.json");
        var technicalPassportPath = Path.Combine(directory, "technical_passport.json");
        var rawObservationsPath = Path.Combine(directory, "raw_observations.json");
        var derivedPatternsPath = Path.Combine(directory, "derived_patterns.json");
        var signalScoresPath = Path.Combine(directory, "signal_scores.json");
        var codeEdgesPath = Path.Combine(directory, "code_edges.json");
        var signatureHintsPath = Path.Combine(directory, "signature_hints.json");
        var symbolHintsPath = Path.Combine(directory, "symbol_hints.json");
        var dependencySurfacePath = Path.Combine(directory, "dependency_surface.json");
        var confidenceAnnotationsPath = Path.Combine(directory, "confidence_annotations.json");
        var entryPointsPath = Path.Combine(directory, "entry_points.json");
        var candidatesPath = Path.Combine(directory, "candidates.json");
        var layerMapPath = Path.Combine(directory, "layer_map.json");
        var moduleCandidatesPath = Path.Combine(directory, "module_candidates.json");
        var dependencyEdgesPath = Path.Combine(directory, "dependency_edges.json");
        var hotspotsPath = Path.Combine(directory, "hotspots.json");
        var stageSignalsPath = Path.Combine(directory, "stage_signals.json");
        var originDetectionPath = Path.Combine(directory, "origin_detection.json");
        var evidenceSnippetsPath = Path.Combine(directory, "evidence_snippets.json");
        var diagramSpecPath = Path.Combine(directory, "diagram_spec.json");
        var architectureMapPath = Path.Combine(directory, "architecture_map.png");
        var previewPath = Path.Combine(Path.GetDirectoryName(directory) ?? directory, "preview.html");
        var bundlePreviewPath = Path.Combine(directory, "preview.html");
        var pdfPath = writePdf ? Path.Combine(directory, "project_report.pdf") : null;
        var artifactRoot = ResolveArtifactRoot(runResult);

        var pack = runResult.PreviewPacket.EvidencePack;
        var packSignals = pack?.Signals ?? Array.Empty<WorkspaceEvidenceSignal>();
        var packModuleCandidates = pack?.Candidates?.ModuleCandidates ?? Array.Empty<WorkspaceEvidenceModule>();
        var packEntryPoints = pack?.EntryPoints ?? Array.Empty<WorkspaceEvidenceEntryPoint>();
        var interpretationEntryPoints = runResult.Interpretation.EntryPoints ?? Array.Empty<WorkspaceImportMaterialEntryPointInterpretation>();
        var interpretationLayers = runResult.Interpretation.Layers ?? Array.Empty<WorkspaceImportMaterialLayerInterpretation>();
        var interpretationModules = runResult.Interpretation.Modules ?? Array.Empty<WorkspaceImportMaterialModuleInterpretation>();
        var interpretationStageSignals = runResult.Interpretation.ProjectStageSignals ?? Array.Empty<string>();
        var interpretationCurrentSignals = runResult.Interpretation.CurrentSignals ?? Array.Empty<string>();
        var interpretationPlannedSignals = runResult.Interpretation.PlannedSignals ?? Array.Empty<string>();
        var interpretationPossiblyStaleSignals = runResult.Interpretation.PossiblyStaleSignals ?? Array.Empty<string>();
        var interpretationConflicts = runResult.Interpretation.Conflicts ?? Array.Empty<string>();
        var entryPointPayload = interpretationEntryPoints.Any()
            ? (object)interpretationEntryPoints
            : packEntryPoints;
        var layerPayload = interpretationLayers.Any()
            ? (object)interpretationLayers
            : Array.Empty<WorkspaceImportMaterialLayerInterpretation>();
        var modulePayload = interpretationModules.Any()
            ? (object)interpretationModules
            : packModuleCandidates;
        var stagePayload = new
        {
            stage = interpretationStageSignals,
            current = interpretationCurrentSignals,
            planned = interpretationPlannedSignals,
            possiblyStale = interpretationPossiblyStaleSignals,
            conflicts = interpretationConflicts,
            evidence = packSignals
                .Where(static signal => string.Equals(signal.Category, "stage", StringComparison.OrdinalIgnoreCase) ||
                                        string.Equals(signal.Category, "temporal", StringComparison.OrdinalIgnoreCase))
                .ToArray()
        };
        var originPayload = packSignals
            .Where(static signal => string.Equals(signal.Category, "origin", StringComparison.OrdinalIgnoreCase))
            .ToArray();

        var projectReport = BuildProjectReport(runResult);

        File.WriteAllText(scanRunPath, JsonSerializer.Serialize(pack?.ScanRun, JsonOptions), Encoding.UTF8);
        File.WriteAllText(projectProfilePath, JsonSerializer.Serialize(pack?.ProjectProfile, JsonOptions), Encoding.UTF8);
        File.WriteAllText(projectReportPath, projectReport, Encoding.UTF8);
        File.WriteAllText(scanSummaryPath, BuildScanSummary(runResult), Encoding.UTF8);
        File.WriteAllText(filesIndexPath, JsonSerializer.Serialize(pack?.FileIndex ?? Array.Empty<WorkspaceEvidenceFileIndexItem>(), JsonOptions), Encoding.UTF8);
        File.WriteAllText(manifestsIndexPath, JsonSerializer.Serialize(BuildManifestIndex(pack), JsonOptions), Encoding.UTF8);
        File.WriteAllText(symbolsIndexPath, JsonSerializer.Serialize(BuildSymbolIndex(pack), JsonOptions), Encoding.UTF8);
        File.WriteAllText(edgesIndexPath, JsonSerializer.Serialize(BuildEdgesIndex(pack), JsonOptions), Encoding.UTF8);
        File.WriteAllText(entryPointsIndexPath, JsonSerializer.Serialize(pack?.Candidates?.EntryPoints ?? Array.Empty<WorkspaceEvidenceEntryPoint>(), JsonOptions), Encoding.UTF8);
        File.WriteAllText(modulesMapPath, JsonSerializer.Serialize(pack?.Candidates?.ModuleCandidates ?? Array.Empty<WorkspaceEvidenceModule>(), JsonOptions), Encoding.UTF8);
        File.WriteAllText(projectUnitsIndexPath, JsonSerializer.Serialize(pack?.Candidates?.ProjectUnits ?? Array.Empty<WorkspaceEvidenceProjectUnit>(), JsonOptions), Encoding.UTF8);
        File.WriteAllText(runProfilesIndexPath, JsonSerializer.Serialize(pack?.Candidates?.RunProfiles ?? Array.Empty<WorkspaceEvidenceRunProfile>(), JsonOptions), Encoding.UTF8);
        File.WriteAllText(topologyIndexPath, JsonSerializer.Serialize(pack?.Topology, JsonOptions), Encoding.UTF8);
        File.WriteAllText(predicateRegistryPath, JsonSerializer.Serialize(pack?.PredicateRegistry ?? Array.Empty<WorkspaceEvidencePredicate>(), JsonOptions), Encoding.UTF8);
        File.WriteAllText(scanBudgetPath, JsonSerializer.Serialize(pack?.ScanBudget, JsonOptions), Encoding.UTF8);
        File.WriteAllText(uncertaintyReportPath, JsonSerializer.Serialize(BuildUncertaintyReport(runResult), JsonOptions), Encoding.UTF8);
        File.WriteAllText(technicalPassportPath, JsonSerializer.Serialize(pack?.TechnicalPassport ?? BuildEmptyPassport(), JsonOptions), Encoding.UTF8);
        File.WriteAllText(rawObservationsPath, JsonSerializer.Serialize(pack?.RawObservations ?? Array.Empty<WorkspaceEvidenceObservation>(), JsonOptions), Encoding.UTF8);
        File.WriteAllText(derivedPatternsPath, JsonSerializer.Serialize(pack?.DerivedPatterns ?? Array.Empty<WorkspaceEvidencePattern>(), JsonOptions), Encoding.UTF8);
        File.WriteAllText(signalScoresPath, JsonSerializer.Serialize(pack?.SignalScores ?? Array.Empty<WorkspaceEvidenceSignalScore>(), JsonOptions), Encoding.UTF8);
        File.WriteAllText(codeEdgesPath, JsonSerializer.Serialize(pack?.CodeEdges ?? Array.Empty<WorkspaceEvidenceCodeEdge>(), JsonOptions), Encoding.UTF8);
        File.WriteAllText(signatureHintsPath, JsonSerializer.Serialize(pack?.SignatureHints ?? Array.Empty<WorkspaceEvidenceSignatureHint>(), JsonOptions), Encoding.UTF8);
        File.WriteAllText(symbolHintsPath, JsonSerializer.Serialize(pack?.SymbolHints ?? Array.Empty<WorkspaceEvidenceSymbolHint>(), JsonOptions), Encoding.UTF8);
        File.WriteAllText(dependencySurfacePath, JsonSerializer.Serialize(pack?.DependencySurface ?? Array.Empty<WorkspaceEvidenceDependencySurfaceItem>(), JsonOptions), Encoding.UTF8);
        File.WriteAllText(confidenceAnnotationsPath, JsonSerializer.Serialize(pack?.ConfidenceAnnotations ?? Array.Empty<WorkspaceEvidenceConfidenceAnnotation>(), JsonOptions), Encoding.UTF8);
        File.WriteAllText(entryPointsPath, JsonSerializer.Serialize(entryPointPayload, JsonOptions), Encoding.UTF8);
        File.WriteAllText(candidatesPath, JsonSerializer.Serialize(pack?.Candidates ?? new WorkspaceEvidenceCandidates(Array.Empty<WorkspaceEvidenceEntryPoint>(), Array.Empty<WorkspaceEvidenceModule>(), Array.Empty<WorkspaceEvidenceFileRole>(), Array.Empty<WorkspaceEvidenceProjectUnit>(), Array.Empty<WorkspaceEvidenceRunProfile>()), JsonOptions), Encoding.UTF8);
        File.WriteAllText(layerMapPath, JsonSerializer.Serialize(layerPayload, JsonOptions), Encoding.UTF8);
        File.WriteAllText(moduleCandidatesPath, JsonSerializer.Serialize(modulePayload, JsonOptions), Encoding.UTF8);
        File.WriteAllText(dependencyEdgesPath, JsonSerializer.Serialize(pack?.Edges ?? Array.Empty<WorkspaceEvidenceDependencyEdge>(), JsonOptions), Encoding.UTF8);
        File.WriteAllText(hotspotsPath, JsonSerializer.Serialize(pack?.Hotspots ?? Array.Empty<WorkspaceEvidenceHotspot>(), JsonOptions), Encoding.UTF8);
        File.WriteAllText(stageSignalsPath, JsonSerializer.Serialize(stagePayload, JsonOptions), Encoding.UTF8);
        File.WriteAllText(originDetectionPath, JsonSerializer.Serialize(originPayload, JsonOptions), Encoding.UTF8);
        File.WriteAllText(evidenceSnippetsPath, JsonSerializer.Serialize(pack?.EvidenceSnippets ?? Array.Empty<WorkspaceEvidenceSnippet>(), JsonOptions), Encoding.UTF8);
        File.WriteAllText(diagramSpecPath, JsonSerializer.Serialize(runResult.Interpretation.DiagramSpec, JsonOptions), Encoding.UTF8);

        var previewDocs = _projectDocumentRuntime.WritePreviewDocs(runResult, artifactRoot);

        var diagnostic = _diagramRuntime.RenderPng(runResult.Interpretation.DiagramSpec, architectureMapPath);
        if (diagnostic is not null)
        {
            throw new InvalidOperationException($"Diagram runtime failed honestly: {diagnostic.Code} {diagnostic.Message}");
        }

        if (writePdf && pdfPath is not null)
        {
            File.WriteAllText(pdfPath, BuildProjectReport(runResult), Encoding.UTF8);
        }

        var previewHtml = BuildPreviewHtml(runResult, artifactRoot, previewPath, projectReportPath, architectureMapPath);
        var bundlePreviewHtml = BuildPreviewHtml(runResult, artifactRoot, bundlePreviewPath, projectReportPath, architectureMapPath);
        File.WriteAllText(previewPath, previewHtml, Encoding.UTF8);
        File.WriteAllText(bundlePreviewPath, bundlePreviewHtml, Encoding.UTF8);

        return new WorkspaceEvidenceArtifactBundle(
            directory,
            previewPath,
            previewDocs.PreviewProjectPath,
            previewDocs.PreviewCapsulePath,
            scanRunPath,
            projectProfilePath,
            projectReportPath,
            scanSummaryPath,
            filesIndexPath,
            manifestsIndexPath,
            symbolsIndexPath,
            edgesIndexPath,
            entryPointsIndexPath,
            modulesMapPath,
            projectUnitsIndexPath,
            runProfilesIndexPath,
            topologyIndexPath,
            predicateRegistryPath,
            scanBudgetPath,
            uncertaintyReportPath,
            technicalPassportPath,
            rawObservationsPath,
            derivedPatternsPath,
            signalScoresPath,
            codeEdgesPath,
            signatureHintsPath,
            symbolHintsPath,
            dependencySurfacePath,
            confidenceAnnotationsPath,
            entryPointsPath,
            candidatesPath,
            layerMapPath,
            moduleCandidatesPath,
            dependencyEdgesPath,
            hotspotsPath,
            stageSignalsPath,
            originDetectionPath,
            evidenceSnippetsPath,
            diagramSpecPath,
            architectureMapPath,
            pdfPath,
            $"Evidence bundle written to {directory}.");
    }

    private static string BuildScanSummary(WorkspaceImportMaterialInterpreterRunResult runResult)
    {
        var pack = runResult.PreviewPacket.EvidencePack;
        var builder = new StringBuilder();
        builder.AppendLine("# Scan Summary");
        builder.AppendLine();
        builder.AppendLine("Cold scanner projection. This summary reports observed evidence only and does not claim project purpose.");
        builder.AppendLine();

        if (pack is null)
        {
            builder.AppendLine("- evidence_pack: none");
            return builder.ToString().TrimEnd();
        }

        var profile = pack.ProjectProfile;
        var budget = pack.ScanBudget;
        builder.AppendLine("## Scan Run");
        builder.AppendLine($"- scan_run_id: `{pack.ScanRun.ScanRunId}`");
        builder.AppendLine($"- scanner_version: `{pack.ScanRun.ScannerVersion}`");
        builder.AppendLine($"- mode: `{pack.ScanRun.Mode}`");
        builder.AppendLine($"- scan_fingerprint: `{pack.ScanRun.RepoRootHash}`");
        builder.AppendLine("- fingerprint_scope: structural scan identity, not content-integrity hash");
        builder.AppendLine($"- budget_status: `{(budget?.IsPartial == true ? "partial" : "complete")}`");
        if (budget is not null)
        {
            builder.AppendLine($"- visited_files: {budget.VisitedFileCount}");
            builder.AppendLine($"- included_relevant_files: {budget.IncludedRelevantFileCount}");
            builder.AppendLine($"- skipped_large_files: {budget.SkippedLargeFileCount}");
            builder.AppendLine($"- skipped_relevant_files: {budget.SkippedRelevantFileCount}");
        }

        builder.AppendLine();
        builder.AppendLine("## Counts");
        builder.AppendLine($"- relevant_files: {profile.RelevantFileCount}");
        builder.AppendLine($"- source_files: {profile.SourceFileCount}");
        builder.AppendLine($"- build_files: {profile.BuildFileCount}");
        builder.AppendLine($"- config_files: {profile.ConfigFileCount}");
        builder.AppendLine($"- document_files: {profile.DocumentFileCount}");
        builder.AppendLine($"- asset_files: {profile.AssetFileCount}");
        builder.AppendLine($"- binary_files: {profile.BinaryFileCount}");
        builder.AppendLine($"- noise_files_ignored: {profile.IgnoredNoiseFileCount}");

        builder.AppendLine();
        builder.AppendLine("## Topology");
        builder.AppendLine($"- kind: `{pack.Topology.Kind}`");
        builder.AppendLine($"- safe_import_mode: `{pack.Topology.SafeImportMode}`");
        builder.AppendLine($"- likely_active_source: {JoinOrNone(pack.Topology.LikelyActiveSourceRoots)}");
        builder.AppendLine($"- release_output_zones: {JoinOrNone(pack.Topology.ReleaseOutputZones)}");
        builder.AppendLine($"- ignored_noise_zones: {JoinOrNone(pack.Topology.IgnoredNoiseZones)}");
        foreach (var zone in pack.Topology.ObservedZones.Take(10))
        {
            builder.AppendLine($"- zone `{zone.Root}`: {zone.Role}, files={zone.FileCount}, confidence={zone.Confidence}, evidence={JoinOrNone(zone.Evidence.Take(4))}");
        }

        AppendSummaryList(
            builder,
            "Entry Points",
            pack.Candidates.EntryPoints.Take(10).Select(static entry => $"`{entry.RelativePath}` ({entry.Role}, score={entry.Score}, marker={FormatMarker(entry.EvidenceMarker)}, evidence={string.Join("; ", entry.Evidence.Take(3))})"));
        AppendSummaryList(
            builder,
            "Project Units",
            pack.Candidates.ProjectUnits.Take(10).Select(static unit => $"`{unit.RootPath}` ({unit.Kind}, {unit.Confidence}, manifests={unit.Manifests.Count}, entries={unit.EntryPoints.Count}, marker={FormatMarker(unit.EvidenceMarker)})"));
        AppendSummaryList(
            builder,
            "Run Profiles",
            pack.Candidates.RunProfiles.Take(10).Select(static profileItem => $"{profileItem.Kind}: `{profileItem.Command}` @ `{profileItem.WorkingDirectory}` ({profileItem.Confidence}, marker={FormatMarker(profileItem.EvidenceMarker)})"));

        builder.AppendLine("## Uncertainty");
        var uncertainty = BuildUncertaintyLines(pack).ToArray();
        if (uncertainty.Length == 0)
        {
            builder.AppendLine("- none");
        }
        else
        {
            foreach (var line in uncertainty)
            {
                builder.AppendLine($"- {line}");
            }
        }

        return builder.ToString().TrimEnd();
    }

    private static IReadOnlyList<object> BuildManifestIndex(WorkspaceEvidencePack? pack)
    {
        if (pack is null)
        {
            return Array.Empty<object>();
        }

        var dependencyCounts = pack.DependencySurface
            .GroupBy(static item => item.SourcePath, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(static group => group.Key, static group => group.Count(), StringComparer.OrdinalIgnoreCase);

        return pack.Candidates.ProjectUnits
            .SelectMany(unit => unit.Manifests.Select(manifest => new
            {
                RelativePath = manifest,
                UnitId = unit.Id,
                UnitRoot = unit.RootPath,
                UnitKind = unit.Kind,
                UnitConfidence = unit.Confidence,
                DependencyCount = dependencyCounts.TryGetValue(manifest, out var count) ? count : 0,
                Evidence = unit.Evidence
            }))
            .OrderBy(static item => item.RelativePath, StringComparer.OrdinalIgnoreCase)
            .ThenBy(static item => item.UnitId, StringComparer.OrdinalIgnoreCase)
            .Cast<object>()
            .ToArray();
    }

    private static object BuildSymbolIndex(WorkspaceEvidencePack? pack)
    {
        return new
        {
            Symbols = pack?.SymbolHints ?? Array.Empty<WorkspaceEvidenceSymbolHint>(),
            Signatures = pack?.SignatureHints ?? Array.Empty<WorkspaceEvidenceSignatureHint>()
        };
    }

    private static object BuildEdgesIndex(WorkspaceEvidencePack? pack)
    {
        return new
        {
            DependencyEdges = pack?.Edges ?? Array.Empty<WorkspaceEvidenceDependencyEdge>(),
            CodeEdges = pack?.CodeEdges ?? Array.Empty<WorkspaceEvidenceCodeEdge>()
        };
    }

    private static object BuildUncertaintyReport(WorkspaceImportMaterialInterpreterRunResult runResult)
    {
        var pack = runResult.PreviewPacket.EvidencePack;
        if (pack is null)
        {
            return new
            {
                scanRunId = (string?)null,
                anomalies = Array.Empty<string>(),
                budget = (WorkspaceScanBudgetReport?)null,
                budgetSkips = Array.Empty<WorkspaceScanBudgetSkip>(),
                rawObservations = Array.Empty<WorkspaceEvidenceObservation>()
            };
        }

        return new
        {
            scanRunId = pack.ScanRun.ScanRunId,
            anomalies = pack.ProjectProfile.StructuralAnomalies,
            budget = pack.ScanBudget,
            budgetSkips = pack.ScanBudget?.Skips ?? Array.Empty<WorkspaceScanBudgetSkip>(),
            topology = pack.Topology,
            rawObservations = pack.RawObservations
                .Where(static observation =>
                    observation.Kind.Contains("budget", StringComparison.OrdinalIgnoreCase) ||
                    observation.Kind.Contains("anomaly", StringComparison.OrdinalIgnoreCase))
                .ToArray()
        };
    }

    private static IEnumerable<string> BuildUncertaintyLines(WorkspaceEvidencePack pack)
    {
        foreach (var anomaly in pack.ProjectProfile.StructuralAnomalies)
        {
            yield return anomaly;
        }

        if (pack.ScanBudget is { IsPartial: true } budget)
        {
            yield return $"partial_scan: visited={budget.VisitedFileCount}, included={budget.IncludedRelevantFileCount}, skipped_large={budget.SkippedLargeFileCount}, skipped_relevant={budget.SkippedRelevantFileCount}";
            foreach (var skip in budget.Skips.Take(10))
            {
                yield return $"budget_skip: `{skip.RelativePath}` reason={skip.Reason}";
            }
        }

        if (pack.Candidates.EntryPoints.Count == 0)
        {
            yield return "entrypoint_unknown: no manifest-backed or conventional entrypoint candidates were confirmed by scanner evidence.";
        }

        foreach (var reason in pack.Topology.UncertaintyReasons)
        {
            yield return $"topology_uncertainty: {reason}";
        }
    }

    private static void AppendSummaryList(StringBuilder builder, string title, IEnumerable<string> lines)
    {
        builder.AppendLine();
        builder.AppendLine($"## {title}");
        var items = lines.ToArray();
        if (items.Length == 0)
        {
            builder.AppendLine("- none");
            return;
        }

        foreach (var item in items)
        {
            builder.AppendLine($"- {item}");
        }
    }

    private static string JoinOrNone(IEnumerable<string> values)
    {
        var items = values
            .Where(static value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        return items.Length == 0 ? "none" : string.Join(", ", items);
    }

    private static string FormatMarker(WorkspaceEvidenceMarker? marker)
    {
        if (marker is null)
        {
            return "none";
        }

        return $"{marker.EvidenceKind}/{marker.Confidence}/partial={marker.IsPartial}/bounded={marker.IsBounded}";
    }

    private static string BuildProjectReport(WorkspaceImportMaterialInterpreterRunResult runResult)
    {
        var language = WorkspaceDocumentationLanguagePolicy.ResolveCurrent();
        var interpretation = runResult.Interpretation;
        var pack = runResult.PreviewPacket.EvidencePack;
        var projectDetails = interpretation.ProjectDetails ?? Array.Empty<string>();
        var confirmedSignals = interpretation.ConfirmedSignals ?? Array.Empty<string>();
        var likelySignals = interpretation.LikelySignals ?? Array.Empty<string>();
        var unknownSignals = interpretation.UnknownSignals ?? Array.Empty<string>();
        var layers = interpretation.Layers ?? Array.Empty<WorkspaceImportMaterialLayerInterpretation>();
        var modules = interpretation.Modules ?? Array.Empty<WorkspaceImportMaterialModuleInterpretation>();
        var entryPoints = interpretation.EntryPoints ?? Array.Empty<WorkspaceImportMaterialEntryPointInterpretation>();
        var currentSignals = interpretation.CurrentSignals ?? Array.Empty<string>();
        var plannedSignals = interpretation.PlannedSignals ?? Array.Empty<string>();
        var possiblyStaleSignals = interpretation.PossiblyStaleSignals ?? Array.Empty<string>();
        var conflicts = interpretation.Conflicts ?? Array.Empty<string>();
        var materials = interpretation.Materials ?? Array.Empty<WorkspaceMaterialPreviewInterpretation>();
        var builder = new StringBuilder();

        builder.AppendLine($"# {GetLabel(language, "Project Report", "Отчет о проекте")}");
        builder.AppendLine();

        builder.AppendLine($"## {GetLabel(language, "What This Project Is", "Что это за проект")}");
        builder.AppendLine($"- {interpretation.SummaryLine}");
        builder.AppendLine();

        if (projectDetails.Count > 0)
        {
            builder.AppendLine($"## {GetLabel(language, "Details", "Детали")}");
            foreach (var detail in projectDetails)
            {
                builder.AppendLine($"- {detail}");
            }

            builder.AppendLine();
        }

        AppendSection(builder, GetLabel(language, "Current Stage", "Текущая стадия"), interpretation.ProjectStageSignals ?? Array.Empty<string>());

        if (interpretation.InterpretationMode == ProjectInterpretationMode.SingleProject)
        {
            builder.AppendLine($"## {GetLabel(language, "Technical Passport (Transitional UX Summary)", "Технический паспорт (переходная UX-сводка)")}");
            AppendInline(builder, GetLabel(language, "Languages", "Языки"), pack?.TechnicalPassport.ObservedLanguages);
            AppendInline(builder, GetLabel(language, "Build Systems", "Системы сборки"), pack?.TechnicalPassport.BuildSystems);
            AppendInline(builder, GetLabel(language, "Toolchains", "Тулчейны"), pack?.TechnicalPassport.Toolchains);
            AppendInline(builder, GetLabel(language, "Frameworks", "Фреймворки"), pack?.TechnicalPassport.Frameworks);
            AppendInline(builder, GetLabel(language, "Version Hints", "Версионные подсказки"), pack?.TechnicalPassport.VersionHints);
            AppendInline(builder, GetLabel(language, "Platforms", "Платформы"), pack?.TechnicalPassport.TargetPlatforms);
            AppendInline(builder, GetLabel(language, "Runtime Surfaces", "Поверхности runtime"), pack?.TechnicalPassport.RuntimeSurfaces);
            AppendInline(builder, GetLabel(language, "Build Variants", "Варианты сборки"), pack?.TechnicalPassport.BuildVariants);
            AppendInline(builder, GetLabel(language, "Notable Options", "Важные опции"), pack?.TechnicalPassport.NotableOptions);
            builder.AppendLine();
        }

        builder.AppendLine($"## {GetLabel(language, "Confidence", "Уверенность")}");
        AppendInline(builder, "Confirmed", confirmedSignals);
        AppendInline(builder, "Likely", likelySignals);
        AppendInline(builder, "Unknown", unknownSignals);
        builder.AppendLine();

        builder.AppendLine($"## {GetLabel(language, "Layers and Modules", "Слои и модули")}");
        var attachedModuleNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (interpretation.InterpretationMode != ProjectInterpretationMode.SingleProject)
        {
            builder.AppendLine($"- {GetLabel(language, "Unified layer/module projection is suppressed for a multi-project container.", "Единая проекция слоёв и модулей подавлена для multi-project container.")}");
        }
        else if (layers.Count > 0)
        {
            foreach (var layer in layers)
            {
                builder.AppendLine($"- **{layer.Name}** [{layer.Confidence}]: {layer.Responsibility}");
                foreach (var module in modules.Where(module => ModuleBelongsToLayer(pack, layers, module, layer.Name)).Take(4))
                {
                    attachedModuleNames.Add(module.Name);
                    builder.AppendLine($"  - {module.Name}: {module.Role} [{module.Confidence}] ({module.EvidenceNote})");
                }
            }
        }
        else
        {
            builder.AppendLine($"- {GetLabel(language, "No clear layers were derived yet.", "Явные слои пока не были выведены.")}");
        }

        builder.AppendLine();

        builder.AppendLine($"## {GetLabel(language, "Entry Points", "Точки входа")}");
        if (interpretation.InterpretationMode == ProjectInterpretationMode.MultipleIndependentProjects)
        {
            builder.AppendLine($"- {GetLabel(language, "Unified entry-point projection is suppressed because multiple independent project roots were detected.", "Единая проекция entry points подавлена, потому что обнаружены несколько независимых project roots.")}");
            builder.AppendLine();
        }
        else
        {
            foreach (var module in modules.Where(module => !attachedModuleNames.Contains(module.Name)).Take(6))
            {
                builder.AppendLine($"- [Unattached] {module.Name} [{module.Confidence}]: {module.Role} ({module.EvidenceNote})");
            }

            builder.AppendLine();

            if (entryPoints.Count > 0)
            {
                foreach (var entryPoint in entryPoints)
                {
                    builder.AppendLine($"- `{entryPoint.RelativePath}`: {entryPoint.Role} [{entryPoint.Confidence}] - {entryPoint.Note}");
                }
            }
            else
            {
                builder.AppendLine($"- {GetLabel(language, "No clear entry points were derived yet.", "Явные точки входа пока не были выведены.")}");
            }

            builder.AppendLine();
        }

        builder.AppendLine($"## {GetLabel(language, "Current / Planned / Possibly Stale", "Что выглядит текущим / planned / possibly stale")}");
        AppendInline(builder, GetLabel(language, "Current", "Текущее"), currentSignals);
        AppendInline(builder, GetLabel(language, "Planned", "Планируемое"), plannedSignals);
        AppendInline(builder, GetLabel(language, "Possibly Stale", "Возможно устаревшее"), possiblyStaleSignals);
        AppendInline(builder, GetLabel(language, "Conflicts", "Конфликты"), conflicts);
        builder.AppendLine();

        if (materials.Count > 0)
        {
            builder.AppendLine($"## {GetLabel(language, "Evidence Materials", "Материалы")}");
            foreach (var material in materials)
            {
                if (material.PossibleUsefulness == WorkspaceMaterialContextUsefulness.Unknown && string.IsNullOrWhiteSpace(material.Summary))
                {
                    continue;
                }

                builder.AppendLine($"- `{material.RelativePath}`: usefulness={material.PossibleUsefulness}, temporal={material.TemporalStatus}, confidence={material.Confidence}, summary={material.Summary}");
            }
        }

        return builder.ToString().TrimEnd();
    }

    private string BuildPreviewHtml(
        WorkspaceImportMaterialInterpreterRunResult runResult,
        string projectRootPath,
        string outputHtmlPath,
        string projectReportPath,
        string architectureMapPath)
    {
        var interpretation = runResult.Interpretation;
        var pack = runResult.PreviewPacket.EvidencePack;
        var sourceRoots = runResult.PreviewPacket.SourceRoots ?? Array.Empty<string>();
        var entryPoints = interpretation.EntryPoints ?? Array.Empty<WorkspaceImportMaterialEntryPointInterpretation>();
        var selection = _projectDocumentRuntime.SelectSources(projectRootPath);
        var mainDocument = _projectDocumentRuntime.Read(projectRootPath, ProjectDocumentKind.Project);
        var directionDocument = _projectDocumentRuntime.Read(projectRootPath, ProjectDocumentKind.Direction);
        var roadmapDocument = _projectDocumentRuntime.Read(projectRootPath, ProjectDocumentKind.Roadmap);
        var canonDocument = _projectDocumentRuntime.Read(projectRootPath, ProjectDocumentKind.Canon);
        var companionDocument = _projectDocumentRuntime.Read(projectRootPath, ProjectDocumentKind.Capsule);
        var title = ResolvePreviewTitle(runResult);
        var styleBlock = ExtractTemplateStyle(TryLoadPreviewTemplate());
        var stageLabel = selection.ActiveStage switch
        {
            ProjectDocumentStage.CanonicalDocs => "Canonical",
            ProjectDocumentStage.PreviewDocs => "Preview Docs",
            _ => "Import"
        };
        var stageClass = selection.ActiveStage switch
        {
            ProjectDocumentStage.CanonicalDocs => "good",
            ProjectDocumentStage.PreviewDocs => "unknown",
            _ => "warn"
        };
        var modeLabel = interpretation.InterpretationMode.ToString();
        var modeClass = interpretation.InterpretationMode switch
        {
            ProjectInterpretationMode.SingleProject => "good",
            ProjectInterpretationMode.AmbiguousContainer => "warn",
            _ => "unknown"
        };
        var extraStyles = @"
.warning-card { border-color: rgba(220,188,127,0.28); background: linear-gradient(180deg, rgba(220,188,127,0.08), rgba(53,59,68,0.96)); }
.markdown { color: var(--text); }
.markdown h1, .markdown h2, .markdown h3, .markdown h4 { margin: 1.2em 0 0.5em; line-height: 1.3; }
.markdown p { margin: 0 0 1em; color: var(--text); }
.markdown ul, .markdown ol { margin: 0 0 1em 1.25em; padding: 0; }
.markdown li { margin: 0.2em 0; }
.markdown code { font-family: ""JetBrains Mono"", ""Consolas"", monospace; background: rgba(255,255,255,0.06); padding: 0.14em 0.35em; border-radius: 6px; }
.markdown pre { overflow-x: auto; padding: 16px; border-radius: var(--radius-md); background: rgba(0,0,0,0.2); border: 1px solid var(--line-soft); }
.markdown pre code { background: transparent; padding: 0; }
.markdown table { width: 100%; border-collapse: collapse; margin: 0 0 1em; }
.markdown th, .markdown td { text-align: left; padding: 10px 12px; border-bottom: 1px solid var(--line-soft); vertical-align: top; }
.markdown thead th { color: var(--muted); font-size: 12px; letter-spacing: 0.06em; text-transform: uppercase; }
.markdown blockquote { margin: 0 0 1em; padding: 0 0 0 16px; border-left: 3px solid var(--accent); color: var(--muted); }
.links { display: flex; gap: 12px; flex-wrap: wrap; margin-top: 18px; }
.link-chip { display: inline-flex; align-items: center; gap: 8px; padding: 10px 14px; border-radius: 999px; background: rgba(255,255,255,0.05); border: 1px solid var(--line-soft); color: var(--text); text-decoration: none; font-size: 13px; }
.hero-badges { display: flex; gap: 10px; flex-wrap: wrap; margin-top: 16px; }
.source-note { display: inline-flex; align-items: center; gap: 8px; margin-top: 16px; font-size: 12px; color: var(--muted); text-transform: uppercase; letter-spacing: 0.06em; }
.content-grid { display: grid; gap: 18px; }
.structure-grid { display: grid; gap: 16px; grid-template-columns: repeat(auto-fit, minmax(220px, 1fr)); margin-top: 18px; }
.mini-card { padding: 16px; border-radius: var(--radius-md); border: 1px solid var(--line-soft); background: rgba(255,255,255,0.03); }
.mini-card h3 { margin: 0 0 10px; font-size: 15px; }
.mini-card ul { margin: 0; padding-left: 18px; }
.mini-card li { margin: 0 0 8px; }
.diagram-preview { width: 100%; border-radius: var(--radius-md); border: 1px solid var(--line-soft); background: rgba(255,255,255,0.02); margin-top: 12px; }
.truth-note { margin-top: 10px; color: var(--muted); font-size: 13px; }
.doc-cards { display: grid; gap: 16px; }
.doc-card { border-radius: var(--radius-md); border: 1px solid var(--line-soft); overflow: hidden; }
.doc-card.doc-missing { opacity: 0.55; }
.doc-card-head { padding: 12px 18px 11px; border-bottom: 1px solid var(--line-soft); display: flex; align-items: center; gap: 10px; background: rgba(255,255,255,0.02); }
.doc-card-label { font-size: 13px; font-weight: 500; flex: 1; }
.doc-card-filename { font-family: ""JetBrains Mono"", ""Consolas"", monospace; font-size: 11px; color: var(--muted); }
.doc-card-body { padding: 16px 18px; }
.doc-card-body .markdown { font-size: 14px; }
.doc-card-body .markdown h1 { font-size: 18px; }
.doc-card-body .markdown h2 { font-size: 15px; }
.doc-missing-note { font-size: 13px; color: var(--muted); font-style: italic; }
@media (max-width: 920px) { .layout { grid-template-columns: 1fr; } body { padding: 32px 16px 40px; } .hero { padding: 30px 24px; } .content-card { padding: 24px 22px; } }";
        var subtitle = selection.ActiveStage switch
        {
            ProjectDocumentStage.CanonicalDocs => "Canonical docs are active for this project root.",
            ProjectDocumentStage.PreviewDocs => "Preview docs are active. This document stream is still not canonical truth.",
            _ => interpretation.InterpretationMode == ProjectInterpretationMode.SingleProject
                ? "Import preview is active. This is a human-readable projection of the current bundle."
                : "Import preview is active for a mixed container. Unified architecture is not assumed."
        };
        var warning = interpretation.InterpretationMode == ProjectInterpretationMode.SingleProject
            ? string.Empty
            : "<div class=\"card content-card warning-card\"><div class=\"section-title\">Container Warning</div><div class=\"lead\">This folder contains multiple independent or loosely related projects. Unified architecture is not assumed.</div></div>";
        var mainMarkdown = mainDocument.Exists
            ? mainDocument.Markdown
            : File.Exists(projectReportPath)
                ? File.ReadAllText(projectReportPath, Encoding.UTF8)
                : BuildProjectReport(runResult);
        // Note: if mainDocument does not exist, content is rebuilt from the current run result.
        // This is expected at ImportPreview stage. The stage badge and truthNote already reflect this.
        var mainDocForSection = mainDocument.Exists
            ? mainDocument
            : new ProjectDocumentReadResult(ProjectDocumentKind.Project, mainDocument.Stage, mainDocument.Path, Exists: true, mainMarkdown);
        var documentsSection = BuildDocumentsSectionHtml(mainDocForSection, directionDocument, roadmapDocument, canonDocument, companionDocument);
        var healthLabel = pack?.ProjectProfile.Health.ToString() ?? "Unknown";
        var importKindLabel = runResult.PreviewPacket.ImportKind.ToString();
        var sourceRootsLabel = sourceRoots.Count > 0
            ? string.Join(", ", sourceRoots.Select(static root => $"`{root}`"))
            : "Unknown";
        var primaryEntry = entryPoints.FirstOrDefault();
        var primaryEntryLabel = primaryEntry is null ? "Unknown" : $"{primaryEntry.RelativePath} [{primaryEntry.Confidence}]";
        var confidenceLabel = BuildConfidenceBadgeLabel(interpretation);
        var truthNote = selection.ActiveStage switch
        {
            ProjectDocumentStage.CanonicalDocs => "Canonical docs active.",
            ProjectDocumentStage.PreviewDocs => "Preview only. Not canonical yet.",
            _ => "Import report only. Preview docs are not materialized yet."
        };
        var structureSection = BuildStructureSectionHtml(runResult, outputHtmlPath, architectureMapPath);

        return $$"""
<!DOCTYPE html>
<html lang="en">
<head>
  <meta charset="UTF-8" />
  <meta name="viewport" content="width=device-width, initial-scale=1.0" />
  <title>{{title}} · ZAVOD Preview</title>
  {{styleBlock}}
  <style>{{extraStyles}}</style>
</head>
<body>
  <div class="page">
    <header class="hero">
      <div class="brand">ZAVOD · Document Preview</div>
      <h1>{{title}}</h1>
      <div class="subtitle">{{HtmlEncode(subtitle)}}</div>
      <div class="hero-badges">
        <span class="badge {{stageClass}}">Stage: {{HtmlEncode(stageLabel)}}</span>
        <span class="badge {{modeClass}}">{{HtmlEncode(modeLabel)}}</span>
        <span class="badge unknown">{{HtmlEncode(confidenceLabel)}}</span>
      </div>
      <div class="source-note">Source: {{HtmlEncode(DescribeSource(mainDocument.Stage, mainDocument.Path))}}</div>
      <div class="links">
        <a class="link-chip" href="{{HtmlAttributeEncode(ResolveRelativeHref(outputHtmlPath, projectReportPath))}}">Open project_report.md</a>
      </div>
    </header>

    <div class="layout">
      <aside class="sidebar">
        <div class="sticky">
          <div class="card">
            <div class="card-inner">
              <div class="section-title">Overview</div>
              <div class="project-name">{{title}}</div>
              <div class="path">{{HtmlEncode(ResolveArtifactRoot(runResult))}}</div>

              <div class="quick-stat">
                <div class="quick-label">Stage</div>
                <div class="quick-value">{{HtmlEncode(stageLabel)}} <span class="badge {{stageClass}}">{{HtmlEncode(stageLabel)}}</span></div>
                <div class="quick-sub">Active document source for this preview.</div>
              </div>

              <div class="quick-stat">
                <div class="quick-label">Interpretation Mode</div>
                <div class="quick-value">{{HtmlEncode(modeLabel)}} <span class="badge {{modeClass}}">{{HtmlEncode(modeLabel)}}</span></div>
                <div class="quick-sub">Importer-owned mode from the current bundle.</div>
              </div>

              <div class="quick-stat">
                <div class="quick-label">Import Kind</div>
                <div class="quick-value">{{HtmlEncode(importKindLabel)}}</div>
                <div class="quick-sub">Observed import boundary for this run.</div>
              </div>

              <div class="quick-stat">
                <div class="quick-label">Health</div>
                <div class="quick-value">{{HtmlEncode(healthLabel)}}</div>
                <div class="quick-sub">Scanner/project-profile health from the current evidence pack.</div>
              </div>

              <div class="quick-stat">
                <div class="quick-label">Confidence</div>
                <div class="quick-value">{{HtmlEncode(confidenceLabel)}}</div>
                <div class="quick-sub">Current confidence split for the active interpretation.</div>
              </div>

              <div class="quick-stat">
                <div class="quick-label">Source Roots</div>
                <div class="quick-value">{{HtmlEncode(sourceRoots.Count.ToString())}}</div>
                <div class="quick-sub">{{HtmlEncode(sourceRootsLabel)}}</div>
              </div>

              <div class="quick-stat">
                <div class="quick-label">Primary Entry</div>
                <div class="quick-value">{{HtmlEncode(primaryEntryLabel)}}</div>
                <div class="quick-sub">Best current bounded entry selection.</div>
              </div>

              <div class="quick-stat">
                <div class="quick-label">Truth Note</div>
                <div class="quick-value">{{HtmlEncode(truthNote)}}</div>
                <div class="quick-sub">This preview does not upgrade document stage silently.</div>
              </div>
            </div>
          </div>
        </div>
      </aside>

      <main class="content">
        {{warning}}
        <div class="content-grid">
          {{documentsSection}}
          {{structureSection}}
        </div>
      </main>
    </div>
  </div>
</body>
</html>
""";
    }

    private static string BuildDocumentsSectionHtml(
        ProjectDocumentReadResult project,
        ProjectDocumentReadResult direction,
        ProjectDocumentReadResult roadmap,
        ProjectDocumentReadResult canon,
        ProjectDocumentReadResult capsule)
    {
        var cards = new[]
        {
            BuildDocCardHtml("Project", "project.md", project),
            BuildDocCardHtml("Direction", "direction.md", direction),
            BuildDocCardHtml("Roadmap", "roadmap.md", roadmap),
            BuildDocCardHtml("Canon", "canon.md", canon),
            BuildDocCardHtml("Capsule", "capsule.md", capsule, "Companion Document"),
        };

        return $"""
<section class="card content-card">
  <div class="section-title">Canonical Documents</div>
  <div class="doc-cards">
    {string.Join("\n    ", cards)}
  </div>
</section>
""";
    }

    private static string BuildDocCardHtml(string label, string fileName, ProjectDocumentReadResult doc, string? roleLabel = null)
    {
        var (stageClass, stageName) = doc.Stage switch
        {
            ProjectDocumentStage.CanonicalDocs => ("good", "Canonical"),
            ProjectDocumentStage.PreviewDocs => ("unknown", "Preview"),
            _ => ("warn", "Import")
        };
        var sourceNote = HtmlEncode(DescribeSource(doc.Stage, doc.Path));
        var roleHtml = string.IsNullOrWhiteSpace(roleLabel)
            ? string.Empty
            : $"<div class=\"source-note\">{HtmlEncode(roleLabel)}</div>";

        if (!doc.Exists)
        {
            return $"""
<div class="doc-card doc-missing">
  <div class="doc-card-head">
    <span class="doc-card-label">{HtmlEncode(label)}</span>
    <span class="doc-card-filename">{HtmlEncode(fileName)}</span>
    <span class="badge warn">Not created</span>
  </div>
  <div class="doc-card-body">
    {roleHtml}
    <div class="source-note">Source: {sourceNote}</div>
    <div class="doc-missing-note">This document has not been created yet.</div>
  </div>
</div>
""";
        }

        return $"""
<div class="doc-card">
  <div class="doc-card-head">
    <span class="doc-card-label">{HtmlEncode(label)}</span>
    <span class="doc-card-filename">{HtmlEncode(fileName)}</span>
    <span class="badge {stageClass}">{HtmlEncode(stageName)}</span>
  </div>
  <div class="doc-card-body">
    {roleHtml}
    <div class="source-note">Source: {sourceNote}</div>
    <div class="markdown">{RenderMarkdownToHtml(doc.Markdown)}</div>
  </div>
</div>
""";
    }

    private static string BuildStructureSectionHtml(
        WorkspaceImportMaterialInterpreterRunResult runResult,
        string outputHtmlPath,
        string architectureMapPath)
    {
        var interpretation = runResult.Interpretation;
        var entryPoints = interpretation.EntryPoints ?? Array.Empty<WorkspaceImportMaterialEntryPointInterpretation>();
        var materials = interpretation.Materials ?? Array.Empty<WorkspaceMaterialPreviewInterpretation>();
        var notes = interpretation.DiagramSpec.Notes ?? Array.Empty<string>();
        var diagramVisible = interpretation.InterpretationMode == ProjectInterpretationMode.SingleProject &&
                             interpretation.DiagramSpec.Nodes.Count > 0 &&
                             File.Exists(architectureMapPath);
        var diagramSection = diagramVisible
            ? $"<div class=\"mini-card\"><h3>Architecture Map</h3><p>Source: diagram artifact</p><img class=\"diagram-preview\" src=\"{HtmlAttributeEncode(ResolveRelativeHref(outputHtmlPath, architectureMapPath))}\" alt=\"Architecture map\" /></div>"
            : "<div class=\"mini-card\"><h3>Architecture Map</h3><p>No richer structure map is projected for this stage.</p></div>";
        var entryItems = entryPoints.Take(5)
            .Select(entry => $"<li><code>{HtmlEncode(entry.RelativePath)}</code> {HtmlEncode($"[{entry.Confidence}] {entry.Note}")}</li>");
        var materialItems = materials
            .Where(static material => material.PossibleUsefulness != WorkspaceMaterialContextUsefulness.Unknown || !string.IsNullOrWhiteSpace(material.Summary))
            .Take(5)
            .Select(material => $"<li><code>{HtmlEncode(material.RelativePath)}</code> {HtmlEncode(material.Summary)}</li>");
        var noteItems = notes.Take(4)
            .Select(note => $"<li>{HtmlEncode(note)}</li>");
        var entriesHtml = entryPoints.Count > 0
            ? $"<ul>{string.Join(string.Empty, entryItems)}</ul>"
            : "<p>No explicit entry points are projected for this stage.</p>";
        var materialsHtml = materials.Any(static material => material.PossibleUsefulness != WorkspaceMaterialContextUsefulness.Unknown || !string.IsNullOrWhiteSpace(material.Summary))
            ? $"<ul>{string.Join(string.Empty, materialItems)}</ul>"
            : "<p>No top materials are stabilized for this stage.</p>";
        var notesHtml = notes.Count > 0
            ? $"<ul>{string.Join(string.Empty, noteItems)}</ul>"
            : "<p>Diagram notes are coarse or suppressed.</p>";

        return $$"""
<section class="card content-card">
  <div class="section-title">Structure / Map</div>
  <div class="structure-grid">
    {{diagramSection}}
    <div class="mini-card">
      <h3>Entry Points</h3>
      {{entriesHtml}}
    </div>
    <div class="mini-card">
      <h3>Materials</h3>
      {{materialsHtml}}
    </div>
    <div class="mini-card">
      <h3>Diagram Notes</h3>
      {{notesHtml}}
    </div>
  </div>
</section>
""";
    }

    private static string BuildConfidenceBadgeLabel(WorkspaceImportMaterialInterpretationResult interpretation)
    {
        return $"Confirmed {interpretation.ConfirmedSignals.Count} · Likely {interpretation.LikelySignals.Count} · Unknown {interpretation.UnknownSignals.Count}";
    }

    private static string DescribeSource(ProjectDocumentStage stage, string path)
    {
        var fileName = string.IsNullOrWhiteSpace(path) ? "unknown" : Path.GetFileName(path);
        return stage switch
        {
            ProjectDocumentStage.CanonicalDocs => $"canonical docs ({fileName})",
            ProjectDocumentStage.PreviewDocs => $"preview docs ({fileName})",
            _ => $"import report ({fileName})"
        };
    }

    private static string ResolveRelativeHref(string fromPath, string toPath)
    {
        var fromDirectory = Path.GetDirectoryName(Path.GetFullPath(fromPath)) ?? Path.GetFullPath(fromPath);
        var relative = Path.GetRelativePath(fromDirectory, Path.GetFullPath(toPath));
        return relative.Replace('\\', '/');
    }

    private static string BuildPreviewHtml(
        WorkspaceImportMaterialInterpreterRunResult runResult,
        string projectReportMarkdown,
        string reportHref)
    {
        var interpretation = runResult.Interpretation;
        var pack = runResult.PreviewPacket.EvidencePack;
        var title = ResolvePreviewTitle(runResult);
        var subtitle = interpretation.InterpretationMode == ProjectInterpretationMode.SingleProject
            ? "Human-readable projection of the current import bundle."
            : "Human-readable projection of a mixed container import. Unified architecture is not assumed.";
        var warning = interpretation.InterpretationMode == ProjectInterpretationMode.SingleProject
            ? string.Empty
            : "<div class=\"card content-card warning-card\"><div class=\"section-title\">Container Warning</div><div class=\"lead\">This folder contains multiple independent or loosely related projects. Unified architecture is not assumed.</div></div>";
        var markdownHtml = RenderMarkdownToHtml(projectReportMarkdown);
        var styleBlock = ExtractTemplateStyle(TryLoadPreviewTemplate());
        var modeLabel = interpretation.InterpretationMode.ToString();
        var modeClass = interpretation.InterpretationMode switch
        {
            ProjectInterpretationMode.SingleProject => "good",
            ProjectInterpretationMode.AmbiguousContainer => "warn",
            _ => "unknown"
        };
        var healthLabel = pack?.ProjectProfile.Health.ToString() ?? "Unknown";
        var importKindLabel = runResult.PreviewPacket.ImportKind.ToString();
        var sourceRootsLabel = runResult.PreviewPacket.SourceRoots.Count.ToString();
        var pathLabel = ResolveArtifactRoot(runResult);
        var extraStyles = @"
.warning-card { border-color: rgba(220,188,127,0.28); background: linear-gradient(180deg, rgba(220,188,127,0.08), rgba(53,59,68,0.96)); }
.markdown { color: var(--text); }
.markdown h1, .markdown h2, .markdown h3, .markdown h4 { margin: 1.2em 0 0.5em; line-height: 1.3; }
.markdown p { margin: 0 0 1em; color: var(--text); }
.markdown ul, .markdown ol { margin: 0 0 1em 1.25em; padding: 0; }
.markdown li { margin: 0.2em 0; }
.markdown code { font-family: ""JetBrains Mono"", ""Consolas"", monospace; background: rgba(255,255,255,0.06); padding: 0.14em 0.35em; border-radius: 6px; }
.markdown pre { overflow-x: auto; padding: 16px; border-radius: var(--radius-md); background: rgba(0,0,0,0.2); border: 1px solid var(--line-soft); }
.markdown pre code { background: transparent; padding: 0; }
.markdown table { width: 100%; border-collapse: collapse; margin: 0 0 1em; }
.markdown th, .markdown td { text-align: left; padding: 10px 12px; border-bottom: 1px solid var(--line-soft); vertical-align: top; }
.markdown thead th { color: var(--muted); font-size: 12px; letter-spacing: 0.06em; text-transform: uppercase; }
.markdown blockquote { margin: 0 0 1em; padding: 0 0 0 16px; border-left: 3px solid var(--accent); color: var(--muted); }
.links { display: flex; gap: 12px; flex-wrap: wrap; margin-top: 18px; }
.link-chip { display: inline-flex; align-items: center; gap: 8px; padding: 10px 14px; border-radius: 999px; background: rgba(255,255,255,0.05); border: 1px solid var(--line-soft); color: var(--text); text-decoration: none; font-size: 13px; }
@media (max-width: 920px) { .layout { grid-template-columns: 1fr; } body { padding: 32px 16px 40px; } .hero { padding: 30px 24px; } .content-card { padding: 24px 22px; } }";

        return $$"""
<!DOCTYPE html>
<html lang="en">
<head>
  <meta charset="UTF-8" />
  <meta name="viewport" content="width=device-width, initial-scale=1.0" />
  <title>{{title}} · ZAVOD Preview</title>
  {{styleBlock}}
  <style>{{extraStyles}}</style>
</head>
<body>
  <div class="page">
    <header class="hero">
      <div class="brand">ZAVOD · Import Preview</div>
      <h1>{{title}}</h1>
      <div class="subtitle">{{HtmlEncode(subtitle)}}</div>
      <div class="links">
        <a class="link-chip" href="{{HtmlAttributeEncode(reportHref)}}">Open project_report.md</a>
      </div>
    </header>

    <div class="layout">
      <aside class="sidebar">
        <div class="sticky">
          <div class="card">
            <div class="card-inner">
              <div class="section-title">Overview</div>
              <div class="project-name">{{title}}</div>
              <div class="path">{{HtmlEncode(pathLabel)}}</div>

              <div class="quick-stat">
                <div class="quick-label">Interpretation Mode</div>
                <div class="quick-value">{{HtmlEncode(modeLabel)}} <span class="badge {{modeClass}}">{{HtmlEncode(modeLabel)}}</span></div>
                <div class="quick-sub">Importer-owned mode from the current bundle.</div>
              </div>

              <div class="quick-stat">
                <div class="quick-label">Import Kind</div>
                <div class="quick-value">{{HtmlEncode(importKindLabel)}}</div>
                <div class="quick-sub">Observed import boundary for this run.</div>
              </div>

              <div class="quick-stat">
                <div class="quick-label">Health</div>
                <div class="quick-value">{{HtmlEncode(healthLabel)}}</div>
                <div class="quick-sub">Scanner/project-profile health from the current evidence pack.</div>
              </div>

              <div class="quick-stat">
                <div class="quick-label">Source Roots</div>
                <div class="quick-value">{{HtmlEncode(sourceRootsLabel)}}</div>
                <div class="quick-sub">Cold observed roots for this import case.</div>
              </div>
            </div>
          </div>
        </div>
      </aside>

      <main class="content">
        {{warning}}
        <section class="card content-card">
          <div class="section-title">Human Report</div>
          <div class="markdown">{{markdownHtml}}</div>
        </section>
      </main>
    </div>
  </div>
</body>
</html>
""";
    }

    private static string TryLoadPreviewTemplate()
    {
        var baseDirectory = AppContext.BaseDirectory;
        var current = new DirectoryInfo(baseDirectory);
        while (current is not null)
        {
            var runtimeTemplateCandidate = Path.Combine(current.FullName, "app", "templates", "import-preview.html");
            if (File.Exists(runtimeTemplateCandidate))
            {
                return File.ReadAllText(runtimeTemplateCandidate, Encoding.UTF8);
            }

            var legacyReferenceCandidate = Path.Combine(current.FullName, "docs", "_legacy", "reference", "project-template", "system", "preview.html");
            if (File.Exists(legacyReferenceCandidate))
            {
                return File.ReadAllText(legacyReferenceCandidate, Encoding.UTF8);
            }

            current = current.Parent;
        }

        throw new FileNotFoundException("Import preview template was not found under app/templates or docs/_legacy/reference.");
    }

    private static string ExtractTemplateStyle(string templateHtml)
    {
        var match = Regex.Match(templateHtml, "<style>(.*?)</style>", RegexOptions.Singleline | RegexOptions.IgnoreCase);
        return match.Success ? match.Value : "<style></style>";
    }

    private static string ResolvePreviewTitle(WorkspaceImportMaterialInterpreterRunResult runResult)
    {
        var artifactRoot = ResolveArtifactRoot(runResult);
        var directoryName = new DirectoryInfo(artifactRoot).Name;
        return string.IsNullOrWhiteSpace(directoryName) ? "ZAVOD Import Preview" : directoryName;
    }

    private static string RenderMarkdownToHtml(string markdown)
    {
        var normalized = markdown.Replace("\r\n", "\n");
        var lines = normalized.Split('\n');
        var builder = new StringBuilder();
        var paragraph = new List<string>();
        var listItems = new List<string>();
        var orderedListItems = new List<string>();
        var codeBlock = new List<string>();
        var tableLines = new List<string>();
        var inCodeBlock = false;

        void FlushParagraph()
        {
            if (paragraph.Count == 0)
            {
                return;
            }

            builder.Append("<p>");
            builder.Append(RenderInline(string.Join(" ", paragraph)));
            builder.AppendLine("</p>");
            paragraph.Clear();
        }

        void FlushList()
        {
            if (listItems.Count > 0)
            {
                builder.AppendLine("<ul>");
                foreach (var item in listItems)
                {
                    builder.Append("<li>");
                    builder.Append(RenderInline(item));
                    builder.AppendLine("</li>");
                }

                builder.AppendLine("</ul>");
                listItems.Clear();
            }

            if (orderedListItems.Count > 0)
            {
                builder.AppendLine("<ol>");
                foreach (var item in orderedListItems)
                {
                    builder.Append("<li>");
                    builder.Append(RenderInline(item));
                    builder.AppendLine("</li>");
                }

                builder.AppendLine("</ol>");
                orderedListItems.Clear();
            }
        }

        void FlushCodeBlock()
        {
            if (codeBlock.Count == 0)
            {
                return;
            }

            builder.Append("<pre><code>");
            builder.Append(HtmlEncode(string.Join("\n", codeBlock)));
            builder.AppendLine("</code></pre>");
            codeBlock.Clear();
        }

        void FlushTable()
        {
            if (tableLines.Count < 2)
            {
                foreach (var line in tableLines)
                {
                    paragraph.Add(line);
                }

                tableLines.Clear();
                return;
            }

            var header = SplitTableRow(tableLines[0]);
            var bodyRows = tableLines.Skip(2).Select(SplitTableRow).ToArray();
            builder.AppendLine("<table>");
            builder.AppendLine("<thead><tr>");
            foreach (var cell in header)
            {
                builder.Append("<th>");
                builder.Append(RenderInline(cell));
                builder.AppendLine("</th>");
            }

            builder.AppendLine("</tr></thead>");
            builder.AppendLine("<tbody>");
            foreach (var row in bodyRows)
            {
                builder.AppendLine("<tr>");
                foreach (var cell in row)
                {
                    builder.Append("<td>");
                    builder.Append(RenderInline(cell));
                    builder.AppendLine("</td>");
                }

                builder.AppendLine("</tr>");
            }

            builder.AppendLine("</tbody>");
            builder.AppendLine("</table>");
            tableLines.Clear();
        }

        foreach (var rawLine in lines)
        {
            var line = rawLine.TrimEnd();
            var trimmed = line.Trim();

            if (inCodeBlock)
            {
                if (trimmed.StartsWith("```", StringComparison.Ordinal))
                {
                    FlushCodeBlock();
                    inCodeBlock = false;
                }
                else
                {
                    codeBlock.Add(line);
                }

                continue;
            }

            if (trimmed.StartsWith("```", StringComparison.Ordinal))
            {
                FlushParagraph();
                FlushList();
                FlushTable();
                inCodeBlock = true;
                continue;
            }

            if (IsTableRow(trimmed))
            {
                FlushParagraph();
                FlushList();
                tableLines.Add(trimmed);
                continue;
            }

            if (tableLines.Count > 0)
            {
                FlushTable();
            }

            if (string.IsNullOrWhiteSpace(trimmed))
            {
                FlushParagraph();
                FlushList();
                continue;
            }

            if (TryParseHeading(trimmed, out var level, out var headingText))
            {
                FlushParagraph();
                FlushList();
                builder.Append($"<h{level}>");
                builder.Append(RenderInline(headingText));
                builder.AppendLine($"</h{level}>");
                continue;
            }

            if (trimmed.StartsWith("- ", StringComparison.Ordinal) || trimmed.StartsWith("* ", StringComparison.Ordinal))
            {
                FlushParagraph();
                orderedListItems.Clear();
                listItems.Add(trimmed[2..].Trim());
                continue;
            }

            if (TryParseOrderedItem(trimmed, out var orderedText))
            {
                FlushParagraph();
                listItems.Clear();
                orderedListItems.Add(orderedText);
                continue;
            }

            paragraph.Add(trimmed);
        }

        if (tableLines.Count > 0)
        {
            FlushTable();
        }

        FlushParagraph();
        FlushList();
        FlushCodeBlock();

        return builder.ToString();
    }

    private static bool TryParseHeading(string line, out int level, out string text)
    {
        level = 0;
        text = string.Empty;
        var hashCount = 0;
        while (hashCount < line.Length && line[hashCount] == '#')
        {
            hashCount++;
        }

        if (hashCount == 0 || hashCount > 6 || hashCount >= line.Length || line[hashCount] != ' ')
        {
            return false;
        }

        level = hashCount;
        text = line[(hashCount + 1)..].Trim();
        return true;
    }

    private static bool TryParseOrderedItem(string line, out string text)
    {
        text = string.Empty;
        var dotIndex = line.IndexOf('.');
        if (dotIndex <= 0)
        {
            return false;
        }

        if (!int.TryParse(line[..dotIndex], out _))
        {
            return false;
        }

        if (dotIndex + 1 >= line.Length || line[dotIndex + 1] != ' ')
        {
            return false;
        }

        text = line[(dotIndex + 2)..].Trim();
        return text.Length > 0;
    }

    private static bool IsTableRow(string line)
    {
        return line.Contains('|', StringComparison.Ordinal);
    }

    private static string[] SplitTableRow(string line)
    {
        return line
            .Trim('|')
            .Split('|', StringSplitOptions.TrimEntries)
            .Where(static cell => cell.Length > 0)
            .ToArray();
    }

    private static string RenderInline(string text)
    {
        var encoded = HtmlEncode(text);
        encoded = Regex.Replace(encoded, @"\*\*(.+?)\*\*", "<strong>$1</strong>");
        encoded = Regex.Replace(encoded, @"`([^`]+)`", "<code>$1</code>");
        return encoded;
    }

    private static string HtmlEncode(string value)
    {
        return System.Net.WebUtility.HtmlEncode(value);
    }

    private static string HtmlAttributeEncode(string value)
    {
        return System.Net.WebUtility.HtmlEncode(value);
    }

    private static void AppendSection(StringBuilder builder, string title, IReadOnlyList<string> values)
    {
        if (values.Count == 0)
        {
            return;
        }

        builder.AppendLine($"## {title}");
        foreach (var value in values)
        {
            builder.AppendLine($"- {value}");
        }

        builder.AppendLine();
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

    private static bool ModuleBelongsToLayer(
        WorkspaceEvidencePack? pack,
        IReadOnlyList<WorkspaceImportMaterialLayerInterpretation> layers,
        WorkspaceImportMaterialModuleInterpretation module,
        string layerName)
    {
        if (pack is null)
        {
            return false;
        }

        var matches = (pack.Candidates?.ModuleCandidates ?? Array.Empty<WorkspaceEvidenceModule>())
            .Where(candidate => string.Equals(candidate.Name, module.Name, StringComparison.OrdinalIgnoreCase))
            .ToArray();
        if (matches.Length != 1)
        {
            return false;
        }

        var candidateLayerName = matches[0].LayerName;
        if (!layers.Any(layer => string.Equals(layer.Name, candidateLayerName, StringComparison.OrdinalIgnoreCase)))
        {
            return false;
        }

        return string.Equals(candidateLayerName, layerName, StringComparison.OrdinalIgnoreCase);
    }

    private static WorkspaceTechnicalPassport BuildEmptyPassport()
    {
        return new WorkspaceTechnicalPassport(
            Array.Empty<string>(),
            Array.Empty<string>(),
            Array.Empty<string>(),
            Array.Empty<string>(),
            Array.Empty<string>(),
            Array.Empty<string>(),
            Array.Empty<string>(),
            Array.Empty<string>(),
            Array.Empty<string>(),
            Array.Empty<string>());
    }

    private static string GetLabel(WorkspaceDocumentationLanguagePolicy language, string english, string russian)
    {
        return language.IsRussian ? russian : english;
    }

    private static string ResolveArtifactRoot(WorkspaceImportMaterialInterpreterRunResult runResult)
    {
        var workspaceRoot = runResult.PreviewPacket.WorkspaceRoot;
        var sourceRoots = runResult.PreviewPacket.SourceRoots ?? Array.Empty<string>();
        if (runResult.Interpretation.InterpretationMode != ProjectInterpretationMode.SingleProject)
        {
            return workspaceRoot;
        }

        var gitBackedRoot = ResolveGitBackedArtifactRoot(runResult);
        if (!string.IsNullOrWhiteSpace(gitBackedRoot))
        {
            return gitBackedRoot;
        }

        if (sourceRoots.Any(static root => string.Equals(root, ".", StringComparison.Ordinal)))
        {
            return workspaceRoot;
        }

        var roots = sourceRoots
            .Where(static root => !string.IsNullOrWhiteSpace(root) && !string.Equals(root, ".", StringComparison.Ordinal))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (roots.Length != 1)
        {
            return workspaceRoot;
        }

        var candidate = Path.GetFullPath(Path.Combine(workspaceRoot, roots[0]));
        if (!IsUnderRoot(workspaceRoot, candidate))
        {
            return workspaceRoot;
        }

        return Directory.Exists(candidate) ? candidate : workspaceRoot;
    }

    private static string? ResolveGitBackedArtifactRoot(WorkspaceImportMaterialInterpreterRunResult runResult)
    {
        var workspaceRoot = Path.GetFullPath(runResult.PreviewPacket.WorkspaceRoot);
        var technicalEvidence = runResult.PreviewPacket.TechnicalEvidence ?? Array.Empty<WorkspaceTechnicalPreviewInput>();
        var materials = runResult.PreviewPacket.Materials ?? Array.Empty<WorkspaceMaterialPreviewInput>();
        var entryPoints = runResult.Interpretation.EntryPoints ?? Array.Empty<WorkspaceImportMaterialEntryPointInterpretation>();
        if (Directory.Exists(Path.Combine(workspaceRoot, ".git")))
        {
            return workspaceRoot;
        }

        var candidateRoots = FindGitBackedProjectRoots(workspaceRoot);
        if (candidateRoots.Length == 0)
        {
            return null;
        }

        var observedPaths = technicalEvidence
            .Select(static item => item.RelativePath)
            .Concat(materials.Select(static item => item.RelativePath))
            .Concat(entryPoints.Select(static item => item.RelativePath))
            .Where(static path => !string.IsNullOrWhiteSpace(path))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var bestCandidate = candidateRoots
            .Select(candidate => new
            {
                Path = candidate,
                Score = ScoreCandidateRoot(workspaceRoot, candidate, observedPaths)
            })
            .OrderByDescending(static item => item.Score)
            .ThenByDescending(static item => item.Path.Length)
            .FirstOrDefault();

        return bestCandidate is { Score: > 0 } ? bestCandidate.Path : null;
    }

    private static string[] FindGitBackedProjectRoots(string workspaceRoot)
    {
        var roots = new List<string>();
        var rootGitDirectory = Path.Combine(workspaceRoot, ".git");
        if (Directory.Exists(rootGitDirectory))
        {
            roots.Add(workspaceRoot);
        }

        foreach (var gitDirectory in Directory.EnumerateDirectories(workspaceRoot, ".git", SearchOption.AllDirectories))
        {
            var candidate = Path.GetDirectoryName(gitDirectory);
            if (string.IsNullOrWhiteSpace(candidate))
            {
                continue;
            }

            var fullCandidate = Path.GetFullPath(candidate);
            if (!IsUnderRoot(workspaceRoot, fullCandidate))
            {
                continue;
            }

            roots.Add(fullCandidate);
        }

        return roots
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(static path => path, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static bool IsUnderRoot(string root, string path)
    {
        var comparison = OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;
        var rootFull = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var pathFull = Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        return string.Equals(pathFull, rootFull, comparison) ||
               pathFull.StartsWith(rootFull + Path.DirectorySeparatorChar, comparison) ||
               pathFull.StartsWith(rootFull + Path.AltDirectorySeparatorChar, comparison);
    }

    private static int ScoreCandidateRoot(string workspaceRoot, string candidateRoot, IReadOnlyList<string> observedPaths)
    {
        var candidateRelativeRoot = Path.GetRelativePath(workspaceRoot, candidateRoot);
        var normalizedCandidateRelativeRoot = string.Equals(candidateRelativeRoot, ".", StringComparison.Ordinal)
            ? string.Empty
            : candidateRelativeRoot.Replace('/', '\\');
        var score = 0;

        foreach (var observedPath in observedPaths)
        {
            var normalizedObservedPath = observedPath.Replace('/', '\\');
            if (normalizedCandidateRelativeRoot.Length == 0)
            {
                score += 1;
                continue;
            }

            if (normalizedObservedPath.StartsWith(normalizedCandidateRelativeRoot + "\\", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(normalizedObservedPath, normalizedCandidateRelativeRoot, StringComparison.OrdinalIgnoreCase))
            {
                score += 3;
            }
        }

        return score;
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };
}
