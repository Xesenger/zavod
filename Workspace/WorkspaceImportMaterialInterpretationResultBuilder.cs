using System;
using System.Collections.Generic;
using System.Linq;

namespace zavod.Workspace;

public static class WorkspaceImportMaterialInterpretationResultBuilder
{
    public static WorkspaceImportMaterialInterpretationResult BuildEmpty(WorkspaceImportMaterialPreviewPacket packet)
    {
        ArgumentNullException.ThrowIfNull(packet);
        var interpretationMode = DetermineInterpretationMode(packet, Array.Empty<WorkspaceImportMaterialEntryPointInterpretation>());

        var materials = packet.Materials
            .Select(static material => new WorkspaceMaterialPreviewInterpretation(
                material.RelativePath,
                material.Kind,
                string.Empty,
                WorkspaceMaterialContextUsefulness.Unknown,
                WorkspaceMaterialTemporalStatus.Unknown,
                string.Empty,
                ContextOnly: true,
                WorkspaceEvidenceConfidenceLevel.Unknown))
            .ToArray();

        var result = new WorkspaceImportMaterialInterpretationResult(
            packet.WorkspaceRoot,
            packet.ImportKind,
            packet.SourceRoots,
            interpretationMode == ProjectInterpretationMode.SingleProject ? Array.Empty<string>() : BuildBoundedTopologyDetails(interpretationMode),
            Array.Empty<string>(),
            Array.Empty<string>(),
            interpretationMode == ProjectInterpretationMode.SingleProject
                ? Array.Empty<string>()
                : BuildBoundedTopologyConfidenceSlices(interpretationMode, packet.EvidencePack).Unknown,
            interpretationMode == ProjectInterpretationMode.SingleProject
                ? Array.Empty<string>()
                : BuildBoundedTopologyStageSignals(interpretationMode, packet.EvidencePack),
            Array.Empty<string>(),
            Array.Empty<string>(),
            Array.Empty<string>(),
            Array.Empty<string>(),
            Array.Empty<WorkspaceImportMaterialLayerInterpretation>(),
            Array.Empty<WorkspaceImportMaterialModuleInterpretation>(),
            Array.Empty<WorkspaceImportMaterialEntryPointInterpretation>(),
            interpretationMode == ProjectInterpretationMode.SingleProject
                ? new ArchitectureDiagramSpec(
                    "Project Architecture",
                    Array.Empty<ArchitectureDiagramNode>(),
                    Array.Empty<ArchitectureDiagramEdge>(),
                    Array.Empty<ArchitectureDiagramGroup>(),
                    Array.Empty<string>(),
                    new ArchitectureDiagramRenderHints("left-to-right", Array.Empty<string>(), ShowLegend: true))
                : BuildBoundedTopologyDiagramSpec(interpretationMode),
            materials,
            interpretationMode == ProjectInterpretationMode.SingleProject
                ? BuildImporterOwnedSummary(packet, Array.Empty<WorkspaceImportMaterialEntryPointInterpretation>(), Array.Empty<WorkspaceImportMaterialModuleInterpretation>(), Array.Empty<string>(), materials.Length)
                : BuildBoundedTopologySummary(interpretationMode))
        {
            InterpretationMode = interpretationMode
        };

        if (interpretationMode != ProjectInterpretationMode.SingleProject)
        {
            var confidenceSlices = BuildBoundedTopologyConfidenceSlices(interpretationMode, packet.EvidencePack);
            result = result with
            {
                ConfirmedSignals = confidenceSlices.Confirmed,
                LikelySignals = confidenceSlices.Likely,
                UnknownSignals = confidenceSlices.Unknown
            };
        }

        return result;
    }

    public static WorkspaceImportMaterialInterpretationResult BuildFromResponse(
        WorkspaceImportMaterialPreviewPacket packet,
        WorkspaceImportMaterialPromptResponse response)
    {
        ArgumentNullException.ThrowIfNull(packet);
        ArgumentNullException.ThrowIfNull(response);

        var byPath = response.Materials
            .GroupBy(static item => item.RelativePath, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(static group => group.Key, static group => group.First(), StringComparer.OrdinalIgnoreCase);

        var materials = packet.Materials
            .Select(material =>
            {
                if (!byPath.TryGetValue(material.RelativePath, out var item))
                {
                    return BuildFallbackMaterialInterpretation(material);
                }

                return new WorkspaceMaterialPreviewInterpretation(
                    material.RelativePath,
                    material.Kind,
                    SanitizeMaterialSummary(item.Summary),
                    item.PossibleUsefulness,
                    item.TemporalStatus,
                    item.StatusNote.Trim(),
                    ContextOnly: true,
                    ResolveMaterialConfidence(packet.EvidencePack, material.RelativePath, item.PossibleUsefulness));
            })
            .ToArray();

        var effectiveEntryPoints = response.EntryPoints.Count > 0
            ? response.EntryPoints
            : BuildFallbackEntryPoints(packet.EvidencePack);
        effectiveEntryPoints = NormalizeEntryPoints(packet.EvidencePack, effectiveEntryPoints);
        var interpretationMode = DetermineInterpretationMode(packet, effectiveEntryPoints);
        var effectiveModules = response.Modules.Count > 0
            ? response.Modules
            : BuildFallbackModules(packet.EvidencePack, effectiveEntryPoints);
        effectiveModules = NormalizeModules(packet.EvidencePack, effectiveModules, effectiveEntryPoints);
        var effectiveLayers = response.Layers.Count > 0
            ? response.Layers
            : BuildFallbackLayers(packet.EvidencePack, effectiveModules, effectiveEntryPoints);
        effectiveLayers = NormalizeLayers(packet.EvidencePack, effectiveLayers, effectiveModules, effectiveEntryPoints);
        var effectiveDetails = NormalizeNarrativeLines(packet.EvidencePack, response.Details, effectiveModules, effectiveEntryPoints);
        var effectiveDiagramSpec = response.DiagramSpec.Nodes.Count > 0
            ? NormalizeDiagramSpec(packet.EvidencePack, response.DiagramSpec, effectiveLayers, effectiveModules, effectiveEntryPoints, response.Summary)
            : BuildFallbackDiagramSpec(packet, response.Summary, effectiveLayers, effectiveModules, effectiveEntryPoints);
        var confidenceSlices = BuildFallbackConfidenceSlices(packet.EvidencePack, response, effectiveEntryPoints, effectiveModules);
        var summary = NormalizeSummary(packet, response.Summary, effectiveDetails, effectiveEntryPoints, effectiveModules, materials.Length);
        var effectiveCurrentSignals = NormalizeNarrativeLines(packet.EvidencePack, response.CurrentSignals, effectiveModules, effectiveEntryPoints);
        var effectivePlannedSignals = NormalizeNarrativeLines(packet.EvidencePack, response.PlannedSignals, effectiveModules, effectiveEntryPoints);
        var effectivePossiblyStaleSignals = NormalizeNarrativeLines(packet.EvidencePack, response.PossiblyStaleSignals, effectiveModules, effectiveEntryPoints);
        var effectiveConflicts = NormalizeNarrativeLines(packet.EvidencePack, response.Conflicts, effectiveModules, effectiveEntryPoints);
        var effectiveStageSignals = NormalizeStageSignals(packet.EvidencePack, response.StageSignals, effectiveModules, effectiveEntryPoints);

        if (RequiresBoundedTopologyProjection(interpretationMode))
        {
            effectiveDetails = BuildBoundedTopologyDetails(interpretationMode);
            effectiveLayers = Array.Empty<WorkspaceImportMaterialLayerInterpretation>();
            effectiveModules = Array.Empty<WorkspaceImportMaterialModuleInterpretation>();
            effectiveEntryPoints = interpretationMode is ProjectInterpretationMode.MultipleIndependentProjects or ProjectInterpretationMode.MaterialOnly
                ? Array.Empty<WorkspaceImportMaterialEntryPointInterpretation>()
                : effectiveEntryPoints.Take(3).ToArray();
            effectiveDiagramSpec = BuildBoundedTopologyDiagramSpec(interpretationMode);
            confidenceSlices = BuildBoundedTopologyConfidenceSlices(interpretationMode, packet.EvidencePack);
            summary = BuildBoundedTopologySummary(interpretationMode);
            effectiveCurrentSignals = Array.Empty<string>();
            effectivePlannedSignals = Array.Empty<string>();
            effectivePossiblyStaleSignals = Array.Empty<string>();
            effectiveConflicts = Array.Empty<string>();
            effectiveStageSignals = BuildBoundedTopologyStageSignals(interpretationMode, packet.EvidencePack);
        }

        return new WorkspaceImportMaterialInterpretationResult(
            packet.WorkspaceRoot,
            packet.ImportKind,
            packet.SourceRoots,
            effectiveDetails,
            confidenceSlices.Confirmed,
            confidenceSlices.Likely,
            confidenceSlices.Unknown,
            effectiveStageSignals,
            effectiveCurrentSignals,
            effectivePlannedSignals,
            effectivePossiblyStaleSignals,
            effectiveConflicts,
            effectiveLayers,
            effectiveModules,
            effectiveEntryPoints,
            effectiveDiagramSpec,
            materials,
            summary)
        {
            InterpretationMode = interpretationMode
        };
    }

    private static string NormalizeSummary(
        WorkspaceImportMaterialPreviewPacket packet,
        string responseSummary,
        IReadOnlyList<string> details,
        IReadOnlyList<WorkspaceImportMaterialEntryPointInterpretation> entryPoints,
        IReadOnlyList<WorkspaceImportMaterialModuleInterpretation> modules,
        int materialCount)
    {
        if (string.IsNullOrWhiteSpace(responseSummary))
        {
            return BuildCoarseSummary(packet, entryPoints, modules, materialCount);
        }

        var summary = responseSummary.Trim();
        if (NarrativeLineNeedsStrongSupport(packet.EvidencePack, summary, modules, entryPoints))
        {
            return BuildCoarseSummary(packet, entryPoints, modules, materialCount);
        }

        var projectionFacts = BuildProjectionFactSuffix(packet, entryPoints, modules);
        return string.IsNullOrWhiteSpace(projectionFacts)
            ? $"{summary} truth=context_only."
            : $"{summary} {projectionFacts} truth=context_only.";
    }

    private static string BuildProjectionFactSuffix(
        WorkspaceImportMaterialPreviewPacket packet,
        IReadOnlyList<WorkspaceImportMaterialEntryPointInterpretation> entryPoints,
        IReadOnlyList<WorkspaceImportMaterialModuleInterpretation> modules)
    {
        var parts = new List<string>();
        if (packet.EvidencePack is not null)
        {
            parts.Add($"scannerEntryCandidatesTotal={packet.EvidencePack.Candidates.EntryPoints.Count}");
            parts.Add($"scannerModuleCandidatesTotal={packet.EvidencePack.Candidates.ModuleCandidates.Count}");
        }

        if (entryPoints.Count > 0)
        {
            parts.Add($"displayedEntryCandidates={entryPoints.Count}");
            parts.Add($"selectedMainEntry={entryPoints[0].RelativePath}");
        }

        if (modules.Count > 0)
        {
            parts.Add($"displayedModules={modules.Count}");
        }

        return parts.Count == 0 ? string.Empty : $"projection=({string.Join(", ", parts)}).";
    }

    private static ProjectInterpretationMode DetermineInterpretationMode(
        WorkspaceImportMaterialPreviewPacket packet,
        IReadOnlyList<WorkspaceImportMaterialEntryPointInterpretation> entryPoints)
    {
        var pack = packet.EvidencePack;
        if (pack is null)
        {
            return ProjectInterpretationMode.SingleProject;
        }

        var topologyKind = pack.Topology.Kind;
        if (topologyKind.Equals("Container", StringComparison.OrdinalIgnoreCase))
        {
            return ProjectInterpretationMode.MultipleIndependentProjects;
        }

        if (topologyKind.Equals("MaterialOnly", StringComparison.OrdinalIgnoreCase))
        {
            return ProjectInterpretationMode.MaterialOnly;
        }

        if (topologyKind.Equals("Mixed", StringComparison.OrdinalIgnoreCase))
        {
            return pack.Topology.ReleaseOutputZones.Count > 0
                ? ProjectInterpretationMode.MixedSourceRelease
                : ProjectInterpretationMode.Ambiguous;
        }

        if (topologyKind.Equals("Decompilation", StringComparison.OrdinalIgnoreCase))
        {
            return ProjectInterpretationMode.Decompilation;
        }

        if (topologyKind.Equals("Legacy", StringComparison.OrdinalIgnoreCase))
        {
            return ProjectInterpretationMode.Legacy;
        }

        if (topologyKind.Equals("Ambiguous", StringComparison.OrdinalIgnoreCase))
        {
            return ProjectInterpretationMode.Ambiguous;
        }

        if (topologyKind.Equals("ReleaseBundle", StringComparison.OrdinalIgnoreCase))
        {
            return ProjectInterpretationMode.ReleaseBundle;
        }

        var hasNestedGitProjects = HasStructuralAnomaly(pack, "NESTED_GIT_PROJECTS");
        var hasMultipleSourceRoots = packet.SourceRoots.Count > 1 || HasStructuralAnomaly(pack, "MULTIPLE_SOURCE_ROOTS");
        var hasCompetingPrimaryEntries = HasCompetingPrimaryEntries(packet.SourceRoots, entryPoints);

        if (hasNestedGitProjects && hasMultipleSourceRoots)
        {
            return ProjectInterpretationMode.MultipleIndependentProjects;
        }

        if (hasNestedGitProjects || (hasMultipleSourceRoots && hasCompetingPrimaryEntries))
        {
            return ProjectInterpretationMode.AmbiguousContainer;
        }

        return ProjectInterpretationMode.SingleProject;
    }

    private static string[] NormalizeNarrativeLines(
        WorkspaceEvidencePack? pack,
        IReadOnlyList<string> lines,
        IReadOnlyList<WorkspaceImportMaterialModuleInterpretation> modules,
        IReadOnlyList<WorkspaceImportMaterialEntryPointInterpretation> entryPoints)
    {
        return lines
            .Where(static line => !string.IsNullOrWhiteSpace(line))
            .Select(static line => line.Trim())
            .Where(line => !NarrativeLineNeedsStrongSupport(pack, line, modules, entryPoints))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
    }

    private static string[] NormalizeStageSignals(
        WorkspaceEvidencePack? pack,
        IReadOnlyList<string> lines,
        IReadOnlyList<WorkspaceImportMaterialModuleInterpretation> modules,
        IReadOnlyList<WorkspaceImportMaterialEntryPointInterpretation> entryPoints)
    {
        var normalized = NormalizeNarrativeLines(pack, lines, modules, entryPoints);
        if (normalized.Length > 0 || pack is null || lines.Count == 0)
        {
            return normalized;
        }

        var hasStageEvidence = pack.Signals.Any(static signal =>
            string.Equals(signal.Category, "stage", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(signal.Category, "temporal", StringComparison.OrdinalIgnoreCase));
        return hasStageEvidence
            ? new[] { "Cold evidence does not confirm a specific delivery stage yet." }
            : Array.Empty<string>();
    }

    private static string[] BuildContainerStageSignals(WorkspaceEvidencePack? pack)
    {
        return HasStageEvidence(pack)
            ? new[] { "Cold evidence does not confirm a single shared delivery stage yet." }
            : Array.Empty<string>();
    }

    private static string[] BuildBoundedTopologyStageSignals(ProjectInterpretationMode mode, WorkspaceEvidencePack? pack)
    {
        if (!HasStageEvidence(pack))
        {
            return Array.Empty<string>();
        }

        return mode switch
        {
            ProjectInterpretationMode.MaterialOnly => new[] { "Cold evidence does not confirm a source project delivery stage for material-only input." },
            ProjectInterpretationMode.MixedSourceRelease => new[] { "Cold evidence does not confirm a single delivery stage across active source and release/output zones." },
            ProjectInterpretationMode.Decompilation => new[] { "Cold evidence does not confirm a normal application delivery stage for decompilation topology." },
            ProjectInterpretationMode.Legacy => new[] { "Cold evidence does not confirm a normal application delivery stage for legacy low-level topology." },
            ProjectInterpretationMode.Ambiguous => new[] { "Cold evidence does not confirm a single delivery stage for ambiguous topology." },
            ProjectInterpretationMode.ReleaseBundle => new[] { "Cold evidence does not confirm a source project delivery stage for release bundle topology." },
            _ => BuildContainerStageSignals(pack)
        };
    }

    private static bool RequiresBoundedTopologyProjection(ProjectInterpretationMode mode)
    {
        return mode != ProjectInterpretationMode.SingleProject &&
               mode != ProjectInterpretationMode.Ambiguous;
    }

    private static (string[] Confirmed, string[] Likely, string[] Unknown) BuildFallbackConfidenceSlices(
        WorkspaceEvidencePack? pack,
        WorkspaceImportMaterialPromptResponse response,
        IReadOnlyList<WorkspaceImportMaterialEntryPointInterpretation> entryPoints,
        IReadOnlyList<WorkspaceImportMaterialModuleInterpretation> modules)
    {
        static string[] NormalizeConfidenceLines(
            WorkspaceEvidencePack? normalizedPack,
            IReadOnlyList<string> lines,
            IReadOnlyList<WorkspaceImportMaterialModuleInterpretation> normalizedModules,
            IReadOnlyList<WorkspaceImportMaterialEntryPointInterpretation> normalizedEntryPoints)
        {
            return lines
                .Where(static item => !string.IsNullOrWhiteSpace(item))
                .Select(static item => item.Trim())
                .Where(line => !NarrativeLineNeedsStrongSupport(normalizedPack, line, normalizedModules, normalizedEntryPoints))
                .Distinct(StringComparer.Ordinal)
                .ToArray();
        }

        if (response.ConfirmedSignals.Count > 0 || response.LikelySignals.Count > 0 || response.UnknownSignals.Count > 0)
        {
            var confirmed = NormalizeConfidenceLines(pack, response.ConfirmedSignals, modules, entryPoints);
            var likely = NormalizeConfidenceLines(pack, response.LikelySignals, modules, entryPoints);
            var unknown = response.UnknownSignals
                .Where(static item => !string.IsNullOrWhiteSpace(item))
                .Select(static item => item.Trim())
                .Distinct(StringComparer.Ordinal)
                .ToArray();

            var downgradedUnknown = response.ConfirmedSignals
                .Concat(response.LikelySignals)
                .Where(static item => !string.IsNullOrWhiteSpace(item))
                .Select(static item => item.Trim())
                .Where(line => NarrativeLineNeedsStrongSupport(pack, line, modules, entryPoints))
                .Distinct(StringComparer.Ordinal)
                .ToArray();

            return (
                confirmed,
                likely,
                unknown.Concat(downgradedUnknown).Distinct(StringComparer.Ordinal).ToArray());
        }

        if (pack is null)
        {
            return (Array.Empty<string>(), Array.Empty<string>(), Array.Empty<string>());
        }

        var fallbackConfirmed = pack.SignalScores
            .Where(static score => score.Score >= 0.75)
            .Take(3)
            .Select(static score => $"Cold evidence confirms {score.Signal} ({score.Score:0.00}).")
            .ToArray();
        var fallbackLikely = pack.SignalScores
            .Where(static score => score.Score >= 0.45 && score.Score < 0.75)
            .Take(3)
            .Select(static score => $"Cold evidence likely supports {score.Signal} ({score.Score:0.00}).")
            .ToArray();

        var fallbackUnknown = new List<string>();
        if (modules.Count == 0)
        {
            fallbackUnknown.Add("Cold evidence is too weak to confirm stable subsystem boundaries.");
        }

        if (entryPoints.Count == 0)
        {
            fallbackUnknown.Add("Cold evidence did not confirm a primary executable or bootstrap entry point.");
        }

        return (fallbackConfirmed, fallbackLikely, fallbackUnknown.ToArray());
    }

    private static (string[] Confirmed, string[] Likely, string[] Unknown) BuildContainerConfidenceSlices(
        ProjectInterpretationMode mode,
        WorkspaceEvidencePack? pack)
    {
        return BuildBoundedTopologyConfidenceSlices(mode, pack);
    }

    private static (string[] Confirmed, string[] Likely, string[] Unknown) BuildBoundedTopologyConfidenceSlices(
        ProjectInterpretationMode mode,
        WorkspaceEvidencePack? pack)
    {
        if (mode == ProjectInterpretationMode.MultipleIndependentProjects)
        {
            return (
                new[] { "Cold evidence confirms multiple nested project roots inside the scanned folder." },
                Array.Empty<string>(),
                new[] { "A single shared project architecture is not confirmed across the container." });
        }

        return mode switch
        {
            ProjectInterpretationMode.MaterialOnly => (
                Array.Empty<string>(),
                Array.Empty<string>(),
                new[] { "Cold evidence did not confirm source project structure; imported content remains material-only." }),
            ProjectInterpretationMode.MixedSourceRelease => (
                Array.Empty<string>(),
                new[] { "Cold evidence shows active source and release/output zones in the same scanned folder." },
                new[] { "A single primary application identity is not confirmed across source and release/output zones." }),
            ProjectInterpretationMode.Decompilation => (
                Array.Empty<string>(),
                new[] { "Cold evidence shows decompilation/reverse-engineering topology." },
                new[] { "Normal application architecture and delivery assumptions are not confirmed." }),
            ProjectInterpretationMode.Legacy => (
                Array.Empty<string>(),
                new[] { "Cold evidence shows legacy or low-level source topology." },
                new[] { "Normal application architecture and delivery assumptions are not confirmed." }),
            ProjectInterpretationMode.ReleaseBundle => (
                Array.Empty<string>(),
                new[] { "Cold evidence shows release/output payload without confirmed active source." },
                new[] { "A source project identity is not confirmed for this import target." }),
            _ => (
                Array.Empty<string>(),
                new[] { "Cold evidence suggests the scanned folder may contain multiple loosely related project roots." },
                new[] { "A single shared project narrative is not confirmed yet." })
        };
    }

    private static WorkspaceImportMaterialEntryPointInterpretation[] BuildFallbackEntryPoints(WorkspaceEvidencePack? pack)
    {
        if (pack is null)
        {
            return Array.Empty<WorkspaceImportMaterialEntryPointInterpretation>();
        }

        return pack.Candidates.EntryPoints
            .OrderByDescending(static entry => entry.Score)
            .ThenByDescending(static entry => entry.EvidenceMarker?.Confidence ?? WorkspaceEvidenceConfidenceLevel.Unknown)
            .ThenBy(static entry => entry.RelativePath.Count(static ch => ch == '\\' || ch == '/'))
            .ThenBy(static entry => entry.RelativePath, StringComparer.OrdinalIgnoreCase)
            .Take(6)
            .Select(entry => new WorkspaceImportMaterialEntryPointInterpretation(
                entry.RelativePath,
                entry.Role,
                entry.Note,
                ResolveConfidence(pack, "entry_point", entry.RelativePath)))
            .ToArray();
    }

    private static WorkspaceImportMaterialModuleInterpretation[] BuildFallbackModules(
        WorkspaceEvidencePack? pack,
        IReadOnlyList<WorkspaceImportMaterialEntryPointInterpretation> entryPoints)
    {
        if (pack is null)
        {
            return Array.Empty<WorkspaceImportMaterialModuleInterpretation>();
        }

        return pack.Candidates.ModuleCandidates
            .Where(module => ScoreModuleCandidate(pack, module, entryPoints) >= 2)
            .OrderByDescending(module => ScoreModuleCandidate(pack, module, entryPoints))
            .ThenBy(static module => module.Name, StringComparer.OrdinalIgnoreCase)
            .Take(6)
            .Select(module => new WorkspaceImportMaterialModuleInterpretation(
                module.Name,
                module.Role,
                module.EvidenceNote,
                ResolveConfidence(pack, "module", module.Name)))
            .ToArray();
    }

    private static WorkspaceImportMaterialEntryPointInterpretation[] NormalizeEntryPoints(
        WorkspaceEvidencePack? pack,
        IReadOnlyList<WorkspaceImportMaterialEntryPointInterpretation> entryPoints)
    {
        if (pack is null)
        {
            return entryPoints.ToArray();
        }

        var normalized = entryPoints
            .Select(entry =>
            {
                var scannerConfidence = ResolveConfidence(pack, "entry_point", entry.RelativePath);
                var confidence = scannerConfidence == WorkspaceEvidenceConfidenceLevel.Unknown
                    ? entry.Confidence
                    : scannerConfidence;
                return entry with { Confidence = confidence };
            })
            .Concat(BuildFallbackEntryPoints(pack))
            .GroupBy(static entry => entry.RelativePath, StringComparer.OrdinalIgnoreCase)
            .Select(group => SelectEntryPointProjection(pack, group))
            .OrderByDescending(entry => ScoreScannerEntryPoint(pack, entry))
            .ThenByDescending(static entry => entry.Confidence)
            .ThenBy(static entry => entry.RelativePath, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var strongestScore = normalized.Length == 0 ? int.MinValue : ScoreScannerEntryPoint(pack, normalized[0]);
        var hasConfirmedScannerMain = pack.Candidates.EntryPoints.Any(IsExecutableOrCodeMainEntry);
        return normalized
            .Where(entry =>
            {
                if (hasConfirmedScannerMain && !HasDirectScannerEntryEvidence(pack, entry))
                {
                    return false;
                }

                var score = ScoreScannerEntryPoint(pack, entry);
                if (strongestScore >= 10 && score <= 0)
                {
                    return false;
                }

                if (ClassifyEntryPath(entry.RelativePath).IsUnderTestLikeSubtree &&
                    entry.Confidence < WorkspaceEvidenceConfidenceLevel.Confirmed)
                {
                    return false;
                }

                return true;
            })
            .Take(6)
            .ToArray();
    }

    private static bool HasDirectScannerEntryEvidence(
        WorkspaceEvidencePack pack,
        WorkspaceImportMaterialEntryPointInterpretation entry)
    {
        return FindScannerEntryPoint(pack, entry.RelativePath) is not null;
    }

    private static bool IsExecutableOrCodeMainEntry(WorkspaceEvidenceEntryPoint entry)
    {
        if (entry.EvidenceMarker?.Confidence != WorkspaceEvidenceConfidenceLevel.Confirmed)
        {
            return false;
        }

        return IsExecutableOrCodeMainRole(entry.Role);
    }

    private static bool IsExecutableOrCodeMainRole(string role)
    {
        return role.Equals("main", StringComparison.OrdinalIgnoreCase) ||
               role.Equals("entry", StringComparison.OrdinalIgnoreCase) ||
               role.Equals("cli", StringComparison.OrdinalIgnoreCase) ||
               role.Equals("service", StringComparison.OrdinalIgnoreCase) ||
               role.Equals("bootstrap", StringComparison.OrdinalIgnoreCase) ||
               role.Equals("ui", StringComparison.OrdinalIgnoreCase);
    }

    private static WorkspaceImportMaterialEntryPointInterpretation SelectEntryPointProjection(
        WorkspaceEvidencePack pack,
        IEnumerable<WorkspaceImportMaterialEntryPointInterpretation> entries)
    {
        var ordered = entries
            .OrderByDescending(entry => ScoreScannerEntryPoint(pack, entry))
            .ThenByDescending(static entry => entry.Confidence)
            .ThenBy(static entry => entry.RelativePath, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var selected = ordered[0];
        var scannerEntry = FindScannerEntryPoint(pack, selected.RelativePath);
        if (scannerEntry is null)
        {
            return selected;
        }

        return selected with
        {
            Role = scannerEntry.Role,
            Note = scannerEntry.Note,
            Confidence = ResolveConfidence(pack, "entry_point", scannerEntry.RelativePath)
        };
    }

    private static WorkspaceImportMaterialModuleInterpretation[] NormalizeModules(
        WorkspaceEvidencePack? pack,
        IReadOnlyList<WorkspaceImportMaterialModuleInterpretation> modules,
        IReadOnlyList<WorkspaceImportMaterialEntryPointInterpretation> entryPoints)
    {
        if (pack is null)
        {
            return modules.ToArray();
        }

        return modules
            .Select(module =>
            {
                var scannerConfidence = ResolveConfidence(pack, "module", module.Name);
                var confidence = scannerConfidence == WorkspaceEvidenceConfidenceLevel.Unknown
                    ? module.Confidence
                    : scannerConfidence;
                return module with { Confidence = confidence };
            })
            .Where(module =>
            {
                var score = ScoreModuleCandidate(
                    pack,
                    new WorkspaceEvidenceModule(module.Name, "root", module.Role, module.EvidenceNote),
                    entryPoints);
                if (IsGenericHumanFacingModule(module.Name) && module.Confidence == WorkspaceEvidenceConfidenceLevel.Unknown && score < 3)
                {
                    return false;
                }

                if (IsBroadHumanFacingModule(module.Name) && score < 4)
                {
                    return false;
                }

                if (RequiresBroadTokenSupport(module.Name) &&
                    !HasStrongSupportForBroadToken(pack, module.Name.ToLowerInvariant(), modules, entryPoints))
                {
                    return false;
                }

                return true;
            })
            .DistinctBy(static module => module.Name, StringComparer.OrdinalIgnoreCase)
            .Take(6)
            .ToArray();
    }

    private static WorkspaceImportMaterialLayerInterpretation[] NormalizeLayers(
        WorkspaceEvidencePack? pack,
        IReadOnlyList<WorkspaceImportMaterialLayerInterpretation> layers,
        IReadOnlyList<WorkspaceImportMaterialModuleInterpretation> modules,
        IReadOnlyList<WorkspaceImportMaterialEntryPointInterpretation> entryPoints)
    {
        if (pack is null)
        {
            return layers.ToArray();
        }

        return layers
            .Select(layer =>
            {
                var confidence = layer.Confidence == WorkspaceEvidenceConfidenceLevel.Unknown
                    ? ResolveConfidence(pack, "layer", layer.Name)
                    : layer.Confidence;
                return layer with { Confidence = confidence };
            })
            .Where(layer =>
            {
                if (!IsLayerSupportedByColdEvidence(pack, layer.Name, modules, entryPoints))
                {
                    return false;
                }

                if (RequiresBroadTokenSupport(layer.Name) &&
                    !HasStrongSupportForBroadToken(pack, layer.Name.ToLowerInvariant(), modules, entryPoints))
                {
                    return false;
                }

                if (!IsRecognizedHumanLayerName(layer.Name) &&
                    layer.Confidence < WorkspaceEvidenceConfidenceLevel.Likely)
                {
                    return false;
                }

                return true;
            })
            .DistinctBy(static layer => layer.Name, StringComparer.OrdinalIgnoreCase)
            .Take(6)
            .ToArray();
    }

    private static WorkspaceImportMaterialLayerInterpretation[] BuildFallbackLayers(
        WorkspaceEvidencePack? pack,
        IReadOnlyList<WorkspaceImportMaterialModuleInterpretation> modules,
        IReadOnlyList<WorkspaceImportMaterialEntryPointInterpretation> entryPoints)
    {
        if (pack is null)
        {
            return Array.Empty<WorkspaceImportMaterialLayerInterpretation>();
        }

        var layers = new List<WorkspaceImportMaterialLayerInterpretation>();
        if (pack.ProjectProfile.SourceRoots.Count > 0)
        {
            layers.Add(new WorkspaceImportMaterialLayerInterpretation(
                "root",
                "Observed project root and primary workspace boundary.",
                "Derived in importer adapter from source roots, not from scanner narrative.",
                ResolveConfidence(pack, "layer", "root")));
        }

        AddLayerIfSupported(layers, pack, modules, entryPoints, "Core", "Central orchestration, registry, and project logic.", "Importer adapter synthesized Core layer from cold candidates.", minSupport: 3);
        AddLayerIfSupported(layers, pack, modules, entryPoints, "Runtime", "Execution, process control, and debugger/runtime handoff.", "Importer adapter synthesized Runtime layer from cold candidates.", minSupport: 4);
        AddLayerIfSupported(layers, pack, modules, entryPoints, "UI", "User-facing interface and presentation surface.", "Importer adapter synthesized UI layer from cold candidates.", RequireUiSupport: true, minSupport: 4);
        AddLayerIfSupported(layers, pack, modules, entryPoints, "CLI", "Command-line and operator-facing entry surface.", "Importer adapter synthesized CLI layer from cold candidates.", requireCliSupport: true, minSupport: 3);
        AddLayerIfSupported(layers, pack, modules, entryPoints, "Service", "Service or remote integration surface.", "Importer adapter synthesized Service layer from cold candidates.", requireServiceSupport: true, minSupport: 5);
        AddLayerIfSupported(layers, pack, modules, entryPoints, "Mod Platform", "Addon or extension preparation surface.", "Importer adapter synthesized Mod Platform layer from cold candidates.", requireModSupport: true, minSupport: 5);

        return layers
            .GroupBy(static layer => layer.Name, StringComparer.OrdinalIgnoreCase)
            .Select(static group => group.First())
            .Take(6)
            .ToArray();
    }

    private static ArchitectureDiagramSpec BuildFallbackDiagramSpec(
        WorkspaceImportMaterialPreviewPacket packet,
        string summary,
        IReadOnlyList<WorkspaceImportMaterialLayerInterpretation> layers,
        IReadOnlyList<WorkspaceImportMaterialModuleInterpretation> modules,
        IReadOnlyList<WorkspaceImportMaterialEntryPointInterpretation> entryPoints)
    {
        var nodes = new System.Collections.Generic.List<ArchitectureDiagramNode>();
        var groups = new System.Collections.Generic.List<ArchitectureDiagramGroup>();
        var edges = new System.Collections.Generic.List<ArchitectureDiagramEdge>();
        var notes = new System.Collections.Generic.List<string>();
        var nodeIds = new System.Collections.Generic.List<string>();

        foreach (var layer in layers.Take(4))
        {
            var nodeId = NormalizeId(layer.Name);
            nodeIds.Add(nodeId);
            nodes.Add(new ArchitectureDiagramNode(nodeId, layer.Name, "layer", "layers", layer.Confidence));
        }

        if (nodes.Count > 0)
        {
            groups.Add(new ArchitectureDiagramGroup("layers", "Observed Layers", nodeIds.ToArray()));
        }

        var allowModuleNodes = layers.Count > 1 && modules.Count >= 2;
        var moduleNodeIds = new System.Collections.Generic.List<string>();
        foreach (var module in allowModuleNodes ? modules.Take(6) : Array.Empty<WorkspaceImportMaterialModuleInterpretation>())
        {
            var nodeId = NormalizeId($"module-{module.Name}");
            moduleNodeIds.Add(nodeId);
            nodes.Add(new ArchitectureDiagramNode(nodeId, module.Name, "module", "modules", module.Confidence));

            var targetLayerName = ResolveStructuredLayerName(packet.EvidencePack, module, layers);
            var targetLayerId = string.IsNullOrWhiteSpace(targetLayerName)
                ? null
                : nodes
                    .FirstOrDefault(node => string.Equals(node.Label, targetLayerName, StringComparison.OrdinalIgnoreCase) &&
                                            string.Equals(node.GroupId, "layers", StringComparison.OrdinalIgnoreCase))
                    ?.Id;
            if (allowModuleNodes && !string.IsNullOrWhiteSpace(targetLayerId))
            {
                edges.Add(new ArchitectureDiagramEdge(nodeId, targetLayerId, module.Role, "module", module.Confidence));
            }
        }

        var entryNodeIds = new System.Collections.Generic.List<string>();
        foreach (var entryPoint in entryPoints.Take(4))
        {
            var nodeId = NormalizeId($"entry-{entryPoint.RelativePath}");
            entryNodeIds.Add(nodeId);
            nodes.Add(new ArchitectureDiagramNode(nodeId, entryPoint.RelativePath, entryPoint.Role, "entries", entryPoint.Confidence));
            if (nodeIds.Count == 1 && entryNodeIds.Count == 1)
            {
                edges.Add(new ArchitectureDiagramEdge(nodeId, nodeIds[0], entryPoint.Role, "entry", entryPoint.Confidence));
            }
        }

        if (entryNodeIds.Count > 0)
        {
            groups.Add(new ArchitectureDiagramGroup("entries", "Observed Entry Points", entryNodeIds.ToArray()));
        }

        if (moduleNodeIds.Count > 0)
        {
            groups.Add(new ArchitectureDiagramGroup("modules", "Observed Modules", moduleNodeIds.ToArray()));
        }

        if (!string.IsNullOrWhiteSpace(summary) &&
            !NarrativeLineNeedsStrongSupport(packet.EvidencePack, summary, modules, entryPoints))
        {
            notes.Add(summary.Trim());
        }

        if (nodes.Count == 0)
        {
            nodes.Add(new ArchitectureDiagramNode("workspace", "Workspace", "root"));
            notes.Add("Diagram fell back to workspace-level architecture because no explicit diagram lines were returned.");
        }

        return new ArchitectureDiagramSpec(
            BuildCoarseDiagramTitle(packet, summary, modules, entryPoints),
            nodes,
            edges,
            groups,
            notes,
            new ArchitectureDiagramRenderHints("left-to-right", nodeIds.ToArray(), ShowLegend: true));
    }

    private static ArchitectureDiagramSpec BuildContainerDiagramSpec(ProjectInterpretationMode mode)
    {
        return BuildBoundedTopologyDiagramSpec(mode);
    }

    private static ArchitectureDiagramSpec BuildBoundedTopologyDiagramSpec(ProjectInterpretationMode mode)
    {
        var notes = mode switch
        {
            ProjectInterpretationMode.MultipleIndependentProjects => new[] { "Unified architecture diagram suppressed because multiple independent project roots were detected." },
            ProjectInterpretationMode.MaterialOnly => new[] { "Architecture diagram suppressed because the import target is material-only." },
            ProjectInterpretationMode.MixedSourceRelease => new[] { "Architecture diagram suppressed because active source and release/output zones must remain separated." },
            ProjectInterpretationMode.Decompilation => new[] { "Architecture diagram suppressed because decompilation topology should not be normalized into a standard application diagram." },
            ProjectInterpretationMode.Legacy => new[] { "Architecture diagram suppressed because legacy low-level topology should not be normalized into a standard application diagram." },
            ProjectInterpretationMode.ReleaseBundle => new[] { "Architecture diagram suppressed because release/output payload does not confirm active source architecture." },
            _ => new[] { "Unified architecture diagram suppressed because the scanned folder remains ambiguous." }
        };
        var title = mode == ProjectInterpretationMode.MultipleIndependentProjects
            ? "Project Container"
            : $"Project {mode}";
        return new ArchitectureDiagramSpec(
            title,
            Array.Empty<ArchitectureDiagramNode>(),
            Array.Empty<ArchitectureDiagramEdge>(),
            Array.Empty<ArchitectureDiagramGroup>(),
            notes,
            new ArchitectureDiagramRenderHints("left-to-right", Array.Empty<string>(), ShowLegend: true));
    }

    private static ArchitectureDiagramSpec NormalizeDiagramSpec(
        WorkspaceEvidencePack? pack,
        ArchitectureDiagramSpec diagramSpec,
        IReadOnlyList<WorkspaceImportMaterialLayerInterpretation> layers,
        IReadOnlyList<WorkspaceImportMaterialModuleInterpretation> modules,
        IReadOnlyList<WorkspaceImportMaterialEntryPointInterpretation> entryPoints,
        string summary)
    {
        var nodes = diagramSpec.Nodes
            .Where(node => IsDiagramNodeSupported(node, layers, modules, entryPoints))
            .ToList();
        var nodeIds = nodes
            .Select(static node => node.Id)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var edges = diagramSpec.Edges
            .Where(edge =>
                nodeIds.Contains(edge.From) &&
                nodeIds.Contains(edge.To) &&
                !NarrativeLineNeedsStrongSupport(pack, edge.Label, modules, entryPoints))
            .ToList();
        var groups = diagramSpec.Groups
            .Select(group => group with
            {
                Members = group.Members
                    .Where(member => nodeIds.Contains(member))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToArray()
            })
            .Where(group => group.Members.Count > 0 && !NarrativeLineNeedsStrongSupport(pack, group.Label, modules, entryPoints))
            .ToList();
        var notes = diagramSpec.Notes
            .Where(static note => !string.IsNullOrWhiteSpace(note))
            .Select(static note => note.Trim())
            .Where(note => !NarrativeLineNeedsStrongSupport(pack, note, modules, entryPoints))
            .Distinct(StringComparer.Ordinal)
            .ToList();

        if (notes.Count == 0 &&
            !string.IsNullOrWhiteSpace(summary) &&
            !NarrativeLineNeedsStrongSupport(pack, summary, modules, entryPoints))
        {
            notes.Add(summary.Trim());
        }

        var layerNodeIds = nodes
            .Where(static node => string.Equals(node.Kind, "layer", StringComparison.OrdinalIgnoreCase) ||
                                  string.Equals(node.GroupId, "layers", StringComparison.OrdinalIgnoreCase))
            .Select(static node => node.Id)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        var entryNodeIds = nodes
            .Where(static node => string.Equals(node.Kind, "entry", StringComparison.OrdinalIgnoreCase) ||
                                  string.Equals(node.Kind, "cli", StringComparison.OrdinalIgnoreCase) ||
                                  string.Equals(node.Kind, "service", StringComparison.OrdinalIgnoreCase) ||
                                  string.Equals(node.GroupId, "entries", StringComparison.OrdinalIgnoreCase))
            .Select(static node => node.Id)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        var moduleNodeIds = nodes
            .Where(static node => string.Equals(node.Kind, "module", StringComparison.OrdinalIgnoreCase) ||
                                  string.Equals(node.GroupId, "modules", StringComparison.OrdinalIgnoreCase))
            .Select(static node => node.Id)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (nodes.Count == 0 && layerNodeIds.Count == 0 && layers.Count > 0)
        {
            foreach (var layer in layers.Take(6))
            {
                var nodeId = NormalizeId(layer.Name);
                if (nodes.Any(node => string.Equals(node.Id, nodeId, StringComparison.OrdinalIgnoreCase)))
                {
                    continue;
                }

                nodes.Add(new ArchitectureDiagramNode(nodeId, layer.Name, "layer", "layers"));
                layerNodeIds.Add(nodeId);
            }
        }

        if (nodes.Count == 0 && entryNodeIds.Count == 0 && entryPoints.Count > 0)
        {
            foreach (var entryPoint in entryPoints.Take(4))
            {
                var nodeId = NormalizeId($"entry-{entryPoint.RelativePath}");
                if (nodes.Any(node => string.Equals(node.Id, nodeId, StringComparison.OrdinalIgnoreCase)))
                {
                    continue;
                }

                nodes.Add(new ArchitectureDiagramNode(nodeId, entryPoint.RelativePath, "entry", "entries"));
                entryNodeIds.Add(nodeId);
            }
        }

        if (moduleNodeIds.Count == 0 && modules.Count >= 2 && layerNodeIds.Count > 1)
        {
            foreach (var module in modules.Take(6))
            {
                var nodeId = NormalizeId($"module-{module.Name}");
                if (nodes.Any(node => string.Equals(node.Id, nodeId, StringComparison.OrdinalIgnoreCase)))
                {
                    continue;
                }

                nodes.Add(new ArchitectureDiagramNode(nodeId, module.Name, "module", "modules"));
                moduleNodeIds.Add(nodeId);
            }
        }

        if (groups.Count == 0)
        {
            if (layerNodeIds.Count > 0)
            {
                groups.Add(new ArchitectureDiagramGroup("layers", "Observed Layers", layerNodeIds.ToArray(), MaxConfidence(layers.Select(static layer => layer.Confidence))));
            }

            if (entryNodeIds.Count > 0)
            {
                groups.Add(new ArchitectureDiagramGroup("entries", "Observed Entry Points", entryNodeIds.ToArray(), MaxConfidence(entryPoints.Select(static entry => entry.Confidence))));
            }

            if (moduleNodeIds.Count > 0)
            {
                groups.Add(new ArchitectureDiagramGroup("modules", "Observed Modules", moduleNodeIds.ToArray(), MaxConfidence(modules.Select(static module => module.Confidence))));
            }
        }

        if (layerNodeIds.Count == 1 && entryNodeIds.Count > 0)
        {
            var firstLayerId = layerNodeIds.FirstOrDefault();
            if (!string.IsNullOrWhiteSpace(firstLayerId))
            {
                foreach (var entryNodeId in entryNodeIds.Take(1))
                {
                    if (edges.Any(edge => string.Equals(edge.From, entryNodeId, StringComparison.OrdinalIgnoreCase)))
                    {
                        continue;
                    }

                    var entryConfidence = nodes.FirstOrDefault(node => string.Equals(node.Id, entryNodeId, StringComparison.OrdinalIgnoreCase))?.Confidence ?? WorkspaceEvidenceConfidenceLevel.Unknown;
                    edges.Add(new ArchitectureDiagramEdge(entryNodeId, firstLayerId, "entry", "entry", entryConfidence));
                }
            }
        }

        return new ArchitectureDiagramSpec(
            BuildCoarseDiagramTitle(pack, diagramSpec.Title, modules, entryPoints),
            nodes,
            edges,
            groups,
            notes,
            diagramSpec.RenderHints);
    }

    private static WorkspaceMaterialPreviewInterpretation BuildFallbackMaterialInterpretation(WorkspaceMaterialPreviewInput material)
    {
        var normalizedPath = material.RelativePath.Replace('/', '\\');
        var preview = material.PreviewText ?? string.Empty;
        var contentScore = GetContentSignalScore(preview);
        var contentSummary = BuildContentDrivenSummary(preview);

        if (contentScore >= 3 || ContainsAny(normalizedPath, "ARCHITECTURE_MAP", "ARCHITECTURE", "Документация проекта", "Конституция проекта", "constitution", "design", "blueprint"))
        {
            var summary = SanitizeMaterialSummary(
                contentSummary.Length > 0 ? contentSummary : InferFallbackSummary(normalizedPath, preview));
            return new WorkspaceMaterialPreviewInterpretation(
                material.RelativePath,
                material.Kind,
                summary,
                contentScore >= 4 ||
                normalizedPath.Contains("ARCHITECTURE", StringComparison.OrdinalIgnoreCase) ||
                normalizedPath.Contains("ARCHITECTURE_MAP", StringComparison.OrdinalIgnoreCase)
                    ? WorkspaceMaterialContextUsefulness.High
                    : WorkspaceMaterialContextUsefulness.Medium,
                WorkspaceMaterialTemporalStatus.Unknown,
                string.Empty,
                ContextOnly: true);
        }

        return new WorkspaceMaterialPreviewInterpretation(
            material.RelativePath,
            material.Kind,
            string.Empty,
            WorkspaceMaterialContextUsefulness.Unknown,
            WorkspaceMaterialTemporalStatus.Unknown,
            string.Empty,
            ContextOnly: true);
    }

    private static string InferFallbackSummary(string normalizedPath, string preview)
    {
        if (normalizedPath.Contains("CONSTITUTION", StringComparison.OrdinalIgnoreCase) ||
            normalizedPath.Contains("Конституция проекта", StringComparison.OrdinalIgnoreCase))
        {
            return "Проектный конституционный документ с правилами, границами и архитектурными инвариантами.";
        }

        if (normalizedPath.Contains("ARCHITECTURE", StringComparison.OrdinalIgnoreCase))
        {
            return "Документ с архитектурной картой, слоями, компонентами и их взаимодействием.";
        }

        if (normalizedPath.Contains("Документация проекта", StringComparison.OrdinalIgnoreCase))
        {
            return "Подробная проектная документация с техническими и архитектурными опорами.";
        }

        var trimmed = preview.Trim();
        if (trimmed.Length == 0)
        {
            return string.Empty;
        }

        return trimmed.Length <= 140 ? trimmed : trimmed[..140];
    }

    private static int GetContentSignalScore(string preview)
    {
        var score = 0;
        if (ContainsAny(preview, "архитектур", "architecture", "слой", "layer", "runtime", "core", "ui"))
        {
            score += 2;
        }

        if (ContainsAny(preview, "инвариант", "principle", "truth", "registry", "manager", "scanner"))
        {
            score += 2;
        }

        if (ContainsAny(preview, "cmake", "qt", "qml", "c++", "msvc", "ninja", "ctest"))
        {
            score += 1;
        }

        return score;
    }

    private static string SanitizeMaterialSummary(string summary)
    {
        if (string.IsNullOrWhiteSpace(summary))
        {
            return string.Empty;
        }

        if (ContainsAny(summary, "architecture invariants", "architectural invariants", "инвариант", "Ð°Ñ€Ñ…Ð¸Ñ‚ÐµÐºÑ‚ÑƒÑ€Ð½Ñ‹Ð¼Ð¸ Ð¸Ð½Ð²Ð°Ñ€Ð¸Ð°Ð½Ñ‚Ð°Ð¼Ð¸"))
        {
            return "Context material with governance or architecture markers; contributor review required.";
        }

        if (ContainsAny(summary, "rules", "principles", "правил", "принцип", "Ð¿Ñ€Ð°Ð²Ð¸Ð»", "Ð¿Ñ€Ð¸Ð½Ñ†Ð¸Ð¿"))
        {
            return "Context material with governance/principle markers; contributor review required.";
        }

        return summary.Trim();
    }

    private static string BuildContentDrivenSummary(string preview)
    {
        if (ContainsAny(preview, "конституц", "constitution", "principle", "инвариант", "truth"))
        {
            return "Проектный документ с принципами, правилами и архитектурными инвариантами.";
        }

        if (ContainsAny(preview, "архитектур", "architecture", "слой", "layer", "runtime", "core", "ui"))
        {
            return "Документ с архитектурным описанием, слоями и взаимодействием ключевых компонентов.";
        }

        if (ContainsAny(preview, "cmake", "qt", "qml", "c++", "msvc", "ninja"))
        {
            return "Технический документ со стеком, билд-маркерами и наблюдаемыми конфигурациями проекта.";
        }

        return string.Empty;
    }

    private static string? ResolveStructuredLayerName(
        WorkspaceEvidencePack? pack,
        WorkspaceImportMaterialModuleInterpretation module,
        IReadOnlyList<WorkspaceImportMaterialLayerInterpretation> layers)
    {
        if (pack is null)
        {
            return null;
        }

        var matches = pack.Candidates.ModuleCandidates
            .Where(candidate => string.Equals(candidate.Name, module.Name, StringComparison.OrdinalIgnoreCase))
            .ToArray();
        if (matches.Length != 1)
        {
            return null;
        }

        var layerName = matches[0].LayerName;
        return layers.Any(layer => string.Equals(layer.Name, layerName, StringComparison.OrdinalIgnoreCase))
            ? layers.First(layer => string.Equals(layer.Name, layerName, StringComparison.OrdinalIgnoreCase)).Name
            : null;
    }

    private static string BuildCoarseDiagramTitle(
        WorkspaceImportMaterialPreviewPacket packet,
        string? candidateTitle,
        IReadOnlyList<WorkspaceImportMaterialModuleInterpretation> modules,
        IReadOnlyList<WorkspaceImportMaterialEntryPointInterpretation> entryPoints)
    {
        return BuildCoarseDiagramTitle(packet.EvidencePack, candidateTitle, modules, entryPoints);
    }

    private static string BuildCoarseDiagramTitle(
        WorkspaceEvidencePack? pack,
        string? candidateTitle,
        IReadOnlyList<WorkspaceImportMaterialModuleInterpretation> modules,
        IReadOnlyList<WorkspaceImportMaterialEntryPointInterpretation> entryPoints)
    {
        if (!string.IsNullOrWhiteSpace(candidateTitle) &&
            !NarrativeLineNeedsStrongSupport(pack, candidateTitle, modules, entryPoints))
        {
            return candidateTitle.Trim();
        }

        return "Project Architecture";
    }

    private static string[] BuildBoundedTopologyDetails(ProjectInterpretationMode mode)
    {
        if (mode is ProjectInterpretationMode.MultipleIndependentProjects or ProjectInterpretationMode.AmbiguousContainer)
        {
            return BuildContainerDetails(mode);
        }

        return mode switch
        {
            ProjectInterpretationMode.MaterialOnly => new[]
            {
                "Scanned folder is material-only; source project structure is not confirmed.",
                "Documents or notes are context materials, not project truth."
            },
            ProjectInterpretationMode.MixedSourceRelease => new[]
            {
                "Scanned folder contains both active source evidence and release/output zones.",
                "Release/dist payloads must not be treated as active source truth."
            },
            ProjectInterpretationMode.Decompilation => new[]
            {
                "Scanned folder has decompilation/reverse-engineering topology.",
                "Normal application architecture is not assumed."
            },
            ProjectInterpretationMode.Legacy => new[]
            {
                "Scanned folder has legacy or low-level source topology.",
                "Normal application architecture is not assumed."
            },
            ProjectInterpretationMode.ReleaseBundle => new[]
            {
                "Scanned folder looks like release/output payload.",
                "Active source project identity is not confirmed."
            },
            _ => new[]
            {
                "Scanned folder remains ambiguous.",
                "Single project narrative is not confirmed by current cold evidence."
            }
        };
    }

    private static string BuildBoundedTopologySummary(ProjectInterpretationMode mode)
    {
        if (mode is ProjectInterpretationMode.MultipleIndependentProjects or ProjectInterpretationMode.AmbiguousContainer)
        {
            return BuildContainerSummary(mode);
        }

        return mode switch
        {
            ProjectInterpretationMode.MaterialOnly => "Folder is material-only. Source project structure and delivery plan are not confirmed. truth=context_only.",
            ProjectInterpretationMode.MixedSourceRelease => "Folder mixes active source evidence with release/output zones. Release payloads are not active source truth. truth=context_only.",
            ProjectInterpretationMode.Decompilation => "Folder has decompilation/reverse-engineering topology. Normal application architecture is not assumed. truth=context_only.",
            ProjectInterpretationMode.Legacy => "Folder has legacy or low-level source topology. Normal application architecture is not assumed. truth=context_only.",
            ProjectInterpretationMode.ReleaseBundle => "Folder looks like release/output payload. Active source project identity is not confirmed. truth=context_only.",
            _ => "Folder remains ambiguous. Single project narrative is not confirmed by cold evidence. truth=context_only."
        };
    }

    private static string[] BuildContainerDetails(ProjectInterpretationMode mode)
    {
        return mode == ProjectInterpretationMode.MultipleIndependentProjects
            ? new[]
            {
                "Папка содержит несколько независимых или слабо связанных project roots.",
                "Единая архитектура проекта не подтверждена текущим cold evidence."
            }
            : new[]
            {
                "Папка выглядит как project container с неоднозначной общей структурой.",
                "Единый project narrative не подтверждён текущим cold evidence."
            };
    }

    private static string BuildContainerSummary(ProjectInterpretationMode mode)
    {
        return mode == ProjectInterpretationMode.MultipleIndependentProjects
            ? "Папка содержит несколько независимых или слабо связанных project roots. Единая архитектура проекта не подтверждена текущим cold evidence. truth=context_only."
            : "Папка выглядит как неоднозначный project container. Единый project narrative не подтверждён текущим cold evidence. truth=context_only.";
    }

    private static string NormalizeId(string value)
    {
        var chars = value
            .ToLowerInvariant()
            .Select(ch => char.IsLetterOrDigit(ch) ? ch : '-')
            .ToArray();
        return new string(chars).Trim('-');
    }

    private static string BuildImporterOwnedSummary(
        WorkspaceImportMaterialPreviewPacket packet,
        IReadOnlyList<WorkspaceImportMaterialEntryPointInterpretation> entryPoints,
        IReadOnlyList<WorkspaceImportMaterialModuleInterpretation> modules,
        IReadOnlyList<string> details,
        int materialCount)
    {
        var parts = new List<string>
        {
            $"Import material interpretation prepared: import={packet.ImportKind}",
            $"sourceRoots={packet.SourceRoots.Count}",
            $"materials={materialCount}"
        };

        if (packet.EvidencePack is not null)
        {
            parts.Add($"observations={packet.EvidencePack.RawObservations.Count}");
            parts.Add($"patterns={packet.EvidencePack.DerivedPatterns.Count}");
            parts.Add($"signals={packet.EvidencePack.SignalScores.Count}");
            parts.Add($"scannerEntryCandidatesTotal={packet.EvidencePack.Candidates.EntryPoints.Count}");
            parts.Add($"scannerModuleCandidatesTotal={packet.EvidencePack.Candidates.ModuleCandidates.Count}");
        }

        if (entryPoints.Count > 0)
        {
            parts.Add($"displayedEntryCandidates={entryPoints.Count}");
            parts.Add($"selectedMainEntry={entryPoints[0].RelativePath}");
        }

        if (modules.Count > 0)
        {
            parts.Add($"displayedModules={modules.Count}");
        }

        if (details.Count > 0)
        {
            parts.Add($"details={details.Count}");
        }

        parts.Add("truth=context_only.");
        return string.Join(", ", parts);
    }

    private static string BuildCoarseSummary(
        WorkspaceImportMaterialPreviewPacket packet,
        IReadOnlyList<WorkspaceImportMaterialEntryPointInterpretation> entryPoints,
        IReadOnlyList<WorkspaceImportMaterialModuleInterpretation> modules,
        int materialCount)
    {
        var pack = packet.EvidencePack;
        var parts = new List<string>
        {
            $"Import material interpretation prepared: import={packet.ImportKind}",
            $"sourceRoots={packet.SourceRoots.Count}",
            $"materials={materialCount}"
        };

        if (pack is not null)
        {
            parts.Add($"observations={pack.RawObservations.Count}");
            parts.Add($"patterns={pack.DerivedPatterns.Count}");
            parts.Add($"signals={pack.SignalScores.Count}");
            parts.Add($"scannerEntryCandidatesTotal={pack.Candidates.EntryPoints.Count}");
            parts.Add($"scannerModuleCandidatesTotal={pack.Candidates.ModuleCandidates.Count}");
        }

        if (pack is not null)
        {
            var languages = pack.TechnicalPassport.ObservedLanguages.Take(3).ToArray();
            var buildSystems = pack.TechnicalPassport.BuildSystems.Take(2).ToArray();
            var runtimeHints = pack.SignalScores
                .Where(static score => score.Signal.StartsWith("runtime.", StringComparison.OrdinalIgnoreCase) && score.Score >= 0.45)
                .Select(static score => score.Signal["runtime.".Length..])
                .Take(2)
                .ToArray();

            if (languages.Length > 0)
            {
                parts.Add($"languages: {string.Join(", ", languages)}");
            }

            if (buildSystems.Length > 0)
            {
                parts.Add($"build: {string.Join(", ", buildSystems)}");
            }

            if (runtimeHints.Length > 0)
            {
                parts.Add($"runtime hints: {string.Join(", ", runtimeHints)}");
            }
        }

        if (entryPoints.Count > 0)
        {
            parts.Add($"displayedEntryCandidates={entryPoints.Count}");
            parts.Add($"selectedMainEntry={entryPoints[0].RelativePath}");
        }

        if (modules.Count > 0)
        {
            parts.Add($"displayedModules={modules.Count}");
        }

        return string.Join(", ", parts) + ", truth=context_only.";
    }

    private static int ScoreScannerEntryPoint(WorkspaceEvidencePack pack, WorkspaceImportMaterialEntryPointInterpretation entry)
    {
        var scannerEntry = FindScannerEntryPoint(pack, entry.RelativePath);
        return scannerEntry?.Score ?? ScoreEntryPoint(entry.RelativePath, entry.Role);
    }

    private static WorkspaceEvidenceEntryPoint? FindScannerEntryPoint(WorkspaceEvidencePack pack, string relativePath)
    {
        return pack.Candidates.EntryPoints.FirstOrDefault(entry =>
            string.Equals(entry.RelativePath, relativePath, StringComparison.OrdinalIgnoreCase));
    }

    private static int ScoreEntryPoint(string relativePath, string role)
    {
        var profile = ClassifyEntryPath(relativePath);
        var score = 0;
        if (string.Equals(role, "main", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(role, "cli", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(role, "service", StringComparison.OrdinalIgnoreCase))
        {
            score += 20;
        }

        if (profile.IsPrimaryEntryFile)
        {
            score += 20;
        }

        if (profile.IsUnderTestLikeSubtree)
        {
            score -= 30;
        }

        if (profile.IsUnderToolsLikeSubtree)
        {
            score -= 15;
        }

        if (profile.IsUnderWorkflowLikeSubtree)
        {
            score -= 22;
        }

        if (profile.IsUnderSupportLikeSubtree)
        {
            score -= 12;
        }

        if (profile.IsUnderDebugLikeSubtree)
        {
            score -= 10;
        }

        if (profile.IsPrimaryEntryFile && !profile.IsUnderSecondarySubtree)
        {
            score += 6;
        }

        if (!profile.IsPrimaryEntryFile && profile.IsUnderSecondarySubtree)
        {
            score -= 8;
        }

        score -= profile.Depth;
        return score;
    }

    private static EntryPathProfile ClassifyEntryPath(string relativePath)
    {
        var normalized = relativePath.Replace('/', '\\');
        var segments = normalized
            .Split(new[] { '\\' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var fileName = segments.Length == 0 ? normalized : segments[^1];
        var fileNameWithoutExtension = fileName;
        var dotIndex = fileName.LastIndexOf('.');
        if (dotIndex > 0)
        {
            fileNameWithoutExtension = fileName[..dotIndex];
        }

        var loweredSegments = segments.Select(static segment => segment.ToLowerInvariant()).ToArray();
        var isTestLike = loweredSegments.Any(static segment =>
            segment.Contains("test", StringComparison.Ordinal) ||
            segment.Contains("example", StringComparison.Ordinal) ||
            segment.Contains("sample", StringComparison.Ordinal) ||
            segment.Contains("demo", StringComparison.Ordinal));
        var isSupportLike = loweredSegments.Any(static segment =>
            string.Equals(segment, "helper", StringComparison.Ordinal) ||
            string.Equals(segment, "helpers", StringComparison.Ordinal) ||
            string.Equals(segment, "support", StringComparison.Ordinal));
        var isToolsLike = loweredSegments.Any(static segment =>
            string.Equals(segment, "tools", StringComparison.Ordinal) ||
            string.Equals(segment, "tool", StringComparison.Ordinal) ||
            string.Equals(segment, "scripts", StringComparison.Ordinal) ||
            string.Equals(segment, "script", StringComparison.Ordinal) ||
            string.Equals(segment, "docs", StringComparison.Ordinal) ||
            string.Equals(segment, "doc", StringComparison.Ordinal));
        var isWorkflowLike = loweredSegments.Any(static segment =>
            string.Equals(segment, ".github", StringComparison.Ordinal) ||
            string.Equals(segment, ".storybook", StringComparison.Ordinal) ||
            string.Equals(segment, "xtask", StringComparison.Ordinal));
        var isDebugLike = loweredSegments.Any(static segment =>
            string.Equals(segment, "debug", StringComparison.Ordinal) ||
            string.Equals(segment, "dbg", StringComparison.Ordinal) ||
            segment.Contains("utility", StringComparison.Ordinal) ||
            segment.Contains("util", StringComparison.Ordinal));
        var isPrimaryEntryFile =
            string.Equals(fileNameWithoutExtension, "main", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(fileNameWithoutExtension, "program", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(fileNameWithoutExtension, "app", StringComparison.OrdinalIgnoreCase);

        return new EntryPathProfile(
            segments.Length,
            isPrimaryEntryFile,
            isTestLike,
            isSupportLike,
            isToolsLike,
            isWorkflowLike,
            isDebugLike);
    }

    private readonly record struct EntryPathProfile(
        int Depth,
        bool IsPrimaryEntryFile,
        bool IsUnderTestLikeSubtree,
        bool IsUnderSupportLikeSubtree,
        bool IsUnderToolsLikeSubtree,
        bool IsUnderWorkflowLikeSubtree,
        bool IsUnderDebugLikeSubtree)
    {
        public bool IsUnderSecondarySubtree => IsUnderTestLikeSubtree || IsUnderSupportLikeSubtree || IsUnderToolsLikeSubtree || IsUnderWorkflowLikeSubtree || IsUnderDebugLikeSubtree;
    }

    private static bool HasStructuralAnomaly(WorkspaceEvidencePack pack, string code)
    {
        return pack.ProjectProfile.StructuralAnomalies.Any(anomaly => anomaly.StartsWith(code + ":", StringComparison.OrdinalIgnoreCase) ||
                                                                     string.Equals(anomaly, code, StringComparison.OrdinalIgnoreCase));
    }

    private static bool HasCompetingPrimaryEntries(
        IReadOnlyList<string> sourceRoots,
        IReadOnlyList<WorkspaceImportMaterialEntryPointInterpretation> entryPoints)
    {
        if (entryPoints.Count < 2)
        {
            return false;
        }

        var competingRoots = entryPoints
            .Where(entry => ScoreEntryPoint(entry.RelativePath, entry.Role) >= 12)
            .Select(entry => ResolveEntryRootBucket(sourceRoots, entry.RelativePath))
            .Where(static root => !string.IsNullOrWhiteSpace(root))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return competingRoots.Length > 1;
    }

    private static string ResolveEntryRootBucket(IReadOnlyList<string> sourceRoots, string relativePath)
    {
        var normalizedPath = relativePath.Replace('/', '\\');
        foreach (var sourceRoot in sourceRoots
                     .Where(static root => !string.IsNullOrWhiteSpace(root) && !string.Equals(root, ".", StringComparison.Ordinal))
                     .OrderByDescending(static root => root.Length))
        {
            var normalizedRoot = sourceRoot.Replace('/', '\\');
            if (normalizedPath.StartsWith(normalizedRoot + "\\", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(normalizedPath, normalizedRoot, StringComparison.OrdinalIgnoreCase))
            {
                return normalizedRoot;
            }
        }

        var separatorIndex = normalizedPath.IndexOf('\\');
        return separatorIndex > 0 ? normalizedPath[..separatorIndex] : normalizedPath;
    }

    private static bool HasStageEvidence(WorkspaceEvidencePack? pack)
    {
        return pack is not null && pack.Signals.Any(static signal =>
            string.Equals(signal.Category, "stage", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(signal.Category, "temporal", StringComparison.OrdinalIgnoreCase));
    }

    private static int ScoreModuleCandidate(
        WorkspaceEvidencePack pack,
        WorkspaceEvidenceModule module,
        IReadOnlyList<WorkspaceImportMaterialEntryPointInterpretation> entryPoints)
    {
        var score = 0;
        if (pack.Candidates.FileRoles.Any(role => ContainsAny(role.Role, module.Role) || ContainsAny(role.RelativePath, module.Name)))
        {
            score += 2;
        }

        if (entryPoints.Any(entry => ContainsAny(entry.RelativePath, module.Name, module.Role)))
        {
            score += 2;
        }

        if (pack.Edges.Any(edge =>
                string.Equals(edge.From, module.Name, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(edge.To, module.Name, StringComparison.OrdinalIgnoreCase)))
        {
            score += 1;
        }

        if (ContainsAny(module.LayerName, "root"))
        {
            score += 1;
        }

        return score;
    }

    private static bool IsGenericHumanFacingModule(string name)
    {
        return string.Equals(name, "Api", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(name, "Cloud", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(name, "UI", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(name, "Tui", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(name, "Core", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(name, "Web", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(name, "Service", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(name, "Platform", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(name, "Main", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(name, "Tools", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(name, "Scripts", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsBroadHumanFacingModule(string name)
    {
        return string.Equals(name, "Api", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(name, "Cloud", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(name, "UI", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(name, "Tui", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(name, "Service", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(name, "Platform", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(name, "Web", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(name, "Core", StringComparison.OrdinalIgnoreCase);
    }

    private static bool RequiresBroadTokenSupport(string value)
    {
        return string.Equals(value, "UI", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(value, "Tui", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(value, "Api", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(value, "Service", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(value, "Platform", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(value, "Cloud", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(value, "Core", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(value, "Web", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsDiagramNodeSupported(
        ArchitectureDiagramNode node,
        IReadOnlyList<WorkspaceImportMaterialLayerInterpretation> layers,
        IReadOnlyList<WorkspaceImportMaterialModuleInterpretation> modules,
        IReadOnlyList<WorkspaceImportMaterialEntryPointInterpretation> entryPoints)
    {
        if (RequiresBroadTokenSupport(node.Label) &&
            !layers.Any(layer => string.Equals(layer.Name, node.Label, StringComparison.OrdinalIgnoreCase)) &&
            !modules.Any(module => string.Equals(module.Name, node.Label, StringComparison.OrdinalIgnoreCase)))
        {
            return false;
        }

        if (string.Equals(node.Kind, "entry", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(node.Kind, "cli", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(node.Kind, "service", StringComparison.OrdinalIgnoreCase))
        {
            return entryPoints.Any(entry => string.Equals(entry.RelativePath, node.Label, StringComparison.OrdinalIgnoreCase));
        }

        return true;
    }

    private static bool NarrativeLineNeedsStrongSupport(
        WorkspaceEvidencePack? pack,
        string line,
        IReadOnlyList<WorkspaceImportMaterialModuleInterpretation> modules,
        IReadOnlyList<WorkspaceImportMaterialEntryPointInterpretation> entryPoints)
    {
        if (pack is null || string.IsNullOrWhiteSpace(line))
        {
            return false;
        }

        return ContainsUnsupportedBroadToken(pack, line, modules, entryPoints, "ui") ||
               ContainsUnsupportedBroadToken(pack, line, modules, entryPoints, "tui") ||
               ContainsUnsupportedBroadToken(pack, line, modules, entryPoints, "api") ||
               ContainsUnsupportedBroadToken(pack, line, modules, entryPoints, "service") ||
               ContainsUnsupportedBroadToken(pack, line, modules, entryPoints, "platform") ||
               ContainsUnsupportedBroadToken(pack, line, modules, entryPoints, "cloud") ||
               ContainsUnsupportedBroadToken(pack, line, modules, entryPoints, "core") ||
               ContainsUnsupportedBroadToken(pack, line, modules, entryPoints, "web") ||
               ContainsUnsupportedBroadToken(pack, line, modules, entryPoints, "linux") ||
               ContainsUnsupportedBroadToken(pack, line, modules, entryPoints, "macos");
    }

    private static bool ContainsUnsupportedBroadToken(
        WorkspaceEvidencePack pack,
        string line,
        IReadOnlyList<WorkspaceImportMaterialModuleInterpretation> modules,
        IReadOnlyList<WorkspaceImportMaterialEntryPointInterpretation> entryPoints,
        string token)
    {
        if (!ContainsBroadToken(line, token))
        {
            return false;
        }

        return !HasStrongSupportForBroadToken(pack, token, modules, entryPoints);
    }

    private static bool HasStrongSupportForBroadToken(
        WorkspaceEvidencePack pack,
        string token,
        IReadOnlyList<WorkspaceImportMaterialModuleInterpretation> modules,
        IReadOnlyList<WorkspaceImportMaterialEntryPointInterpretation> entryPoints)
    {
        return token.ToLowerInvariant() switch
        {
            "ui" => GetSignalScore(pack, "behavior.ui_rendering") >= 0.75,
            "tui" => pack.DependencySurface.Any(dep => ContainsAny(dep.Name, "ratatui", "crossterm", "tui")) ||
                     modules.Any(module => string.Equals(module.Name, "Tui", StringComparison.OrdinalIgnoreCase) &&
                                           module.Confidence >= WorkspaceEvidenceConfidenceLevel.Likely),
            "api" => GetSignalScore(pack, "runtime.service") >= 0.8 ||
                     pack.DependencySurface.Any(dep => ContainsAny(dep.Name, "axum", "warp", "actix", "httplib", "grpc", "rest")),
            "service" => GetSignalScore(pack, "runtime.service") >= 0.8 &&
                         entryPoints.Any(entry =>
                             entry.Confidence >= WorkspaceEvidenceConfidenceLevel.Likely &&
                             ContainsAny(entry.RelativePath, "\\server\\", "\\service\\", "\\api\\", "\\http\\")),
            "platform" => GetSignalScore(pack, "origin.modding") >= 0.8,
            "cloud" => GetSignalScore(pack, "runtime.service") >= 0.8,
            "core" => modules.Any(module => string.Equals(module.Name, "Core", StringComparison.OrdinalIgnoreCase) &&
                                            module.Confidence >= WorkspaceEvidenceConfidenceLevel.Likely),
            "web" => GetSignalScore(pack, "runtime.web") >= 0.75 ||
                     (pack.DerivedPatterns.Any(pattern => string.Equals(pattern.Code, "browser_surface_pattern", StringComparison.OrdinalIgnoreCase)) &&
                      pack.Candidates.FileRoles.Any(role =>
                          string.Equals(role.Role, "asset", StringComparison.OrdinalIgnoreCase) &&
                          ContainsAny(role.RelativePath, "\\public\\", ".html", ".tsx", ".jsx", ".vue"))) ||
                     (pack.DependencySurface.Any(dep => ContainsAny(dep.Name, "react", "vue", "vite", "svelte", "next", "nuxt")) &&
                      pack.Candidates.FileRoles.Any(role =>
                          string.Equals(role.Role, "asset", StringComparison.OrdinalIgnoreCase) &&
                          ContainsAny(role.RelativePath, "\\public\\", ".html", ".tsx", ".jsx", ".vue"))),
            "linux" => pack.TechnicalPassport.TargetPlatforms.Contains("Linux", StringComparer.OrdinalIgnoreCase),
            "macos" => pack.TechnicalPassport.TargetPlatforms.Contains("macOS", StringComparer.OrdinalIgnoreCase),
            _ => false
        };
    }

    private static bool ContainsBroadToken(string line, string token)
    {
        return token.ToLowerInvariant() switch
        {
            "ui" => ContainsAny(line, "ui", "интерфейс", "графическ"),
            "tui" => ContainsAny(line, "tui", "терминальн"),
            "api" => ContainsAny(line, "api", "апи"),
            "service" => ContainsAny(line, "service", "services", "сервис", "сервер"),
            "platform" => ContainsAny(line, "platform", "платформ"),
            "cloud" => ContainsAny(line, "cloud", "облач"),
            "core" => ContainsAny(line, "core", "ядро"),
            "web" => ContainsAny(line, "web", "веб", "браузер"),
            "linux" => ContainsAny(line, "linux", "линукс"),
            "macos" => ContainsAny(line, "macos", "darwin", "osx", "мак"),
            _ => ContainsAny(line, token)
        };
    }

    private static bool IsRecognizedHumanLayerName(string name)
    {
        return string.Equals(name, "root", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(name, "workspace", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(name, "source", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(name, "build system", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(name, "core", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(name, "runtime", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(name, "ui", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(name, "cli", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(name, "service", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(name, "mod platform", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsLayerSupportedByColdEvidence(
        WorkspaceEvidencePack pack,
        string layerName,
        IReadOnlyList<WorkspaceImportMaterialModuleInterpretation> modules,
        IReadOnlyList<WorkspaceImportMaterialEntryPointInterpretation> entryPoints)
    {
        if (string.Equals(layerName, "root", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(layerName, "workspace", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (string.Equals(layerName, "UI", StringComparison.OrdinalIgnoreCase))
        {
            return GetSignalScore(pack, "behavior.ui_rendering") >= 0.7 &&
                   modules.Any(module => module.Confidence >= WorkspaceEvidenceConfidenceLevel.Likely);
        }

        if (string.Equals(layerName, "Service", StringComparison.OrdinalIgnoreCase))
        {
            return GetSignalScore(pack, "runtime.service") >= 0.8 &&
                   entryPoints.Any(entry =>
                       entry.Confidence >= WorkspaceEvidenceConfidenceLevel.Likely &&
                       ContainsAny(entry.RelativePath, "\\server\\", "\\service\\", "\\api\\", "\\http\\"));
        }

        if (string.Equals(layerName, "CLI", StringComparison.OrdinalIgnoreCase))
        {
            return GetSignalScore(pack, "runtime.cli") >= 0.6;
        }

        if (string.Equals(layerName, "Mod Platform", StringComparison.OrdinalIgnoreCase))
        {
            return GetSignalScore(pack, "origin.modding") >= 0.8;
        }

        if (string.Equals(layerName, "Runtime", StringComparison.OrdinalIgnoreCase))
        {
            return GetSignalScore(pack, "behavior.process_control") >= 0.55 ||
                   GetSignalScore(pack, "origin.reverse") >= 0.65;
        }

        if (string.Equals(layerName, "Core", StringComparison.OrdinalIgnoreCase))
        {
            return modules.Any(module => module.Confidence >= WorkspaceEvidenceConfidenceLevel.Likely);
        }

        return true;
    }

    private static void AddLayerIfSupported(
        List<WorkspaceImportMaterialLayerInterpretation> target,
        WorkspaceEvidencePack pack,
        IReadOnlyList<WorkspaceImportMaterialModuleInterpretation> modules,
        IReadOnlyList<WorkspaceImportMaterialEntryPointInterpretation> entryPoints,
        string layerName,
        string responsibility,
        string evidenceNote,
        bool RequireUiSupport = false,
        bool requireCliSupport = false,
        bool requireServiceSupport = false,
        bool requireModSupport = false,
        int minSupport = 2)
    {
        var support = 0;
        if (modules.Any(module => ContainsAny(module.Name, layerName) || ContainsAny(module.EvidenceNote, layerName)))
        {
            support += 2;
        }

        if (pack.Candidates.FileRoles.Any(role => ContainsAny(role.Role, layerName.ToLowerInvariant()) || ContainsAny(role.RelativePath, layerName)))
        {
            support += 1;
        }

        if (entryPoints.Any(entry => ContainsAny(entry.Role, layerName.ToLowerInvariant()) || ContainsAny(entry.RelativePath, layerName)))
        {
            support += 1;
        }

        if (RequireUiSupport && GetSignalScore(pack, "behavior.ui_rendering") < 0.6)
        {
            support = 0;
        }

        if (requireCliSupport && GetSignalScore(pack, "runtime.cli") < 0.55)
        {
            support = 0;
        }

        if (requireServiceSupport && GetSignalScore(pack, "runtime.service") < 0.7)
        {
            support = 0;
        }

        if (requireModSupport && GetSignalScore(pack, "origin.modding") < 0.7)
        {
            support = 0;
        }

        if (support < minSupport)
        {
            return;
        }

        target.Add(new WorkspaceImportMaterialLayerInterpretation(
            layerName,
            responsibility,
            evidenceNote,
            ResolveConfidence(pack, "layer", layerName)));
    }

    private static double GetSignalScore(WorkspaceEvidencePack pack, string signal)
    {
        return pack.SignalScores
            .FirstOrDefault(score => string.Equals(score.Signal, signal, StringComparison.OrdinalIgnoreCase))
            ?.Score ?? 0.0;
    }

    private static WorkspaceEvidenceConfidenceLevel ResolveMaterialConfidence(
        WorkspaceEvidencePack? pack,
        string relativePath,
        WorkspaceMaterialContextUsefulness usefulness)
    {
        if (usefulness == WorkspaceMaterialContextUsefulness.High)
        {
            return WorkspaceEvidenceConfidenceLevel.Confirmed;
        }

        if (usefulness == WorkspaceMaterialContextUsefulness.Medium)
        {
            return WorkspaceEvidenceConfidenceLevel.Likely;
        }

        return ResolveConfidence(pack, "material", relativePath);
    }

    private static WorkspaceEvidenceConfidenceLevel ResolveConfidence(
        WorkspaceEvidencePack? pack,
        string targetKind,
        string targetId)
    {
        if (pack is null)
        {
            return WorkspaceEvidenceConfidenceLevel.Unknown;
        }

        if (string.Equals(targetKind, "entry_point", StringComparison.OrdinalIgnoreCase))
        {
            var entry = FindScannerEntryPoint(pack, targetId);
            if (entry?.EvidenceMarker?.Confidence is { } confidence)
            {
                return confidence;
            }
        }

        if (string.Equals(targetKind, "module", StringComparison.OrdinalIgnoreCase))
        {
            var module = pack.Candidates.ModuleCandidates.FirstOrDefault(item =>
                string.Equals(item.Name, targetId, StringComparison.OrdinalIgnoreCase));
            if (module?.EvidenceMarker?.Confidence is { } confidence)
            {
                return confidence;
            }
        }

        return pack.ConfidenceAnnotations
            .FirstOrDefault(item =>
                string.Equals(item.TargetKind, targetKind, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(item.TargetId, targetId, StringComparison.OrdinalIgnoreCase))
            ?.Confidence ?? WorkspaceEvidenceConfidenceLevel.Unknown;
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

    private static WorkspaceEvidenceConfidenceLevel MaxConfidence(IEnumerable<WorkspaceEvidenceConfidenceLevel> values)
    {
        return values.DefaultIfEmpty(WorkspaceEvidenceConfidenceLevel.Unknown).Max();
    }
}
