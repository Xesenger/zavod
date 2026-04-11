using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace zavod.Workspace;

public static class WorkspaceEvidencePackBuilder
{
    private static readonly string[] TechnicalNarrativeMarkers =
    {
        "cmake", "qt", "qml", "package.json", "go.mod", "cargo", "docker", "makefile",
        "debugger", "breakpoint", "disassembly", "opcode", "memory", "process",
        "runtime", "launch", "cli", "command", "http", "server", "api",
        "javascript", "typescript", "dom", "canvas", "css transforms",
        "terraform", "terragrunt", "opentofu", "registry", "scanner"
    };

    public static WorkspaceEvidencePack Build(
        WorkspaceScanResult scanResult,
        IReadOnlyList<WorkspaceTechnicalPreviewInput> technicalEvidence,
        IReadOnlyList<WorkspaceMaterialPreviewInput> materials)
    {
        ArgumentNullException.ThrowIfNull(scanResult);
        ArgumentNullException.ThrowIfNull(technicalEvidence);
        ArgumentNullException.ThrowIfNull(materials);

        var state = scanResult.State;
        var symbolHints = BuildSymbolHints(scanResult);
        var snippets = BuildSnippets(technicalEvidence, materials, symbolHints);
        var observations = BuildRawObservations(scanResult, technicalEvidence, materials);
        var patterns = BuildDerivedPatterns(scanResult, technicalEvidence, materials);
        var observedLayers = BuildObservedLayers(scanResult);
        var entryPoints = BuildEntryPoints(scanResult);
        var modules = BuildModuleCandidates(scanResult, observedLayers, entryPoints);
        var codeEdges = BuildCodeEdges(scanResult);
        var signatureHints = BuildSignatureHints(scanResult);
        var dependencySurface = BuildDependencySurface(scanResult);
        var relativeRelevantFiles = scanResult.RelevantFiles
            .Select(path => Path.GetRelativePath(state.WorkspaceRoot, path).Replace('/', '\\'))
            .ToArray();
        var dependencyEdges = BuildDependencyEdges(relativeRelevantFiles, observedLayers, modules, entryPoints, codeEdges, dependencySurface);
        var fileRoles = BuildFileRoles(scanResult);
        var hotspots = BuildHotspots(scanResult, materials, dependencyEdges);
        var candidates = new WorkspaceEvidenceCandidates(entryPoints, modules, fileRoles);
        var signals = BuildSignals(scanResult, technicalEvidence, materials);
        var signalScores = BuildSignalScores(signals, patterns, observations, entryPoints, fileRoles);
        var confidenceAnnotations = BuildConfidenceAnnotations(signalScores, candidates, dependencyEdges, codeEdges, dependencySurface, materials);

        return new WorkspaceEvidencePack(
            new WorkspaceProjectProfile(
                state.WorkspaceRoot,
                state.ImportKind,
                state.Health,
                state.DriftStatus,
                state.Summary.RelevantFileCount,
                state.Summary.SourceFileCount,
                state.Summary.BuildFileCount,
                state.Summary.ConfigFileCount,
                state.Summary.DocumentFileCount,
                state.Summary.AssetFileCount,
                state.Summary.BinaryFileCount,
                state.Summary.SourceRoots,
                state.Summary.BuildRoots,
                state.StructuralAnomalies.Select(static anomaly => $"{anomaly.Code}: {anomaly.Message}").ToArray()),
            BuildTechnicalPassport(scanResult, technicalEvidence, materials),
            string.Empty,
            observations,
            patterns,
            signalScores,
            candidates,
            codeEdges,
            signatureHints,
            symbolHints,
            dependencySurface,
            confidenceAnnotations,
            dependencyEdges,
            hotspots,
            entryPoints,
            observedLayers,
            modules,
            dependencyEdges,
            materials.Select(material => new WorkspaceEvidenceMaterial(
                material.RelativePath,
                material.Kind,
                material.SelectionReason,
                material.PreparationStatus ?? string.Empty,
                material.BackendId ?? string.Empty,
                material.PreparationSummary ?? string.Empty,
                material.PreviewText,
                material.WasTruncated)).ToArray(),
            signals,
            snippets);
    }

    private static (string RelativePath, string PreviewText)[] BuildPatternEligibleSnippets(
        WorkspaceScanResult scanResult,
        IReadOnlyList<WorkspaceTechnicalPreviewInput> technicalEvidence,
        IReadOnlyList<WorkspaceMaterialPreviewInput> materials)
    {
        var eligible = new List<(string RelativePath, string PreviewText)>(technicalEvidence.Count + materials.Count);
        eligible.AddRange(technicalEvidence.Select(static item => (item.RelativePath, item.PreviewText)));
        eligible.AddRange(materials
            .Where(item => IsPatternEligibleDocument(scanResult, technicalEvidence, item.RelativePath, item.PreviewText))
            .Select(static item => (item.RelativePath, item.PreviewText)));
        return eligible.ToArray();
    }

    private static WorkspaceEvidenceObservation[] BuildRawObservations(
        WorkspaceScanResult scanResult,
        IReadOnlyList<WorkspaceTechnicalPreviewInput> technicalEvidence,
        IReadOnlyList<WorkspaceMaterialPreviewInput> materials)
    {
        var observations = new List<WorkspaceEvidenceObservation>();
        var workspaceRoot = scanResult.State.WorkspaceRoot;

        foreach (var file in scanResult.RelevantFiles
                     .Select(path => Path.GetRelativePath(workspaceRoot, path).Replace('/', '\\'))
                     .Distinct(StringComparer.OrdinalIgnoreCase)
                     .OrderBy(static path => path, StringComparer.OrdinalIgnoreCase)
                     .Take(80))
        {
            observations.Add(new WorkspaceEvidenceObservation("file_found", file, file));
        }

        foreach (var root in scanResult.State.Summary.SourceRoots)
        {
            observations.Add(new WorkspaceEvidenceObservation("source_root_detected", root, root));
        }

        foreach (var root in scanResult.State.Summary.BuildRoots)
        {
            observations.Add(new WorkspaceEvidenceObservation("build_root_detected", root, root));
        }

        foreach (var candidate in scanResult.State.Summary.EntryCandidates.Take(24))
        {
            observations.Add(new WorkspaceEvidenceObservation("entry_candidate_detected", candidate, candidate));
        }

        foreach (var anomaly in scanResult.State.StructuralAnomalies)
        {
            observations.Add(new WorkspaceEvidenceObservation("structural_anomaly_detected", anomaly.Code, anomaly.Scope));
        }

        foreach (var evidence in technicalEvidence.Take(24))
        {
            observations.Add(new WorkspaceEvidenceObservation("technical_preview_detected", evidence.Category, evidence.RelativePath));
        }

        foreach (var material in materials.Take(24))
        {
            observations.Add(new WorkspaceEvidenceObservation("material_preview_detected", material.Kind.ToString(), material.RelativePath));
            if (material.Kind is WorkspaceMaterialKind.TextDocument or WorkspaceMaterialKind.PdfDocument)
            {
                observations.Add(new WorkspaceEvidenceObservation(
                    "document_kind_detected",
                    ClassifyDocumentKind(material.RelativePath, material.PreviewText),
                    material.RelativePath));
            }
        }

        return observations
            .GroupBy(static observation => $"{observation.Kind}|{observation.Value}|{observation.EvidencePath}", StringComparer.OrdinalIgnoreCase)
            .Select(static group => group.First())
            .ToArray();
    }

    private static WorkspaceEvidencePattern[] BuildDerivedPatterns(
        WorkspaceScanResult scanResult,
        IReadOnlyList<WorkspaceTechnicalPreviewInput> technicalEvidence,
        IReadOnlyList<WorkspaceMaterialPreviewInput> materials)
    {
        var patterns = new List<WorkspaceEvidencePattern>();
        var relevantFiles = scanResult.RelevantFiles
            .Select(static path => path.Replace('/', '\\'))
            .ToArray();
        var fileNames = relevantFiles.Select(static path => Path.GetFileName(path)).ToArray();
        var patternSnippets = BuildPatternEligibleSnippets(scanResult, technicalEvidence, materials);
        var combinedText = string.Join("\n", patternSnippets.Select(static item => item.PreviewText));

        AddPatternIf(true, patterns, "workspace_scan_completed", "Scanner completed bounded workspace scan.", scanResult.State.WorkspaceRoot);
        AddPatternIf(scanResult.State.Summary.SourceRoots.Count == 1, patterns, "single_root_layout", "A single primary source root is visible in the scanned workspace.", scanResult.State.Summary.SourceRoots.ToArray());
        AddPatternIf(scanResult.State.Summary.SourceRoots.Count > 1, patterns, "multi_root_layout", "Multiple source roots are visible in the scanned workspace.", scanResult.State.Summary.SourceRoots.ToArray());
        AddPatternIf(scanResult.State.ImportKind == WorkspaceImportKind.MixedImport, patterns, "mixed_binary_source_layout", "Source/build structure coexists with non-source materials.", scanResult.State.WorkspaceRoot);
        AddPatternIf(scanResult.State.StructuralAnomalies.Any(static anomaly => anomaly.Code == "NESTED_NON_SOURCE_PAYLOADS"), patterns, "nested_payload_layout", "Nested non-source payloads are visible beside the host project.", scanResult.State.WorkspaceRoot);
        AddPatternIf(scanResult.State.StructuralAnomalies.Any(static anomaly => anomaly.Code == "NESTED_GIT_PROJECTS"), patterns, "nested_git_project_layout", "Nested git-backed project roots are visible inside the scanned workspace.", scanResult.State.WorkspaceRoot);
        AddPatternIf(BuildBuildSystems(fileNames).Count > 0, patterns, "build_manifest_present", "Build-system markers are visible in the workspace.", relevantFiles.Where(path => IsBuildLikePath(path)).Take(12).ToArray());
        AddPatternIf(
            relevantFiles.Any(static path => path.EndsWith(".qml", StringComparison.OrdinalIgnoreCase) || path.EndsWith(".xaml", StringComparison.OrdinalIgnoreCase)) ||
            CountMatchingMarkers(combinedText, "qml", "frontend", "widget", "mainwindow", "theme") >= 3,
            patterns,
            "desktop_bootstrap_pattern",
            "Desktop/bootstrap markers are visible in bounded structural evidence.",
            patternSnippets.Select(static item => item.RelativePath).Take(6).ToArray());
        AddPatternIf(
            relevantFiles.Count(path =>
                path.EndsWith(".vue", StringComparison.OrdinalIgnoreCase) ||
                path.EndsWith(".tsx", StringComparison.OrdinalIgnoreCase) ||
                path.EndsWith(".jsx", StringComparison.OrdinalIgnoreCase) ||
                path.EndsWith(".html", StringComparison.OrdinalIgnoreCase)) >= 2 ||
            CountMatchingMarkers(combinedText, "dom", "canvas", "css transforms", "javascript", "html") >= 3,
            patterns,
            "browser_surface_pattern",
            "Browser-surface markers are visible in bounded structural evidence.",
            patternSnippets.Select(static item => item.RelativePath).Take(6).ToArray());
        AddPatternIf(
            relevantFiles.Any(static path =>
                path.EndsWith(".asm", StringComparison.OrdinalIgnoreCase) ||
                path.Contains("\\dbg\\", StringComparison.OrdinalIgnoreCase) ||
                path.Contains("\\analysis\\", StringComparison.OrdinalIgnoreCase) ||
                path.Contains("disasm", StringComparison.OrdinalIgnoreCase)) ||
            CountMatchingMarkers(combinedText, "debugger", "breakpoint", "opcode", "disassembly", "hook", "memory") >= 3,
            patterns,
            "analysis_tooling_pattern",
            "Analysis/debug tooling markers are visible in bounded structural evidence.",
            patternSnippets.Select(static item => item.RelativePath).Take(6).ToArray());
        AddPatternIf(relevantFiles.Any(static path => path.Contains("\\assets\\", StringComparison.OrdinalIgnoreCase) || path.Contains("\\public\\", StringComparison.OrdinalIgnoreCase)), patterns, "asset_cluster_detected", "Asset-heavy directories are visible in the workspace.", relevantFiles.Where(path => path.Contains("\\assets\\", StringComparison.OrdinalIgnoreCase) || path.Contains("\\public\\", StringComparison.OrdinalIgnoreCase)).Take(12).ToArray());
        AddPatternIf(scanResult.State.Summary.EntryCandidates.Count > 0, patterns, "entry_cluster_detected", "Scanner collected entry-point candidates.", scanResult.State.Summary.EntryCandidates.Take(12).ToArray());
        AddPatternIf(scanResult.State.Summary.DocumentFileCount > 0, patterns, "documentation_cluster_present", "Documentation files are visible in the scanned workspace.", materials.Select(static item => item.RelativePath).Take(12).ToArray());
        AddPatternIf(scanResult.State.Summary.ConfigFileCount > 0, patterns, "config_cluster_present", "Configuration files are visible in the scanned workspace.", relevantFiles.Where(static path => path.EndsWith(".json", StringComparison.OrdinalIgnoreCase) || path.EndsWith(".toml", StringComparison.OrdinalIgnoreCase) || path.EndsWith(".yaml", StringComparison.OrdinalIgnoreCase) || path.EndsWith(".yml", StringComparison.OrdinalIgnoreCase) || path.EndsWith(".ini", StringComparison.OrdinalIgnoreCase)).Take(12).ToArray());

        return patterns
            .GroupBy(static pattern => pattern.Code, StringComparer.OrdinalIgnoreCase)
            .Select(static group => group.First())
            .ToArray();
    }

    private static WorkspaceEvidenceSignalScore[] BuildSignalScores(
        IReadOnlyList<WorkspaceEvidenceSignal> signals,
        IReadOnlyList<WorkspaceEvidencePattern> patterns,
        IReadOnlyList<WorkspaceEvidenceObservation> observations,
        IReadOnlyList<WorkspaceEvidenceEntryPoint> entryPoints,
        IReadOnlyList<WorkspaceEvidenceFileRole> fileRoles)
    {
        return signals
            .GroupBy(static signal => $"{signal.Category}.{signal.Code}", StringComparer.OrdinalIgnoreCase)
            .Select(group =>
            {
                var signalKey = group.Key;
                var supportCount = group.Count();
                var evidenceCount = group
                    .Select(static signal => signal.EvidencePath)
                    .Where(static path => !string.IsNullOrWhiteSpace(path))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .Count();
                var patternSupport = patterns.Count(pattern => PatternSupportsSignal(signalKey, pattern.Code));
                var observationSupport = observations.Count(observation => ObservationSupportsSignal(signalKey, observation));
                var entrySupport = entryPoints.Count(entry => EntryPointSupportsSignal(signalKey, entry));
                var fileRoleSupport = fileRoles.Count(role => FileRoleSupportsSignal(signalKey, role));

                var score = 0.10;
                score += Math.Min(0.35, supportCount * 0.15);
                score += Math.Min(0.20, evidenceCount * 0.05);
                score += Math.Min(0.15, patternSupport * 0.10);
                score += Math.Min(0.10, observationSupport * 0.03);
                score += Math.Min(0.10, entrySupport * 0.05);
                score += Math.Min(0.10, fileRoleSupport * 0.05);

                return new WorkspaceEvidenceSignalScore(signalKey, Math.Round(Math.Min(1.0, score), 2, MidpointRounding.AwayFromZero));
            })
            .OrderByDescending(static score => score.Score)
            .ThenBy(static score => score.Signal, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static WorkspaceTechnicalPassport BuildTechnicalPassport(
        WorkspaceScanResult scanResult,
        IReadOnlyList<WorkspaceTechnicalPreviewInput> technicalEvidence,
        IReadOnlyList<WorkspaceMaterialPreviewInput> materials)
    {
        var relevantFileNames = scanResult.RelevantFiles.Select(static path => Path.GetFileName(path)).ToArray();
        var relevantFiles = scanResult.RelevantFiles.ToArray();
        var relevantExtensions = scanResult.RelevantFiles.Select(static path => Path.GetExtension(path)).ToArray();
        var patternSnippets = BuildPatternEligibleSnippets(scanResult, technicalEvidence, materials);
        var technicalText = string.Join("\n", technicalEvidence.Select(static evidence => evidence.PreviewText));
        var materialText = string.Join("\n", patternSnippets.Select(static material => material.PreviewText));
        var combinedText = $"{technicalText}\n{materialText}";
        var entryPoints = BuildEntryPoints(scanResult);
        var runtimeSurfaces = InferRuntimeSurfaces(
            relevantFiles,
            scanResult.State.Summary.SourceRoots,
            technicalEvidence,
            entryPoints,
            combinedText);

        return new WorkspaceTechnicalPassport(
            BuildObservedLanguages(relevantExtensions, combinedText),
            BuildBuildSystems(relevantFileNames),
            BuildToolchains(relevantFileNames, combinedText),
            BuildFrameworks(relevantFileNames, relevantFiles, combinedText),
            BuildVersionHints(combinedText),
            BuildTargetPlatforms(relevantFiles, combinedText, runtimeSurfaces),
            runtimeSurfaces,
            BuildConfigMarkers(scanResult.RelevantFiles),
            BuildBuildVariants(combinedText),
            BuildNotableOptions(combinedText));
    }

    private static WorkspaceEvidenceEntryPoint[] BuildEntryPoints(WorkspaceScanResult scanResult)
    {
        return scanResult.State.Summary.EntryCandidates
            .Select(path => new WorkspaceEvidenceEntryPoint(path, ClassifyEntryRole(path), BuildEntryNote(path)))
            .ToArray();
    }

    private static WorkspaceEvidenceLayer[] BuildObservedLayers(WorkspaceScanResult scanResult)
    {
        return scanResult.State.Summary.SourceRoots
            .Where(static root => !string.IsNullOrWhiteSpace(root))
            .OrderBy(static root => GetLayerPriority(root))
            .ThenBy(static root => root, StringComparer.OrdinalIgnoreCase)
            .Select(root => new WorkspaceEvidenceLayer(
                root == "." ? "root" : root,
                root,
                "Observed structural root cluster from scanner source roots.",
                "Legacy layer boundary preserved only for transition compatibility.",
                $"Observed source root '{root}'."))
            .Take(12)
            .ToArray();
    }

    private static WorkspaceEvidenceModule[] BuildModuleCandidates(
        WorkspaceScanResult scanResult,
        IReadOnlyList<WorkspaceEvidenceLayer> observedLayers,
        IReadOnlyList<WorkspaceEvidenceEntryPoint> entryPoints)
    {
        var candidates = new List<WorkspaceEvidenceModule>();
        var moduleBuckets = new Dictionary<string, (string SourceRoot, int Count)>(StringComparer.OrdinalIgnoreCase);
        var sourceRoots = scanResult.State.Summary.SourceRoots
            .Where(static root => !string.IsNullOrWhiteSpace(root))
            .ToArray();

        foreach (var path in scanResult.RelevantFiles)
        {
            if (!TryExtractModuleBucket(scanResult.State.WorkspaceRoot, path, sourceRoots, out var sourceRoot, out var bucket))
            {
                continue;
            }

            if (moduleBuckets.TryGetValue(bucket, out var existing))
            {
                moduleBuckets[bucket] = (existing.SourceRoot, existing.Count + 1);
            }
            else
            {
                moduleBuckets[bucket] = (sourceRoot, 1);
            }
        }

        foreach (var bucket in moduleBuckets
                     .OrderByDescending(static pair => pair.Value.Count)
                     .ThenBy(static pair => pair.Key, StringComparer.OrdinalIgnoreCase)
                     .Take(8))
        {
            var layerName = InferLayerNameForModule(bucket.Key, observedLayers);
            candidates.Add(new WorkspaceEvidenceModule(
                NormalizeModuleName(bucket.Key),
                InferModuleRole(bucket.Key),
                layerName,
                $"Observed {bucket.Value.Count} files clustered under '{bucket.Key}' from source root '{bucket.Value.SourceRoot}'."));
        }

        foreach (var entryPoint in entryPoints.Take(6))
        {
            var name = NormalizeModuleName(Path.GetFileNameWithoutExtension(entryPoint.RelativePath));
            if (string.IsNullOrWhiteSpace(name))
            {
                continue;
            }

            if (candidates.Any(candidate => string.Equals(candidate.Name, name, StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            candidates.Add(new WorkspaceEvidenceModule(
                name,
                "entry-surface",
                InferLayerNameForModule(entryPoint.RelativePath, observedLayers),
                $"Observed entry cluster anchored at '{entryPoint.RelativePath}'."));
        }

        return candidates
            .GroupBy(static candidate => candidate.Name, StringComparer.OrdinalIgnoreCase)
            .Select(static group => group.First())
            .OrderBy(static module => GetModulePriority(module))
            .ThenBy(static module => module.Name, StringComparer.OrdinalIgnoreCase)
            .Take(10)
            .ToArray();
    }

    private static WorkspaceEvidenceDependencyEdge[] BuildDependencyEdges(
        IReadOnlyList<string> relevantFiles,
        IReadOnlyList<WorkspaceEvidenceLayer> layers,
        IReadOnlyList<WorkspaceEvidenceModule> modules,
        IReadOnlyList<WorkspaceEvidenceEntryPoint> entryPoints,
        IReadOnlyList<WorkspaceEvidenceCodeEdge> codeEdges,
        IReadOnlyList<WorkspaceEvidenceDependencySurfaceItem> dependencySurface)
    {
        var edges = new List<WorkspaceEvidenceDependencyEdge>();
        var layerByName = layers.ToDictionary(static layer => layer.Name, StringComparer.OrdinalIgnoreCase);

        foreach (var entryPoint in entryPoints.Take(8))
        {
            var matchedModule = modules.FirstOrDefault(module =>
                ContainsAny(entryPoint.RelativePath, module.Name.Replace(' ', '_'), module.Name.Replace(' ', '-'), module.Name.Replace(' ', '\\')) ||
                ContainsAny(entryPoint.RelativePath, module.Role));
            if (matchedModule is not null)
            {
                edges.Add(new WorkspaceEvidenceDependencyEdge(
                    entryPoint.RelativePath,
                    matchedModule.Name,
                    entryPoint.Role,
                    $"Entry candidate '{entryPoint.RelativePath}' overlaps with module candidate '{matchedModule.Name}'.",
                    entryPoint.RelativePath));
                continue;
            }

            var matchedLayer = layers.FirstOrDefault(layer =>
                ContainsAny(entryPoint.RelativePath, layer.Root, layer.Name.Replace(' ', '_'), layer.Name.Replace(' ', '-')));
            if (matchedLayer is not null)
            {
                edges.Add(new WorkspaceEvidenceDependencyEdge(
                    entryPoint.RelativePath,
                    matchedLayer.Name,
                    entryPoint.Role,
                    $"Entry candidate '{entryPoint.RelativePath}' overlaps with layer/root '{matchedLayer.Name}'.",
                    entryPoint.RelativePath));
            }
        }

        foreach (var module in modules)
        {
            if (string.IsNullOrWhiteSpace(module.LayerName) ||
                !layerByName.TryGetValue(module.LayerName, out var layer))
            {
                continue;
            }

            edges.Add(new WorkspaceEvidenceDependencyEdge(
                module.Name,
                layer.Name,
                module.Role,
                $"Module candidate '{module.Name}' is grouped under layer/root '{layer.Name}'.",
                layer.Root));
        }

        foreach (var codeEdge in codeEdges.Take(24))
        {
            edges.Add(new WorkspaceEvidenceDependencyEdge(
                codeEdge.FromPath,
                codeEdge.ToPath,
                codeEdge.Kind,
                codeEdge.Reason,
                codeEdge.FromPath));
        }

        foreach (var dependency in dependencySurface.Take(24))
        {
            edges.Add(new WorkspaceEvidenceDependencyEdge(
                dependency.SourcePath,
                dependency.Name,
                dependency.Scope,
                $"Dependency surface extracted from '{dependency.SourcePath}'.",
                dependency.SourcePath));
        }

        return edges
            .GroupBy(static edge => $"{edge.From}|{edge.To}|{edge.Label}", StringComparer.OrdinalIgnoreCase)
            .Select(static group => group.First())
            .OrderBy(static edge => edge.From, StringComparer.OrdinalIgnoreCase)
            .ThenBy(static edge => edge.To, StringComparer.OrdinalIgnoreCase)
            .Take(16)
            .ToArray();
    }

    private static WorkspaceEvidenceFileRole[] BuildFileRoles(WorkspaceScanResult scanResult)
    {
        var workspaceRoot = scanResult.State.WorkspaceRoot;
        var roles = new List<WorkspaceEvidenceFileRole>();

        foreach (var file in scanResult.RelevantFiles)
        {
            var relativePath = Path.GetRelativePath(workspaceRoot, file).Replace('/', '\\');
            var role = InferFileRole(relativePath);
            if (role is null)
            {
                continue;
            }

            roles.Add(new WorkspaceEvidenceFileRole(relativePath, role.Value.Role, role.Value.Confidence, role.Value.Reason));
        }

        return roles
            .GroupBy(static item => item.RelativePath, StringComparer.OrdinalIgnoreCase)
            .Select(static group => group.OrderByDescending(static item => item.Confidence).First())
            .OrderByDescending(static item => item.Confidence)
            .ThenBy(static item => item.RelativePath, StringComparer.OrdinalIgnoreCase)
            .Take(32)
            .ToArray();
    }

    private static WorkspaceEvidenceHotspot[] BuildHotspots(
        WorkspaceScanResult scanResult,
        IReadOnlyList<WorkspaceMaterialPreviewInput> materials,
        IReadOnlyList<WorkspaceEvidenceDependencyEdge> dependencyEdges)
    {
        var workspaceRoot = scanResult.State.WorkspaceRoot;
        var hotspots = new List<WorkspaceEvidenceHotspot>();

        foreach (var file in scanResult.RelevantFiles)
        {
            var relativePath = Path.GetRelativePath(workspaceRoot, file).Replace('/', '\\');
            try
            {
                var info = new FileInfo(file);
                if (info.Exists && info.Length >= 256 * 1024)
                {
                    hotspots.Add(new WorkspaceEvidenceHotspot("large_file", relativePath, $"Observed file size {info.Length} bytes."));
                }
            }
            catch (IOException)
            {
                // Hotspot collection stays best-effort and bounded.
            }
            catch (UnauthorizedAccessException)
            {
                // Hotspot collection stays best-effort and bounded.
            }
        }

        foreach (var group in dependencyEdges
                     .GroupBy(static edge => edge.From, StringComparer.OrdinalIgnoreCase)
                     .Where(static group => group.Count() >= 3))
        {
            hotspots.Add(new WorkspaceEvidenceHotspot("high_fanout_candidate", group.Key, $"Observed {group.Count()} coarse outgoing edges from '{group.Key}'."));
        }

        foreach (var anomaly in scanResult.State.StructuralAnomalies)
        {
            if (string.Equals(anomaly.Code, "NESTED_NON_SOURCE_PAYLOADS", StringComparison.OrdinalIgnoreCase))
            {
                hotspots.Add(new WorkspaceEvidenceHotspot("nested_payload_boundary", anomaly.Scope ?? scanResult.State.WorkspaceRoot, anomaly.Message));
            }

            if (string.Equals(anomaly.Code, "NESTED_GIT_PROJECTS", StringComparison.OrdinalIgnoreCase))
            {
                hotspots.Add(new WorkspaceEvidenceHotspot("binary_source_mixed_layout", anomaly.Scope ?? scanResult.State.WorkspaceRoot, anomaly.Message));
            }
        }

        foreach (var material in materials)
        {
            if (ContainsAny(material.PreviewText, "todo", "fixme", "temporary", "workaround") &&
                CountMatchingMarkers(material.PreviewText, "runtime", "ui", "config", "build", "api", "process") >= 2)
            {
                hotspots.Add(new WorkspaceEvidenceHotspot("mixed_concerns_candidate", material.RelativePath, "Observed temporary/procedural wording mixed with technical concerns in one document."));
            }
        }

        return hotspots
            .GroupBy(static hotspot => $"{hotspot.Code}|{hotspot.RelativePath}", StringComparer.OrdinalIgnoreCase)
            .Select(static group => group.First())
            .OrderBy(static hotspot => hotspot.Code, StringComparer.OrdinalIgnoreCase)
            .ThenBy(static hotspot => hotspot.RelativePath, StringComparer.OrdinalIgnoreCase)
            .Take(24)
            .ToArray();
    }

    private static WorkspaceEvidenceSnippet[] BuildSnippets(
        IReadOnlyList<WorkspaceTechnicalPreviewInput> technicalEvidence,
        IReadOnlyList<WorkspaceMaterialPreviewInput> materials,
        IReadOnlyList<WorkspaceEvidenceSymbolHint> symbolHints)
    {
        var snippets = new List<WorkspaceEvidenceSnippet>(technicalEvidence.Count + materials.Count + symbolHints.Count);
        snippets.AddRange(technicalEvidence.Select(evidence => new WorkspaceEvidenceSnippet(
            evidence.RelativePath,
            evidence.Category,
            evidence.PreviewText,
            evidence.WasTruncated)));
        snippets.AddRange(materials.Select(material => new WorkspaceEvidenceSnippet(
            material.RelativePath,
            material.Kind.ToString(),
            material.PreviewText,
            material.WasTruncated)));
        snippets.AddRange(symbolHints.Select(static hint => new WorkspaceEvidenceSnippet(
            hint.RelativePath,
            "binary_symbol_hint",
            hint.Symbol,
            WasTruncated: false)));
        return snippets.ToArray();
    }

    private static WorkspaceEvidenceSignal[] BuildSignals(
        WorkspaceScanResult scanResult,
        IReadOnlyList<WorkspaceTechnicalPreviewInput> technicalEvidence,
        IReadOnlyList<WorkspaceMaterialPreviewInput> materials)
    {
        var signals = new List<WorkspaceEvidenceSignal>();
        var state = scanResult.State;
        var relevantFiles = scanResult.RelevantFiles;
        var combinedText = string.Join("\n", BuildPatternEligibleSnippets(scanResult, technicalEvidence, materials)
            .Select(static snippet => snippet.PreviewText));
        var entryPoints = BuildEntryPoints(scanResult);

        if (state.Summary.SourceRoots.Count <= 1)
        {
            signals.Add(new WorkspaceEvidenceSignal("structure", "single_root", "Scanner observed a single primary source root.", state.Summary.SourceRoots.FirstOrDefault()));
        }
        else
        {
            signals.Add(new WorkspaceEvidenceSignal("structure", "multi_root", $"Scanner observed {state.Summary.SourceRoots.Count} primary source roots.", state.Summary.SourceRoots[0]));
        }

        if (state.ImportKind == WorkspaceImportKind.MixedImport)
        {
            signals.Add(new WorkspaceEvidenceSignal("structure", "mixed_import", "Source/build structure coexists with non-source materials.", state.WorkspaceRoot));
        }

        if (state.StructuralAnomalies.Any(static anomaly => anomaly.Code == "NESTED_NON_SOURCE_PAYLOADS"))
        {
            signals.Add(new WorkspaceEvidenceSignal("structure", "nested_payloads", "Scanner detected nested non-source payload roots beside host structure.", state.WorkspaceRoot));
        }

        if (state.StructuralAnomalies.Any(static anomaly => anomaly.Code == "NESTED_GIT_PROJECTS"))
        {
            signals.Add(new WorkspaceEvidenceSignal("structure", "nested_git_projects", "Scanner detected nested git-backed project roots inside the scanned workspace.", state.WorkspaceRoot));
        }

        foreach (var buildSystem in BuildBuildSystems(relevantFiles.Select(static path => Path.GetFileName(path)).ToArray()))
        {
            signals.Add(new WorkspaceEvidenceSignal("build", buildSystem, $"Observed build marker for {buildSystem}.", FindRelevantFile(relevantFiles, buildSystem)));
        }

        foreach (var runtimeSurface in InferRuntimeSurfaces(relevantFiles, state.Summary.SourceRoots, technicalEvidence, entryPoints, combinedText))
        {
            signals.Add(new WorkspaceEvidenceSignal("runtime", runtimeSurface, $"Observed runtime surface signal '{runtimeSurface}'.", state.WorkspaceRoot));
        }

        foreach (var behaviorSignal in InferBehaviorSignals(relevantFiles, combinedText))
        {
            signals.Add(new WorkspaceEvidenceSignal("behavior", behaviorSignal, $"Observed behavior signal '{behaviorSignal}'.", state.WorkspaceRoot));
        }

        foreach (var origin in InferOriginSignals(relevantFiles, combinedText))
        {
            signals.Add(new WorkspaceEvidenceSignal("origin", origin, $"Observed origin signal '{origin}'.", state.WorkspaceRoot));
        }

        foreach (var stageSignal in InferStageSignals(scanResult, materials, combinedText))
        {
            signals.Add(new WorkspaceEvidenceSignal("stage", stageSignal, $"Observed stage signal '{stageSignal}'.", state.WorkspaceRoot));
        }

        foreach (var material in materials)
        {
            var noiseCode = InferNoiseCode(material.RelativePath, material.Kind);
            if (!string.IsNullOrWhiteSpace(noiseCode))
            {
                signals.Add(new WorkspaceEvidenceSignal("noise", noiseCode!, $"Material looks noisy or secondary for import focus: {material.RelativePath}.", material.RelativePath));
            }

            var temporalCode = InferTemporalCode(material.RelativePath, material.PreviewText);
            if (!string.IsNullOrWhiteSpace(temporalCode))
            {
                signals.Add(new WorkspaceEvidenceSignal("temporal", temporalCode!, $"Material carries temporal/status wording: {material.RelativePath}.", material.RelativePath));
            }
        }

        return signals
            .GroupBy(static signal => $"{signal.Category}|{signal.Code}|{signal.EvidencePath}", StringComparer.OrdinalIgnoreCase)
            .Select(static group => group.First())
            .ToArray();
    }

    private static IReadOnlyList<string> BuildObservedLanguages(IEnumerable<string> extensions, string combinedText)
    {
        var languages = new List<string>();
        AddIfAnyExtension(languages, extensions, "C", ".c", ".h");
        AddIfAnyExtension(languages, extensions, "C++", ".cc", ".cpp", ".hpp", ".inc");
        AddIfAnyExtension(languages, extensions, "C#", ".cs");
        AddIfAnyExtension(languages, extensions, "Go", ".go");
        AddIfAnyExtension(languages, extensions, "Java", ".java");
        AddIfAnyExtension(languages, extensions, "JavaScript", ".js", ".cjs", ".mjs", ".jsx");
        AddIfAnyExtension(languages, extensions, "TypeScript", ".ts", ".tsx");
        AddIfAnyExtension(languages, extensions, "Python", ".py");
        AddIfAnyExtension(languages, extensions, "Rust", ".rs");
        AddIfAnyExtension(languages, extensions, "SQL", ".sql");
        AddIfAnyExtension(languages, extensions, "Lua", ".lua");
        AddIfAnyExtension(languages, extensions, "Assembly", ".asm", ".s", ".inc");
        AddIfAnyExtension(languages, extensions, "Vue", ".vue");
        AddIfAnyExtension(languages, extensions, "Svelte", ".svelte");
        AddIfAnyExtension(languages, extensions, "Astro", ".astro");

        if (combinedText.Contains("QML", StringComparison.OrdinalIgnoreCase))
        {
            languages.Add("QML");
        }

        return languages.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
    }

    private static IReadOnlyList<string> BuildBuildSystems(IEnumerable<string> fileNames)
    {
        var buildSystems = new List<string>();
        AddIfAnyFile(buildSystems, fileNames, "cmake", "CMakeLists.txt", "CMakePresets.json", "CMakeCache.txt");
        AddIfAnyFile(buildSystems, fileNames, "cargo", "Cargo.toml");
        AddIfAnyFile(buildSystems, fileNames, "go_mod", "go.mod");
        AddIfAnyFile(buildSystems, fileNames, "dotnet", ".sln", ".csproj", ".props", ".targets");
        AddIfAnyFile(buildSystems, fileNames, "package_json", "package.json");
        AddIfAnyFile(buildSystems, fileNames, "docker", "Dockerfile", "docker-compose.yml", "docker-compose.yaml", "compose.yml", "compose.yaml");
        AddIfAnyFile(buildSystems, fileNames, "make", "Makefile");
        return buildSystems.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
    }

    private static IReadOnlyList<string> BuildToolchains(
        IReadOnlyList<string> fileNames,
        string combinedText)
    {
        var toolchains = new List<string>();
        if (fileNames.Any(static name =>
                name.EndsWith(".sln", StringComparison.OrdinalIgnoreCase) ||
                name.EndsWith(".vcxproj", StringComparison.OrdinalIgnoreCase) ||
                name.EndsWith(".props", StringComparison.OrdinalIgnoreCase) ||
                name.EndsWith(".targets", StringComparison.OrdinalIgnoreCase)) ||
            CountMatchingMarkers(combinedText, "msvc", "visual studio", "cl.exe") >= 2)
        {
            toolchains.Add("MSVC");
        }

        if (fileNames.Any(static name =>
                string.Equals(name, "build.ninja", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(name, "CMakePresets.json", StringComparison.OrdinalIgnoreCase)) &&
            ContainsAny(combinedText, "ninja", "generator"))
        {
            toolchains.Add("Ninja");
        }

        if (fileNames.Any(static name =>
                string.Equals(name, "Makefile", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(name, "configure", StringComparison.OrdinalIgnoreCase) ||
                name.EndsWith(".mk", StringComparison.OrdinalIgnoreCase)) ||
            CountMatchingMarkers(combinedText, "gcc", "g++", "gnu") >= 2)
        {
            toolchains.Add("GNU");
        }

        if (CountMatchingMarkers(combinedText, "clang", "clang++", "llvm") >= 2)
        {
            toolchains.Add("Clang");
        }

        if (fileNames.Any(static name =>
                name.EndsWith(".sln", StringComparison.OrdinalIgnoreCase) ||
                name.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(name, "global.json", StringComparison.OrdinalIgnoreCase)) ||
            CountMatchingMarkers(combinedText, "dotnet", "sdk-style", "nuget") >= 2)
        {
            toolchains.Add("dotnet-sdk");
        }

        return toolchains.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
    }

    private static IReadOnlyList<string> BuildFrameworks(
        IReadOnlyList<string> fileNames,
        IReadOnlyList<string> relevantFiles,
        string combinedText)
    {
        var frameworks = new List<string>();
        if (relevantFiles.Any(static path => path.EndsWith(".qml", StringComparison.OrdinalIgnoreCase)) ||
            CountMatchingMarkers(combinedText, "qt6", "qt 6", "qml", "qt widgets") >= 2)
        {
            frameworks.Add("Qt");
        }

        if (relevantFiles.Any(static path => path.EndsWith(".xaml", StringComparison.OrdinalIgnoreCase)) &&
            ContainsAny(combinedText, "winui", "windows ui", "wpf"))
        {
            frameworks.Add("WinUI");
        }

        if (fileNames.Any(static name => string.Equals(name, "terragrunt.hcl", StringComparison.OrdinalIgnoreCase)) ||
            CountMatchingMarkers(combinedText, "terraform", "terragrunt", "opentofu") >= 2)
        {
            if (ContainsAny(combinedText, "terraform"))
            {
                frameworks.Add("Terraform");
            }

            if (ContainsAny(combinedText, "opentofu"))
            {
                frameworks.Add("OpenTofu");
            }

            if (ContainsAny(combinedText, "terragrunt"))
            {
                frameworks.Add("Terragrunt");
            }
        }

        if (relevantFiles.Any(static path => path.EndsWith(".vue", StringComparison.OrdinalIgnoreCase)) ||
            fileNames.Any(static name =>
                string.Equals(name, "vite.config.ts", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(name, "vite.config.js", StringComparison.OrdinalIgnoreCase)))
        {
            frameworks.Add("Vue");
        }

        return frameworks.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
    }

    private static IReadOnlyList<string> BuildVersionHints(string combinedText)
    {
        var hints = new List<string>();
        foreach (var marker in new[] { "c++20", "qt6 6.10", "qt 6.10", "go 1.24", "windows 10", "windows 11" })
        {
            if (combinedText.Contains(marker, StringComparison.OrdinalIgnoreCase))
            {
                hints.Add(marker);
            }
        }

        return hints.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
    }

    private static IReadOnlyList<string> BuildTargetPlatforms(
        IEnumerable<string> relevantFiles,
        string combinedText,
        IReadOnlyList<string> runtimeSurfaces)
    {
        var platforms = new List<string>();
        var normalizedFiles = relevantFiles.Select(static path => path.Replace('/', '\\')).ToArray();

        if (normalizedFiles.Any(static path =>
                path.EndsWith(".sln", StringComparison.OrdinalIgnoreCase) ||
                path.EndsWith(".vcxproj", StringComparison.OrdinalIgnoreCase) ||
                path.EndsWith(".props", StringComparison.OrdinalIgnoreCase) ||
                path.EndsWith(".targets", StringComparison.OrdinalIgnoreCase) ||
                path.EndsWith(".xaml", StringComparison.OrdinalIgnoreCase) ||
                path.EndsWith(".qml", StringComparison.OrdinalIgnoreCase)) ||
            CountMatchingMarkers(combinedText, "windows", "win32", "msvc", "visual studio") >= 2)
        {
            platforms.Add("Windows");
        }

        if (normalizedFiles.Any(static path =>
                path.EndsWith(".sh", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(Path.GetFileName(path), "configure", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(Path.GetFileName(path), "meson.build", StringComparison.OrdinalIgnoreCase)) &&
            CountMatchingMarkers(combinedText, "linux", "unix", "posix") >= 2)
        {
            platforms.Add("Linux");
        }

        if (normalizedFiles.Any(static path =>
                path.Contains("\\darwin\\", StringComparison.OrdinalIgnoreCase) ||
                path.Contains("\\macos\\", StringComparison.OrdinalIgnoreCase) ||
                path.EndsWith(".xcodeproj", StringComparison.OrdinalIgnoreCase)) ||
            CountMatchingMarkers(combinedText, "macos", "darwin", "osx") >= 2)
        {
            platforms.Add("macOS");
        }

        if (runtimeSurfaces.Contains("web", StringComparer.OrdinalIgnoreCase) &&
            normalizedFiles.Any(static path =>
                path.EndsWith(".vue", StringComparison.OrdinalIgnoreCase) ||
                path.EndsWith(".tsx", StringComparison.OrdinalIgnoreCase) ||
                path.EndsWith(".jsx", StringComparison.OrdinalIgnoreCase) ||
                path.EndsWith(".html", StringComparison.OrdinalIgnoreCase) ||
                path.Contains("\\public\\", StringComparison.OrdinalIgnoreCase) ||
                path.Contains("\\frontend\\", StringComparison.OrdinalIgnoreCase) ||
                path.Contains("\\web\\", StringComparison.OrdinalIgnoreCase)))
        {
            platforms.Add("Web");
        }

        return platforms.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
    }

    private static IReadOnlyList<string> BuildConfigMarkers(IEnumerable<string> relevantFiles)
    {
        return relevantFiles
            .Select(static path => Path.GetFileName(path))
            .Where(static fileName => fileName.EndsWith(".json", StringComparison.OrdinalIgnoreCase) ||
                                      fileName.EndsWith(".toml", StringComparison.OrdinalIgnoreCase) ||
                                      fileName.EndsWith(".yaml", StringComparison.OrdinalIgnoreCase) ||
                                      fileName.EndsWith(".yml", StringComparison.OrdinalIgnoreCase) ||
                                      fileName.EndsWith(".ini", StringComparison.OrdinalIgnoreCase) ||
                                      fileName.EndsWith(".env", StringComparison.OrdinalIgnoreCase))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(static value => value, StringComparer.OrdinalIgnoreCase)
            .Take(12)
            .ToArray();
    }

    private static IReadOnlyList<string> BuildBuildVariants(string combinedText)
    {
        var variants = new List<string>();
        foreach (var marker in new[] { "debug", "release", "test", "tests", "ninja-debug", "main", "build-tests", "build-main" })
        {
            if (combinedText.Contains(marker, StringComparison.OrdinalIgnoreCase))
            {
                variants.Add(marker);
            }
        }

        return variants.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
    }

    private static IReadOnlyList<string> BuildNotableOptions(string combinedText)
    {
        var options = new List<string>();
        foreach (var line in combinedText.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
        {
            var trimmed = line.Trim();
            var isBoundedOption =
                trimmed.Contains(":BOOL=", StringComparison.OrdinalIgnoreCase) ||
                trimmed.Contains("option(", StringComparison.OrdinalIgnoreCase) ||
                (trimmed.Contains("set(", StringComparison.OrdinalIgnoreCase) &&
                 ContainsAny(trimmed, "BUILD_", "ENABLE_", "USE_", "WITH_", "X64DBG_", "CODEX_"));

            if (isBoundedOption)
            {
                if (trimmed.StartsWith("#", StringComparison.OrdinalIgnoreCase) ||
                    trimmed.StartsWith("cmake_minimum_required(", StringComparison.OrdinalIgnoreCase) ||
                    trimmed.Contains("function(", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (trimmed.Length > 96)
                {
                    continue;
                }

                options.Add(trimmed);
            }
        }

        return options.Distinct(StringComparer.OrdinalIgnoreCase).Take(6).ToArray();
    }

    private static IReadOnlyList<string> InferRuntimeSurfaces(
        IEnumerable<string> relevantFiles,
        IReadOnlyList<string> sourceRoots,
        IReadOnlyList<WorkspaceTechnicalPreviewInput> technicalEvidence,
        IReadOnlyList<WorkspaceEvidenceEntryPoint> entryPoints,
        string combinedText)
    {
        var surfaces = new List<string>();
        var normalizedFiles = relevantFiles.Select(static path => path.Replace('/', '\\')).ToArray();
        var normalizedRoots = sourceRoots.Select(static root => root.Replace('/', '\\')).ToArray();
        var technicalCategories = technicalEvidence.Select(static evidence => evidence.Category).ToArray();

        if (entryPoints.Any(entry => string.Equals(entry.Role, "cli", StringComparison.OrdinalIgnoreCase)) ||
            normalizedFiles.Any(static path => path.Contains("\\cmd\\", StringComparison.OrdinalIgnoreCase)))
        {
            surfaces.Add("cli");
        }

        if (normalizedFiles.Any(static path =>
                path.EndsWith("App.xaml", StringComparison.OrdinalIgnoreCase) ||
                path.EndsWith(".qml", StringComparison.OrdinalIgnoreCase) ||
                path.EndsWith(".xaml", StringComparison.OrdinalIgnoreCase)) ||
            (normalizedFiles.Any(static path => path.Contains("\\ui\\", StringComparison.OrdinalIgnoreCase) || path.Contains("\\qml\\", StringComparison.OrdinalIgnoreCase)) &&
             CountMatchingMarkers(combinedText, "qml", "mainwindow", "desktop ui", "qt widgets", "winui", "wpf") >= 2))
        {
            surfaces.Add("desktop");
        }

        var webPathCount = normalizedFiles.Count(path =>
            !ContainsAny(path, "\\docs\\", "\\doc\\", "\\test\\", "\\tests\\", "\\vendor\\", "\\third_party\\", "\\.github\\") &&
            (path.EndsWith(".vue", StringComparison.OrdinalIgnoreCase) ||
             path.EndsWith(".tsx", StringComparison.OrdinalIgnoreCase) ||
             path.EndsWith(".jsx", StringComparison.OrdinalIgnoreCase) ||
             path.EndsWith(".html", StringComparison.OrdinalIgnoreCase) ||
             path.Contains("\\public\\", StringComparison.OrdinalIgnoreCase) ||
             path.Contains("\\frontend\\", StringComparison.OrdinalIgnoreCase) ||
             path.Contains("\\web\\", StringComparison.OrdinalIgnoreCase)));
        var hasWebPaths = webPathCount >= 3;
        var hasWebBuild = technicalCategories.Any(static category => string.Equals(category, "web-build", StringComparison.OrdinalIgnoreCase));
        if ((hasWebBuild && webPathCount >= 1) ||
            (hasWebPaths && CountMatchingMarkers(combinedText, "dom", "webgl", "canvas", "html", "css", "javascript") >= 2))
        {
            surfaces.Add("web");
        }

        var servicePathCount = normalizedFiles.Count(path =>
            !ContainsAny(path, "\\docs\\", "\\doc\\", "\\test\\", "\\tests\\", "\\examples\\", "\\samples\\") &&
            (path.Contains("\\server\\", StringComparison.OrdinalIgnoreCase) ||
             path.Contains("\\service\\", StringComparison.OrdinalIgnoreCase) ||
             path.Contains("\\api\\", StringComparison.OrdinalIgnoreCase) ||
             path.Contains("\\http\\", StringComparison.OrdinalIgnoreCase)));
        var hasServicePaths = servicePathCount >= 2;
        var hasServiceEntryCluster = entryPoints.Any(entry =>
            IsServiceEntryCandidate(entry.RelativePath) &&
            !IsTestLikeServiceEntry(entry.RelativePath));
        var serviceMarkerCount = CountMatchingMarkers(combinedText, "http server", "grpc", "daemon", "background service", "rest api", "remote server");
        var confirmedServiceEntry = entryPoints.Any(entry =>
            string.Equals(entry.Role, "service", StringComparison.OrdinalIgnoreCase) &&
            !IsTestLikeServiceEntry(entry.RelativePath));
        if ((confirmedServiceEntry && (servicePathCount >= 1 || serviceMarkerCount >= 1)) ||
            (hasServiceEntryCluster && hasServicePaths && serviceMarkerCount >= 1) ||
            (hasServicePaths && serviceMarkerCount >= 3))
        {
            surfaces.Add("service");
        }

        if (normalizedFiles.Any(static path =>
                path.EndsWith(".asm", StringComparison.OrdinalIgnoreCase) ||
                path.EndsWith(".s", StringComparison.OrdinalIgnoreCase)) ||
            (normalizedFiles.Any(static path => path.Contains("\\firmware\\", StringComparison.OrdinalIgnoreCase) || path.Contains("\\embedded\\", StringComparison.OrdinalIgnoreCase)) &&
             CountMatchingMarkers(combinedText, "firmware", "embedded", "mcu", "bare metal") >= 1))
        {
            surfaces.Add("embedded");
        }

        if (normalizedFiles.Any(static path =>
                path.EndsWith(".asm", StringComparison.OrdinalIgnoreCase) ||
                path.EndsWith(".s", StringComparison.OrdinalIgnoreCase) ||
                path.Contains("\\disasm\\", StringComparison.OrdinalIgnoreCase) ||
                path.Contains("\\analysis\\", StringComparison.OrdinalIgnoreCase) ||
                path.Contains("\\dbg\\", StringComparison.OrdinalIgnoreCase)) ||
            ((normalizedFiles.Any(static path => path.Contains("\\tools\\", StringComparison.OrdinalIgnoreCase) || path.Contains("\\plugins\\", StringComparison.OrdinalIgnoreCase)) ||
              normalizedFiles.Any(static path => path.Contains("\\analysis\\", StringComparison.OrdinalIgnoreCase) || path.Contains("\\dbg\\", StringComparison.OrdinalIgnoreCase))) &&
             CountMatchingMarkers(combinedText, "reverse engineering", "emulator", "disassembly", "opcode", "breakpoint", "debugger") >= 2))
        {
            surfaces.Add("analysis");
        }

        if (surfaces.Count > 1)
        {
            surfaces.Add("mixed");
        }

        return surfaces.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
    }

    private static IEnumerable<string> InferBehaviorSignals(IEnumerable<string> relevantFiles, string combinedText)
    {
        var joinedPaths = string.Join("\n", relevantFiles);

        if (ContainsAny(joinedPaths, "\\qml\\", "\\ui\\", "\\theme\\") ||
            CountMatchingMarkers(combinedText, "qml", "render", "theme", "visual", "widget", "mainwindow") >= 3)
        {
            yield return "ui_rendering";
        }

        if (CountMatchingMarkers(combinedText, "filesystem", "directory", "archive", "extract", "path") >= 2 ||
            (ContainsAny(joinedPaths, "\\archive\\", "\\storage\\") && CountMatchingMarkers(combinedText, "filesystem", "extract", "archive") >= 1))
        {
            yield return "filesystem";
        }

        if (ContainsAny(joinedPaths, "runner", "launch", "\\dbg\\", "debugger") ||
            CountMatchingMarkers(combinedText, "process", "runner", "launch", "execute", "breakpoint", "debugger") >= 2)
        {
            yield return "process_control";
        }

        if (ContainsAny(joinedPaths, "\\http\\", "\\api\\", "\\server\\") ||
            CountMatchingMarkers(combinedText, "http", "network", "socket", "remote", "tcp", "grpc") >= 2)
        {
            yield return "network";
        }

        if (ContainsAny(joinedPaths, "\\scripts\\", "\\automation\\", "\\pipeline\\") ||
            CountMatchingMarkers(combinedText, "automation", "script", "pipeline", "generate") >= 2)
        {
            yield return "automation";
        }

        if (CountMatchingMarkers(combinedText, "render", "preview", "theme", "asset", "sprite", "texture", "qml") >= 2)
        {
            yield return "content_rendering";
        }

        if (ContainsAny(joinedPaths, "\\hook\\", "\\patch\\") ||
            CountMatchingMarkers(combinedText, "hook", "inject", "patch", "extension") >= 3)
        {
            yield return "hooking";
        }

        if (CountMatchingMarkers(combinedText, "memory", "register", "opcode", "assembler", "disassembly") >= 3)
        {
            yield return "low_level_memory";
        }

        if (ContainsAny(joinedPaths, "\\parser\\", "\\scan\\", "\\decode\\") ||
            CountMatchingMarkers(combinedText, "parse", "parser", "scan", "token", "decode") >= 2)
        {
            yield return "parsing";
        }
    }

    private static IEnumerable<string> InferOriginSignals(IEnumerable<string> relevantFiles, string combinedText)
    {
        var joinedPaths = string.Join("\n", relevantFiles);
        if (ContainsAny(joinedPaths, "\\mods\\", "\\modding\\", "modloader", "mod-loader") ||
            CountMatchingMarkers(combinedText, "mod loader", "modding", "mod platform", "mods") >= 2)
        {
            yield return "modding";
        }

        if (ContainsAny(joinedPaths, "reverse", "disasm", "\\dbg\\", "\\analysis\\") ||
            CountMatchingMarkers(combinedText, "debugger", "breakpoint", "opcode", "disassembly", "memory", "emulator", "assembler") >= 3)
        {
            yield return "reverse";
        }

        if (ContainsAny(joinedPaths, "\\generated\\", "\\autogen\\", "\\codegen\\", "\\out\\") ||
            (ContainsAny(joinedPaths, "\\generated\\", "\\autogen\\", "\\codegen\\") &&
             CountMatchingMarkers(combinedText, "generated by", "code generation", "autogenerated") >= 1))
        {
            yield return "generated";
        }

        yield return "native";
    }

    private static IEnumerable<string> InferStageSignals(
        WorkspaceScanResult scanResult,
        IReadOnlyList<WorkspaceMaterialPreviewInput> materials,
        string combinedText)
    {
        if (scanResult.State.Summary.BuildFileCount > 0)
        {
            yield return "build_present";
        }

        if (scanResult.State.Summary.EntryCandidates.Count > 0)
        {
            yield return "entry_present";
        }

        if (scanResult.State.Summary.DocumentFileCount > 0)
        {
            yield return "docs_present";
        }

        if (materials.Any(static material => material.RelativePath.Contains("snapshot", StringComparison.OrdinalIgnoreCase)))
        {
            yield return "snapshot_pressure";
        }

        if (ContainsAny(combinedText, "todo", "next step", "future", "planned"))
        {
            yield return "todo_density";
        }

        if (ContainsAny(combinedText, "current", "active", "implemented", "stabilized", "stabilisation", "stabilization"))
        {
            yield return "current_activity";
        }
    }

    private static string? InferNoiseCode(string relativePath, WorkspaceMaterialKind kind)
    {
        var normalized = relativePath.Replace('/', '\\');
        if (ContainsAny(normalized, "LICENSE", "SECURITY", "CODE_OF_CONDUCT", "CONTRIBUTING", "ISSUE_TEMPLATE", "pull_request_template", "ThirdPartyNotice"))
        {
            return "community_legal_meta";
        }

        if (kind == WorkspaceMaterialKind.ImageAsset && ContainsAny(normalized, "\\assets\\flats\\", "\\assets\\textures\\", "\\assets\\sprites\\", "\\tiles\\"))
        {
            return "bulk_assets";
        }

        if (ContainsAny(normalized, "test_output", "test-output", "coverage", "projection", "results"))
        {
            return "generated_test_output";
        }

        if (ContainsAny(normalized, "CHEATSHEET", "REMINDER", "NOTE", "PLAN", "для себя", "не забыть"))
        {
            return "procedural_notes";
        }

        return null;
    }

    private static string? InferTemporalCode(string relativePath, string previewText)
    {
        if (ContainsAny(relativePath, "roadmap", "plan", "draft"))
        {
            return "planned_hint";
        }

        if (ContainsAny(previewText, "todo", "next step", "future"))
        {
            return "planned_hint";
        }

        if (ContainsAny(previewText, "legacy", "historical", "snapshot"))
        {
            return "historical_hint";
        }

        if (ContainsAny(previewText, "current", "active", "implementation"))
        {
            return "current_hint";
        }

        return null;
    }

    private static string ClassifyEntryRole(string relativePath)
    {
        var normalized = relativePath.Replace('/', '\\');
        if (ContainsAny(normalized, "\\test\\", "\\tests\\", "_test.", ".spec.", ".e2e."))
        {
            return "test";
        }

        if (normalized.Contains("\\server\\", StringComparison.OrdinalIgnoreCase) ||
            normalized.Contains("\\service\\", StringComparison.OrdinalIgnoreCase) ||
            normalized.Contains("\\api\\", StringComparison.OrdinalIgnoreCase))
        {
            return "service";
        }

        if (normalized.Contains("\\cmd\\", StringComparison.OrdinalIgnoreCase))
        {
            return "cli";
        }

        if (normalized.EndsWith("App.xaml", StringComparison.OrdinalIgnoreCase))
        {
            return "ui";
        }

        return "entry";
    }

    private static string BuildEntryNote(string relativePath)
    {
        return $"Observed entry candidate at {relativePath}.";
    }

    private static bool IsPatternEligibleDocument(
        WorkspaceScanResult scanResult,
        IReadOnlyList<WorkspaceTechnicalPreviewInput> technicalEvidence,
        string relativePath,
        string previewText)
    {
        if (string.IsNullOrWhiteSpace(previewText))
        {
            return false;
        }

        var normalizedPath = relativePath.Replace('/', '\\');
        if (IsBuildLikePath(normalizedPath) ||
            normalizedPath.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase) ||
            normalizedPath.EndsWith(".vcxproj", StringComparison.OrdinalIgnoreCase) ||
            normalizedPath.EndsWith("package.json", StringComparison.OrdinalIgnoreCase) ||
            normalizedPath.EndsWith("go.mod", StringComparison.OrdinalIgnoreCase) ||
            normalizedPath.EndsWith("Cargo.toml", StringComparison.OrdinalIgnoreCase) ||
            normalizedPath.EndsWith("pyproject.toml", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (CountMatchingMarkers(previewText, TechnicalNarrativeMarkers) < 2)
        {
            return false;
        }

        return HasNonDocumentTechnicalOverlap(scanResult, technicalEvidence, relativePath, previewText);
    }

    private static int GetLayerPriority(string root)
    {
        if (string.Equals(root, ".", StringComparison.OrdinalIgnoreCase))
        {
            return 0;
        }

        if (ContainsAny(root, "src"))
        {
            return 1;
        }

        if (ContainsAny(root, "cmd", "cli"))
        {
            return 2;
        }

        if (ContainsAny(root, "engine", "core", "runtime", "project", "stack"))
        {
            return 3;
        }

        if (ContainsAny(root, "ui", "presentation", "web"))
        {
            return 4;
        }

        if (ContainsAny(root, "cloud", "http", "api", "service"))
        {
            return 5;
        }

        if (ContainsAny(root, "config", "hcl", "yaml", "ls", "lsp", "tg", "tf"))
        {
            return 6;
        }

        if (ContainsAny(root, "generate", "scaffold", "test", "bench", "e2e"))
        {
            return 7;
        }

        return 10;
    }

    private static bool TryExtractModuleBucket(string workspaceRoot, string path, IReadOnlyList<string> sourceRoots, out string sourceRoot, out string bucket)
    {
        var normalized = path.Replace('/', '\\');
        if (Path.IsPathRooted(normalized))
        {
            normalized = Path.GetRelativePath(workspaceRoot, normalized).Replace('/', '\\');
        }

        foreach (var root in sourceRoots.OrderByDescending(static value => value.Length))
        {
            var normalizedRoot = root.Replace('/', '\\');
            string remainder;
            if (normalizedRoot == ".")
            {
                remainder = normalized;
            }
            else if (normalized.StartsWith(normalizedRoot + "\\", StringComparison.OrdinalIgnoreCase))
            {
                remainder = normalized[(normalizedRoot.Length + 1)..];
            }
            else
            {
                continue;
            }

            var segments = remainder.Split('\\', StringSplitOptions.RemoveEmptyEntries);
            if (segments.Length == 0)
            {
                continue;
            }

            var firstSegment = segments[0];
            if (ContainsAny(firstSegment, "src", "source", "sources", "lib", "app") && segments.Length > 1)
            {
                firstSegment = segments[1];
            }

            if (string.IsNullOrWhiteSpace(firstSegment) ||
                ContainsAny(firstSegment, ".github", "docs", "doc", "assets", "public", "images", "tests", "test", "build", "bin", "obj"))
            {
                continue;
            }

            sourceRoot = root;
            bucket = firstSegment;
            return true;
        }

        sourceRoot = string.Empty;
        bucket = string.Empty;
        return false;
    }

    private static void AddDerivedModuleIfSeen(
        List<WorkspaceEvidenceModule> target,
        IReadOnlyList<(string RelativePath, string PreviewText)> snippets,
        string name,
        string role,
        string layerName,
        string evidenceNote,
        int minSnippetMatches = 1,
        params string[] markers)
    {
        if (target.Any(module => string.Equals(module.Name, name, StringComparison.OrdinalIgnoreCase)))
        {
            return;
        }

        var matchCount = 0;
        foreach (var snippet in snippets)
        {
            if (!ContainsAny($"{snippet.RelativePath}\n{snippet.PreviewText}", markers))
            {
                continue;
            }

            matchCount++;
            if (matchCount < minSnippetMatches)
            {
                continue;
            }

            target.Add(new WorkspaceEvidenceModule(name, role, layerName, $"{evidenceNote} Evidence: {snippet.RelativePath}."));
            return;
        }
    }

    private static string NormalizeModuleName(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var spaced = value.Replace('-', ' ').Replace('_', ' ').Trim();
        return string.Join(" ", spaced
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Select(static part => part.Length == 0 ? part : char.ToUpperInvariant(part[0]) + part[1..]));
    }

    private static string InferModuleRole(string bucket)
    {
        if (ContainsAny(bucket, "cmd", "cli"))
        {
            return "command-cluster";
        }

        if (ContainsAny(bucket, "test", "tests", "spec"))
        {
            return "test-cluster";
        }

        if (ContainsAny(bucket, "dbg", "debug", "hook", "memory", "emu", "disasm", "analysis"))
        {
            return "low-level-cluster";
        }

        if (ContainsAny(bucket, "config", "settings"))
        {
            return "config-cluster";
        }

        return "subsystem-cluster";
    }

    private static (string Role, double Confidence, string Reason)? InferFileRole(string relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath))
        {
            return null;
        }

        var normalized = relativePath.Replace('/', '\\');
        if (ContainsAny(normalized, "\\test\\", "\\tests\\", "test_", "_test.", ".spec.", ".e2e."))
        {
            return ("test", 0.9, "Path markers indicate testing scope.");
        }

        if (ContainsAny(normalized, "\\config\\", ".json", ".yaml", ".yml", ".toml", ".ini", ".env"))
        {
            return ("config", 0.8, "Path or extension markers indicate configuration.");
        }

        if (ContainsAny(normalized, "\\assets\\", "\\public\\", "\\images\\", ".png", ".jpg", ".jpeg", ".svg"))
        {
            return ("asset", 0.85, "Path or extension markers indicate asset content.");
        }

        if (ContainsAny(normalized, "\\ui\\", "\\qml\\", "\\widgets\\", "mainwindow", ".xaml", ".qml"))
        {
            return ("ui", 0.8, "Path markers indicate presentation-facing code.");
        }

        if (ContainsAny(normalized, "\\runtime\\", "\\dbg\\", "runner", "launch"))
        {
            return ("runtime", 0.7, "Path markers indicate runtime or execution surface.");
        }

        if (ContainsAny(normalized, "\\core\\", "registry", "manager", "scanner"))
        {
            return ("core", 0.7, "Path markers indicate central orchestration or registry code.");
        }

        return null;
    }

    private static string InferLayerNameForModule(string value, IReadOnlyList<WorkspaceEvidenceLayer> observedLayers)
    {
        foreach (var layer in observedLayers)
        {
            if (ContainsAny(value, layer.Name, layer.Root))
            {
                return layer.Name;
            }
        }

        return observedLayers.FirstOrDefault()?.Name ?? "root";
    }

    private static int GetModulePriority(WorkspaceEvidenceModule module)
    {
        if (string.Equals(module.LayerName, "root", StringComparison.OrdinalIgnoreCase))
        {
            return 0;
        }

        if (string.Equals(module.Role, "command-cluster", StringComparison.OrdinalIgnoreCase))
        {
            return 1;
        }

        if (string.Equals(module.Role, "low-level-cluster", StringComparison.OrdinalIgnoreCase))
        {
            return 2;
        }

        return 10;
    }

    private static string ClassifyDocumentKind(string relativePath, string previewText)
    {
        var normalized = relativePath.Replace('/', '\\');
        if (ContainsAny(normalized, "snapshot", "history", "changelog") ||
            ContainsAny(previewText, "snapshot", "historical", "legacy"))
        {
            return "snapshot_doc";
        }

        if (ContainsAny(normalized, "guide", "howto", "runbook", "procedure", "playbook") ||
            ContainsAny(previewText, "step ", "run ", "install ", "usage", "how to"))
        {
            return "procedural_doc";
        }

        if (IsBuildLikePath(normalized) ||
            CountMatchingMarkers(previewText, TechnicalNarrativeMarkers) >= 3)
        {
            return "technical_doc";
        }

        return "narrative_doc";
    }

    private static bool HasNonDocumentTechnicalOverlap(
        WorkspaceScanResult scanResult,
        IReadOnlyList<WorkspaceTechnicalPreviewInput> technicalEvidence,
        string relativePath,
        string previewText)
    {
        var nonDocCorpus = string.Join(
            "\n",
            technicalEvidence.Select(static item => $"{item.Category}\n{item.PreviewText}")
                .Concat(scanResult.RelevantFiles.Select(path => Path.GetRelativePath(scanResult.State.WorkspaceRoot, path).Replace('/', '\\'))));

        var overlapCount = TechnicalNarrativeMarkers.Count(marker =>
            previewText.Contains(marker, StringComparison.OrdinalIgnoreCase) &&
            nonDocCorpus.Contains(marker, StringComparison.OrdinalIgnoreCase));

        if (overlapCount >= 2)
        {
            return true;
        }

        var normalized = relativePath.Replace('/', '\\');
        return scanResult.RelevantFiles.Any(path =>
        {
            var relative = Path.GetRelativePath(scanResult.State.WorkspaceRoot, path).Replace('/', '\\');
            return !string.Equals(relative, normalized, StringComparison.OrdinalIgnoreCase) &&
                   TechnicalNarrativeMarkers.Any(marker =>
                       previewText.Contains(marker, StringComparison.OrdinalIgnoreCase) &&
                       relative.Contains(marker, StringComparison.OrdinalIgnoreCase));
        });
    }

    private static bool PatternSupportsSignal(string signalKey, string patternCode)
    {
        return signalKey switch
        {
            "structure.single_root" => string.Equals(patternCode, "single_root_layout", StringComparison.OrdinalIgnoreCase),
            "structure.multi_root" => string.Equals(patternCode, "multi_root_layout", StringComparison.OrdinalIgnoreCase),
            "structure.mixed_import" => string.Equals(patternCode, "mixed_binary_source_layout", StringComparison.OrdinalIgnoreCase),
            "structure.nested_payloads" => string.Equals(patternCode, "nested_payload_layout", StringComparison.OrdinalIgnoreCase) ||
                                           string.Equals(patternCode, "nested_git_project_layout", StringComparison.OrdinalIgnoreCase),
            "build.cmake" or "build.cargo" or "build.go_mod" or "build.dotnet" or "build.package_json" or "build.docker" or "build.make"
                => string.Equals(patternCode, "build_manifest_present", StringComparison.OrdinalIgnoreCase),
            "runtime.desktop" => string.Equals(patternCode, "desktop_bootstrap_pattern", StringComparison.OrdinalIgnoreCase),
            "runtime.web" => string.Equals(patternCode, "browser_surface_pattern", StringComparison.OrdinalIgnoreCase),
            "runtime.analysis" => string.Equals(patternCode, "analysis_tooling_pattern", StringComparison.OrdinalIgnoreCase),
            _ => false
        };
    }

    private static bool ObservationSupportsSignal(string signalKey, WorkspaceEvidenceObservation observation)
    {
        return signalKey switch
        {
            "structure.single_root" or "structure.multi_root" => string.Equals(observation.Kind, "source_root_detected", StringComparison.OrdinalIgnoreCase),
            "structure.nested_payloads" => string.Equals(observation.Kind, "structural_anomaly_detected", StringComparison.OrdinalIgnoreCase),
            "runtime.web" => string.Equals(observation.Kind, "technical_preview_detected", StringComparison.OrdinalIgnoreCase) &&
                             string.Equals(observation.Value, "web-build", StringComparison.OrdinalIgnoreCase),
            "runtime.cli" or "runtime.service" => string.Equals(observation.Kind, "entry_candidate_detected", StringComparison.OrdinalIgnoreCase),
            _ => false
        };
    }

    private static bool EntryPointSupportsSignal(string signalKey, WorkspaceEvidenceEntryPoint entryPoint)
    {
        return signalKey switch
        {
            "runtime.cli" => string.Equals(entryPoint.Role, "cli", StringComparison.OrdinalIgnoreCase),
            "runtime.service" => string.Equals(entryPoint.Role, "service", StringComparison.OrdinalIgnoreCase),
            "stage.entry_present" => true,
            _ => false
        };
    }

    private static bool FileRoleSupportsSignal(string signalKey, WorkspaceEvidenceFileRole fileRole)
    {
        return signalKey switch
        {
            "runtime.desktop" or "behavior.ui_rendering" => string.Equals(fileRole.Role, "ui", StringComparison.OrdinalIgnoreCase),
            "runtime.web" => string.Equals(fileRole.Role, "asset", StringComparison.OrdinalIgnoreCase) &&
                             ContainsAny(fileRole.RelativePath, "\\public\\", ".html", ".tsx", ".jsx", ".vue"),
            "runtime.service" or "behavior.network" => ContainsAny(fileRole.RelativePath, "\\service\\", "\\server\\", "\\api\\", "\\http\\"),
            _ => false
        };
    }

    private static WorkspaceEvidenceCodeEdge[] BuildCodeEdges(WorkspaceScanResult scanResult)
    {
        var workspaceRoot = scanResult.State.WorkspaceRoot;
        var relevantMap = scanResult.RelevantFiles
            .Select(path => Path.GetRelativePath(workspaceRoot, path).Replace('/', '\\'))
            .ToDictionary(static path => path, static path => path, StringComparer.OrdinalIgnoreCase);
        var edges = new List<WorkspaceEvidenceCodeEdge>();

        foreach (var file in scanResult.RelevantFiles.Take(160))
        {
            var relativePath = Path.GetRelativePath(workspaceRoot, file).Replace('/', '\\');
            if (!IsCodeLikePath(relativePath))
            {
                continue;
            }

            foreach (var line in ReadBoundedLines(file, 160))
            {
                foreach (var reference in ExtractCodeReferences(relativePath, line))
                {
                    if (!TryResolveCodeReference(relativePath, reference, relevantMap.Keys, out var targetPath))
                    {
                        continue;
                    }

                    edges.Add(new WorkspaceEvidenceCodeEdge(
                        relativePath,
                        targetPath,
                        reference.Kind,
                        $"Observed {reference.Kind} reference in '{relativePath}'."));
                }
            }
        }

        return edges
            .GroupBy(static edge => $"{edge.FromPath}|{edge.ToPath}|{edge.Kind}", StringComparer.OrdinalIgnoreCase)
            .Select(static group => group.First())
            .OrderBy(static edge => edge.FromPath, StringComparer.OrdinalIgnoreCase)
            .ThenBy(static edge => edge.ToPath, StringComparer.OrdinalIgnoreCase)
            .Take(48)
            .ToArray();
    }

    private static WorkspaceEvidenceSignatureHint[] BuildSignatureHints(WorkspaceScanResult scanResult)
    {
        var workspaceRoot = scanResult.State.WorkspaceRoot;
        var hints = new List<WorkspaceEvidenceSignatureHint>();

        foreach (var file in scanResult.RelevantFiles.Take(160))
        {
            var relativePath = Path.GetRelativePath(workspaceRoot, file).Replace('/', '\\');
            if (!IsCodeLikePath(relativePath))
            {
                continue;
            }

            foreach (var line in ReadBoundedLines(file, 200))
            {
                var trimmed = line.Trim();
                if (TryExtractSignature(relativePath, trimmed, out var kind, out var signature))
                {
                    hints.Add(new WorkspaceEvidenceSignatureHint(relativePath, kind, signature, $"Observed cheap {kind} signature in '{relativePath}'."));
                }
            }
        }

        return hints
            .GroupBy(static hint => $"{hint.RelativePath}|{hint.Kind}|{hint.Signature}", StringComparer.OrdinalIgnoreCase)
            .Select(static group => group.First())
            .OrderBy(static hint => hint.RelativePath, StringComparer.OrdinalIgnoreCase)
            .ThenBy(static hint => hint.Signature, StringComparer.OrdinalIgnoreCase)
            .Take(64)
            .ToArray();
    }

    private static WorkspaceEvidenceSymbolHint[] BuildSymbolHints(WorkspaceScanResult scanResult)
    {
        var workspaceRoot = scanResult.State.WorkspaceRoot;
        var hints = new List<WorkspaceEvidenceSymbolHint>();

        foreach (var file in scanResult.RelevantFiles.Take(48))
        {
            var relativePath = Path.GetRelativePath(workspaceRoot, file).Replace('/', '\\');
            if (!IsBinaryLikePath(relativePath))
            {
                continue;
            }

            var strings = ExtractPrintableStrings(file, 3, 10);
            foreach (var symbol in strings)
            {
                hints.Add(new WorkspaceEvidenceSymbolHint(relativePath, symbol, "binary-string", $"Observed bounded printable string in '{relativePath}'."));
            }
        }

        return hints
            .GroupBy(static hint => $"{hint.RelativePath}|{hint.Symbol}", StringComparer.OrdinalIgnoreCase)
            .Select(static group => group.First())
            .Take(48)
            .ToArray();
    }

    private static WorkspaceEvidenceDependencySurfaceItem[] BuildDependencySurface(WorkspaceScanResult scanResult)
    {
        var workspaceRoot = scanResult.State.WorkspaceRoot;
        var items = new List<WorkspaceEvidenceDependencySurfaceItem>();

        foreach (var file in scanResult.RelevantFiles.Take(200))
        {
            var relativePath = Path.GetRelativePath(workspaceRoot, file).Replace('/', '\\');
            var fileName = Path.GetFileName(relativePath);
            if (!IsDependencyManifest(fileName))
            {
                continue;
            }

            foreach (var item in ExtractDependencies(file, relativePath))
            {
                items.Add(item);
            }
        }

        return items
            .GroupBy(static item => $"{item.Name}|{item.SourcePath}|{item.Scope}|{item.Kind}", StringComparer.OrdinalIgnoreCase)
            .Select(static group => group.First())
            .OrderBy(static item => item.SourcePath, StringComparer.OrdinalIgnoreCase)
            .ThenBy(static item => item.Name, StringComparer.OrdinalIgnoreCase)
            .Take(96)
            .ToArray();
    }

    private static WorkspaceEvidenceConfidenceAnnotation[] BuildConfidenceAnnotations(
        IReadOnlyList<WorkspaceEvidenceSignalScore> signalScores,
        WorkspaceEvidenceCandidates candidates,
        IReadOnlyList<WorkspaceEvidenceDependencyEdge> edges,
        IReadOnlyList<WorkspaceEvidenceCodeEdge> codeEdges,
        IReadOnlyList<WorkspaceEvidenceDependencySurfaceItem> dependencySurface,
        IReadOnlyList<WorkspaceMaterialPreviewInput> materials)
    {
        var annotations = new List<WorkspaceEvidenceConfidenceAnnotation>();

        foreach (var score in signalScores.Take(24))
        {
            annotations.Add(new WorkspaceEvidenceConfidenceAnnotation(
                "signal",
                score.Signal,
                ToConfidence(score.Score),
                $"Derived from cold signal score {score.Score:0.00}."));
        }

        foreach (var entry in candidates.EntryPoints.Take(12))
        {
            var support = 0.25;
            if (ContainsAny(entry.Role, "main", "cli", "service", "entry"))
            {
                support += 0.20;
            }

            if (edges.Any(edge => string.Equals(edge.From, entry.RelativePath, StringComparison.OrdinalIgnoreCase)))
            {
                support += 0.25;
            }

            if (codeEdges.Any(edge => string.Equals(edge.FromPath, entry.RelativePath, StringComparison.OrdinalIgnoreCase)))
            {
                support += 0.20;
            }

            annotations.Add(new WorkspaceEvidenceConfidenceAnnotation(
                "entry_point",
                entry.RelativePath,
                ToConfidence(support),
                "Derived from entry role plus edge/code support."));
        }

        foreach (var module in candidates.ModuleCandidates.Take(16))
        {
            var support = 0.20;
            if (!string.Equals(module.LayerName, "root", StringComparison.OrdinalIgnoreCase))
            {
                support += 0.15;
            }

            if (edges.Any(edge => string.Equals(edge.From, module.Name, StringComparison.OrdinalIgnoreCase) ||
                                  string.Equals(edge.To, module.Name, StringComparison.OrdinalIgnoreCase)))
            {
                support += 0.25;
            }

            if (codeEdges.Any(edge => ContainsAny(edge.FromPath, module.Name) || ContainsAny(edge.ToPath, module.Name)))
            {
                support += 0.15;
            }

            annotations.Add(new WorkspaceEvidenceConfidenceAnnotation(
                "module",
                module.Name,
                ToConfidence(support),
                "Derived from structural module bucket plus edge overlap."));
        }

        foreach (var dependency in dependencySurface.Take(32))
        {
            annotations.Add(new WorkspaceEvidenceConfidenceAnnotation(
                "dependency",
                $"{dependency.SourcePath}:{dependency.Name}",
                dependency.Scope is "direct" or "linked-library" ? WorkspaceEvidenceConfidenceLevel.Confirmed : WorkspaceEvidenceConfidenceLevel.Likely,
                "Derived from manifest/build dependency surface extraction."));
        }

        foreach (var material in materials.Take(24))
        {
            var confidence = CountMatchingMarkers(material.PreviewText, TechnicalNarrativeMarkers) >= 3
                ? WorkspaceEvidenceConfidenceLevel.Likely
                : WorkspaceEvidenceConfidenceLevel.Unknown;
            annotations.Add(new WorkspaceEvidenceConfidenceAnnotation(
                "material",
                material.RelativePath,
                confidence,
                "Derived from bounded preview density and overlap policy."));
        }

        return annotations
            .GroupBy(static item => $"{item.TargetKind}|{item.TargetId}", StringComparer.OrdinalIgnoreCase)
            .Select(static group => group.OrderByDescending(static item => item.Confidence).First())
            .ToArray();
    }

    private static bool IsCodeLikePath(string relativePath)
    {
        var extension = Path.GetExtension(relativePath);
        return string.Equals(extension, ".c", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(extension, ".cc", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(extension, ".cpp", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(extension, ".h", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(extension, ".hpp", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(extension, ".cs", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(extension, ".js", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(extension, ".jsx", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(extension, ".ts", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(extension, ".tsx", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(extension, ".go", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(extension, ".py", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(extension, ".rs", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsBinaryLikePath(string relativePath)
    {
        var extension = Path.GetExtension(relativePath);
        return string.Equals(extension, ".exe", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(extension, ".dll", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(extension, ".so", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(extension, ".dylib", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(extension, ".bin", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(extension, ".lib", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(extension, ".a", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsDependencyManifest(string fileName)
    {
        return string.Equals(fileName, "package.json", StringComparison.OrdinalIgnoreCase) ||
               fileName.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(fileName, "CMakeLists.txt", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(fileName, "requirements.txt", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(fileName, "pyproject.toml", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(fileName, "Cargo.toml", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(fileName, "go.mod", StringComparison.OrdinalIgnoreCase);
    }

    private static IEnumerable<string> ReadBoundedLines(string path, int maxLines)
    {
        try
        {
            return File.ReadLines(path).Take(maxLines).ToArray();
        }
        catch (IOException)
        {
            return Array.Empty<string>();
        }
        catch (UnauthorizedAccessException)
        {
            return Array.Empty<string>();
        }
        catch (DecoderFallbackException)
        {
            return Array.Empty<string>();
        }
    }

    private static IEnumerable<(string Kind, string Reference)> ExtractCodeReferences(string relativePath, string line)
    {
        if (string.IsNullOrWhiteSpace(line))
        {
            yield break;
        }

        if (TryExtractBetween(line, "#include \"", "\"", out var includeRef))
        {
            yield return ("include", includeRef);
        }

        if (TryExtractBetween(line, "import ", " from ", out var importName) && importName.Length > 0)
        {
            yield return ("import", importName.Replace("{", string.Empty).Replace("}", string.Empty).Trim());
        }

        if (TryExtractBetween(line, "from ", " import ", out var fromImport) && fromImport.Length > 0)
        {
            yield return ("python-import", fromImport.Trim());
        }

        if (TryExtractBetween(line, "require(\"", "\")", out var requireRef))
        {
            yield return ("require", requireRef);
        }

        if (relativePath.EndsWith(".go", StringComparison.OrdinalIgnoreCase) &&
            TryExtractBetween(line, "\"", "\"", out var goImport) &&
            goImport.Contains('/', StringComparison.Ordinal))
        {
            yield return ("go-import", goImport);
        }

        if (TryExtractAfterPrefix(line.TrimStart(), "mod ", out var rustMod))
        {
            yield return ("rust-mod", rustMod.TrimEnd(';'));
        }

        if (TryExtractAfterPrefix(line.TrimStart(), "use ", out var rustUse))
        {
            yield return ("rust-use", rustUse.TrimEnd(';'));
        }

        if (relativePath.EndsWith(".cs", StringComparison.OrdinalIgnoreCase) &&
            TryExtractAfterPrefix(line.TrimStart(), "using ", out var csUsing))
        {
            yield return ("csharp-using", csUsing.TrimEnd(';'));
        }
    }

    private static bool TryResolveCodeReference(
        string relativePath,
        (string Kind, string Reference) reference,
        IEnumerable<string> relevantPaths,
        out string targetPath)
    {
        targetPath = string.Empty;
        var normalizedReference = reference.Reference.Replace('/', '\\').Trim();
        if (normalizedReference.Length == 0)
        {
            return false;
        }

        var candidates = new List<string>();
        var directory = Path.GetDirectoryName(relativePath)?.Replace('/', '\\') ?? string.Empty;

        if (reference.Kind is "include" or "import" or "require")
        {
            candidates.Add(Path.GetFullPath(Path.Combine("C:\\", "tmp"))); // placeholder to avoid empty; removed below
        }

        candidates.Clear();
        if (reference.Kind == "include")
        {
            candidates.Add(NormalizeRelativeCandidate(Path.Combine(directory, normalizedReference)));
            candidates.Add(NormalizeRelativeCandidate(normalizedReference));
        }
        else if (reference.Kind is "import" or "require")
        {
            candidates.Add(NormalizeRelativeCandidate(Path.Combine(directory, normalizedReference + ".ts")));
            candidates.Add(NormalizeRelativeCandidate(Path.Combine(directory, normalizedReference + ".tsx")));
            candidates.Add(NormalizeRelativeCandidate(Path.Combine(directory, normalizedReference + ".js")));
            candidates.Add(NormalizeRelativeCandidate(Path.Combine(directory, normalizedReference + ".jsx")));
            candidates.Add(NormalizeRelativeCandidate(Path.Combine(directory, normalizedReference, "index.ts")));
            candidates.Add(NormalizeRelativeCandidate(Path.Combine(directory, normalizedReference, "index.js")));
        }
        else if (reference.Kind == "python-import")
        {
            candidates.Add(NormalizeRelativeCandidate(normalizedReference.Replace('.', '\\') + ".py"));
            candidates.Add(NormalizeRelativeCandidate(Path.Combine(normalizedReference.Replace('.', '\\'), "__init__.py")));
        }
        else if (reference.Kind == "go-import")
        {
            var goPath = normalizedReference.Replace('/', '\\');
            var goSegments = goPath.Split('\\', StringSplitOptions.RemoveEmptyEntries);
            if (goSegments.Length > 0)
            {
                var last = goSegments[^1];
                candidates.Add(NormalizeRelativeCandidate(Path.Combine(last, $"{last}.go")));
                candidates.Add(NormalizeRelativeCandidate(Path.Combine(last, "main.go")));
                candidates.Add(NormalizeRelativeCandidate(Path.Combine("src", last, $"{last}.go")));
                candidates.Add(NormalizeRelativeCandidate(Path.Combine("src", last, "main.go")));
                if (goSegments.Length > 1)
                {
                    var suffix = Path.Combine(goSegments[^2], goSegments[^1]);
                    candidates.Add(NormalizeRelativeCandidate(Path.Combine(suffix, $"{last}.go")));
                    candidates.Add(NormalizeRelativeCandidate(Path.Combine(suffix, "main.go")));
                    candidates.Add(NormalizeRelativeCandidate(Path.Combine("src", suffix, $"{last}.go")));
                    candidates.Add(NormalizeRelativeCandidate(Path.Combine("src", suffix, "main.go")));
                }

                for (var count = goSegments.Length; count >= 1; count--)
                {
                    var suffix = Path.Combine(goSegments.Skip(goSegments.Length - count).ToArray());
                    candidates.Add(NormalizeRelativeCandidate(Path.Combine(suffix, "main.go")));
                    candidates.Add(NormalizeRelativeCandidate(Path.Combine(suffix, $"{goSegments[^1]}.go")));
                    candidates.Add(NormalizeRelativeCandidate(Path.Combine("src", suffix, "main.go")));
                    candidates.Add(NormalizeRelativeCandidate(Path.Combine("src", suffix, $"{goSegments[^1]}.go")));
                }
            }
        }
        else if (reference.Kind is "rust-mod" or "rust-use")
        {
            var rustPath = normalizedReference.Replace("crate::", string.Empty).Replace("self::", string.Empty).Replace("super::", string.Empty).Replace("::", "\\");
            candidates.Add(NormalizeRelativeCandidate(Path.Combine(directory, rustPath + ".rs")));
            candidates.Add(NormalizeRelativeCandidate(Path.Combine(directory, rustPath, "mod.rs")));
            var rustSegments = rustPath.Split('\\', StringSplitOptions.RemoveEmptyEntries);
            for (var count = rustSegments.Length; count >= 1; count--)
            {
                var suffix = Path.Combine(rustSegments.Take(count).ToArray());
                candidates.Add(NormalizeRelativeCandidate(Path.Combine("src", suffix + ".rs")));
                candidates.Add(NormalizeRelativeCandidate(Path.Combine("src", suffix, "mod.rs")));
                candidates.Add(NormalizeRelativeCandidate(suffix + ".rs"));
                candidates.Add(NormalizeRelativeCandidate(Path.Combine(suffix, "mod.rs")));
            }
        }
        else if (reference.Kind == "csharp-using")
        {
            var namespacePath = normalizedReference.Replace("global::", string.Empty).Replace('.', '\\');
            if (!string.IsNullOrWhiteSpace(namespacePath))
            {
                candidates.Add(NormalizeRelativeCandidate(namespacePath + ".cs"));
                candidates.Add(NormalizeRelativeCandidate(Path.Combine(namespacePath, "Program.cs")));
                candidates.Add(NormalizeRelativeCandidate(Path.Combine(namespacePath, "MainWindow.xaml.cs")));
            }
        }

        var matched = relevantPaths.FirstOrDefault(path =>
            candidates.Contains(path, StringComparer.OrdinalIgnoreCase) ||
            candidates.Any(candidate =>
                path.EndsWith(candidate, StringComparison.OrdinalIgnoreCase) ||
                (!string.IsNullOrWhiteSpace(Path.GetFileNameWithoutExtension(candidate)) &&
                 path.Contains("\\" + Path.GetFileNameWithoutExtension(candidate) + "\\", StringComparison.OrdinalIgnoreCase))) ||
            path.EndsWith(normalizedReference, StringComparison.OrdinalIgnoreCase));
        if (matched is null)
        {
            return false;
        }

        targetPath = matched;
        return true;
    }

    private static bool TryExtractSignature(string relativePath, string line, out string kind, out string signature)
    {
        kind = string.Empty;
        signature = string.Empty;
        if (line.Length == 0)
        {
            return false;
        }

        if (ContainsAny(relativePath, ".c", ".cc", ".cpp", ".h", ".hpp", ".cs", ".rs") && line.Contains(" main(", StringComparison.Ordinal))
        {
            kind = "bootstrap";
            signature = line;
            return true;
        }

        if (line.StartsWith("class ", StringComparison.Ordinal) ||
            line.StartsWith("struct ", StringComparison.Ordinal) ||
            line.StartsWith("interface ", StringComparison.Ordinal) ||
            line.Contains(" class ", StringComparison.Ordinal))
        {
            kind = "type";
            signature = line;
            return true;
        }

        if ((line.Contains('(') && line.Contains(')') && (line.EndsWith("{", StringComparison.Ordinal) || line.EndsWith(";", StringComparison.Ordinal))) &&
            !line.StartsWith("if ", StringComparison.OrdinalIgnoreCase) &&
            !line.StartsWith("for ", StringComparison.OrdinalIgnoreCase) &&
            !line.StartsWith("while ", StringComparison.OrdinalIgnoreCase) &&
            !line.StartsWith("switch ", StringComparison.OrdinalIgnoreCase))
        {
            kind = "function";
            signature = line;
            return true;
        }

        return false;
    }

    private static bool IsServiceEntryCandidate(string relativePath)
    {
        return ContainsAny(relativePath, "\\server\\", "\\service\\", "\\api\\", "\\http\\");
    }

    private static bool IsTestLikeServiceEntry(string relativePath)
    {
        var normalized = relativePath.Replace('/', '\\');
        return ContainsAny(normalized, "\\test\\", "\\tests\\", "\\examples\\", "\\samples\\") ||
               normalized.EndsWith("\\cmd\\testserver\\main.go", StringComparison.OrdinalIgnoreCase) ||
               normalized.Contains("\\testserver\\cmd\\testserver\\", StringComparison.OrdinalIgnoreCase);
    }

    private static WorkspaceEvidenceDependencySurfaceItem[] ExtractDependencies(string fullPath, string relativePath)
    {
        var lines = ReadBoundedLines(fullPath, 240).ToArray();
        var items = new List<WorkspaceEvidenceDependencySurfaceItem>();
        var fileName = Path.GetFileName(relativePath);
        string? packageJsonSection = null;
        string? cargoSection = null;
        var insidePyProjectDependencies = false;

        foreach (var line in lines)
        {
            var trimmed = line.Trim();
            if (trimmed.Length == 0 || trimmed.StartsWith('#') || trimmed.StartsWith("//", StringComparison.Ordinal))
            {
                continue;
            }

            if (string.Equals(fileName, "package.json", StringComparison.OrdinalIgnoreCase))
            {
                var packageSection = TryMatchPackageJsonDependencySection(trimmed);
                if (packageSection is not null)
                {
                    packageJsonSection = packageSection;
                    continue;
                }

                if (packageJsonSection is not null && (trimmed.StartsWith("}", StringComparison.Ordinal) || trimmed.StartsWith("},", StringComparison.Ordinal)))
                {
                    packageJsonSection = null;
                    continue;
                }

                if (packageJsonSection is not null && TryExtractJsonPropertyName(trimmed, out var depName))
                {
                    items.Add(new WorkspaceEvidenceDependencySurfaceItem(depName, relativePath, packageJsonSection, "package"));
                }
            }
            else if (fileName.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase))
            {
                if (TryExtractBetween(trimmed, "PackageReference Include=\"", "\"", out var packageRef))
                {
                    items.Add(new WorkspaceEvidenceDependencySurfaceItem(packageRef, relativePath, "direct", "package"));
                }
            }
            else if (string.Equals(fileName, "CMakeLists.txt", StringComparison.OrdinalIgnoreCase))
            {
                if (TryExtractBetween(trimmed, "find_package(", " ", out var findPackage))
                {
                    items.Add(new WorkspaceEvidenceDependencySurfaceItem(findPackage, relativePath, "direct", "package"));
                }
                else if (TryExtractBetween(trimmed, "target_link_libraries(", ")", out var targetLibs))
                {
                    foreach (var token in targetLibs.Split(' ', StringSplitOptions.RemoveEmptyEntries).Skip(1).Take(4))
                    {
                        items.Add(new WorkspaceEvidenceDependencySurfaceItem(token.Trim(), relativePath, "linked-library", "library"));
                    }
                }
            }
            else if (string.Equals(fileName, "requirements.txt", StringComparison.OrdinalIgnoreCase))
            {
                var dep = trimmed.Split(new[] { "==", ">=", "<=", "~=", "[" }, StringSplitOptions.None)[0].Trim();
                if (dep.Length > 0)
                {
                    items.Add(new WorkspaceEvidenceDependencySurfaceItem(dep, relativePath, "direct", "package"));
                }
            }
            else if (string.Equals(fileName, "pyproject.toml", StringComparison.OrdinalIgnoreCase))
            {
                if (trimmed.StartsWith("[", StringComparison.Ordinal))
                {
                    insidePyProjectDependencies = string.Equals(trimmed, "[project]", StringComparison.OrdinalIgnoreCase);
                    continue;
                }

                if (insidePyProjectDependencies && trimmed.StartsWith("dependencies", StringComparison.OrdinalIgnoreCase))
                {
                    insidePyProjectDependencies = true;
                    continue;
                }

                if (insidePyProjectDependencies && trimmed.StartsWith("\"", StringComparison.Ordinal) && TryExtractBetween(trimmed, "\"", "\"", out var pyDep))
                {
                    items.Add(new WorkspaceEvidenceDependencySurfaceItem(pyDep, relativePath, "direct", "package"));
                }
            }
            else if (string.Equals(fileName, "Cargo.toml", StringComparison.OrdinalIgnoreCase))
            {
                if (trimmed.StartsWith("[", StringComparison.Ordinal) && trimmed.EndsWith("]", StringComparison.Ordinal))
                {
                    cargoSection = NormalizeCargoDependencySection(trimmed);
                    continue;
                }

                if (cargoSection is not null && trimmed.Contains("=", StringComparison.Ordinal) && !trimmed.StartsWith("[", StringComparison.Ordinal))
                {
                    var depName = trimmed.Split('=', 2)[0].Trim();
                    if (!string.IsNullOrWhiteSpace(depName) && !IsCargoDependencyProperty(depName))
                    {
                        items.Add(new WorkspaceEvidenceDependencySurfaceItem(depName, relativePath, cargoSection, "crate"));
                    }
                }
            }
            else if (string.Equals(fileName, "go.mod", StringComparison.OrdinalIgnoreCase) && trimmed.StartsWith("require ", StringComparison.Ordinal))
            {
                var dep = trimmed["require ".Length..].Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
                if (!string.IsNullOrWhiteSpace(dep))
                {
                    items.Add(new WorkspaceEvidenceDependencySurfaceItem(dep, relativePath, "direct", "module"));
                }
            }
        }

        return items
            .GroupBy(static item => $"{item.Name}|{item.SourcePath}|{item.Scope}|{item.Kind}", StringComparer.OrdinalIgnoreCase)
            .Select(static group => group.First())
            .Take(24)
            .ToArray();
    }

    private static string? TryMatchPackageJsonDependencySection(string trimmed)
    {
        if (trimmed.StartsWith("\"dependencies\"", StringComparison.Ordinal))
        {
            return "direct";
        }

        if (trimmed.StartsWith("\"devDependencies\"", StringComparison.Ordinal))
        {
            return "dev";
        }

        if (trimmed.StartsWith("\"peerDependencies\"", StringComparison.Ordinal))
        {
            return "peer";
        }

        if (trimmed.StartsWith("\"optionalDependencies\"", StringComparison.Ordinal))
        {
            return "optional";
        }

        return null;
    }

    private static bool TryExtractJsonPropertyName(string trimmed, out string propertyName)
    {
        propertyName = string.Empty;
        if (!trimmed.StartsWith("\"", StringComparison.Ordinal))
        {
            return false;
        }

        var endQuote = trimmed.IndexOf('"', 1);
        if (endQuote <= 1)
        {
            return false;
        }

        if (trimmed.Length <= endQuote + 1 || trimmed[endQuote + 1] != ':')
        {
            return false;
        }

        propertyName = trimmed[1..endQuote];
        return !string.IsNullOrWhiteSpace(propertyName);
    }

    private static string? NormalizeCargoDependencySection(string trimmed)
    {
        var section = trimmed.Trim('[', ']', ' ');
        if (string.Equals(section, "dependencies", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(section, "workspace.dependencies", StringComparison.OrdinalIgnoreCase) ||
            section.EndsWith(".dependencies", StringComparison.OrdinalIgnoreCase))
        {
            return "direct";
        }

        if (string.Equals(section, "dev-dependencies", StringComparison.OrdinalIgnoreCase) ||
            section.EndsWith(".dev-dependencies", StringComparison.OrdinalIgnoreCase))
        {
            return "dev";
        }

        if (string.Equals(section, "build-dependencies", StringComparison.OrdinalIgnoreCase) ||
            section.EndsWith(".build-dependencies", StringComparison.OrdinalIgnoreCase))
        {
            return "build";
        }

        return null;
    }

    private static bool IsCargoDependencyProperty(string name)
    {
        return string.Equals(name, "version", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(name, "path", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(name, "features", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(name, "default-features", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(name, "optional", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(name, "workspace", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(name, "package", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(name, "git", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(name, "branch", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(name, "tag", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(name, "rev", StringComparison.OrdinalIgnoreCase);
    }

    private static string[] ExtractPrintableStrings(string fullPath, int minLength, int maxCount)
    {
        try
        {
            using var stream = File.OpenRead(fullPath);
            var buffer = new byte[Math.Min(65536, (int)Math.Min(stream.Length, 65536))];
            var bytesRead = stream.Read(buffer, 0, buffer.Length);
            var strings = new List<string>();
            var current = new List<char>();
            for (var index = 0; index < bytesRead; index++)
            {
                var value = buffer[index];
                if (value >= 32 && value <= 126)
                {
                    current.Add((char)value);
                    continue;
                }

                if (current.Count >= minLength)
                {
                    strings.Add(new string(current.ToArray()));
                    if (strings.Count >= maxCount)
                    {
                        break;
                    }
                }

                current.Clear();
            }

            if (current.Count >= minLength && strings.Count < maxCount)
            {
                strings.Add(new string(current.ToArray()));
            }

            return strings
                .Where(static item => !string.IsNullOrWhiteSpace(item))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Take(maxCount)
                .ToArray();
        }
        catch (IOException)
        {
            return Array.Empty<string>();
        }
        catch (UnauthorizedAccessException)
        {
            return Array.Empty<string>();
        }
    }

    private static WorkspaceEvidenceConfidenceLevel ToConfidence(double score)
    {
        if (score >= 0.75)
        {
            return WorkspaceEvidenceConfidenceLevel.Confirmed;
        }

        if (score >= 0.45)
        {
            return WorkspaceEvidenceConfidenceLevel.Likely;
        }

        return WorkspaceEvidenceConfidenceLevel.Unknown;
    }

    private static bool TryExtractBetween(string value, string prefix, string suffix, out string extracted)
    {
        extracted = string.Empty;
        var start = value.IndexOf(prefix, StringComparison.OrdinalIgnoreCase);
        if (start < 0)
        {
            return false;
        }

        start += prefix.Length;
        var end = value.IndexOf(suffix, start, StringComparison.OrdinalIgnoreCase);
        if (end < 0 || end <= start)
        {
            return false;
        }

        extracted = value[start..end].Trim();
        return extracted.Length > 0;
    }

    private static bool TryExtractAfterPrefix(string value, string prefix, out string extracted)
    {
        extracted = string.Empty;
        if (!value.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        extracted = value[prefix.Length..].Trim();
        return extracted.Length > 0;
    }

    private static string NormalizeRelativeCandidate(string value)
    {
        return value.Replace('/', '\\').TrimStart('\\');
    }

    private static void AddEdgeIfBothSeen(
        List<WorkspaceEvidenceDependencyEdge> target,
        IEnumerable<string> knownNames,
        string from,
        string to,
        string label,
        string reason,
        IReadOnlyList<WorkspaceEvidenceSignal>? signals = null,
        string? requiredCategory = null,
        params string[] requiredCodes)
    {
        var names = knownNames.ToArray();
        if (!names.Contains(from, StringComparer.OrdinalIgnoreCase) || !names.Contains(to, StringComparer.OrdinalIgnoreCase))
        {
            return;
        }

        if (!HasRequiredSignal(signals, requiredCategory, requiredCodes))
        {
            return;
        }

        target.Add(new WorkspaceEvidenceDependencyEdge(from, to, label, reason));
    }

    private static bool HasRequiredSignal(
        IReadOnlyList<WorkspaceEvidenceSignal>? signals,
        string? requiredCategory,
        IReadOnlyList<string> requiredCodes)
    {
        if (signals is null || string.IsNullOrWhiteSpace(requiredCategory) || requiredCodes.Count == 0)
        {
            return true;
        }

        return signals.Any(signal =>
            string.Equals(signal.Category, requiredCategory, StringComparison.OrdinalIgnoreCase) &&
            requiredCodes.Contains(signal.Code, StringComparer.OrdinalIgnoreCase));
    }

    private static int CountMatchingMarkers(string value, params string[] markers)
    {
        var count = 0;
        foreach (var marker in markers.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (value.Contains(marker, StringComparison.OrdinalIgnoreCase))
            {
                count++;
            }
        }

        return count;
    }

    private static string? FindRelevantFile(IEnumerable<string> relevantFiles, string marker)
    {
        return relevantFiles.FirstOrDefault(path => path.Contains(marker.Replace('_', '.'), StringComparison.OrdinalIgnoreCase)) ??
               relevantFiles.FirstOrDefault(path => path.Contains(marker, StringComparison.OrdinalIgnoreCase));
    }

    private static void AddPatternIf(
        bool condition,
        List<WorkspaceEvidencePattern> target,
        string code,
        string reason,
        params string[] evidencePaths)
    {
        if (!condition)
        {
            return;
        }

        target.Add(new WorkspaceEvidencePattern(
            code,
            reason,
            evidencePaths
                .Where(static path => !string.IsNullOrWhiteSpace(path))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray()));
    }

    private static bool IsBuildLikePath(string path)
    {
        var fileName = Path.GetFileName(path);
        return string.Equals(fileName, "CMakeLists.txt", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(fileName, "CMakePresets.json", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(fileName, "CMakeCache.txt", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(fileName, "Cargo.toml", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(fileName, "go.mod", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(fileName, "package.json", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(fileName, "Dockerfile", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(fileName, "Makefile", StringComparison.OrdinalIgnoreCase) ||
               fileName.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase) ||
               fileName.EndsWith(".sln", StringComparison.OrdinalIgnoreCase) ||
               fileName.EndsWith(".vcxproj", StringComparison.OrdinalIgnoreCase);
    }

    private static void AddIfAnyExtension(List<string> target, IEnumerable<string> extensions, string label, params string[] candidates)
    {
        if (extensions.Any(extension => candidates.Contains(extension, StringComparer.OrdinalIgnoreCase)))
        {
            target.Add(label);
        }
    }

    private static void AddIfAnyFile(List<string> target, IEnumerable<string> fileNames, string label, params string[] candidates)
    {
        if (fileNames.Any(fileName =>
                candidates.Any(candidate =>
                    string.Equals(fileName, candidate, StringComparison.OrdinalIgnoreCase) ||
                    fileName.EndsWith(candidate, StringComparison.OrdinalIgnoreCase))))
        {
            target.Add(label);
        }
    }

    private static void AddIfContains(List<string> target, string text, string label, params string[] candidates)
    {
        if (ContainsAny(text, candidates))
        {
            target.Add(label);
        }
    }

    private static bool ContainsAny(string value, params string[] candidates)
    {
        foreach (var candidate in candidates)
        {
            if (value.Contains(candidate, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }
}
