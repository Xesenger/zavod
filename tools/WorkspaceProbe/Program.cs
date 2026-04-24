using System.Text.Json;
using zavod.Execution;
using zavod.Workspace;

if (args.Length < 1 || string.IsNullOrWhiteSpace(args[0]))
{
    Console.Error.WriteLine("Usage: WorkspaceProbe <workspace-root> [--import-interpret]");
    return 2;
}

var workspaceRoot = args[0];
var runImportInterpret = args.Skip(1).Any(static arg => string.Equals(arg, "--import-interpret", StringComparison.OrdinalIgnoreCase));
var scan = WorkspaceScanner.Scan(new WorkspaceScanRequest(workspaceRoot));
var state = scan.State;
var totalMaterialCandidates = scan.MaterialCandidates.Count;
var importFacingCandidates = scan.MaterialCandidates
    .Where(static candidate => candidate.Kind is WorkspaceMaterialKind.TextDocument or WorkspaceMaterialKind.PdfDocument or WorkspaceMaterialKind.ArchiveArtifact or WorkspaceMaterialKind.ImageAsset)
    .ToArray();
var importFacingCounts = importFacingCandidates
    .GroupBy(static candidate => candidate.Kind)
    .OrderBy(static group => group.Key.ToString(), StringComparer.OrdinalIgnoreCase)
    .Select(static group => new { kind = group.Key.ToString(), count = group.Count() })
    .ToArray();

object payload;
if (runImportInterpret)
{
    var runtime = new WorkspaceImportMaterialInterpreterRuntime();
    var run = runtime.Interpret(scan);
    payload = new
    {
        workspaceRoot = state.WorkspaceRoot,
        importKind = state.ImportKind.ToString(),
        shortlistDiagnostics = new
        {
            totalMaterialCandidates,
            importFacingCandidates = importFacingCandidates.Length,
            maxMaterials = WorkspaceMaterialRuntimeFront.DefaultMaxMaterials,
            countsByKind = importFacingCounts,
            selectedPreviewMaterials = run.PreviewPacket.Materials.Count
        },
        previewMaterials = run.PreviewPacket.Materials.Select(m => new
        {
            path = m.RelativePath,
            kind = m.Kind.ToString(),
            preparationStatus = m.PreparationStatus,
            backendId = m.BackendId,
            wasTruncated = m.WasTruncated,
            preview = m.PreviewText
        }).ToArray(),
        technicalEvidence = run.PreviewPacket.TechnicalEvidence.Select(e => new
        {
            path = e.RelativePath,
            category = e.Category,
            wasTruncated = e.WasTruncated,
            preview = e.PreviewText
        }).ToArray(),
        evidencePack = run.PreviewPacket.EvidencePack is null
            ? null
            : new
            {
                scanRun = run.PreviewPacket.EvidencePack.ScanRun,
                predicateRegistry = run.PreviewPacket.EvidencePack.PredicateRegistry,
                scanBudget = run.PreviewPacket.EvidencePack.ScanBudget,
                projectProfile = run.PreviewPacket.EvidencePack.ProjectProfile,
                rawObservations = run.PreviewPacket.EvidencePack.RawObservations,
                derivedPatterns = run.PreviewPacket.EvidencePack.DerivedPatterns,
                signalScores = run.PreviewPacket.EvidencePack.SignalScores,
                fileIndex = run.PreviewPacket.EvidencePack.FileIndex,
                candidates = run.PreviewPacket.EvidencePack.Candidates,
                codeEdges = run.PreviewPacket.EvidencePack.CodeEdges,
                signatureHints = run.PreviewPacket.EvidencePack.SignatureHints,
                symbolHints = run.PreviewPacket.EvidencePack.SymbolHints,
                dependencySurface = run.PreviewPacket.EvidencePack.DependencySurface,
                confidenceAnnotations = run.PreviewPacket.EvidencePack.ConfidenceAnnotations,
                projectUnits = run.PreviewPacket.EvidencePack.Candidates.ProjectUnits,
                runProfiles = run.PreviewPacket.EvidencePack.Candidates.RunProfiles,
                edges = run.PreviewPacket.EvidencePack.Edges,
                hotspots = run.PreviewPacket.EvidencePack.Hotspots,
                signals = run.PreviewPacket.EvidencePack.Signals,
                snippetCount = run.PreviewPacket.EvidencePack.EvidenceSnippets.Count,
                deprecatedComparison = new
                {
                    technicalPassport = run.PreviewPacket.EvidencePack.TechnicalPassport,
                    treeSummary = run.PreviewPacket.EvidencePack.TreeSummary,
                    observedLayers = run.PreviewPacket.EvidencePack.ObservedLayers,
                    entryPoints = run.PreviewPacket.EvidencePack.EntryPoints,
                    moduleCandidates = run.PreviewPacket.EvidencePack.ModuleCandidates,
                    dependencyEdges = run.PreviewPacket.EvidencePack.DependencyEdges
                }
            },
        promptRequest = new
        {
            systemLength = run.PromptRequest.SystemPrompt.Length,
            userLength = run.PromptRequest.UserPrompt.Length
        },
        openRouter = new
        {
            configurationSource = OpenRouterConfiguration.FromEnvironment()?.Source,
            success = run.ExecutionResponse.Success,
            model = run.ExecutionResponse.ModelId,
            statusCode = run.ExecutionResponse.StatusCode,
            rawContentLength = run.ExecutionResponse.Content.Length,
            rawContent = run.ExecutionResponse.Content,
            diagnostic = run.ExecutionResponse.Diagnostic is null
                ? null
                : new { run.ExecutionResponse.Diagnostic.Code, run.ExecutionResponse.Diagnostic.Message },
            summary = run.ExecutionResponse.SummaryLine
        },
        interpretation = new
        {
            summary = run.Interpretation.SummaryLine,
            parsedSummary = WorkspaceImportMaterialPromptResponseParser.Parse(run.ExecutionResponse.Content).Summary,
            projectDetails = run.Interpretation.ProjectDetails,
            projectStageSignals = run.Interpretation.ProjectStageSignals,
            currentSignals = run.Interpretation.CurrentSignals,
            plannedSignals = run.Interpretation.PlannedSignals,
            possiblyStaleSignals = run.Interpretation.PossiblyStaleSignals,
            confirmedSignals = run.Interpretation.ConfirmedSignals,
            likelySignals = run.Interpretation.LikelySignals,
            unknownSignals = run.Interpretation.UnknownSignals,
            conflicts = run.Interpretation.Conflicts,
            layers = run.Interpretation.Layers,
            modules = run.Interpretation.Modules,
            entryPoints = run.Interpretation.EntryPoints,
            diagramSpec = run.Interpretation.DiagramSpec,
            materials = run.Interpretation.Materials.Select(m => new
            {
                path = m.RelativePath,
                kind = m.Kind.ToString(),
                usefulness = m.PossibleUsefulness.ToString(),
                temporalStatus = m.TemporalStatus.ToString(),
                statusNote = m.StatusNote,
                contextOnly = m.ContextOnly,
                summary = m.Summary
            }).ToArray()
        },
        artifactBundle = run.ArtifactBundle,
        runtimeSummary = run.SummaryLine
    };
}
else
{
    payload = new
    {
        workspaceRoot = state.WorkspaceRoot,
        health = state.Health.ToString(),
        drift = state.DriftStatus.ToString(),
        importKind = state.ImportKind.ToString(),
        relevant = state.Summary.RelevantFileCount,
        source = state.Summary.SourceFileCount,
        build = state.Summary.BuildFileCount,
        config = state.Summary.ConfigFileCount,
        docs = state.Summary.DocumentFileCount,
        assets = state.Summary.AssetFileCount,
        binaries = state.Summary.BinaryFileCount,
        noise = state.Summary.IgnoredNoiseFileCount,
        scanBudget = scan.BudgetReport,
        roots = state.Summary.SourceRoots,
        entries = state.Summary.EntryCandidates,
        anomalies = state.StructuralAnomalies.Select(a => new { a.Code, a.Message, a.Scope }).ToArray(),
        materials = scan.MaterialCandidates.Select(m => new
        {
            path = m.RelativePath,
            kind = m.Kind.ToString()
        }).ToArray()
    };
}

Console.WriteLine(JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = true }));
return 0;
