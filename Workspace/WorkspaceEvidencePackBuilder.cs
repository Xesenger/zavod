using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

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
        var scannerConfig = WorkspaceScannerConfig.Load(state.WorkspaceRoot);
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
        var fileIndex = BuildFileIndex(scanResult, fileRoles, scannerConfig);
        var hotspots = BuildHotspots(scanResult, materials, dependencyEdges);
        var projectUnits = BuildProjectUnits(scanResult, entryPoints, scannerConfig);
        var runProfiles = BuildRunProfiles(scanResult, projectUnits);
        var candidates = new WorkspaceEvidenceCandidates(entryPoints, modules, fileRoles, projectUnits, runProfiles);
        var signals = BuildSignals(scanResult, technicalEvidence, materials);
        var topology = BuildTopology(scanResult, fileIndex, candidates, signals, patterns);
        var signalScores = BuildSignalScores(signals, patterns, observations, entryPoints, fileRoles);
        var confidenceAnnotations = BuildConfidenceAnnotations(signalScores, candidates, dependencyEdges, codeEdges, dependencySurface, materials);

        return new WorkspaceEvidencePack(
            BuildScanRun(scanResult),
            WorkspaceEvidencePredicateRegistry.All,
            scanResult.BudgetReport,
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
                state.Summary.IgnoredNoiseFileCount,
                state.Summary.SourceRoots,
                state.Summary.BuildRoots,
                state.StructuralAnomalies.Select(static anomaly => $"{anomaly.Code}: {anomaly.Message}").ToArray()),
            topology,
            BuildTechnicalPassport(scanResult, technicalEvidence, materials),
            string.Empty,
            observations,
            patterns,
            signalScores,
            fileIndex,
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

    private static WorkspaceEvidenceProjectUnit[] BuildProjectUnits(
        WorkspaceScanResult scanResult,
        IReadOnlyList<WorkspaceEvidenceEntryPoint> entryPoints,
        WorkspaceScannerConfig scannerConfig)
    {
        var workspaceRoot = scanResult.State.WorkspaceRoot;
        var unitsByRoot = new Dictionary<string, UnitDraft>(StringComparer.OrdinalIgnoreCase);
        var cargoDefaultMembers = BuildCargoDefaultMemberRoots(scanResult);

        foreach (var file in scanResult.RelevantFiles)
        {
            var relativePath = Path.GetRelativePath(workspaceRoot, file).Replace('/', '\\');
            if (!TryClassifyUnitManifest(relativePath, out var rootPath, out var kind, out var evidence))
            {
                continue;
            }

            if (IsUnsupportedUnitRoot(rootPath) || scannerConfig.IsIgnoredUnit(rootPath))
            {
                continue;
            }

            var draft = GetOrCreateUnit(unitsByRoot, rootPath, kind, scanResult.BudgetReport?.IsPartial == true);
            draft.Manifests.Add(relativePath);
            draft.Evidence.Add(evidence);
            draft.Evidence.Add($"unit_zone:{ClassifyUnitZone(rootPath)}");
            AddManifestUnitEvidence(workspaceRoot, relativePath, draft.Evidence);
            if (scannerConfig.IsPrimaryUnit(rootPath))
            {
                draft.Evidence.Add("config_primary_unit");
                if (scannerConfig.ConfigPath is not null)
                {
                    draft.Evidence.Add($"config:{scannerConfig.ConfigPath}");
                }
            }

            if (scannerConfig.IsVendorUnit(rootPath))
            {
                draft.Evidence.Add("config_vendor_zone");
            }

            if (cargoDefaultMembers.Any(member => string.Equals(member, rootPath, StringComparison.OrdinalIgnoreCase)))
            {
                draft.Evidence.Add("cargo_default_member");
            }
        }

        foreach (var entryPoint in entryPoints)
        {
            var matched = unitsByRoot.Values
                .Where(unit => IsPathInsideUnit(entryPoint.RelativePath, unit.RootPath))
                .OrderByDescending(unit => UnitRootDepth(unit.RootPath))
                .FirstOrDefault();
            if (matched is null)
            {
                continue;
            }

            matched.EntryPoints.Add(entryPoint.RelativePath);
            matched.Evidence.Add($"entry:{entryPoint.RelativePath}");
            if (entryPoint.EvidenceMarker?.Confidence == WorkspaceEvidenceConfidenceLevel.Confirmed)
            {
                matched.Evidence.Add("confirmed_entry_overlap");
            }

            foreach (var entryEvidence in entryPoint.Evidence)
            {
                matched.Evidence.Add($"entry_evidence:{entryEvidence}");
            }
        }

        return unitsByRoot.Values
            .Where(static unit => unit.Manifests.Count > 0)
            .Select(static unit => unit.ToProjectUnit())
            .OrderByDescending(static unit => UnitScore(unit))
            .ThenBy(static unit => UnitRootDepth(unit.RootPath))
            .ThenBy(static unit => unit.RootPath, StringComparer.OrdinalIgnoreCase)
            .Take(24)
            .ToArray();
    }

    private static WorkspaceEvidenceRunProfile[] BuildRunProfiles(
        WorkspaceScanResult scanResult,
        IReadOnlyList<WorkspaceEvidenceProjectUnit> projectUnits)
    {
        var profiles = new List<WorkspaceEvidenceRunProfile>();
        foreach (var unit in projectUnits)
        {
            foreach (var manifest in unit.Manifests)
            {
                var fileName = Path.GetFileName(manifest);
                if (string.Equals(fileName, "Cargo.toml", StringComparison.OrdinalIgnoreCase))
                {
                    AddCargoRunProfiles(profiles, unit, manifest);
                }
                else if (string.Equals(fileName, "package.json", StringComparison.OrdinalIgnoreCase))
                {
                    AddPackageJsonRunProfiles(profiles, scanResult.State.WorkspaceRoot, unit, manifest);
                }
                else if (string.Equals(fileName, "go.mod", StringComparison.OrdinalIgnoreCase))
                {
                    AddGoRunProfiles(profiles, unit, manifest);
                }
                else if (string.Equals(fileName, "CMakeLists.txt", StringComparison.OrdinalIgnoreCase))
                {
                    AddCMakeRunProfiles(profiles, unit, manifest);
                }
                else if (Path.GetExtension(fileName).Equals(".csproj", StringComparison.OrdinalIgnoreCase))
                {
                    AddDotnetRunProfiles(profiles, unit, manifest);
                }
                else if (IsPythonManifest(fileName))
                {
                    AddPythonRunProfiles(profiles, scanResult.State.WorkspaceRoot, unit, manifest);
                }
            }
        }

        return profiles
            .GroupBy(static profile => $"{profile.Kind}|{profile.Command}|{profile.WorkingDirectory}", StringComparer.OrdinalIgnoreCase)
            .Select(static group => group.First())
            .OrderByDescending(static profile => RunProfileScore(profile))
            .ThenBy(static profile => profile.WorkingDirectory, StringComparer.OrdinalIgnoreCase)
            .ThenBy(static profile => profile.Kind, StringComparer.OrdinalIgnoreCase)
            .ThenBy(static profile => profile.Command, StringComparer.OrdinalIgnoreCase)
            .Take(48)
            .ToArray();
    }

    private static WorkspaceEvidenceTopology BuildTopology(
        WorkspaceScanResult scanResult,
        IReadOnlyList<WorkspaceEvidenceFileIndexItem> fileIndex,
        WorkspaceEvidenceCandidates candidates,
        IReadOnlyList<WorkspaceEvidenceSignal> signals,
        IReadOnlyList<WorkspaceEvidencePattern> patterns)
    {
        var zones = BuildTopologyZones(scanResult, fileIndex, candidates).ToArray();
        var releaseZones = zones
            .Where(static zone => zone.Role is "release-output" or "runtime-payload")
            .Select(static zone => zone.Root)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(static zone => zone, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var ignoredNoiseZones = zones
            .Where(static zone => zone.Role is "ignored-noise" or "generated" or "vendor")
            .Select(static zone => zone.Root)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(static zone => zone, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var activeSourceRoots = BuildLikelyActiveSourceRoots(scanResult, candidates, zones);
        var uncertaintyReasons = BuildTopologyUncertaintyReasons(scanResult, zones, activeSourceRoots).ToArray();
        var topologyKind = ClassifyTopologyKind(scanResult, fileIndex, zones, candidates, signals, patterns, activeSourceRoots);
        var safeImportMode = RecommendSafeImportMode(topologyKind, zones, uncertaintyReasons);
        var confidence = topologyKind is "SingleProject" or "ReleaseBundle" or "MaterialOnly"
            ? WorkspaceEvidenceConfidenceLevel.Confirmed
            : WorkspaceEvidenceConfidenceLevel.Likely;

        return new WorkspaceEvidenceTopology(
            topologyKind,
            safeImportMode,
            zones,
            activeSourceRoots,
            releaseZones,
            ignoredNoiseZones,
            uncertaintyReasons,
            EvidenceMarker(
                "topology_classification",
                scanResult.State.WorkspaceRoot,
                $"topology:{topologyKind}; safe_import:{safeImportMode}",
                confidence,
                scanResult.BudgetReport?.IsPartial == true,
                isBounded: false));
    }

    private static IEnumerable<WorkspaceEvidenceTopologyZone> BuildTopologyZones(
        WorkspaceScanResult scanResult,
        IReadOnlyList<WorkspaceEvidenceFileIndexItem> fileIndex,
        WorkspaceEvidenceCandidates candidates)
    {
        var zones = new List<WorkspaceEvidenceTopologyZone>();
        foreach (var group in fileIndex.GroupBy(static item => TopologyRoot(item.RelativePath), StringComparer.OrdinalIgnoreCase))
        {
            var items = group.ToArray();
            var role = ClassifyTopologyZoneRole(group.Key, items, candidates);
            var evidence = BuildTopologyZoneEvidence(group.Key, role, items, candidates);
            var confidence = role is "active-source" or "release-output" or "runtime-payload" or "generated" or "vendor"
                ? WorkspaceEvidenceConfidenceLevel.Confirmed
                : WorkspaceEvidenceConfidenceLevel.Likely;
            zones.Add(new WorkspaceEvidenceTopologyZone(
                group.Key,
                role,
                items.Length,
                evidence,
                confidence,
                EvidenceMarker(
                    "topology_zone",
                    group.Key,
                    string.Join("; ", evidence.Take(4)),
                    confidence,
                    scanResult.BudgetReport?.IsPartial == true,
                    isBounded: false)));
        }

        foreach (var ignoredRoot in scanResult.State.Summary.IgnoredNoiseRoots)
        {
            if (zones.Any(zone => string.Equals(zone.Root, ignoredRoot, StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            var role = IsReleaseOutputRoot(ignoredRoot)
                ? "release-output"
                : "ignored-noise";
            var evidence = role == "release-output"
                ? new[] { "scanner_ignored_release_output_root", "release_output_root_name" }
                : new[] { "scanner_ignored_noise_root" };

            zones.Add(new WorkspaceEvidenceTopologyZone(
                ignoredRoot,
                role,
                0,
                evidence,
                WorkspaceEvidenceConfidenceLevel.Confirmed,
                EvidenceMarker(
                    "topology_zone",
                    ignoredRoot,
                    string.Join("; ", evidence),
                    WorkspaceEvidenceConfidenceLevel.Confirmed,
                    scanResult.BudgetReport?.IsPartial == true,
                    isBounded: true)));
        }

        return zones
            .OrderBy(static zone => TopologyZoneRank(zone.Role))
            .ThenByDescending(static zone => zone.FileCount)
            .ThenBy(static zone => zone.Root, StringComparer.OrdinalIgnoreCase)
            .Take(32);
    }

    private static string ClassifyTopologyZoneRole(
        string root,
        IReadOnlyList<WorkspaceEvidenceFileIndexItem> items,
        WorkspaceEvidenceCandidates candidates)
    {
        if (items.Any(static item => item.Zone == "generated") || IsGeneratedRoot(root))
        {
            return "generated";
        }

        if (items.Any(static item => item.Zone == "vendor") || IsVendorRoot(root))
        {
            return "vendor";
        }

        if (IsReleaseOutputRoot(root))
        {
            return "release-output";
        }

        if (items.Any(static item => item.Zone == "binary") && !items.Any(IsSourceOrBuildIndexItem))
        {
            return "runtime-payload";
        }

        if (candidates.ProjectUnits.Any(unit => string.Equals(unit.RootPath, root, StringComparison.OrdinalIgnoreCase)) ||
            items.Any(IsSourceOrBuildIndexItem))
        {
            return "active-source";
        }

        if (items.All(static item => item.Zone == "document" || item.Zone == "material"))
        {
            return "documentation";
        }

        if (items.Any(static item => item.Zone == "asset"))
        {
            return "asset-payload";
        }

        return "context";
    }

    private static IReadOnlyList<string> BuildTopologyZoneEvidence(
        string root,
        string role,
        IReadOnlyList<WorkspaceEvidenceFileIndexItem> items,
        WorkspaceEvidenceCandidates candidates)
    {
        var evidence = new List<string>
        {
            $"topology_zone:{role}",
            $"file_count:{items.Count}"
        };
        foreach (var zone in items.Select(static item => item.Zone).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(static zone => zone, StringComparer.OrdinalIgnoreCase).Take(4))
        {
            evidence.Add($"file_zone:{zone}");
        }

        var matchingUnit = candidates.ProjectUnits.FirstOrDefault(unit => string.Equals(unit.RootPath, root, StringComparison.OrdinalIgnoreCase));
        if (matchingUnit is not null)
        {
            evidence.Add($"project_unit:{matchingUnit.Id}");
        }

        if (IsReleaseOutputRoot(root))
        {
            evidence.Add("release_output_root_name");
        }

        if (IsVendorRoot(root))
        {
            evidence.Add("vendor_root_name");
        }

        if (IsGeneratedRoot(root))
        {
            evidence.Add("generated_root_name");
        }

        return evidence.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
    }

    private static IReadOnlyList<string> BuildLikelyActiveSourceRoots(
        WorkspaceScanResult scanResult,
        WorkspaceEvidenceCandidates candidates,
        IReadOnlyList<WorkspaceEvidenceTopologyZone> zones)
    {
        var roots = candidates.ProjectUnits
            .Where(static unit => unit.Confidence != WorkspaceEvidenceConfidenceLevel.Unknown)
            .Where(static unit => !IsVendorRoot(unit.RootPath) && !IsGeneratedRoot(unit.RootPath) && !IsReleaseOutputRoot(unit.RootPath))
            .Select(static unit => unit.RootPath)
            .Concat(scanResult.State.Summary.SourceRoots.Where(root => zones.Any(zone =>
                string.Equals(zone.Root, root, StringComparison.OrdinalIgnoreCase) &&
                zone.Role == "active-source")))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(static root => root == "." ? 0 : 1)
            .ThenBy(static root => root, StringComparer.OrdinalIgnoreCase)
            .Take(12)
            .ToArray();

        var hasNestedActiveSourceRoot = roots.Any(static root => root != ".");
        var rootZoneHasDirectSource = zones.Any(static zone =>
            zone.Root == "." &&
            zone.Evidence.Any(static evidence => string.Equals(evidence, "file_zone:source", StringComparison.OrdinalIgnoreCase)));
        if (hasNestedActiveSourceRoot && !rootZoneHasDirectSource)
        {
            roots = roots
                .Where(static root => root != ".")
                .ToArray();
        }

        return roots;
    }

    private static IEnumerable<string> BuildTopologyUncertaintyReasons(
        WorkspaceScanResult scanResult,
        IReadOnlyList<WorkspaceEvidenceTopologyZone> zones,
        IReadOnlyList<string> activeSourceRoots)
    {
        foreach (var anomaly in scanResult.State.StructuralAnomalies)
        {
            yield return $"{anomaly.Code}: {anomaly.Message}";
        }

        if (activeSourceRoots.Count == 0 && scanResult.State.Summary.SourceFileCount > 0)
        {
            yield return "SOURCE_WITHOUT_ACTIVE_UNIT: source files are present, but no active project unit was confirmed.";
        }

        if (activeSourceRoots.Count > 1)
        {
            yield return $"MULTIPLE_ACTIVE_SOURCE_ROOTS: {string.Join(", ", activeSourceRoots)}";
        }

        if (zones.Any(static zone => zone.Role == "release-output") && activeSourceRoots.Count > 0)
        {
            yield return "SOURCE_AND_RELEASE_OUTPUT: active source and release/output zones coexist.";
        }

        if (scanResult.BudgetReport?.IsPartial == true)
        {
            yield return "PARTIAL_SCAN: topology may be incomplete because scanner budget was exhausted or files were skipped.";
        }
    }

    private static string ClassifyTopologyKind(
        WorkspaceScanResult scanResult,
        IReadOnlyList<WorkspaceEvidenceFileIndexItem> fileIndex,
        IReadOnlyList<WorkspaceEvidenceTopologyZone> zones,
        WorkspaceEvidenceCandidates candidates,
        IReadOnlyList<WorkspaceEvidenceSignal> signals,
        IReadOnlyList<WorkspaceEvidencePattern> patterns,
        IReadOnlyList<string> activeSourceRoots)
    {
        if (scanResult.State.ImportKind == WorkspaceImportKind.Empty)
        {
            return "Empty";
        }

        if (scanResult.State.StructuralAnomalies.Any(static anomaly => anomaly.Code == "NESTED_GIT_PROJECTS") ||
            (candidates.ProjectUnits.Count(unit => unit.RootPath != ".") >= 2 && !candidates.ProjectUnits.Any(static unit => unit.RootPath == ".")))
        {
            return "Container";
        }

        var hasLowLevelSource = fileIndex.Any(static item =>
            string.Equals(item.Extension, ".asm", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(item.Extension, ".s", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(item.Extension, ".inc", StringComparison.OrdinalIgnoreCase));
        var hasReversePathZone = zones.Any(static zone =>
            zone.Root.Contains("decomp", StringComparison.OrdinalIgnoreCase) ||
            zone.Root.Contains("disasm", StringComparison.OrdinalIgnoreCase));
        if ((signals.Any(static signal => signal.Category == "origin" && signal.Code == "reverse") && hasLowLevelSource) ||
            hasReversePathZone)
        {
            return "Decompilation";
        }

        if (hasLowLevelSource)
        {
            return "Legacy";
        }

        if (activeSourceRoots.Count == 0 &&
            zones.Any(static zone => zone.Role is "release-output" or "runtime-payload") &&
            !zones.Any(static zone => zone.Role == "active-source"))
        {
            return "ReleaseBundle";
        }

        if (scanResult.State.ImportKind == WorkspaceImportKind.NonSourceImport ||
            (scanResult.State.Summary.SourceFileCount == 0 &&
             !zones.Any(static zone => zone.Role == "active-source")))
        {
            return "MaterialOnly";
        }

        if (activeSourceRoots.Count > 1)
        {
            return "Ambiguous";
        }

        if (scanResult.State.ImportKind == WorkspaceImportKind.MixedImport ||
            zones.Count(static zone => zone.Role is "release-output" or "runtime-payload" or "asset-payload") > 0)
        {
            return "Mixed";
        }

        return "SingleProject";
    }

    private static string RecommendSafeImportMode(
        string topologyKind,
        IReadOnlyList<WorkspaceEvidenceTopologyZone> zones,
        IReadOnlyList<string> uncertaintyReasons)
    {
        return topologyKind switch
        {
            "Container" => "container-review: require user-selected active root before treating one project as primary",
            "Ambiguous" => "ambiguous-review: keep competing roots visible and avoid single-main claims",
            "Decompilation" => "decompilation-safe-import: preserve source/assets/tooling zones and avoid normal app assumptions",
            "Legacy" => "legacy-low-level-source-review: preserve low-level source/tooling zones and avoid normal app assumptions",
            "ReleaseBundle" => "release-bundle-review: treat binaries/dist payloads as materials unless source is selected",
            "MaterialOnly" => "material-only-review: treat documents/context as materials, not as source project truth",
            "Mixed" when zones.Any(static zone => zone.Role == "release-output") => "mixed-source-release: keep active source and release/output zones separated",
            "Mixed" => "mixed-safe-import: keep source, payload, and context zones separated",
            "Empty" => "no-import: no relevant evidence found",
            _ when uncertaintyReasons.Count > 0 => "standard-source-import-with-uncertainty",
            _ => "standard-source-import"
        };
    }

    private static string TopologyRoot(string relativePath)
    {
        var normalized = relativePath.Replace('/', '\\').Trim('\\');
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return ".";
        }

        var separatorIndex = normalized.IndexOf('\\');
        return separatorIndex < 0 ? "." : normalized[..separatorIndex];
    }

    private static bool IsSourceOrBuildIndexItem(WorkspaceEvidenceFileIndexItem item)
    {
        return item.Zone is "source" or "build";
    }

    private static bool IsReleaseOutputRoot(string root)
    {
        var normalized = root.Replace('/', '\\').Trim('\\');
        return normalized.Equals("dist", StringComparison.OrdinalIgnoreCase) ||
               normalized.Equals("dist-module", StringComparison.OrdinalIgnoreCase) ||
               normalized.Equals("build", StringComparison.OrdinalIgnoreCase) ||
               normalized.Equals("out", StringComparison.OrdinalIgnoreCase) ||
               normalized.Equals("release", StringComparison.OrdinalIgnoreCase) ||
               normalized.Equals("releases", StringComparison.OrdinalIgnoreCase) ||
               normalized.Equals("publish", StringComparison.OrdinalIgnoreCase) ||
               normalized.Equals("target", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsVendorRoot(string root)
    {
        var normalized = root.Replace('/', '\\').Trim('\\');
        return normalized.Equals("vendor", StringComparison.OrdinalIgnoreCase) ||
               normalized.Equals("third_party", StringComparison.OrdinalIgnoreCase) ||
               normalized.Equals("node_modules", StringComparison.OrdinalIgnoreCase) ||
               normalized.StartsWith("vendor\\", StringComparison.OrdinalIgnoreCase) ||
               normalized.StartsWith("third_party\\", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsGeneratedRoot(string root)
    {
        var normalized = root.Replace('/', '\\').Trim('\\');
        return normalized.Equals("generated", StringComparison.OrdinalIgnoreCase) ||
               normalized.Equals("gen", StringComparison.OrdinalIgnoreCase) ||
               normalized.Equals("autogen", StringComparison.OrdinalIgnoreCase) ||
               normalized.Equals("codegen", StringComparison.OrdinalIgnoreCase) ||
               normalized.StartsWith("generated\\", StringComparison.OrdinalIgnoreCase) ||
               normalized.StartsWith("autogen\\", StringComparison.OrdinalIgnoreCase);
    }

    private static int TopologyZoneRank(string role)
    {
        return role switch
        {
            "active-source" => 0,
            "release-output" => 1,
            "runtime-payload" => 2,
            "generated" => 3,
            "vendor" => 4,
            "ignored-noise" => 5,
            "asset-payload" => 6,
            "documentation" => 7,
            _ => 8
        };
    }

    private static WorkspaceScanRun BuildScanRun(WorkspaceScanResult scanResult)
    {
        var state = scanResult.State;
        var scanTime = state.LastScanAt.ToUniversalTime();
        var scanStamp = scanTime.ToString("yyyy-MM-ddTHH-mm-ssZ", System.Globalization.CultureInfo.InvariantCulture);
        return new WorkspaceScanRun(
            $"SCAN-{scanStamp}",
            ComputeScanFingerprint(scanResult),
            "2.0.0-alpha",
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["file_inventory"] = "0.2.0",
                ["manifest_index"] = "0.2.0",
                ["shallow_symbols"] = "0.1.0",
                ["dependency_edges"] = "0.1.0",
                ["entrypoint_ranking"] = "0.2.0",
                ["predicate_registry"] = "0.1.0",
                ["scanner_config"] = "0.1.0",
                ["run_profile_index"] = "0.1.0"
            },
            scanTime.ToString("O", System.Globalization.CultureInfo.InvariantCulture),
            scanTime.ToString("O", System.Globalization.CultureInfo.InvariantCulture),
            "full");
    }

    private static string ComputeScanFingerprint(WorkspaceScanResult scanResult)
    {
        var state = scanResult.State;
        var lines = new List<string>
        {
            $"root={Path.GetFileName(state.WorkspaceRoot.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar))}",
            $"import={state.ImportKind}",
            $"health={state.Health}",
            $"relevant={state.Summary.RelevantFileCount}",
            $"source={state.Summary.SourceFileCount}",
            $"build={state.Summary.BuildFileCount}"
        };
        lines.AddRange(scanResult.RelevantFiles
            .Select(path => Path.GetRelativePath(state.WorkspaceRoot, path).Replace('/', '\\'))
            .OrderBy(static path => path, StringComparer.OrdinalIgnoreCase)
            .Select(static path => $"file={path}"));
        lines.AddRange(state.StructuralAnomalies
            .OrderBy(static anomaly => anomaly.Code, StringComparer.OrdinalIgnoreCase)
            .Select(static anomaly => $"anomaly={anomaly.Code}:{anomaly.Scope}"));

        var payload = string.Join("\n", lines);
        return $"sha256:{Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(payload))).ToLowerInvariant()}";
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

        foreach (var sensitiveFile in GetSensitiveRelativeFiles(scanResult).Take(24))
        {
            observations.Add(new WorkspaceEvidenceObservation("sensitive_file_detected", WorkspaceSensitiveFilePolicy.GetSensitiveReason(sensitiveFile), sensitiveFile));
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

        if (scanResult.BudgetReport is { IsPartial: true } budgetReport)
        {
            observations.Add(new WorkspaceEvidenceObservation("scan_budget_degraded", "partial_scan", workspaceRoot));
            foreach (var skip in budgetReport.Skips.Take(24))
            {
                observations.Add(new WorkspaceEvidenceObservation("scan_budget_skip_detected", skip.Reason, skip.RelativePath));
            }
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

        return EnrichObservations(observations
            .GroupBy(static observation => $"{observation.Kind}|{observation.Value}|{observation.EvidencePath}", StringComparer.OrdinalIgnoreCase)
            .Select(static group => group.First())
            .ToArray());
    }

    private static WorkspaceEvidenceObservation[] EnrichObservations(IReadOnlyList<WorkspaceEvidenceObservation> observations)
    {
        return observations
            .Select((observation, index) =>
            {
                var predicate = WorkspaceEvidencePredicateRegistry.PredicateForObservationKind(observation.Kind);
                var source = ResolveObservationSource(observation.Kind);
                var extractorVersion = ResolveObservationExtractorVersion(source);
                return observation with
                {
                    Id = BuildStableEvidenceId(observation, predicate, source),
                    DisplayId = $"EV-{index + 1:00000}",
                    Predicate = predicate,
                    Source = source,
                    ExtractorVersion = extractorVersion
                };
            })
            .ToArray();
    }

    private static string BuildStableEvidenceId(WorkspaceEvidenceObservation observation, string predicate, string source)
    {
        var subject = observation.EvidencePath ?? string.Empty;
        var payload = string.Join(
            "\n",
            subject.Trim().Replace('/', '\\').ToLowerInvariant(),
            predicate.Trim().ToLowerInvariant(),
            observation.Value.Trim().ToLowerInvariant(),
            source.Trim().ToLowerInvariant());
        return $"EV-{Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(payload)))[..12].ToLowerInvariant()}";
    }

    private static string ResolveObservationSource(string kind)
    {
        return kind switch
        {
            "file_found" or "source_root_detected" or "build_root_detected" or "entry_candidate_detected" or "structural_anomaly_detected" or "scan_budget_degraded" or "scan_budget_skip_detected" => "file_inventory",
            "sensitive_file_detected" => "sensitive_file_policy",
            "technical_preview_detected" or "material_preview_detected" or "document_kind_detected" => "material_runtime",
            _ => "scanner"
        };
    }

    private static string ResolveObservationExtractorVersion(string source)
    {
        return source switch
        {
            "file_inventory" => "0.2.0",
            "sensitive_file_policy" => "0.1.0",
            "material_runtime" => "0.1.0",
            _ => "0.1.0"
        };
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
                path.EndsWith(".s", StringComparison.OrdinalIgnoreCase) ||
                path.Contains("\\dbg\\", StringComparison.OrdinalIgnoreCase) ||
                path.Contains("\\analysis\\", StringComparison.OrdinalIgnoreCase) ||
                path.Contains("disasm", StringComparison.OrdinalIgnoreCase)) ||
            CountMatchingMarkers(combinedText, "debugger", "breakpoint", "opcode", "disassembly", "decompilation", "decompiled", "hook", "memory") >= 3,
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
        var cargoDefaultMembers = BuildCargoDefaultMemberRoots(scanResult);
        var manifestHints = BuildManifestEntryHints(scanResult)
            .GroupBy(static hint => hint.RelativePath, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(static group => group.Key, static group => group.ToArray(), StringComparer.OrdinalIgnoreCase);
        var candidatePaths = scanResult.State.Summary.EntryCandidates
            .Concat(manifestHints.Keys)
            .Distinct(StringComparer.OrdinalIgnoreCase);
        return candidatePaths
            .Select(path => BuildEntryPoint(
                scanResult,
                path,
                cargoDefaultMembers,
                manifestHints.TryGetValue(path, out var hints) ? hints : Array.Empty<ManifestEntryHint>()))
            .OrderByDescending(static entry => entry.Score)
            .ThenBy(static entry => entry.RelativePath.Count(static ch => ch == '\\' || ch == '/'))
            .ThenBy(static entry => entry.RelativePath, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static WorkspaceEvidenceEntryPoint BuildEntryPoint(
        WorkspaceScanResult scanResult,
        string relativePath,
        IReadOnlyCollection<string> cargoDefaultMembers,
        IReadOnlyList<ManifestEntryHint> manifestHints)
    {
        var classifiedRole = ClassifyEntryRole(relativePath);
        var role = !string.Equals(classifiedRole, "entry", StringComparison.OrdinalIgnoreCase) || manifestHints.Count == 0
            ? classifiedRole
            : manifestHints
                .Select(static hint => hint.Role)
                .FirstOrDefault(static hintRole => !string.IsNullOrWhiteSpace(hintRole)) ?? classifiedRole;
        var evidence = BuildEntryEvidence(scanResult, relativePath, role, cargoDefaultMembers)
            .Concat(manifestHints.SelectMany(static hint => hint.Evidence))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(static item => item, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var score = ScoreEntryCandidate(relativePath, role, evidence);
        var confidence = evidence.Contains("cargo_default_member", StringComparer.OrdinalIgnoreCase)
            || manifestHints.Any(static hint => hint.Confidence == WorkspaceEvidenceConfidenceLevel.Confirmed)
            ? WorkspaceEvidenceConfidenceLevel.Confirmed
            : WorkspaceEvidenceConfidenceLevel.Likely;
        return new WorkspaceEvidenceEntryPoint(
            relativePath,
            role,
            BuildEntryNote(relativePath, score, evidence),
            score,
            evidence,
            EvidenceMarker(
                "entrypoint_candidate",
                relativePath,
                string.Join("; ", evidence),
                confidence,
                scanResult.BudgetReport?.IsPartial == true,
                isBounded: false));
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
                $"Observed {bucket.Value.Count} files clustered under '{bucket.Key}' from source root '{bucket.Value.SourceRoot}'.",
                EvidenceMarker(
                    "module_candidate",
                    bucket.Key,
                    $"Observed {bucket.Value.Count} files clustered under '{bucket.Key}' from source root '{bucket.Value.SourceRoot}'.",
                    WorkspaceEvidenceConfidenceLevel.Likely,
                    scanResult.BudgetReport?.IsPartial == true,
                    isBounded: false)));
        }

        foreach (var pythonPackageRoot in BuildPythonPackageRoots(scanResult).Take(8))
        {
            var name = NormalizeModuleName(Path.GetFileName(pythonPackageRoot));
            if (string.IsNullOrWhiteSpace(name) ||
                candidates.Any(candidate => string.Equals(candidate.Name, name, StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            candidates.Add(new WorkspaceEvidenceModule(
                name,
                "python-package",
                InferLayerNameForModule(pythonPackageRoot, observedLayers),
                $"Observed Python package root '{pythonPackageRoot}' via __init__.py.",
                EvidenceMarker(
                    "module_candidate",
                    pythonPackageRoot,
                    $"Observed Python package root '{pythonPackageRoot}' via __init__.py.",
                    WorkspaceEvidenceConfidenceLevel.Likely,
                    scanResult.BudgetReport?.IsPartial == true,
                    isBounded: false)));
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
                $"Observed entry cluster anchored at '{entryPoint.RelativePath}'.",
                EvidenceMarker(
                    "module_candidate",
                    entryPoint.RelativePath,
                    $"Observed entry cluster anchored at '{entryPoint.RelativePath}'.",
                    WorkspaceEvidenceConfidenceLevel.Likely,
                    scanResult.BudgetReport?.IsPartial == true,
                    isBounded: false)));
        }

        return candidates
            .GroupBy(static candidate => candidate.Name, StringComparer.OrdinalIgnoreCase)
            .Select(static group => group.First())
            .OrderBy(static module => GetModulePriority(module))
            .ThenBy(static module => module.Name, StringComparer.OrdinalIgnoreCase)
            .Take(10)
            .ToArray();
    }

    private sealed class UnitDraft(string rootPath, string kind)
    {
        public string RootPath { get; } = rootPath;

        public string Kind { get; } = kind;

        public bool IsPartial { get; set; }

        public HashSet<string> Manifests { get; } = new(StringComparer.OrdinalIgnoreCase);

        public HashSet<string> EntryPoints { get; } = new(StringComparer.OrdinalIgnoreCase);

        public HashSet<string> Evidence { get; } = new(StringComparer.OrdinalIgnoreCase);

        public WorkspaceEvidenceProjectUnit ToProjectUnit()
        {
            var confidence = Manifests.Count > 0 &&
                             Evidence.Contains("confirmed_entry_overlap", StringComparer.OrdinalIgnoreCase)
                ? WorkspaceEvidenceConfidenceLevel.Confirmed
                : WorkspaceEvidenceConfidenceLevel.Likely;
            var id = RootPath == "." ? "unit-root" : $"unit-{SanitizeUnitId(RootPath)}";
            var evidence = Evidence.OrderBy(static item => item, StringComparer.OrdinalIgnoreCase).ToArray();
            return new WorkspaceEvidenceProjectUnit(
                id,
                RootPath,
                Kind,
                Manifests.OrderBy(static path => path, StringComparer.OrdinalIgnoreCase).ToArray(),
                EntryPoints.OrderBy(static path => path, StringComparer.OrdinalIgnoreCase).ToArray(),
                confidence,
                evidence,
                EvidenceMarker(
                    "project_unit_candidate",
                    RootPath,
                    string.Join("; ", evidence),
                    confidence,
                    IsPartial,
                    isBounded: false));
        }
    }

    private sealed record ManifestEntryHint(
        string RelativePath,
        string Role,
        WorkspaceEvidenceConfidenceLevel Confidence,
        IReadOnlyList<string> Evidence);

    private static void AddCargoRunProfiles(
        ICollection<WorkspaceEvidenceRunProfile> profiles,
        WorkspaceEvidenceProjectUnit unit,
        string manifest)
    {
        var manifestArg = QuoteCommandArgument(manifest);
        var evidence = UnitProfileEvidence(unit, manifest);
        AddRunProfile(profiles, unit, "build", $"cargo build --manifest-path {manifestArg}", manifest, unit.Confidence, evidence);
        AddRunProfile(profiles, unit, "test", $"cargo test --manifest-path {manifestArg}", manifest, unit.Confidence, evidence);
        if (unit.EntryPoints.Count > 0)
        {
            AddRunProfile(profiles, unit, "run", $"cargo run --manifest-path {manifestArg}", manifest, WorkspaceEvidenceConfidenceLevel.Confirmed, evidence.Concat(new[] { "entrypoint_overlap" }).ToArray());
        }
    }

    private static void AddPackageJsonRunProfiles(
        ICollection<WorkspaceEvidenceRunProfile> profiles,
        string workspaceRoot,
        WorkspaceEvidenceProjectUnit unit,
        string manifest)
    {
        var fullPath = Path.Combine(workspaceRoot, manifest);
        if (!File.Exists(fullPath))
        {
            return;
        }

        try
        {
            using var document = JsonDocument.Parse(File.ReadAllText(fullPath, Encoding.UTF8));
            if (!document.RootElement.TryGetProperty("scripts", out var scripts) ||
                scripts.ValueKind != JsonValueKind.Object)
            {
                return;
            }

            foreach (var script in scripts.EnumerateObject())
            {
                var kind = ClassifyPackageScript(script.Name);
                if (string.IsNullOrWhiteSpace(kind))
                {
                    continue;
                }

                AddRunProfile(
                    profiles,
                    unit,
                    kind,
                    $"npm run {script.Name}",
                    manifest,
                    WorkspaceEvidenceConfidenceLevel.Confirmed,
                    UnitProfileEvidence(unit, manifest).Concat(new[] { $"script:{script.Name}" }).ToArray());
            }
        }
        catch (JsonException)
        {
            return;
        }
        catch (IOException)
        {
            return;
        }
        catch (UnauthorizedAccessException)
        {
            return;
        }
    }

    private static void AddPythonRunProfiles(
        ICollection<WorkspaceEvidenceRunProfile> profiles,
        string workspaceRoot,
        WorkspaceEvidenceProjectUnit unit,
        string manifest)
    {
        var fullPath = Path.Combine(workspaceRoot, manifest);
        var fileName = Path.GetFileName(manifest);
        foreach (var script in ExtractPythonConsoleScripts(workspaceRoot, manifest))
        {
            AddRunProfile(
                profiles,
                unit,
                "run",
                script.Name,
                manifest,
                WorkspaceEvidenceConfidenceLevel.Confirmed,
                UnitProfileEvidence(unit, manifest).Concat(new[] { $"python_console_script:{script.Name}", $"python_target:{script.Target}" }).ToArray());
        }

        if (File.Exists(fullPath) && ManifestHasPytestConfig(fullPath, fileName))
        {
            AddRunProfile(
                profiles,
                unit,
                "test",
                "pytest",
                manifest,
                WorkspaceEvidenceConfidenceLevel.Confirmed,
                UnitProfileEvidence(unit, manifest).Concat(new[] { "pytest_config" }).ToArray());
        }
    }

    private static void AddGoRunProfiles(
        ICollection<WorkspaceEvidenceRunProfile> profiles,
        WorkspaceEvidenceProjectUnit unit,
        string manifest)
    {
        var evidence = UnitProfileEvidence(unit, manifest);
        AddRunProfile(profiles, unit, "build", "go build ./...", manifest, unit.Confidence, evidence);
        AddRunProfile(profiles, unit, "test", "go test ./...", manifest, unit.Confidence, evidence);
    }

    private static void AddCMakeRunProfiles(
        ICollection<WorkspaceEvidenceRunProfile> profiles,
        WorkspaceEvidenceProjectUnit unit,
        string manifest)
    {
        var evidence = UnitProfileEvidence(unit, manifest);
        AddRunProfile(profiles, unit, "configure", "cmake -S . -B build", manifest, unit.Confidence, evidence);
        AddRunProfile(profiles, unit, "build", "cmake --build build", manifest, unit.Confidence, evidence);
    }

    private static void AddDotnetRunProfiles(
        ICollection<WorkspaceEvidenceRunProfile> profiles,
        WorkspaceEvidenceProjectUnit unit,
        string manifest)
    {
        var projectArg = QuoteCommandArgument(manifest);
        var evidence = UnitProfileEvidence(unit, manifest);
        AddRunProfile(profiles, unit, "build", $"dotnet build {projectArg}", manifest, unit.Confidence, evidence);
        if (ContainsAny(manifest, "test", "tests"))
        {
            AddRunProfile(profiles, unit, "test", $"dotnet test {projectArg}", manifest, WorkspaceEvidenceConfidenceLevel.Confirmed, evidence.Concat(new[] { "test_project_name" }).ToArray());
        }
    }

    private static void AddRunProfile(
        ICollection<WorkspaceEvidenceRunProfile> profiles,
        WorkspaceEvidenceProjectUnit unit,
        string kind,
        string command,
        string sourcePath,
        WorkspaceEvidenceConfidenceLevel confidence,
        IReadOnlyList<string> evidence)
    {
        profiles.Add(new WorkspaceEvidenceRunProfile(
            $"profile-{SanitizeUnitId(unit.RootPath)}-{kind}-{profiles.Count + 1}",
            kind,
            command,
            unit.RootPath,
            sourcePath,
            confidence,
            evidence,
            EvidenceMarker(
                "run_profile_candidate",
                sourcePath,
                string.Join("; ", evidence),
                confidence,
                unit.EvidenceMarker?.IsPartial == true,
                isBounded: false)));
    }

    private static IReadOnlyList<string> UnitProfileEvidence(WorkspaceEvidenceProjectUnit unit, string manifest)
    {
        return unit.Evidence
            .Concat(new[] { $"manifest:{manifest}", $"unit:{unit.RootPath}" })
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(static item => item, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static string ClassifyPackageScript(string scriptName)
    {
        var normalized = scriptName.Trim().ToLowerInvariant();
        if (normalized is "build" or "compile")
        {
            return "build";
        }

        if (normalized is "start" or "dev" or "serve" || normalized.StartsWith("start:", StringComparison.Ordinal))
        {
            return "run";
        }

        if (normalized is "test" || normalized.StartsWith("test:", StringComparison.Ordinal))
        {
            return "test";
        }

        if (normalized is "lint" or "check" or "typecheck" || normalized.StartsWith("lint:", StringComparison.Ordinal))
        {
            return "check";
        }

        return string.Empty;
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
                    entryPoint.RelativePath,
                    WorkspaceEvidenceEdgeResolution.Lexical,
                    EvidenceMarker(
                        "dependency_edge_candidate",
                        entryPoint.RelativePath,
                        $"Entry candidate '{entryPoint.RelativePath}' overlaps with module candidate '{matchedModule.Name}'.",
                        WorkspaceEvidenceConfidenceLevel.Likely,
                        entryPoint.EvidenceMarker?.IsPartial == true,
                        isBounded: false)));
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
                    entryPoint.RelativePath,
                    WorkspaceEvidenceEdgeResolution.Lexical,
                    EvidenceMarker(
                        "dependency_edge_candidate",
                        entryPoint.RelativePath,
                        $"Entry candidate '{entryPoint.RelativePath}' overlaps with layer/root '{matchedLayer.Name}'.",
                        WorkspaceEvidenceConfidenceLevel.Likely,
                        entryPoint.EvidenceMarker?.IsPartial == true,
                        isBounded: false)));
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
                layer.Root,
                WorkspaceEvidenceEdgeResolution.Lexical,
                EvidenceMarker(
                    "dependency_edge_candidate",
                    layer.Root,
                    $"Module candidate '{module.Name}' is grouped under layer/root '{layer.Name}'.",
                    WorkspaceEvidenceConfidenceLevel.Likely,
                    module.EvidenceMarker?.IsPartial == true,
                    isBounded: false)));
        }

        foreach (var codeEdge in codeEdges.Take(24))
        {
            edges.Add(new WorkspaceEvidenceDependencyEdge(
                codeEdge.FromPath,
                codeEdge.ToPath,
                codeEdge.Kind,
                codeEdge.Reason,
                codeEdge.FromPath,
                codeEdge.Resolution,
                codeEdge.EvidenceMarker));
        }

        foreach (var dependency in dependencySurface.Take(24))
        {
            edges.Add(new WorkspaceEvidenceDependencyEdge(
                dependency.SourcePath,
                dependency.Name,
                dependency.Scope,
                $"Dependency surface extracted from '{dependency.SourcePath}'.",
                dependency.SourcePath,
                WorkspaceEvidenceEdgeResolution.Manifest,
                EvidenceMarker(
                    "manifest_dependency_edge",
                    dependency.SourcePath,
                    $"Dependency surface extracted from '{dependency.SourcePath}'.",
                    WorkspaceEvidenceConfidenceLevel.Confirmed,
                    isPartial: false,
                    isBounded: false)));
        }

        return edges
            .GroupBy(static edge => $"{edge.From}|{edge.To}|{edge.Label}|{edge.Resolution}", StringComparer.OrdinalIgnoreCase)
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

            roles.Add(new WorkspaceEvidenceFileRole(
                relativePath,
                role.Value.Role,
                role.Value.Confidence,
                role.Value.Reason,
                EvidenceMarker(
                    "file_role_candidate",
                    relativePath,
                    role.Value.Reason,
                    WorkspaceEvidenceConfidenceLevel.Likely,
                    scanResult.BudgetReport?.IsPartial == true,
                    isBounded: false)));
        }

        return roles
            .GroupBy(static item => item.RelativePath, StringComparer.OrdinalIgnoreCase)
            .Select(static group => group.OrderByDescending(static item => item.Confidence).First())
            .OrderByDescending(static item => item.Confidence)
            .ThenBy(static item => item.RelativePath, StringComparer.OrdinalIgnoreCase)
            .Take(32)
            .ToArray();
    }

    private static WorkspaceEvidenceFileIndexItem[] BuildFileIndex(
        WorkspaceScanResult scanResult,
        IReadOnlyList<WorkspaceEvidenceFileRole> fileRoles,
        WorkspaceScannerConfig scannerConfig)
    {
        var workspaceRoot = scanResult.State.WorkspaceRoot;
        var roleByPath = fileRoles
            .GroupBy(static role => role.RelativePath, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(static group => group.Key, static group => group.OrderByDescending(static role => role.Confidence).First(), StringComparer.OrdinalIgnoreCase);
        var materialByPath = scanResult.MaterialCandidates
            .GroupBy(static material => material.RelativePath, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(static group => group.Key, static group => group.First().Kind.ToString(), StringComparer.OrdinalIgnoreCase);

        return scanResult.RelevantFiles
            .Select(file =>
            {
                var relativePath = Path.GetRelativePath(workspaceRoot, file).Replace('/', '\\');
                _ = roleByPath.TryGetValue(relativePath, out var role);
                _ = materialByPath.TryGetValue(relativePath, out var materialKind);
                return new WorkspaceEvidenceFileIndexItem(
                    relativePath,
                    Path.GetExtension(relativePath),
                    TryGetFileSize(file),
                    ClassifyFileIndexZone(relativePath, materialKind, scannerConfig),
                    role?.Role ?? "unclassified",
                    WorkspaceSensitiveFilePolicy.IsSensitivePath(relativePath),
                    materialKind ?? string.Empty,
                    BuildFileIndexEvidence(relativePath, role?.Role, materialKind, scannerConfig));
            })
            .OrderBy(static item => item.RelativePath, StringComparer.OrdinalIgnoreCase)
            .Take(5000)
            .ToArray();
    }

    private static long TryGetFileSize(string file)
    {
        try
        {
            return new FileInfo(file).Length;
        }
        catch (IOException)
        {
            return -1;
        }
        catch (UnauthorizedAccessException)
        {
            return -1;
        }
    }

    private static string ClassifyFileIndexZone(
        string relativePath,
        string? materialKind,
        WorkspaceScannerConfig scannerConfig)
    {
        if (scannerConfig.IsGeneratedFile(relativePath))
        {
            return "generated";
        }

        if (scannerConfig.IsVendorUnit(relativePath))
        {
            return "vendor";
        }

        if (scannerConfig.IsIgnoredUnit(relativePath))
        {
            return "ignored";
        }

        if (!string.IsNullOrWhiteSpace(materialKind))
        {
            return "material";
        }

        var extension = Path.GetExtension(relativePath).ToLowerInvariant();
        var fileName = Path.GetFileName(relativePath);
        if (IsBuildLikePath(relativePath))
        {
            return "build";
        }

        if (IsConfigLikeFileName(fileName, extension))
        {
            return "config";
        }

        if (IsDocumentLikeExtension(extension))
        {
            return "document";
        }

        if (IsAssetLikeExtension(extension))
        {
            return "asset";
        }

        if (IsBinaryLikeExtension(extension))
        {
            return "binary";
        }

        return "source";
    }

    private static string BuildFileIndexEvidence(
        string relativePath,
        string? role,
        string? materialKind,
        WorkspaceScannerConfig scannerConfig)
    {
        var parts = new List<string> { "relevant_file" };
        if (scannerConfig.IsGeneratedFile(relativePath))
        {
            parts.Add("config_generated_pattern");
        }

        if (scannerConfig.IsVendorUnit(relativePath))
        {
            parts.Add("config_vendor_zone");
        }

        if (scannerConfig.IsIgnoredUnit(relativePath))
        {
            parts.Add("config_ignore_zone");
        }

        if (!string.IsNullOrWhiteSpace(role))
        {
            parts.Add($"role:{role}");
        }

        if (!string.IsNullOrWhiteSpace(materialKind))
        {
            parts.Add($"material:{materialKind}");
        }

        if (WorkspaceSensitiveFilePolicy.IsSensitivePath(relativePath))
        {
            parts.Add("sensitive_policy");
        }

        return string.Join("; ", parts);
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
                    hotspots.Add(new WorkspaceEvidenceHotspot(
                        "large_file",
                        relativePath,
                        $"Observed file size {info.Length} bytes.",
                        EvidenceMarker(
                            "risk_zone",
                            relativePath,
                            $"Observed file size {info.Length} bytes.",
                            WorkspaceEvidenceConfidenceLevel.Confirmed,
                            scanResult.BudgetReport?.IsPartial == true,
                            isBounded: false)));
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
            hotspots.Add(new WorkspaceEvidenceHotspot(
                "high_fanout_candidate",
                group.Key,
                $"Observed {group.Count()} coarse outgoing edges from '{group.Key}'.",
                EvidenceMarker(
                    "risk_zone",
                    group.Key,
                    $"Observed {group.Count()} coarse outgoing edges from '{group.Key}'.",
                    WorkspaceEvidenceConfidenceLevel.Likely,
                    scanResult.BudgetReport?.IsPartial == true,
                    isBounded: false)));
        }

        foreach (var anomaly in scanResult.State.StructuralAnomalies)
        {
            if (string.Equals(anomaly.Code, "NESTED_NON_SOURCE_PAYLOADS", StringComparison.OrdinalIgnoreCase))
            {
                hotspots.Add(new WorkspaceEvidenceHotspot(
                    "nested_payload_boundary",
                    anomaly.Scope ?? scanResult.State.WorkspaceRoot,
                    anomaly.Message,
                    EvidenceMarker(
                        "risk_zone",
                        anomaly.Scope,
                        anomaly.Message,
                        WorkspaceEvidenceConfidenceLevel.Confirmed,
                        scanResult.BudgetReport?.IsPartial == true,
                        isBounded: false)));
            }

            if (string.Equals(anomaly.Code, "NESTED_GIT_PROJECTS", StringComparison.OrdinalIgnoreCase))
            {
                hotspots.Add(new WorkspaceEvidenceHotspot(
                    "binary_source_mixed_layout",
                    anomaly.Scope ?? scanResult.State.WorkspaceRoot,
                    anomaly.Message,
                    EvidenceMarker(
                        "risk_zone",
                        anomaly.Scope,
                        anomaly.Message,
                        WorkspaceEvidenceConfidenceLevel.Confirmed,
                        scanResult.BudgetReport?.IsPartial == true,
                        isBounded: false)));
            }
        }

        foreach (var material in materials)
        {
            if (ContainsAny(material.PreviewText, "todo", "fixme", "temporary", "workaround") &&
                CountMatchingMarkers(material.PreviewText, "runtime", "ui", "config", "build", "api", "process") >= 2)
            {
                hotspots.Add(new WorkspaceEvidenceHotspot(
                    "mixed_concerns_candidate",
                    material.RelativePath,
                    "Observed temporary/procedural wording mixed with technical concerns in one document.",
                    EvidenceMarker(
                        "risk_zone",
                        material.RelativePath,
                        "Observed temporary/procedural wording mixed with technical concerns in one document.",
                        WorkspaceEvidenceConfidenceLevel.Likely,
                        scanResult.BudgetReport?.IsPartial == true,
                        isBounded: material.WasTruncated)));
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

        signals.AddRange(BuildRootReadmeIdentitySignals(scanResult));
        signals.AddRange(BuildSensitiveFileSignals(scanResult));

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

    private static IEnumerable<WorkspaceEvidenceSignal> BuildSensitiveFileSignals(WorkspaceScanResult scanResult)
    {
        foreach (var relativePath in GetSensitiveRelativeFiles(scanResult).Take(24))
        {
            yield return new WorkspaceEvidenceSignal(
                "safety",
                "sensitive_file_present",
                $"Scanner detected a sensitive-looking file and skipped content extraction: {WorkspaceSensitiveFilePolicy.GetSensitiveReason(relativePath)}.",
                relativePath);
        }
    }

    private static IEnumerable<string> GetSensitiveRelativeFiles(WorkspaceScanResult scanResult)
    {
        var workspaceRoot = scanResult.State.WorkspaceRoot;
        return scanResult.RelevantFiles
            .Select(path => Path.GetRelativePath(workspaceRoot, path).Replace('/', '\\'))
            .Where(WorkspaceSensitiveFilePolicy.IsSensitivePath)
            .OrderBy(static path => path, StringComparer.OrdinalIgnoreCase);
    }

    private static IEnumerable<WorkspaceEvidenceSignal> BuildRootReadmeIdentitySignals(WorkspaceScanResult scanResult)
    {
        var workspaceRoot = scanResult.State.WorkspaceRoot;
        var readmePath = scanResult.RelevantFiles.FirstOrDefault(path =>
            string.Equals(Path.GetRelativePath(workspaceRoot, path), "README.md", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(Path.GetRelativePath(workspaceRoot, path), "README.txt", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(Path.GetRelativePath(workspaceRoot, path), "README.rst", StringComparison.OrdinalIgnoreCase));
        if (readmePath is null)
        {
            yield break;
        }

        var title = ReadBoundedLines(readmePath, 32)
            .Select(static line => line.Trim())
            .Where(static line => !string.IsNullOrWhiteSpace(line))
            .Select(NormalizeReadmeIdentityLine)
            .FirstOrDefault(static line => !string.IsNullOrWhiteSpace(line));
        if (string.IsNullOrWhiteSpace(title))
        {
            yield break;
        }

        yield return new WorkspaceEvidenceSignal(
            "identity",
            "root_readme_title",
            $"Root README title observed: {TruncateEvidenceValue(title, 96)}.",
            Path.GetRelativePath(workspaceRoot, readmePath).Replace('/', '\\'));
    }

    private static string NormalizeReadmeIdentityLine(string line)
    {
        var normalized = line.Trim().TrimStart('#', '=', '-', '*').Trim();
        if (normalized.Length == 0)
        {
            return string.Empty;
        }

        return normalized;
    }

    private static string TruncateEvidenceValue(string value, int maxLength)
    {
        if (value.Length <= maxLength)
        {
            return value;
        }

        return value[..maxLength].TrimEnd() + "...";
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

    private static bool TryClassifyUnitManifest(
        string relativePath,
        out string rootPath,
        out string kind,
        out string evidence)
    {
        var normalizedPath = relativePath.Replace('/', '\\');
        var fileName = Path.GetFileName(normalizedPath);
        var extension = Path.GetExtension(normalizedPath);

        kind = fileName switch
        {
            "Cargo.toml" => "rust-cargo",
            "package.json" => "node-package",
            "pyproject.toml" => "python-project",
            "setup.cfg" => "python-project",
            "setup.py" => "python-project",
            "go.mod" => "go-module",
            "CMakeLists.txt" => "cmake-project",
            _ when extension.Equals(".csproj", StringComparison.OrdinalIgnoreCase) => "dotnet-project",
            _ => string.Empty
        };

        if (string.IsNullOrWhiteSpace(kind))
        {
            rootPath = string.Empty;
            evidence = string.Empty;
            return false;
        }

        rootPath = Path.GetDirectoryName(normalizedPath)?.Replace('/', '\\') ?? string.Empty;
        if (string.IsNullOrWhiteSpace(rootPath))
        {
            rootPath = ".";
        }

        evidence = $"manifest:{fileName}";
        return true;
    }

    private static UnitDraft GetOrCreateUnit(IDictionary<string, UnitDraft> unitsByRoot, string rootPath, string kind, bool isPartial)
    {
        if (unitsByRoot.TryGetValue(rootPath, out var existing))
        {
            existing.IsPartial |= isPartial;
            return existing;
        }

        var draft = new UnitDraft(rootPath, kind);
        draft.IsPartial = isPartial;
        unitsByRoot[rootPath] = draft;
        return draft;
    }

    private static IReadOnlyList<string> BuildPythonPackageRoots(WorkspaceScanResult scanResult)
    {
        var workspaceRoot = scanResult.State.WorkspaceRoot;
        return scanResult.RelevantFiles
            .Select(path => Path.GetRelativePath(workspaceRoot, path).Replace('/', '\\'))
            .Where(static path => path.EndsWith("__init__.py", StringComparison.OrdinalIgnoreCase))
            .Select(static path => Path.GetDirectoryName(path)?.Replace('/', '\\') ?? string.Empty)
            .Where(static path => !string.IsNullOrWhiteSpace(path))
            .Where(static path => !ContainsAny(path, "\\tests\\", "\\test\\", "\\examples\\", "\\samples\\", "\\vendor\\", "\\third_party\\"))
            .Where(static path => !StartsWithAnyRootSegment(path, "tests", "test", "examples", "samples", "vendor", "third_party"))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(static path => path.Count(static ch => ch is '\\' or '/'))
            .ThenBy(static path => path, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static string ClassifyUnitZone(string rootPath)
    {
        var normalized = rootPath.Replace('/', '\\').Trim('\\');
        if (string.IsNullOrWhiteSpace(normalized) || string.Equals(normalized, ".", StringComparison.OrdinalIgnoreCase))
        {
            return "workspace";
        }

        if (normalized.StartsWith("apps\\", StringComparison.OrdinalIgnoreCase) ||
            normalized.StartsWith("app\\", StringComparison.OrdinalIgnoreCase) ||
            normalized.StartsWith("bin\\", StringComparison.OrdinalIgnoreCase) ||
            normalized.StartsWith("cmd\\", StringComparison.OrdinalIgnoreCase))
        {
            return "application";
        }

        if (normalized.StartsWith("crates\\", StringComparison.OrdinalIgnoreCase) ||
            normalized.StartsWith("packages\\", StringComparison.OrdinalIgnoreCase) ||
            normalized.StartsWith("libs\\", StringComparison.OrdinalIgnoreCase) ||
            normalized.StartsWith("lib\\", StringComparison.OrdinalIgnoreCase))
        {
            return "library";
        }

        if (normalized.StartsWith("tools\\", StringComparison.OrdinalIgnoreCase) ||
            normalized.StartsWith("scripts\\", StringComparison.OrdinalIgnoreCase) ||
            normalized.StartsWith("xtask\\", StringComparison.OrdinalIgnoreCase))
        {
            return "tooling";
        }

        if (normalized.StartsWith("examples\\", StringComparison.OrdinalIgnoreCase) ||
            normalized.StartsWith("samples\\", StringComparison.OrdinalIgnoreCase) ||
            normalized.StartsWith("demo\\", StringComparison.OrdinalIgnoreCase))
        {
            return "example";
        }

        if (normalized.StartsWith("test\\", StringComparison.OrdinalIgnoreCase) ||
            normalized.StartsWith("tests\\", StringComparison.OrdinalIgnoreCase))
        {
            return "test";
        }

        return "source";
    }

    private static bool IsUnsupportedUnitRoot(string rootPath)
    {
        var normalized = rootPath.Replace('/', '\\');
        return normalized.StartsWith(".github", StringComparison.OrdinalIgnoreCase) ||
               normalized.StartsWith(".storybook", StringComparison.OrdinalIgnoreCase) ||
               normalized.StartsWith("node_modules", StringComparison.OrdinalIgnoreCase) ||
               normalized.StartsWith("target", StringComparison.OrdinalIgnoreCase) ||
               normalized.StartsWith("vendor", StringComparison.OrdinalIgnoreCase) ||
               normalized.StartsWith("third_party", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsPathInsideUnit(string relativePath, string unitRoot)
    {
        var normalizedPath = relativePath.Replace('/', '\\');
        var normalizedRoot = unitRoot.Replace('/', '\\').Trim('\\');
        return normalizedRoot == "." ||
               string.Equals(normalizedPath, normalizedRoot, StringComparison.OrdinalIgnoreCase) ||
               normalizedPath.StartsWith(normalizedRoot + "\\", StringComparison.OrdinalIgnoreCase);
    }

    private static int UnitRootDepth(string rootPath)
    {
        return rootPath == "."
            ? 0
            : rootPath.Count(static ch => ch is '\\' or '/') + 1;
    }

    private static int UnitScore(WorkspaceEvidenceProjectUnit unit)
    {
        var score = unit.Manifests.Count * 20 + unit.EntryPoints.Count * 30;
        if (unit.RootPath == ".")
        {
            score += 60;
        }

        if (unit.Evidence.Contains("cargo_default_member", StringComparer.OrdinalIgnoreCase))
        {
            score += 50;
        }

        if (unit.Evidence.Contains("config_primary_unit", StringComparer.OrdinalIgnoreCase))
        {
            score += 120;
        }

        if (unit.RootPath.StartsWith("bin\\", StringComparison.OrdinalIgnoreCase))
        {
            score += 10;
        }

        if (unit.RootPath.StartsWith("crates\\", StringComparison.OrdinalIgnoreCase) ||
            unit.RootPath.StartsWith("packages\\", StringComparison.OrdinalIgnoreCase) ||
            unit.RootPath.StartsWith("apps\\", StringComparison.OrdinalIgnoreCase))
        {
            score += 20;
        }

        if (unit.RootPath.StartsWith("tools\\", StringComparison.OrdinalIgnoreCase) ||
            unit.RootPath.StartsWith("examples\\", StringComparison.OrdinalIgnoreCase) ||
            unit.RootPath.StartsWith("samples\\", StringComparison.OrdinalIgnoreCase) ||
            unit.RootPath.StartsWith("test\\", StringComparison.OrdinalIgnoreCase) ||
            unit.RootPath.StartsWith("tests\\", StringComparison.OrdinalIgnoreCase))
        {
            score -= 50;
        }

        return score;
    }

    private static int RunProfileScore(WorkspaceEvidenceRunProfile profile)
    {
        var score = 0;
        if (profile.Confidence == WorkspaceEvidenceConfidenceLevel.Confirmed)
        {
            score += 30;
        }

        if (profile.Evidence.Contains("cargo_default_member", StringComparer.OrdinalIgnoreCase))
        {
            score += 50;
        }

        if (profile.Evidence.Contains("config_primary_unit", StringComparer.OrdinalIgnoreCase))
        {
            score += 120;
        }

        score += profile.Kind switch
        {
            "test" => 25,
            "build" => 20,
            "run" => 15,
            "check" => 10,
            _ => 0
        };

        if (profile.WorkingDirectory == ".")
        {
            score += 20;
        }

        if (profile.WorkingDirectory.StartsWith("tools\\", StringComparison.OrdinalIgnoreCase) ||
            profile.WorkingDirectory.StartsWith("examples\\", StringComparison.OrdinalIgnoreCase) ||
            profile.WorkingDirectory.StartsWith("samples\\", StringComparison.OrdinalIgnoreCase) ||
            profile.WorkingDirectory.StartsWith("test\\", StringComparison.OrdinalIgnoreCase) ||
            profile.WorkingDirectory.StartsWith("tests\\", StringComparison.OrdinalIgnoreCase))
        {
            score -= 50;
        }

        return score;
    }

    private static string SanitizeUnitId(string rootPath)
    {
        var builder = new StringBuilder(rootPath.Length);
        foreach (var ch in rootPath)
        {
            builder.Append(char.IsLetterOrDigit(ch) ? char.ToLowerInvariant(ch) : '-');
        }

        return builder.ToString().Trim('-');
    }

    private static string QuoteCommandArgument(string value)
    {
        return value.Contains(' ', StringComparison.Ordinal) ? $"\"{value}\"" : value;
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

        if (ContainsAny(joinedPaths, "\\reverse\\", "\\reversing\\", "\\disasm\\", "\\disassembly\\", "\\dbg\\", "\\analysis\\") ||
            CountMatchingMarkers(combinedText, "debugger", "breakpoint", "opcode", "disassembly", "decompilation", "decompiled", "memory", "emulator", "assembler") >= 3)
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

    private static IReadOnlyCollection<string> BuildCargoDefaultMemberRoots(WorkspaceScanResult scanResult)
    {
        var roots = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var workspaceRoot = scanResult.State.WorkspaceRoot;

        foreach (var file in scanResult.RelevantFiles)
        {
            if (!string.Equals(Path.GetFileName(file), "Cargo.toml", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var manifestRelativeDirectory = Path.GetRelativePath(workspaceRoot, Path.GetDirectoryName(file) ?? workspaceRoot)
                .Replace('/', '\\');
            if (string.Equals(manifestRelativeDirectory, ".", StringComparison.Ordinal))
            {
                manifestRelativeDirectory = string.Empty;
            }

            var defaultMembers = ExtractCargoDefaultMembers(ReadBoundedLines(file, 120));
            foreach (var member in defaultMembers)
            {
                var normalizedMember = member.Replace('/', '\\').Trim('\\');
                if (string.IsNullOrWhiteSpace(normalizedMember))
                {
                    continue;
                }

                var root = string.IsNullOrWhiteSpace(manifestRelativeDirectory)
                    ? normalizedMember
                    : Path.Combine(manifestRelativeDirectory, normalizedMember).Replace('/', '\\');
                roots.Add(root.Trim('\\'));
            }
        }

        return roots;
    }

    private static IReadOnlyList<string> ExtractCargoDefaultMembers(IEnumerable<string> lines)
    {
        var text = string.Join("\n", lines);
        var markerIndex = text.IndexOf("default-members", StringComparison.OrdinalIgnoreCase);
        if (markerIndex < 0)
        {
            return Array.Empty<string>();
        }

        var openIndex = text.IndexOf('[', markerIndex);
        var closeIndex = openIndex < 0 ? -1 : text.IndexOf(']', openIndex);
        if (openIndex < 0 || closeIndex <= openIndex)
        {
            return Array.Empty<string>();
        }

        return text[(openIndex + 1)..closeIndex]
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(static value => value.Trim().Trim('"', '\''))
            .Where(static value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static IReadOnlyList<ManifestEntryHint> BuildManifestEntryHints(WorkspaceScanResult scanResult)
    {
        var hints = new List<ManifestEntryHint>();
        var workspaceRoot = scanResult.State.WorkspaceRoot;
        var relevant = scanResult.RelevantFiles
            .Select(path => Path.GetRelativePath(workspaceRoot, path).Replace('/', '\\'))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var file in scanResult.RelevantFiles)
        {
            var relativePath = Path.GetRelativePath(workspaceRoot, file).Replace('/', '\\');
            var fileName = Path.GetFileName(relativePath);
            if (string.Equals(fileName, "package.json", StringComparison.OrdinalIgnoreCase))
            {
                hints.AddRange(BuildPackageJsonEntryHints(workspaceRoot, relativePath, relevant));
            }
            else if (IsPythonManifest(fileName))
            {
                hints.AddRange(BuildPythonManifestEntryHints(workspaceRoot, relativePath, relevant));
            }
        }

        return hints
            .GroupBy(static hint => $"{hint.RelativePath}|{hint.Role}|{string.Join("|", hint.Evidence)}", StringComparer.OrdinalIgnoreCase)
            .Select(static group => group.First())
            .ToArray();
    }

    private static IEnumerable<ManifestEntryHint> BuildPackageJsonEntryHints(
        string workspaceRoot,
        string manifest,
        IReadOnlySet<string> relevant)
    {
        var fullPath = Path.Combine(workspaceRoot, manifest);
        if (!File.Exists(fullPath))
        {
            yield break;
        }

        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(File.ReadAllText(fullPath, Encoding.UTF8));
        }
        catch (JsonException)
        {
            yield break;
        }
        catch (IOException)
        {
            yield break;
        }
        catch (UnauthorizedAccessException)
        {
            yield break;
        }

        using (document)
        {
            if (document.RootElement.TryGetProperty("bin", out var bin))
            {
                foreach (var value in ExtractPackageJsonPathValues(bin))
                {
                    if (TryResolveManifestReferencedPath(workspaceRoot, manifest, value, relevant, out var entryPath))
                    {
                        yield return new ManifestEntryHint(
                            entryPath,
                            "cli",
                            WorkspaceEvidenceConfidenceLevel.Confirmed,
                            new[] { $"manifest:{manifest}", "package_bin", $"package_bin_path:{value}" });
                    }
                }
            }

            foreach (var property in new[] { "main", "module" })
            {
                if (document.RootElement.TryGetProperty(property, out var element) &&
                    element.ValueKind == JsonValueKind.String &&
                    TryResolveManifestReferencedPath(workspaceRoot, manifest, element.GetString(), relevant, out var entryPath))
                {
                    yield return new ManifestEntryHint(
                        entryPath,
                        "package-surface",
                        WorkspaceEvidenceConfidenceLevel.Likely,
                        new[] { $"manifest:{manifest}", $"package_{property}_hint", $"package_{property}_path:{element.GetString()}" });
                }
            }

            if (document.RootElement.TryGetProperty("exports", out var exports))
            {
                foreach (var value in ExtractPackageJsonPathValues(exports))
                {
                    if (TryResolveManifestReferencedPath(workspaceRoot, manifest, value, relevant, out var entryPath))
                    {
                        yield return new ManifestEntryHint(
                            entryPath,
                            "package-surface",
                            WorkspaceEvidenceConfidenceLevel.Likely,
                            new[] { $"manifest:{manifest}", "package_exports_hint", $"package_exports_path:{value}" });
                    }
                }
            }
        }
    }

    private static IEnumerable<string> ExtractPackageJsonPathValues(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.String)
        {
            var value = element.GetString();
            if (LooksLikeManifestPath(value))
            {
                yield return value!;
            }

            yield break;
        }

        if (element.ValueKind != JsonValueKind.Object)
        {
            yield break;
        }

        foreach (var property in element.EnumerateObject())
        {
            foreach (var value in ExtractPackageJsonPathValues(property.Value))
            {
                yield return value;
            }
        }
    }

    private static IEnumerable<ManifestEntryHint> BuildPythonManifestEntryHints(
        string workspaceRoot,
        string manifest,
        IReadOnlySet<string> relevant)
    {
        foreach (var script in ExtractPythonConsoleScripts(workspaceRoot, manifest))
        {
            if (TryResolvePythonTargetPath(workspaceRoot, manifest, script.Target, relevant, out var entryPath))
            {
                yield return new ManifestEntryHint(
                    entryPath,
                    "cli",
                    WorkspaceEvidenceConfidenceLevel.Confirmed,
                    new[] { $"manifest:{manifest}", $"python_console_script:{script.Name}", $"python_target:{script.Target}" });
            }
        }
    }

    private static void AddManifestUnitEvidence(string workspaceRoot, string manifest, ISet<string> evidence)
    {
        var fullPath = Path.Combine(workspaceRoot, manifest);
        if (!File.Exists(fullPath))
        {
            return;
        }

        var fileName = Path.GetFileName(manifest);
        if (string.Equals(fileName, "package.json", StringComparison.OrdinalIgnoreCase))
        {
            AddPackageJsonUnitEvidence(fullPath, evidence);
        }
        else if (IsPythonManifest(fileName))
        {
            foreach (var script in ExtractPythonConsoleScripts(workspaceRoot, manifest).Take(12))
            {
                evidence.Add($"python_console_script:{script.Name}");
            }

            if (ManifestHasPytestConfig(fullPath, fileName))
            {
                evidence.Add("pytest_config");
            }
        }
    }

    private static void AddPackageJsonUnitEvidence(string fullPath, ISet<string> evidence)
    {
        try
        {
            using var document = JsonDocument.Parse(File.ReadAllText(fullPath, Encoding.UTF8));
            if (document.RootElement.TryGetProperty("scripts", out var scripts) &&
                scripts.ValueKind == JsonValueKind.Object)
            {
                foreach (var script in scripts.EnumerateObject())
                {
                    var kind = ClassifyPackageScript(script.Name);
                    if (!string.IsNullOrWhiteSpace(kind))
                    {
                        evidence.Add($"package_script:{script.Name}");
                    }
                }
            }

            foreach (var property in new[] { "main", "module", "exports", "bin" })
            {
                if (document.RootElement.TryGetProperty(property, out _))
                {
                    evidence.Add($"package_{property}_hint");
                }
            }
        }
        catch (JsonException)
        {
            return;
        }
        catch (IOException)
        {
            return;
        }
        catch (UnauthorizedAccessException)
        {
            return;
        }
    }

    private static bool LooksLikeManifestPath(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var trimmed = value.Trim();
        return trimmed.StartsWith(".", StringComparison.Ordinal) ||
               trimmed.StartsWith("/", StringComparison.Ordinal) ||
               trimmed.Contains('/', StringComparison.Ordinal) ||
               trimmed.Contains('\\', StringComparison.Ordinal);
    }

    private static bool TryResolveManifestReferencedPath(
        string workspaceRoot,
        string manifest,
        string? reference,
        IReadOnlySet<string> relevant,
        out string relativePath)
    {
        relativePath = string.Empty;
        if (string.IsNullOrWhiteSpace(reference))
        {
            return false;
        }

        var trimmed = reference.Trim().Trim('"', '\'');
        if (trimmed.Length == 0 || trimmed == ".")
        {
            return false;
        }

        trimmed = trimmed.TrimStart('.', '/', '\\');
        var manifestRoot = Path.GetDirectoryName(manifest)?.Replace('/', '\\') ?? string.Empty;
        var candidates = new List<string>();
        AddManifestPathCandidate(candidates, manifestRoot, trimmed);
        if (Path.GetExtension(trimmed).Length == 0)
        {
            foreach (var extension in new[] { ".js", ".mjs", ".cjs", ".ts", ".tsx", ".jsx", ".py" })
            {
                AddManifestPathCandidate(candidates, manifestRoot, trimmed + extension);
            }

            foreach (var extension in new[] { ".js", ".mjs", ".cjs", ".ts", ".tsx", ".jsx" })
            {
                AddManifestPathCandidate(candidates, manifestRoot, Path.Combine(trimmed, "index" + extension));
            }
        }

        foreach (var candidate in candidates)
        {
            if (relevant.Contains(candidate))
            {
                relativePath = candidate;
                return true;
            }

            var fullPath = Path.Combine(workspaceRoot, candidate);
            if (File.Exists(fullPath))
            {
                relativePath = candidate;
                return true;
            }
        }

        return false;
    }

    private static void AddManifestPathCandidate(ICollection<string> candidates, string manifestRoot, string reference)
    {
        var normalized = reference.Replace('/', '\\').Trim('\\');
        var candidate = string.IsNullOrWhiteSpace(manifestRoot) || manifestRoot == "."
            ? normalized
            : Path.Combine(manifestRoot, normalized).Replace('/', '\\');
        if (!string.IsNullOrWhiteSpace(candidate))
        {
            candidates.Add(candidate);
        }
    }

    private static IReadOnlyList<(string Name, string Target)> ExtractPythonConsoleScripts(string workspaceRoot, string manifest)
    {
        var fullPath = Path.Combine(workspaceRoot, manifest);
        if (!File.Exists(fullPath))
        {
            return Array.Empty<(string Name, string Target)>();
        }

        var fileName = Path.GetFileName(manifest);
        var lines = ReadBoundedLines(fullPath, 240).ToArray();
        if (string.Equals(fileName, "pyproject.toml", StringComparison.OrdinalIgnoreCase))
        {
            return ExtractPyprojectScripts(lines);
        }

        if (string.Equals(fileName, "setup.cfg", StringComparison.OrdinalIgnoreCase))
        {
            return ExtractSetupCfgConsoleScripts(lines);
        }

        if (string.Equals(fileName, "setup.py", StringComparison.OrdinalIgnoreCase))
        {
            return ExtractSetupPyConsoleScripts(lines);
        }

        return Array.Empty<(string Name, string Target)>();
    }

    private static IReadOnlyList<(string Name, string Target)> ExtractPyprojectScripts(IEnumerable<string> lines)
    {
        var scripts = new List<(string Name, string Target)>();
        var inProjectScripts = false;
        foreach (var rawLine in lines)
        {
            var line = StripInlineComment(rawLine).Trim();
            if (line.StartsWith("[", StringComparison.Ordinal) && line.EndsWith("]", StringComparison.Ordinal))
            {
                inProjectScripts = string.Equals(line, "[project.scripts]", StringComparison.OrdinalIgnoreCase);
                continue;
            }

            if (inProjectScripts && TryParseNameValue(line, out var name, out var target) && target.Contains(':', StringComparison.Ordinal))
            {
                scripts.Add((name, target));
            }
        }

        return scripts
            .DistinctBy(static script => script.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static IReadOnlyList<(string Name, string Target)> ExtractSetupCfgConsoleScripts(IEnumerable<string> lines)
    {
        var scripts = new List<(string Name, string Target)>();
        var inEntryPoints = false;
        var inConsoleScripts = false;
        foreach (var rawLine in lines)
        {
            var line = StripInlineComment(rawLine).Trim();
            if (line.StartsWith("[", StringComparison.Ordinal) && line.EndsWith("]", StringComparison.Ordinal))
            {
                inEntryPoints = string.Equals(line, "[options.entry_points]", StringComparison.OrdinalIgnoreCase);
                inConsoleScripts = false;
                continue;
            }

            if (!inEntryPoints)
            {
                continue;
            }

            if (line.StartsWith("console_scripts", StringComparison.OrdinalIgnoreCase))
            {
                inConsoleScripts = true;
                if (line.Contains('=', StringComparison.Ordinal) &&
                    TryParseNameValue(line, out var name, out var target) &&
                    !string.Equals(name, "console_scripts", StringComparison.OrdinalIgnoreCase) &&
                    target.Contains(':', StringComparison.Ordinal))
                {
                    scripts.Add((name, target));
                }

                continue;
            }

            if (inConsoleScripts && TryParseNameValue(line, out var scriptName, out var scriptTarget) && scriptTarget.Contains(':', StringComparison.Ordinal))
            {
                scripts.Add((scriptName, scriptTarget));
            }
        }

        return scripts
            .DistinctBy(static script => script.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static IReadOnlyList<(string Name, string Target)> ExtractSetupPyConsoleScripts(IEnumerable<string> lines)
    {
        var scripts = new List<(string Name, string Target)>();
        var text = string.Join("\n", lines);
        if (!text.Contains("console_scripts", StringComparison.OrdinalIgnoreCase))
        {
            return scripts;
        }

        foreach (var value in ExtractQuotedStrings(text))
        {
            if (TryParseNameValue(value, out var name, out var target) && target.Contains(':', StringComparison.Ordinal))
            {
                scripts.Add((name, target));
            }
        }

        return scripts
            .DistinctBy(static script => script.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static IEnumerable<string> ExtractQuotedStrings(string text)
    {
        for (var index = 0; index < text.Length; index++)
        {
            var quote = text[index];
            if (quote is not ('"' or '\''))
            {
                continue;
            }

            var start = index + 1;
            var end = text.IndexOf(quote, start);
            if (end <= start)
            {
                continue;
            }

            yield return text[start..end];
            index = end;
        }
    }

    private static bool TryParseNameValue(string line, out string name, out string value)
    {
        name = string.Empty;
        value = string.Empty;
        var separatorIndex = line.IndexOf('=', StringComparison.Ordinal);
        if (separatorIndex <= 0 || separatorIndex >= line.Length - 1)
        {
            return false;
        }

        name = line[..separatorIndex].Trim().Trim('"', '\'');
        value = line[(separatorIndex + 1)..].Trim().Trim(',', '"', '\'');
        return !string.IsNullOrWhiteSpace(name) && !string.IsNullOrWhiteSpace(value);
    }

    private static string StripInlineComment(string line)
    {
        var commentIndex = line.IndexOf('#', StringComparison.Ordinal);
        return commentIndex >= 0 ? line[..commentIndex] : line;
    }

    private static bool TryResolvePythonTargetPath(
        string workspaceRoot,
        string manifest,
        string target,
        IReadOnlySet<string> relevant,
        out string relativePath)
    {
        relativePath = string.Empty;
        var module = target.Split(':', 2)[0].Trim();
        if (string.IsNullOrWhiteSpace(module))
        {
            return false;
        }

        var modulePath = module.Replace('.', '\\');
        var manifestRoot = Path.GetDirectoryName(manifest)?.Replace('/', '\\') ?? string.Empty;
        var candidates = new List<string>();
        AddManifestPathCandidate(candidates, manifestRoot, modulePath + ".py");
        AddManifestPathCandidate(candidates, manifestRoot, Path.Combine(modulePath, "__init__.py"));
        AddManifestPathCandidate(candidates, manifestRoot, Path.Combine("src", modulePath + ".py"));
        AddManifestPathCandidate(candidates, manifestRoot, Path.Combine("src", modulePath, "__init__.py"));

        foreach (var candidate in candidates.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (relevant.Contains(candidate))
            {
                relativePath = candidate;
                return true;
            }

            if (File.Exists(Path.Combine(workspaceRoot, candidate)))
            {
                relativePath = candidate;
                return true;
            }
        }

        return false;
    }

    private static bool ManifestHasPytestConfig(string fullPath, string fileName)
    {
        var lines = ReadBoundedLines(fullPath, 240).Select(static line => line.Trim()).ToArray();
        if (string.Equals(fileName, "pyproject.toml", StringComparison.OrdinalIgnoreCase))
        {
            return lines.Any(static line => line.Equals("[tool.pytest.ini_options]", StringComparison.OrdinalIgnoreCase));
        }

        if (string.Equals(fileName, "setup.cfg", StringComparison.OrdinalIgnoreCase))
        {
            return lines.Any(static line => line.Equals("[tool:pytest]", StringComparison.OrdinalIgnoreCase) ||
                                            line.Equals("[pytest]", StringComparison.OrdinalIgnoreCase));
        }

        if (string.Equals(fileName, "setup.py", StringComparison.OrdinalIgnoreCase))
        {
            return lines.Any(static line => line.Contains("pytest", StringComparison.OrdinalIgnoreCase));
        }

        return false;
    }

    private static IReadOnlyList<string> BuildEntryEvidence(
        WorkspaceScanResult scanResult,
        string relativePath,
        string role,
        IReadOnlyCollection<string> cargoDefaultMembers)
    {
        var evidence = new List<string> { $"entry_file_name:{Path.GetFileName(relativePath)}" };
        var normalized = relativePath.Replace('/', '\\');

        if (!string.Equals(role, "entry", StringComparison.OrdinalIgnoreCase))
        {
            evidence.Add($"role:{role}");
        }

        if (cargoDefaultMembers.Any(member =>
                normalized.StartsWith(member + "\\", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(normalized, member, StringComparison.OrdinalIgnoreCase)))
        {
            evidence.Add("cargo_default_member");
        }

        if (scanResult.State.Summary.SourceRoots.Any(root =>
                string.Equals(root, ".", StringComparison.OrdinalIgnoreCase) ||
                normalized.StartsWith(root.Replace('/', '\\') + "\\", StringComparison.OrdinalIgnoreCase)))
        {
            evidence.Add("source_root_overlap");
        }

        if (IsConventionalEntrypointLocation(normalized))
        {
            evidence.Add("conventional_entry_location");
        }

        if (IsWorkflowOrSupportEntryLocation(normalized))
        {
            evidence.Add("secondary_or_workflow_location");
        }

        return evidence;
    }

    private static int ScoreEntryCandidate(string relativePath, string role, IReadOnlyList<string> evidence)
    {
        var normalized = relativePath.Replace('/', '\\');
        var score = 0;

        if (evidence.Contains("cargo_default_member", StringComparer.OrdinalIgnoreCase))
        {
            score += 50;
        }

        if (evidence.Any(static item => item.StartsWith("package_bin", StringComparison.OrdinalIgnoreCase) ||
                                        item.StartsWith("python_console_script:", StringComparison.OrdinalIgnoreCase)))
        {
            score += 50;
        }

        if (evidence.Any(static item => item is "package_main_hint" or "package_module_hint" or "package_exports_hint"))
        {
            score += 10;
        }

        if (string.Equals(Path.GetFileNameWithoutExtension(normalized), "main", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(Path.GetFileNameWithoutExtension(normalized), "program", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(Path.GetFileNameWithoutExtension(normalized), "app", StringComparison.OrdinalIgnoreCase))
        {
            score += 40;
        }

        if (!string.Equals(role, "entry", StringComparison.OrdinalIgnoreCase))
        {
            score += 20;
        }

        if (evidence.Contains("source_root_overlap", StringComparer.OrdinalIgnoreCase))
        {
            score += 15;
        }

        if (evidence.Contains("conventional_entry_location", StringComparer.OrdinalIgnoreCase))
        {
            score += 15;
        }

        if (ContainsAny(normalized, "\\test\\", "\\tests\\", "\\examples\\", "\\samples\\", "\\demo\\"))
        {
            score -= 30;
        }

        if (ContainsAny(normalized, "\\.github\\", "\\.storybook\\", "\\xtask\\", "\\tools\\", "\\scripts\\"))
        {
            score -= 50;
        }

        if (ContainsAny(normalized, "\\vendor\\", "\\third_party\\", "\\node_modules\\"))
        {
            score -= 50;
        }

        score -= normalized.Split('\\', StringSplitOptions.RemoveEmptyEntries).Length;
        return score;
    }

    private static bool IsConventionalEntrypointLocation(string normalizedPath)
    {
        return string.Equals(normalizedPath, "main.rs", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(normalizedPath, "main.go", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(normalizedPath, "Program.cs", StringComparison.OrdinalIgnoreCase) ||
               normalizedPath.StartsWith("src\\", StringComparison.OrdinalIgnoreCase) ||
               normalizedPath.Contains("\\src\\", StringComparison.OrdinalIgnoreCase) ||
               normalizedPath.Contains("\\cmd\\", StringComparison.OrdinalIgnoreCase) ||
               normalizedPath.Contains("\\bin\\", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsWorkflowOrSupportEntryLocation(string normalizedPath)
    {
        return ContainsAny(
            normalizedPath,
            "\\.github\\",
            "\\.storybook\\",
            "\\xtask\\",
            "\\tools\\",
            "\\scripts\\",
            "\\test\\",
            "\\tests\\",
            "\\examples\\",
            "\\samples\\") ||
            StartsWithAnyRootSegment(
                normalizedPath,
                ".github",
                ".storybook",
                "xtask",
                "tools",
                "scripts",
                "test",
                "tests",
                "examples",
                "samples");
    }

    private static bool StartsWithAnyRootSegment(string normalizedPath, params string[] segments)
    {
        return segments.Any(segment =>
            string.Equals(normalizedPath, segment, StringComparison.OrdinalIgnoreCase) ||
            normalizedPath.StartsWith(segment + "\\", StringComparison.OrdinalIgnoreCase));
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

    private static string BuildEntryNote(string relativePath, int score, IReadOnlyList<string> evidence)
    {
        return $"Observed entry candidate at {relativePath}. score={score}; evidence={string.Join(", ", evidence)}.";
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
                    if (!TryResolveCodeReference(relativePath, reference, relevantMap.Keys, out var targetPath, out var resolution, out var matchCount))
                    {
                        if (IsLocalCodeReference(reference))
                        {
                            edges.Add(new WorkspaceEvidenceCodeEdge(
                                relativePath,
                                reference.Reference.Replace('/', '\\').Trim(),
                                reference.Kind,
                                $"Observed unresolved {reference.Kind} reference in '{relativePath}'.",
                                WorkspaceEvidenceEdgeResolution.Unresolved,
                                EvidenceMarker(
                                    "code_edge_candidate",
                                    relativePath,
                                    $"Observed unresolved {reference.Kind} reference in '{relativePath}'.",
                                    WorkspaceEvidenceConfidenceLevel.Unknown,
                                    scanResult.BudgetReport?.IsPartial == true,
                                    isBounded: true)));
                        }

                        continue;
                    }

                    var reason = resolution == WorkspaceEvidenceEdgeResolution.Ambiguous
                        ? $"Observed {reference.Kind} reference in '{relativePath}' with {matchCount} possible local targets."
                        : $"Observed {reference.Kind} reference in '{relativePath}'.";
                    edges.Add(new WorkspaceEvidenceCodeEdge(
                        relativePath,
                        targetPath,
                        reference.Kind,
                        reason,
                        resolution,
                        EvidenceMarker(
                            "code_edge_candidate",
                            relativePath,
                            reason,
                            resolution == WorkspaceEvidenceEdgeResolution.Resolved
                                ? WorkspaceEvidenceConfidenceLevel.Confirmed
                                : WorkspaceEvidenceConfidenceLevel.Likely,
                            scanResult.BudgetReport?.IsPartial == true,
                            isBounded: true)));
                }
            }
        }

        return edges
            .GroupBy(static edge => $"{edge.FromPath}|{edge.ToPath}|{edge.Kind}|{edge.Resolution}", StringComparer.OrdinalIgnoreCase)
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

        foreach (var profile in candidates.RunProfiles.Take(16))
        {
            annotations.Add(new WorkspaceEvidenceConfidenceAnnotation(
                "run_profile",
                profile.Id,
                profile.Confidence,
                "Derived from manifest/script evidence; command is discovered, not executed."));
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
               string.Equals(fileName, "setup.cfg", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(fileName, "setup.py", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(fileName, "Cargo.toml", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(fileName, "go.mod", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsPythonManifest(string fileName)
    {
        return string.Equals(fileName, "pyproject.toml", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(fileName, "setup.cfg", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(fileName, "setup.py", StringComparison.OrdinalIgnoreCase);
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
        out string targetPath,
        out WorkspaceEvidenceEdgeResolution resolution,
        out int matchCount)
    {
        targetPath = string.Empty;
        resolution = WorkspaceEvidenceEdgeResolution.Unresolved;
        matchCount = 0;
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

        var matches = relevantPaths
            .Where(path =>
                candidates.Contains(path, StringComparer.OrdinalIgnoreCase) ||
                candidates.Any(candidate =>
                    path.EndsWith(candidate, StringComparison.OrdinalIgnoreCase) ||
                    (!string.IsNullOrWhiteSpace(Path.GetFileNameWithoutExtension(candidate)) &&
                     path.Contains("\\" + Path.GetFileNameWithoutExtension(candidate) + "\\", StringComparison.OrdinalIgnoreCase))) ||
                path.EndsWith(normalizedReference, StringComparison.OrdinalIgnoreCase))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(static path => path, StringComparer.OrdinalIgnoreCase)
            .Take(3)
            .ToArray();
        if (matches.Length == 0)
        {
            return false;
        }

        targetPath = matches[0];
        matchCount = matches.Length;
        resolution = matches.Length > 1
            ? WorkspaceEvidenceEdgeResolution.Ambiguous
            : WorkspaceEvidenceEdgeResolution.Resolved;
        return true;
    }

    private static bool IsLocalCodeReference((string Kind, string Reference) reference)
    {
        var normalized = reference.Reference.Trim();
        return reference.Kind == "include" ||
               reference.Kind is "rust-mod" or "rust-use" ||
               normalized.StartsWith(".", StringComparison.Ordinal) ||
               normalized.StartsWith("/", StringComparison.Ordinal) ||
               normalized.StartsWith("\\", StringComparison.Ordinal);
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
        if (score >= 0.45)
        {
            return WorkspaceEvidenceConfidenceLevel.Likely;
        }

        return WorkspaceEvidenceConfidenceLevel.Unknown;
    }

    private static WorkspaceEvidenceMarker EvidenceMarker(
        string evidenceKind,
        string? sourcePath,
        string reason,
        WorkspaceEvidenceConfidenceLevel confidence,
        bool isPartial,
        bool isBounded)
    {
        return new WorkspaceEvidenceMarker(
            evidenceKind,
            string.IsNullOrWhiteSpace(sourcePath) ? null : sourcePath,
            string.IsNullOrWhiteSpace(reason) ? "Observed scanner evidence." : reason,
            confidence,
            isPartial,
            isBounded);
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
               string.Equals(fileName, "pyproject.toml", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(fileName, "setup.cfg", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(fileName, "setup.py", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(fileName, "Dockerfile", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(fileName, "Makefile", StringComparison.OrdinalIgnoreCase) ||
               fileName.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase) ||
               fileName.EndsWith(".sln", StringComparison.OrdinalIgnoreCase) ||
               fileName.EndsWith(".vcxproj", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsConfigLikeFileName(string fileName, string extension)
    {
        return string.Equals(fileName, ".editorconfig", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(fileName, ".env", StringComparison.OrdinalIgnoreCase) ||
               extension is ".env" or ".ini" or ".json" or ".toml" or ".yaml" or ".yml";
    }

    private static bool IsDocumentLikeExtension(string extension)
    {
        return extension is ".md" or ".pdf" or ".rst" or ".txt";
    }

    private static bool IsAssetLikeExtension(string extension)
    {
        return extension is ".bmp" or ".gif" or ".jpeg" or ".jpg" or ".png" or ".rar" or ".svg" or ".tar" or ".webp" or ".zip" or ".7z";
    }

    private static bool IsBinaryLikeExtension(string extension)
    {
        return extension is ".bin" or ".dll" or ".dylib" or ".exe" or ".so";
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
