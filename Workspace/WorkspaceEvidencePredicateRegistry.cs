using System;
using System.Collections.Generic;
using System.Linq;

namespace zavod.Workspace;

public sealed record WorkspaceEvidencePredicate(
    string Id,
    string SubjectType,
    string ObjectType,
    IReadOnlyList<string> ValidSources,
    string ConfidenceRule);

public static class WorkspaceEvidencePredicateRegistry
{
    public const string ObservesFile = "observes_file";
    public const string DetectsSensitiveFile = "detects_sensitive_file";
    public const string DetectsSourceRoot = "detects_source_root";
    public const string DetectsBuildRoot = "detects_build_root";
    public const string DetectsEntryCandidate = "detects_entry_candidate";
    public const string DetectsStructuralAnomaly = "detects_structural_anomaly";
    public const string DetectsTechnicalPreview = "detects_technical_preview";
    public const string DetectsMaterialPreview = "detects_material_preview";
    public const string ClassifiesDocumentKind = "classifies_document_kind";
    public const string DetectsRootReadmeIdentity = "detects_root_readme_identity";
    public const string DetectsBuildSystem = "detects_build_system";
    public const string DetectsRuntimeSurface = "detects_runtime_surface";
    public const string DetectsBehaviorSignal = "detects_behavior_signal";
    public const string DetectsOriginSignal = "detects_origin_signal";
    public const string DetectsStageSignal = "detects_stage_signal";
    public const string ClassifiesNoise = "classifies_noise";
    public const string ClassifiesTemporalSignal = "classifies_temporal_signal";
    public const string ClassifiesFileRole = "classifies_file_role";
    public const string ReportsScanBudget = "reports_scan_budget";
    public const string DeclaresProjectUnit = "declares_project_unit";
    public const string DeclaresRunProfile = "declares_run_profile";
    public const string DeclaresSymbol = "declares_symbol";
    public const string DeclaresCodeEdge = "declares_code_edge";
    public const string DeclaresDependency = "declares_dependency";
    public const string GroupsCandidate = "groups_candidate";
    public const string ResolvesTaskScope = "resolves_task_scope";
    public const string ExcludesTaskScopeSoftly = "excludes_task_scope_softly";

    private static readonly WorkspaceEvidencePredicate[] Registered =
    {
        Predicate(ObservesFile, "workspace", "file", "file_inventory", "Confirmed when a filesystem entry is observed inside the scan boundary."),
        Predicate(DetectsSensitiveFile, "file", "sensitive_policy", "sensitive_file_policy", "Confirmed by deterministic path/name policy; content is not read into context."),
        Predicate(DetectsSourceRoot, "workspace", "path", "file_inventory", "Likely when source-like files cluster under the path."),
        Predicate(DetectsBuildRoot, "workspace", "path", "manifest_index", "Likely when build/manifest files cluster under the path."),
        Predicate(DetectsEntryCandidate, "file", "entrypoint_candidate", "entrypoint_ranking", "Confirmed for observed bootstrap filenames; score explains priority."),
        Predicate(DetectsStructuralAnomaly, "workspace", "anomaly", "file_inventory", "Confirmed when deterministic scanner topology rules fire."),
        Predicate(DetectsTechnicalPreview, "file", "technical_preview", "material_runtime", "Confirmed when bounded technical preview extraction succeeds."),
        Predicate(DetectsMaterialPreview, "file", "material_preview", "material_runtime", "Confirmed when bounded material preview extraction succeeds."),
        Predicate(ClassifiesDocumentKind, "file", "document_kind", "material_runtime", "Likely from bounded document path/content markers."),
        Predicate(DetectsRootReadmeIdentity, "README", "identity_signal", "manifest_index", "Likely doc identity evidence; importer decides meaning."),
        Predicate(DetectsBuildSystem, "manifest", "build_system", "manifest_index", "Confirmed from manifest filenames or repeated technical markers."),
        Predicate(DetectsRuntimeSurface, "workspace", "runtime_signal", "manifest_index", "Likely from repeated structural/runtime markers."),
        Predicate(DetectsBehaviorSignal, "workspace", "behavior_signal", "manifest_index", "Likely from repeated structural/technical markers."),
        Predicate(DetectsOriginSignal, "workspace", "origin_signal", "manifest_index", "Likely from repeated structural/technical markers."),
        Predicate(DetectsStageSignal, "workspace", "stage_signal", "history_index", "Likely from git/history/doc markers."),
        Predicate(ClassifiesNoise, "file", "noise_classification", "material_runtime", "Likely from deterministic material path/kind rules."),
        Predicate(ClassifiesTemporalSignal, "file", "temporal_classification", "material_runtime", "Likely from bounded material wording/path rules."),
        Predicate(ClassifiesFileRole, "file", "file_role", "file_inventory", "Likely from deterministic path/extension markers."),
        Predicate(ReportsScanBudget, "scan_run", "budget_report", "file_inventory", "Confirmed when deterministic scan budget rules skip or limit evidence."),
        Predicate(DeclaresProjectUnit, "path", "project_unit", "manifest_index", "Likely from manifest root; confirmed with entrypoint overlap."),
        Predicate(DeclaresRunProfile, "manifest", "run_profile", "run_profile_index", "Confirmed for explicit scripts; likely for conventional manifest commands."),
        Predicate(DeclaresSymbol, "file", "symbol", "shallow_symbol_index", "Confirmed for shallow declarations/signatures extracted without semantic interpretation."),
        Predicate(DeclaresCodeEdge, "file", "file", "dependency_edges", "Resolution field distinguishes resolved, ambiguous, unresolved, lexical, or manifest evidence."),
        Predicate(DeclaresDependency, "manifest", "dependency", "manifest_index", "Confirmed for direct manifest dependency sections."),
        Predicate(GroupsCandidate, "candidate", "candidate_group", "module_boundary_map", "Likely structural grouping, not semantic ownership."),
        Predicate(ResolvesTaskScope, "task", "file", "task_scope_resolver", "Likely recommendation from task terms plus cold evidence."),
        Predicate(ExcludesTaskScopeSoftly, "task", "file", "task_scope_resolver", "Soft recommendation only; not a hard path prohibition.")
    };

    public static IReadOnlyList<WorkspaceEvidencePredicate> All { get; } = Registered;

    public static bool IsRegistered(string id)
    {
        return Registered.Any(predicate => string.Equals(predicate.Id, id, StringComparison.OrdinalIgnoreCase));
    }

    public static string PredicateForObservationKind(string kind)
    {
        return kind switch
        {
            "file_found" => ObservesFile,
            "sensitive_file_detected" => DetectsSensitiveFile,
            "source_root_detected" => DetectsSourceRoot,
            "build_root_detected" => DetectsBuildRoot,
            "entry_candidate_detected" => DetectsEntryCandidate,
            "structural_anomaly_detected" => DetectsStructuralAnomaly,
            "technical_preview_detected" => DetectsTechnicalPreview,
            "material_preview_detected" => DetectsMaterialPreview,
            "document_kind_detected" => ClassifiesDocumentKind,
            "scan_budget_degraded" or "scan_budget_skip_detected" => ReportsScanBudget,
            _ => string.Empty
        };
    }

    public static string PredicateForSignal(string category, string code)
    {
        _ = code;
        return category switch
        {
            "structure" => DetectsStructuralAnomaly,
            "build" => DetectsBuildSystem,
            "runtime" => DetectsRuntimeSurface,
            "behavior" => DetectsBehaviorSignal,
            "origin" => DetectsOriginSignal,
            "stage" => DetectsStageSignal,
            "noise" => ClassifiesNoise,
            "temporal" => ClassifiesTemporalSignal,
            "identity" => DetectsRootReadmeIdentity,
            _ => string.Empty
        };
    }

    public static string PredicateForEdge(string labelOrKind, WorkspaceEvidenceEdgeResolution resolution)
    {
        if (resolution == WorkspaceEvidenceEdgeResolution.Manifest)
        {
            return DeclaresDependency;
        }

        return labelOrKind switch
        {
            "include" or "import" or "require" or "python-import" or "go-import" or "rust-mod" or "rust-use" or "csharp-using" => DeclaresCodeEdge,
            "direct" or "dev" or "build" or "linked-library" => DeclaresDependency,
            "entry" or "cli" or "service" or "main" or "entry-surface" or "subsystem-cluster" or "command-cluster" or "low-level-cluster" or "config-cluster" => GroupsCandidate,
            _ => string.Empty
        };
    }

    public static bool TryResolveScopeEvidence(string evidence, out string predicateId)
    {
        predicateId = evidence switch
        {
            _ when evidence.StartsWith("path_term:", StringComparison.OrdinalIgnoreCase) => ResolvesTaskScope,
            _ when evidence.StartsWith("filename_term:", StringComparison.OrdinalIgnoreCase) => ResolvesTaskScope,
            _ when evidence.StartsWith("neighbor:", StringComparison.OrdinalIgnoreCase) => ResolvesTaskScope,
            _ when evidence.StartsWith("term_related", StringComparison.OrdinalIgnoreCase) => ResolvesTaskScope,
            _ when evidence.StartsWith("outside_allowed_paths", StringComparison.OrdinalIgnoreCase) => ExcludesTaskScopeSoftly,
            _ when evidence.StartsWith("entrypoint_candidate", StringComparison.OrdinalIgnoreCase) => DetectsEntryCandidate,
            _ when evidence.StartsWith("file_role:", StringComparison.OrdinalIgnoreCase) => ClassifiesFileRole,
            _ when evidence.StartsWith("project_unit:", StringComparison.OrdinalIgnoreCase) => DeclaresProjectUnit,
            _ when evidence.StartsWith("project_unit_entry:", StringComparison.OrdinalIgnoreCase) => DeclaresProjectUnit,
            _ when evidence.StartsWith("run_profile:", StringComparison.OrdinalIgnoreCase) => DeclaresRunProfile,
            _ when evidence.StartsWith("edge_from:", StringComparison.OrdinalIgnoreCase) => DeclaresCodeEdge,
            _ when evidence.StartsWith("edge_to:", StringComparison.OrdinalIgnoreCase) => DeclaresCodeEdge,
            _ when evidence.StartsWith("signature:", StringComparison.OrdinalIgnoreCase) => DeclaresSymbol,
            _ when evidence.StartsWith("material:", StringComparison.OrdinalIgnoreCase) => DetectsMaterialPreview,
            _ when evidence.StartsWith("dependency_manifest:", StringComparison.OrdinalIgnoreCase) => DeclaresDependency,
            _ => string.Empty
        };

        return !string.IsNullOrWhiteSpace(predicateId);
    }

    private static WorkspaceEvidencePredicate Predicate(
        string id,
        string subjectType,
        string objectType,
        string validSource,
        string confidenceRule)
    {
        return new WorkspaceEvidencePredicate(id, subjectType, objectType, new[] { validSource }, confidenceRule);
    }
}
