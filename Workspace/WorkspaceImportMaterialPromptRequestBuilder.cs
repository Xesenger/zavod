using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using zavod.Prompting;

namespace zavod.Workspace;

public static class WorkspaceImportMaterialPromptRequestBuilder
{
    public static WorkspaceImportMaterialPromptRequest Build(WorkspaceImportMaterialPreviewPacket packet)
    {
        ArgumentNullException.ThrowIfNull(packet);

        return new WorkspaceImportMaterialPromptRequest(
            PromptSystemCatalog.GetImportSystemPrompt(),
            BuildUserPrompt(packet));
    }

    private static string BuildUserPrompt(WorkspaceImportMaterialPreviewPacket packet)
    {
        var languagePolicy = WorkspaceDocumentationLanguagePolicy.ResolveCurrent();
        var builder = new StringBuilder();
        builder.AppendLine("[IMPORT PROJECT]");
        builder.AppendLine($"workspace_root: {packet.WorkspaceRoot}");
        builder.AppendLine($"import_kind: {packet.ImportKind}");
        builder.AppendLine();
        builder.AppendLine("[DOCUMENTATION LANGUAGE]");
        builder.AppendLine($"language_tag: {languagePolicy.LanguageTag}");
        builder.AppendLine($"language_english: {languagePolicy.EnglishName}");
        builder.AppendLine($"language_native: {languagePolicy.NativeName}");
        builder.AppendLine();
        builder.AppendLine("[SOURCE ROOTS]");
        builder.AppendLine(JoinOrNone(packet.SourceRoots));

        builder.AppendLine();
        builder.AppendLine("[EVIDENCE PACK SNAPSHOT]");
        AppendEvidencePack(builder, packet.EvidencePack);

        builder.AppendLine();
        builder.AppendLine("[TECHNICAL EVIDENCE]");
        if (packet.TechnicalEvidence.Count == 0)
        {
            builder.AppendLine("- none");
        }
        else
        {
            for (var index = 0; index < packet.TechnicalEvidence.Count; index++)
            {
                var evidence = packet.TechnicalEvidence[index];
                builder.AppendLine($"Technical #{index + 1}");
                builder.AppendLine($"path: {evidence.RelativePath}");
                builder.AppendLine($"category: {evidence.Category}");
                builder.AppendLine($"was_truncated: {evidence.WasTruncated}");
                builder.AppendLine("preview:");
                builder.AppendLine(evidence.PreviewText);
                if (index < packet.TechnicalEvidence.Count - 1)
                {
                    builder.AppendLine();
                }
            }
        }

        builder.AppendLine();
        builder.AppendLine("[MATERIAL PREVIEW INPUTS]");
        if (packet.Materials.Count == 0)
        {
            builder.AppendLine("- none");
        }
        else
        {
            for (var index = 0; index < packet.Materials.Count; index++)
            {
                var material = packet.Materials[index];
                builder.AppendLine($"Material #{index + 1}");
                builder.AppendLine($"path: {material.RelativePath}");
                builder.AppendLine($"kind: {material.Kind}");
                builder.AppendLine($"selection_reason: {material.SelectionReason}");
                builder.AppendLine($"was_truncated: {material.WasTruncated}");
                if (!string.IsNullOrWhiteSpace(material.PreparationStatus))
                {
                    builder.AppendLine($"preparation_status: {material.PreparationStatus}");
                }

                if (!string.IsNullOrWhiteSpace(material.BackendId))
                {
                    builder.AppendLine($"backend_id: {material.BackendId}");
                }

                if (!string.IsNullOrWhiteSpace(material.PreparationSummary))
                {
                    builder.AppendLine($"preparation_summary: {material.PreparationSummary}");
                }

                builder.AppendLine("preview:");
                builder.AppendLine(material.PreviewText);
                if (index < packet.Materials.Count - 1)
                {
                    builder.AppendLine();
                }
            }
        }

        builder.AppendLine();
        builder.AppendLine("[RESPONSE FORMAT]");
        builder.AppendLine("Return plain text only. Do not use markdown, bullets, headings, or code fences.");
        builder.AppendLine("First line exactly:");
        builder.AppendLine("SUMMARY: <one evidence-based sentence that says what the project/system is>");
        builder.AppendLine("Then three to eight optional lines exactly in this shape when evidence supports them:");
        builder.AppendLine("DETAIL: <concrete project detail>");
        builder.AppendLine("Then zero or more optional confidence lines exactly in these shapes when evidence supports them:");
        builder.AppendLine("CONFIRMED: <strongly confirmed evidence-based fact>");
        builder.AppendLine("LIKELY: <plausible evidence-based fact with incomplete support>");
        builder.AppendLine("UNKNOWN: <honest uncertainty or unresolved area>");
        builder.AppendLine("Then one to four optional stage/status lines exactly in this shape when evidence supports them:");
        builder.AppendLine("STAGE: <short evidence-based signal about current stage, active work, likely plan, or possible staleness>");
        builder.AppendLine("Then zero or more optional lines exactly in these shapes when evidence supports them:");
        builder.AppendLine("CURRENT_SIGNALS: <current implementation or build reality>");
        builder.AppendLine("PLANNED_SIGNALS: <future/proposal/intent signal>");
        builder.AppendLine("POSSIBLY_STALE: <possibly stale or superseded signal>");
        builder.AppendLine("CONFLICT: <short evidence-based conflict between stronger and weaker signals>");
        builder.AppendLine("Then zero or more optional layer lines exactly in this shape:");
        builder.AppendLine("LAYER: <name> | <responsibility> | <evidence note>");
        builder.AppendLine("Then zero or more optional module lines exactly in this shape:");
        builder.AppendLine("MODULE: <name> | <role> | <evidence note>");
        builder.AppendLine("Then zero or more optional entry point lines exactly in this shape:");
        builder.AppendLine("ENTRY_POINT: <relative path> | <role> | <note>");
        builder.AppendLine("Then zero or more lines exactly in this shape:");
        builder.AppendLine("MATERIAL: <relative path> | <Unknown|Low|Medium|High> | <short evidence-based summary>");
        builder.AppendLine("Then optional material status lines exactly in this shape when evidence supports them:");
        builder.AppendLine("MATERIAL_STATE: <relative path> | <Unknown|Current|Planned|Historical|PossiblyStale|Conflicting> | <short evidence-based status note>");
        builder.AppendLine("Then zero or more optional diagram lines exactly in these shapes:");
        builder.AppendLine("DIAGRAM_NODE: <id> | <label> | <kind> | <Confirmed|Likely|Unknown>");
        builder.AppendLine("DIAGRAM_EDGE: <from> | <to> | <label> | <kind> | <Confirmed|Likely|Unknown>");
        builder.AppendLine("DIAGRAM_GROUP: <id> | <label> | <member-id-1, member-id-2, ...> | <Confirmed|Likely|Unknown>");
        builder.AppendLine("Keep the control prefixes exactly as written above.");
        builder.AppendLine($"Write all human-facing content after the prefixes in the user's documentation language: {languagePolicy.NativeName} ({languagePolicy.LanguageTag}).");
        builder.AppendLine("Use only paths from the packet above.");
        builder.AppendLine("Treat README and other narrative documents as evidence sources, not as authoritative project explanations on their own.");
        builder.AppendLine("Treat technical_passport as a transitional UX summary derived from cold evidence, not as authoritative scanner truth.");
        builder.AppendLine("Treat deprecated comparison payloads as compatibility-only context, not as primary evidence.");
        builder.AppendLine("Prefer a concrete project portrait over generic wording.");
        builder.AppendLine("When evidence is rich, prefer four to eight DETAIL lines instead of stopping at one generic technical sentence.");
        builder.AppendLine("Make the DETAIL lines cover distinct axes when possible: project purpose, architecture/layers, stack/build/toolchain, build variants or custom flags, and strong constraints or runtime signals.");
        builder.AppendLine("Prefer concrete technical statements over generic labels when the evidence pack shows manifests, build files, entry points, config markers, or subsystem roots.");
        builder.AppendLine("Use module candidates and dependency edges to describe coarse subsystem structure even when document names are generic.");
        builder.AppendLine("Do not promote short or generic folder buckets like ui, tui, api, core, main, tools, scripts into human-facing modules unless repeated cold evidence supports them across candidates, file roles, entry points, or edges.");
        builder.AppendLine("For infrastructure, tooling, and workspace-style repositories, prefer build/config/command structure over invented application layers.");
        builder.AppendLine("Use STAGE lines to distinguish current implementation reality from plans, proposals, historical notes, and possibly stale signals when the packet supports that distinction.");
        builder.AppendLine("When technical evidence exists, mention observed build systems, toolchains, frameworks, version hints, preset splits, custom flags, and separate build variants only if the markers are visible in the packet.");
        builder.AppendLine("If a visible config flag or split build exists, name it directly instead of hiding it behind generic abstractions.");
        builder.AppendLine("Do not infer technical environment from file names alone when snippets do not support it.");
        builder.AppendLine("Treat runtime surfaces, platforms, and behavior markers as bounded cold hints, not as licenses for broad project claims.");
        builder.AppendLine("Do not write UI, Service, Mod Platform, Web, Linux, or macOS unless repeated cold evidence is visible in entry points, file roles, dependency edges, or top signal scores.");
        builder.AppendLine("When confidence is weak, prefer UNKNOWN and omit the layer/module/edge rather than filling the gap with plausible prose.");
        builder.AppendLine("Do not promote folder names like api, ui, tui, core, scripts, tools, cloud, or main into human-facing modules unless confidence is at least Likely and cold support is repeated.");
        builder.AppendLine("Treat helper mains, test mains, sample mains, and testserver-style entries as secondary unless stronger bootstrap evidence clearly beats them.");
        builder.AppendLine("Do not mark a material Current unless evidence is stronger than note-like wording alone.");
        builder.AppendLine("Prefer CONFIRMED, LIKELY, and UNKNOWN over confident generic prose when cold evidence is uneven.");
        builder.AppendLine("Emit at least one LAYER line and one ENTRY_POINT line whenever the evidence pack or material previews make them visible.");
        builder.AppendLine("If architecture or subsystem structure is visible, emit a minimal DIAGRAM spec instead of leaving the map empty.");
        builder.AppendLine("If evidence is weak, omit the material or mark it Unknown.");

        return builder.ToString().TrimEnd();
    }

    private static void AppendEvidencePack(StringBuilder builder, WorkspaceEvidencePack? pack)
    {
        if (pack is null)
        {
            builder.AppendLine("- none");
            return;
        }

        builder.AppendLine("project_profile:");
        builder.AppendLine($"  health: {pack.ProjectProfile.Health}");
        builder.AppendLine($"  drift: {pack.ProjectProfile.DriftStatus}");
        builder.AppendLine($"  relevant_files: {pack.ProjectProfile.RelevantFileCount}");
        builder.AppendLine($"  source_files: {pack.ProjectProfile.SourceFileCount}");
        builder.AppendLine($"  build_files: {pack.ProjectProfile.BuildFileCount}");
        builder.AppendLine($"  config_files: {pack.ProjectProfile.ConfigFileCount}");
        builder.AppendLine($"  document_files: {pack.ProjectProfile.DocumentFileCount}");
        builder.AppendLine($"  asset_files: {pack.ProjectProfile.AssetFileCount}");
        builder.AppendLine($"  binary_files: {pack.ProjectProfile.BinaryFileCount}");
        builder.AppendLine($"  source_roots: {JoinOrNone(pack.ProjectProfile.SourceRoots)}");
        builder.AppendLine($"  build_roots: {JoinOrNone(pack.ProjectProfile.BuildRoots)}");
        builder.AppendLine($"  anomalies: {JoinOrNone(pack.ProjectProfile.StructuralAnomalies)}");
        builder.AppendLine($"importer_adapter_summary: {BuildImporterAdapterSummary(pack)}");
        builder.AppendLine($"raw_observation_count: {pack.RawObservations.Count}");
        builder.AppendLine($"derived_pattern_count: {pack.DerivedPatterns.Count}");
        builder.AppendLine($"hotspot_count: {pack.Hotspots.Count}");
        builder.AppendLine($"code_edge_count: {pack.CodeEdges.Count}");
        builder.AppendLine($"signature_hint_count: {pack.SignatureHints.Count}");
        builder.AppendLine($"dependency_surface_count: {pack.DependencySurface.Count}");
        builder.AppendLine("technical_passport_transitional_ux:");
        builder.AppendLine($"  observed_languages: {JoinOrNone(pack.TechnicalPassport.ObservedLanguages)}");
        builder.AppendLine($"  build_systems: {JoinOrNone(pack.TechnicalPassport.BuildSystems)}");
        builder.AppendLine($"  toolchains: {JoinOrNone(pack.TechnicalPassport.Toolchains)}");
        builder.AppendLine($"  frameworks: {JoinOrNone(pack.TechnicalPassport.Frameworks)}");
        builder.AppendLine($"  version_hints: {JoinOrNone(pack.TechnicalPassport.VersionHints)}");
        builder.AppendLine($"  target_platforms: {JoinOrNone(pack.TechnicalPassport.TargetPlatforms)}");
        builder.AppendLine($"  runtime_surfaces: {JoinOrNone(pack.TechnicalPassport.RuntimeSurfaces)}");
        builder.AppendLine($"  config_markers: {JoinOrNone(pack.TechnicalPassport.ConfigMarkers)}");
        builder.AppendLine($"  build_variants: {JoinOrNone(pack.TechnicalPassport.BuildVariants)}");
        builder.AppendLine($"  notable_options: {JoinOrNone(pack.TechnicalPassport.NotableOptions)}");
        builder.AppendLine($"entry_points: {JoinOrNone(pack.Candidates.EntryPoints.Select(static entry => $"{entry.RelativePath} ({entry.Role})"))}");
        builder.AppendLine($"file_roles: {JoinOrNone(pack.Candidates.FileRoles.Select(static role => $"{role.RelativePath} ({role.Role} {role.Confidence:0.00})"))}");
        builder.AppendLine($"module_candidates: {JoinOrNone(pack.Candidates.ModuleCandidates.Select(static module => $"{module.Name} ({module.LayerName}/{module.Role})"))}");
        builder.AppendLine("code_edges:");
        foreach (var edge in pack.CodeEdges.Take(16))
        {
            builder.AppendLine($"- {edge.FromPath} -> {edge.ToPath} | {edge.Kind} | {edge.Reason}");
        }
        builder.AppendLine("signature_hints:");
        foreach (var hint in pack.SignatureHints.Take(16))
        {
            builder.AppendLine($"- {hint.RelativePath} | {hint.Kind} | {hint.Signature}");
        }
        builder.AppendLine("dependency_surface:");
        foreach (var dependency in pack.DependencySurface.Take(20))
        {
            builder.AppendLine($"- {dependency.SourcePath} | {dependency.Name} | {dependency.Scope} | {dependency.Kind}");
        }
        builder.AppendLine("dependency_edges:");
        foreach (var edge in pack.Edges.Take(16))
        {
            builder.AppendLine($"- {edge.From} -> {edge.To} | {edge.Label} | {edge.Reason}");
        }
        builder.AppendLine("hotspots:");
        foreach (var hotspot in pack.Hotspots.Take(12))
        {
            builder.AppendLine($"- {hotspot.Code} | {hotspot.RelativePath} | {hotspot.Reason}");
        }
        builder.AppendLine("signals:");
        foreach (var signal in pack.Signals.Take(24))
        {
            builder.AppendLine($"- {signal.Category}:{signal.Code} | {signal.Reason} | {signal.EvidencePath ?? "n/a"}");
        }

        builder.AppendLine("signal_scores:");
        foreach (var score in pack.SignalScores.Take(16))
        {
            builder.AppendLine($"- {score.Signal} = {score.Score:0.00}");
        }
        builder.AppendLine("confidence_annotations:");
        foreach (var item in pack.ConfidenceAnnotations.Take(16))
        {
            builder.AppendLine($"- {item.TargetKind}:{item.TargetId} = {item.Confidence} | {item.Reason}");
        }

        builder.AppendLine("derived_patterns:");
        foreach (var pattern in pack.DerivedPatterns.Take(16))
        {
            builder.AppendLine($"- {pattern.Code} | {pattern.Reason} | evidence={JoinOrNone(pattern.EvidencePaths)}");
        }

        if (pack.Signals.Count > 24)
        {
            builder.AppendLine($"- truncated_signals: {pack.Signals.Count - 24} more");
        }

        builder.AppendLine("evidence_snippets:");
        foreach (var snippet in pack.EvidenceSnippets.Take(12))
        {
            builder.AppendLine($"Snippet: {snippet.RelativePath} [{snippet.Category}] truncated={snippet.WasTruncated}");
            builder.AppendLine(snippet.PreviewText);
        }
    }

    private static string JoinOrNone(IEnumerable<string> values)
    {
        var items = values.Where(static value => !string.IsNullOrWhiteSpace(value)).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        return items.Length == 0 ? "none" : string.Join(", ", items);
    }

    private static string BuildImporterAdapterSummary(WorkspaceEvidencePack pack)
    {
        var parts = new List<string>
        {
            $"import={pack.ProjectProfile.ImportKind}",
            $"roots={pack.ProjectProfile.SourceRoots.Count}",
            $"patterns={pack.DerivedPatterns.Count}",
            $"entries={pack.Candidates.EntryPoints.Count}",
            $"modules={pack.Candidates.ModuleCandidates.Count}",
            $"code_edges={pack.CodeEdges.Count}",
            $"hotspots={pack.Hotspots.Count}"
        };

        var topSignals = pack.SignalScores
            .Take(4)
            .Select(static score => $"{score.Signal}:{score.Score:0.00}")
            .ToArray();
        if (topSignals.Length > 0)
        {
            parts.Add($"top_signals={string.Join(",", topSignals)}");
        }

        return string.Join("; ", parts);
    }
}
