using System;
using System.Collections.Generic;
using System.Linq;

namespace zavod.Workspace;

public static class WorkspaceImportMaterialPromptResponseParser
{
    public static WorkspaceImportMaterialPromptResponse Parse(string responseText)
    {
        if (string.IsNullOrWhiteSpace(responseText))
        {
            return new WorkspaceImportMaterialPromptResponse(
                string.Empty,
                Array.Empty<string>(),
                Array.Empty<string>(),
                Array.Empty<string>(),
                Array.Empty<string>(),
                Array.Empty<string>(),
                Array.Empty<string>(),
                Array.Empty<string>(),
                Array.Empty<string>(),
                Array.Empty<string>(),
                Array.Empty<WorkspaceImportMaterialLayerInterpretation>(),
                Array.Empty<WorkspaceImportMaterialModuleInterpretation>(),
                Array.Empty<WorkspaceImportMaterialEntryPointInterpretation>(),
                BuildEmptyDiagramSpec(),
                Array.Empty<WorkspaceImportMaterialPromptResponseItem>());
        }

        var summary = string.Empty;
        var details = new List<string>();
        var confirmedSignals = new List<string>();
        var likelySignals = new List<string>();
        var unknownSignals = new List<string>();
        var stageSignals = new List<string>();
        var currentSignals = new List<string>();
        var plannedSignals = new List<string>();
        var possiblyStaleSignals = new List<string>();
        var conflicts = new List<string>();
        var layers = new List<WorkspaceImportMaterialLayerInterpretation>();
        var modules = new List<WorkspaceImportMaterialModuleInterpretation>();
        var entryPoints = new List<WorkspaceImportMaterialEntryPointInterpretation>();
        var diagramNodes = new List<ArchitectureDiagramNode>();
        var diagramEdges = new List<ArchitectureDiagramEdge>();
        var diagramGroups = new List<ArchitectureDiagramGroup>();
        var materials = new Dictionary<string, ParsedMaterialState>(StringComparer.OrdinalIgnoreCase);
        var lines = responseText.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);

        foreach (var rawLine in lines)
        {
            var line = rawLine.Trim();
            if (line.Length == 0)
            {
                continue;
            }

            if (line.StartsWith("SUMMARY:", StringComparison.OrdinalIgnoreCase))
            {
                summary = line["SUMMARY:".Length..].Trim();
                continue;
            }

            if (line.StartsWith("DETAIL:", StringComparison.OrdinalIgnoreCase))
            {
                var detail = line["DETAIL:".Length..].Trim();
                if (detail.Length > 0)
                {
                    details.Add(detail);
                }

                continue;
            }

            if (TryCollectSimpleSignal(line, "CONFIRMED:", confirmedSignals) ||
                TryCollectSimpleSignal(line, "LIKELY:", likelySignals) ||
                TryCollectSimpleSignal(line, "UNKNOWN:", unknownSignals))
            {
                continue;
            }

            if (line.StartsWith("STAGE:", StringComparison.OrdinalIgnoreCase))
            {
                var signal = line["STAGE:".Length..].Trim();
                if (signal.Length > 0)
                {
                    stageSignals.Add(signal);
                }

                continue;
            }

            if (TryCollectSimpleSignal(line, "CURRENT_SIGNALS:", currentSignals) ||
                TryCollectSimpleSignal(line, "PLANNED_SIGNALS:", plannedSignals) ||
                TryCollectSimpleSignal(line, "POSSIBLY_STALE:", possiblyStaleSignals) ||
                TryCollectSimpleSignal(line, "CONFLICT:", conflicts))
            {
                continue;
            }

            if (line.StartsWith("LAYER:", StringComparison.OrdinalIgnoreCase))
            {
                var layerPayload = line["LAYER:".Length..].Trim();
                var layerParts = layerPayload.Split('|', 3, StringSplitOptions.TrimEntries);
                if (layerParts.Length == 3 && !string.IsNullOrWhiteSpace(layerParts[0]))
                {
                    layers.Add(new WorkspaceImportMaterialLayerInterpretation(layerParts[0], layerParts[1], layerParts[2]));
                }

                continue;
            }

            if (line.StartsWith("ENTRY_POINT:", StringComparison.OrdinalIgnoreCase))
            {
                var entryPayload = line["ENTRY_POINT:".Length..].Trim();
                var entryParts = entryPayload.Split('|', 3, StringSplitOptions.TrimEntries);
                if (entryParts.Length == 3 && !string.IsNullOrWhiteSpace(entryParts[0]))
                {
                    entryPoints.Add(new WorkspaceImportMaterialEntryPointInterpretation(entryParts[0], entryParts[1], entryParts[2]));
                }

                continue;
            }

            if (line.StartsWith("MODULE:", StringComparison.OrdinalIgnoreCase))
            {
                var modulePayload = line["MODULE:".Length..].Trim();
                var moduleParts = modulePayload.Split('|', 3, StringSplitOptions.TrimEntries);
                if (moduleParts.Length == 3 && !string.IsNullOrWhiteSpace(moduleParts[0]))
                {
                    modules.Add(new WorkspaceImportMaterialModuleInterpretation(moduleParts[0], moduleParts[1], moduleParts[2]));
                }

                continue;
            }

            if (line.StartsWith("DIAGRAM_NODE:", StringComparison.OrdinalIgnoreCase))
            {
                var nodePayload = line["DIAGRAM_NODE:".Length..].Trim();
                var nodeParts = nodePayload.Split('|', 4, StringSplitOptions.TrimEntries);
                if (nodeParts.Length >= 3 && !string.IsNullOrWhiteSpace(nodeParts[0]))
                {
                    var confidence = nodeParts.Length >= 4 ? ParseConfidence(nodeParts[3]) : WorkspaceEvidenceConfidenceLevel.Unknown;
                    diagramNodes.Add(new ArchitectureDiagramNode(nodeParts[0], nodeParts[1], nodeParts[2], null, confidence));
                }

                continue;
            }

            if (line.StartsWith("DIAGRAM_EDGE:", StringComparison.OrdinalIgnoreCase))
            {
                var edgePayload = line["DIAGRAM_EDGE:".Length..].Trim();
                var edgeParts = edgePayload.Split('|', 5, StringSplitOptions.TrimEntries);
                if (edgeParts.Length >= 3 && !string.IsNullOrWhiteSpace(edgeParts[0]) && !string.IsNullOrWhiteSpace(edgeParts[1]))
                {
                    var kind = edgeParts.Length >= 4 && !string.IsNullOrWhiteSpace(edgeParts[3])
                        ? edgeParts[3]
                        : "observed";
                    var confidence = edgeParts.Length >= 5 ? ParseConfidence(edgeParts[4]) : WorkspaceEvidenceConfidenceLevel.Unknown;
                    diagramEdges.Add(new ArchitectureDiagramEdge(edgeParts[0], edgeParts[1], edgeParts[2], kind, confidence));
                }

                continue;
            }

            if (line.StartsWith("DIAGRAM_GROUP:", StringComparison.OrdinalIgnoreCase))
            {
                var groupPayload = line["DIAGRAM_GROUP:".Length..].Trim();
                var groupParts = groupPayload.Split('|', 4, StringSplitOptions.TrimEntries);
                if (groupParts.Length >= 3 && !string.IsNullOrWhiteSpace(groupParts[0]))
                {
                    var members = groupParts[2]
                        .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
                        .ToArray();
                    var confidence = groupParts.Length >= 4 ? ParseConfidence(groupParts[3]) : WorkspaceEvidenceConfidenceLevel.Unknown;
                    diagramGroups.Add(new ArchitectureDiagramGroup(groupParts[0], groupParts[1], members, confidence));
                }

                continue;
            }

            if (line.StartsWith("MATERIAL_STATE:", StringComparison.OrdinalIgnoreCase))
            {
                var statePayload = line["MATERIAL_STATE:".Length..].Trim();
                var stateParts = statePayload.Split('|', 3, StringSplitOptions.TrimEntries);
                if (stateParts.Length < 3 || string.IsNullOrWhiteSpace(stateParts[0]))
                {
                    continue;
                }

                var state = GetOrCreate(materials, stateParts[0]);
                state.TemporalStatus = ParseTemporalStatus(stateParts[1]);
                state.StatusNote = stateParts[2];
                continue;
            }

            if (!line.StartsWith("MATERIAL:", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var payload = line["MATERIAL:".Length..].Trim();
            var parts = payload.Split('|', 3, StringSplitOptions.TrimEntries);
            if (parts.Length < 3 || string.IsNullOrWhiteSpace(parts[0]))
            {
                continue;
            }

            var material = GetOrCreate(materials, parts[0]);
            material.PossibleUsefulness = ParseUsefulness(parts[1]);
            material.Summary = parts[2];
        }

        if (string.IsNullOrWhiteSpace(summary) && materials.Count == 0)
        {
            summary = ExtractFallbackSummary(responseText);
        }

        return new WorkspaceImportMaterialPromptResponse(
            summary,
            details,
            confirmedSignals,
            likelySignals,
            unknownSignals,
            stageSignals,
            currentSignals,
            plannedSignals,
            possiblyStaleSignals,
            conflicts,
            layers
                .Where(static layer => !string.IsNullOrWhiteSpace(layer.Name))
                .Distinct()
                .ToArray(),
            modules
                .Where(static module => !string.IsNullOrWhiteSpace(module.Name))
                .Distinct()
                .ToArray(),
            entryPoints
                .Where(static entry => !string.IsNullOrWhiteSpace(entry.RelativePath))
                .Distinct()
                .ToArray(),
            BuildDiagramSpec(summary, diagramNodes, diagramEdges, diagramGroups),
            materials.Values
                .Select(static item => new WorkspaceImportMaterialPromptResponseItem(
                    item.RelativePath,
                    item.PossibleUsefulness,
                    item.Summary,
                    item.TemporalStatus,
                    item.StatusNote))
                .ToArray());
    }

    private static bool TryCollectSimpleSignal(string line, string prefix, List<string> target)
    {
        if (!line.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var payload = line[prefix.Length..].Trim();
        if (payload.Length > 0)
        {
            target.Add(payload);
        }

        return true;
    }

    private static WorkspaceMaterialContextUsefulness ParseUsefulness(string value)
    {
        if (Enum.TryParse<WorkspaceMaterialContextUsefulness>(value, ignoreCase: true, out var parsed))
        {
            return parsed;
        }

        return WorkspaceMaterialContextUsefulness.Unknown;
    }

    private static WorkspaceMaterialTemporalStatus ParseTemporalStatus(string value)
    {
        if (Enum.TryParse<WorkspaceMaterialTemporalStatus>(value, ignoreCase: true, out var parsed))
        {
            return parsed;
        }

        return WorkspaceMaterialTemporalStatus.Unknown;
    }

    private static WorkspaceEvidenceConfidenceLevel ParseConfidence(string value)
    {
        if (Enum.TryParse<WorkspaceEvidenceConfidenceLevel>(value, ignoreCase: true, out var parsed))
        {
            return parsed;
        }

        return WorkspaceEvidenceConfidenceLevel.Unknown;
    }

    private static ParsedMaterialState GetOrCreate(Dictionary<string, ParsedMaterialState> materials, string path)
    {
        if (materials.TryGetValue(path, out var existing))
        {
            return existing;
        }

        var created = new ParsedMaterialState(path);
        materials[path] = created;
        return created;
    }

    private static string ExtractFallbackSummary(string responseText)
    {
        if (string.IsNullOrWhiteSpace(responseText))
        {
            return string.Empty;
        }

        var normalized = responseText
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Trim();
        if (normalized.Length == 0)
        {
            return string.Empty;
        }

        var paragraph = normalized.Split(new[] { "\n\n" }, StringSplitOptions.RemoveEmptyEntries)[0].Trim();
        if (paragraph.Length == 0)
        {
            return string.Empty;
        }

        return paragraph.Length <= 280 ? paragraph : $"{paragraph[..277].TrimEnd()}...";
    }

    private static ArchitectureDiagramSpec BuildDiagramSpec(
        string summary,
        IReadOnlyList<ArchitectureDiagramNode> nodes,
        IReadOnlyList<ArchitectureDiagramEdge> edges,
        IReadOnlyList<ArchitectureDiagramGroup> groups)
    {
        if (nodes.Count == 0)
        {
            return BuildEmptyDiagramSpec(summary);
        }

        var nodesById = nodes
            .GroupBy(static node => node.Id, StringComparer.OrdinalIgnoreCase)
            .Select(static group => group.First())
            .ToArray();
        var normalizedGroups = groups
            .GroupBy(static group => group.Id, StringComparer.OrdinalIgnoreCase)
            .Select(static group => group.First())
            .ToArray();
        var groupedNodeIds = normalizedGroups
            .SelectMany(static group => group.Members)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var normalizedNodes = nodesById
            .Select(node =>
            {
                var groupId = normalizedGroups.FirstOrDefault(group => group.Members.Contains(node.Id, StringComparer.OrdinalIgnoreCase))?.Id;
                return node with { GroupId = groupId ?? node.GroupId };
            })
            .ToArray();
        var knownNodeIds = normalizedNodes.Select(static node => node.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var normalizedEdges = edges
            .Where(edge => knownNodeIds.Contains(edge.From) && knownNodeIds.Contains(edge.To))
            .GroupBy(static edge => $"{edge.From}|{edge.To}|{edge.Label}|{edge.Kind}", StringComparer.OrdinalIgnoreCase)
            .Select(static group => group.First())
            .ToArray();
        var notes = new List<string>();
        if (!string.IsNullOrWhiteSpace(summary))
        {
            notes.Add(summary.Trim());
        }

        if (normalizedGroups.Length == 0 && normalizedNodes.Any(static node => !string.IsNullOrWhiteSpace(node.GroupId)))
        {
            normalizedGroups = normalizedNodes
                .Where(static node => !string.IsNullOrWhiteSpace(node.GroupId))
                .GroupBy(static node => node.GroupId!, StringComparer.OrdinalIgnoreCase)
                .Select(static group => new ArchitectureDiagramGroup(group.Key, group.Key, group.Select(static node => node.Id).ToArray()))
                .ToArray();
        }

        return new ArchitectureDiagramSpec(
            string.IsNullOrWhiteSpace(summary) ? "Project Architecture" : summary.Trim(),
            normalizedNodes,
            normalizedEdges,
            normalizedGroups,
            notes,
            new ArchitectureDiagramRenderHints("left-to-right", groupedNodeIds.ToArray(), ShowLegend: true));
    }

    private static ArchitectureDiagramSpec BuildEmptyDiagramSpec(string? summary = null)
    {
        return new ArchitectureDiagramSpec(
            string.IsNullOrWhiteSpace(summary) ? "Project Architecture" : summary.Trim(),
            Array.Empty<ArchitectureDiagramNode>(),
            Array.Empty<ArchitectureDiagramEdge>(),
            Array.Empty<ArchitectureDiagramGroup>(),
            Array.Empty<string>(),
            new ArchitectureDiagramRenderHints("left-to-right", Array.Empty<string>(), ShowLegend: true));
    }

    private sealed class ParsedMaterialState(string relativePath)
    {
        public string RelativePath { get; } = relativePath;
        public WorkspaceMaterialContextUsefulness PossibleUsefulness { get; set; } = WorkspaceMaterialContextUsefulness.Unknown;
        public string Summary { get; set; } = string.Empty;
        public WorkspaceMaterialTemporalStatus TemporalStatus { get; set; } = WorkspaceMaterialTemporalStatus.Unknown;
        public string StatusNote { get; set; } = string.Empty;
    }
}
