using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using ContextCapsule = zavod.Contexting.Capsule;
using ContextCapsuleBuilder = zavod.Contexting.CapsuleBuilder;
using zavod.Demo;
using ExecutionCoreCheckpointBundleBuilder = zavod.Execution.CoreCheckpointBundleBuilder;
using ExecutionRuntimeCapsule = zavod.Execution.RuntimeCapsule;
using ExecutionTaskProjectionBundleBuilder = zavod.Execution.TaskProjectionBundleBuilder;
using ExecutionRuntimeCoreCheckpointBundle = zavod.Execution.CoreCheckpointBundle;
using ExecutionRuntimeTaskState = zavod.Execution.RuntimeTaskState;
using ExecutionRuntimeTaskProjectionBundle = zavod.Execution.TaskProjectionBundle;
using ExecutionRuntimeTaskView = zavod.Execution.TaskView;
using ExecutionRuntimeSnapshot = zavod.Execution.RuntimeSnapshot;
using ExecutionSnapshotBuilder = zavod.Execution.RuntimeSnapshotBuilder;
using ExecutionCapsuleBuilder = zavod.Execution.RuntimeCapsuleBuilder;
using ExecutionEntryPackBuilder = zavod.Execution.EntryPackBuilder;
using ExecutionTaskStateBuilder = zavod.Execution.TaskStateBuilder;
using ExecutionTaskViewBuilder = zavod.Execution.TaskViewBuilder;
using StateTaskState = zavod.State.TaskState;
using TraceSnapshotBuilder = zavod.Traceing.SnapshotBuilder;
using zavod.Bootstrap;
using zavod.Boundary;
using zavod.Contexting;
using zavod.Dispatching;
using zavod.Entry;
using zavod.Execution;
using zavod.Flow;
using zavod.Lead;
using zavod.Orchestration;
using zavod.Outcome;
using zavod.Persistence;
using zavod.Planning;
using zavod.Presentation;
using zavod.Prompting;
using zavod.Retrieval;
using zavod.Router;
using zavod.Qc;
using zavod.State;
using zavod.Traceing;
using zavod.Acceptance;
using zavod.Tooling;
using zavod.Worker;
using zavod.UI.Modes.Chats;
using zavod.UI.Modes.Projects;
using zavod.UI.Modes.Projects.WorkCycle.Actions;
using zavod.UI.Rendering.Conversation;
using zavod.Welcoming;
using zavod.Workspace;

var tests = new (string Name, Action Run)[]
{
    ("Canonical anchor ordering is deterministic", CanonicalAnchorOrderingIsDeterministic),
    ("Invalid input fails fast", InvalidInputFailsFast),
    ("Missing required section fails fast", MissingRequiredSectionFailsFast),
    ("Identical input yields identical serialized output", IdenticalInputYieldsIdenticalSerializedOutput),
    ("Assembled prompt contains required sections", AssembledPromptContainsRequiredSections),
    ("Runtime truth mode is predictable", RuntimeTruthModeIsPredictable),
    ("Prompt role cores load from versioned prompt files", PromptRoleCoresLoadFromVersionedPromptFiles),
    ("Import system prompt loads from versioned prompt file", ImportSystemPromptLoadsFromVersionedPromptFile),
    ("Pipeline is the only public prompt entry path", PipelineIsTheOnlyPublicPromptEntryPath),
    ("Artifact typing is deterministic", ArtifactTypingIsDeterministic),
    ("Identical input metadata yields deterministic artifact shape", IdenticalInputMetadataYieldsDeterministicArtifactShape),
    ("Tools return structured results", ToolsReturnStructuredResults),
    ("Workspace tool summary carries structural reasons honestly", WorkspaceToolSummaryCarriesStructuralReasonsHonestly),
    ("Workspace scanner detects nested source roots honestly", WorkspaceScannerDetectsNestedSourceRootsHonestly),
    ("Workspace scanner flags multiple nested source roots honestly", WorkspaceScannerFlagsMultipleNestedSourceRootsHonestly),
    ("Workspace scanner flags nested non-source payloads beside host project honestly", WorkspaceScannerFlagsNestedNonSourcePayloadsBesideHostProjectHonestly),
    ("Workspace scanner flags nested git-backed projects honestly", WorkspaceScannerFlagsNestedGitBackedProjectsHonestly),
    ("Workspace scanner ignores generated noise directories honestly", WorkspaceScannerIgnoresGeneratedNoiseDirectoriesHonestly),
    ("Workspace scanner keeps automation folders out of primary source roots honestly", WorkspaceScannerKeepsAutomationFoldersOutOfPrimarySourceRootsHonestly),
    ("Workspace scanner rejects sibling include path prefix honestly", WorkspaceScannerRejectsSiblingIncludePathPrefixHonestly),
    ("Workspace scanner reports noisy workspace hint honestly", WorkspaceScannerReportsNoisyWorkspaceHintHonestly),
    ("Workspace scanner preserves user materials as context candidates honestly", WorkspaceScannerPreservesUserMaterialsAsContextCandidatesHonestly),
    ("Workspace scanner preserves office and multimedia materials honestly", WorkspaceScannerPreservesOfficeAndMultimediaMaterialsHonestly),
    ("Workspace scanner preserves localized history-like materials as plain context honestly", WorkspaceScannerPreservesLocalizedHistoryLikeMaterialsAsPlainContextHonestly),
    ("Workspace material shortlist prefers text preview candidates honestly", WorkspaceMaterialShortlistPrefersTextPreviewCandidatesHonestly),
    ("Workspace material shortlist prefers shallower preview candidates honestly", WorkspaceMaterialShortlistPrefersShallowerPreviewCandidatesHonestly),
    ("Workspace material text equalizer normalizes bounded text honestly", WorkspaceMaterialTextEqualizerNormalizesBoundedTextHonestly),
    ("Workspace material text equalizer skips unsupported kinds honestly", WorkspaceMaterialTextEqualizerSkipsUnsupportedKindsHonestly),
    ("Workspace import material preview packet keeps bounded text context honestly", WorkspaceImportMaterialPreviewPacketKeepsBoundedTextContextHonestly),
    ("Workspace import material preview packet skips non-text shortlist items honestly", WorkspaceImportMaterialPreviewPacketSkipsNonTextShortlistItemsHonestly),
    ("Workspace import material prompt request keeps bounded context honestly", WorkspaceImportMaterialPromptRequestKeepsBoundedContextHonestly),
    ("Workspace import material prompt request follows user language honestly", WorkspaceImportMaterialPromptRequestFollowsUserLanguageHonestly),
    ("Workspace import material prompt request is deterministic honestly", WorkspaceImportMaterialPromptRequestIsDeterministicHonestly),
    ("Workspace import material prompt response parser stays narrow honestly", WorkspaceImportMaterialPromptResponseParserStaysNarrowHonestly),
    ("Workspace import material prompt response parser accepts three-part diagram edges honestly", WorkspaceImportMaterialPromptResponseParserAcceptsThreePartDiagramEdgesHonestly),
    ("Workspace import material prompt response parser preserves fallback summary honestly", WorkspaceImportMaterialPromptResponseParserPreservesFallbackSummaryHonestly),
    ("Workspace import material interpretation maps response honestly", WorkspaceImportMaterialInterpretationMapsResponseHonestly),
    ("Workspace import material interpretation infers strong content anchors honestly", WorkspaceImportMaterialInterpretationInfersStrongContentAnchorsHonestly),
    ("Workspace import material interpretation result keeps context-only contract honestly", WorkspaceImportMaterialInterpretationResultKeepsContextOnlyContractHonestly),
    ("Workspace import material interpretation result is deterministic honestly", WorkspaceImportMaterialInterpretationResultIsDeterministicHonestly),
    ("Workspace import material interpretation does not invent layers from weak cold pack honestly", WorkspaceImportMaterialInterpretationDoesNotInventLayersFromWeakColdPackHonestly),
    ("Workspace import material diagram fallback stays coarse when evidence is weak honestly", WorkspaceImportMaterialDiagramFallbackStaysCoarseWhenEvidenceIsWeakHonestly),
    ("Workspace import material interpretation prefers bootstrap entry points over nested mains honestly", WorkspaceImportMaterialInterpretationPrefersBootstrapEntryPointsOverNestedMainsHonestly),
    ("Workspace import material interpretation demotes neutral secondary mains honestly", WorkspaceImportMaterialInterpretationDemotesNeutralSecondaryMainsHonestly),
    ("Workspace import material interpretation demotes workflow and tool mains honestly", WorkspaceImportMaterialInterpretationDemotesWorkflowAndToolMainsHonestly),
    ("Workspace import material summary is adapter-owned, not scanner-owned honestly", WorkspaceImportMaterialSummaryIsAdapterOwnedNotScannerOwnedHonestly),
    ("Workspace material runtime front prepares mixed materials honestly", WorkspaceMaterialRuntimeFrontPreparesMixedMaterialsHonestly),
    ("Workspace material runtime front skips sensitive file content honestly", WorkspaceMaterialRuntimeFrontSkipsSensitiveFileContentHonestly),
    ("Workspace material runtime front expands technical evidence beyond cmake honestly", WorkspaceMaterialRuntimeFrontExpandsTechnicalEvidenceBeyondCmakeHonestly),
    ("Workspace material runtime front keeps mixed coverage under text pressure honestly", WorkspaceMaterialRuntimeFrontKeepsMixedCoverageUnderTextPressureHonestly),
    ("Workspace evidence pack keeps runtime surfaces bounded honestly", WorkspaceEvidencePackKeepsRuntimeSurfacesBoundedHonestly),
    ("Workspace evidence pack emits cold observations patterns and scores honestly", WorkspaceEvidencePackEmitsColdObservationsPatternsAndScoresHonestly),
    ("Workspace evidence pack does not infer browser runtime from narrative readme alone honestly", WorkspaceEvidencePackDoesNotInferBrowserRuntimeFromNarrativeReadmeAloneHonestly),
    ("Workspace evidence pack tree summary is no longer scanner-authored truth honestly", WorkspaceEvidencePackTreeSummaryIsNoLongerScannerAuthoredTruthHonestly),
    ("Workspace evidence pack derives coarse modules honestly", WorkspaceEvidencePackDerivesCoarseModulesHonestly),
    ("Workspace evidence pack derives coarse dependency edges honestly", WorkspaceEvidencePackDerivesCoarseDependencyEdgesHonestly),
    ("Workspace evidence pack avoids broad reverse project inference honestly", WorkspaceEvidencePackAvoidsBroadReverseProjectInferenceHonestly),
    ("Workspace evidence pack requires supporting signals for coarse dependency edges honestly", WorkspaceEvidencePackRequiresSupportingSignalsForCoarseDependencyEdgesHonestly),
    ("Workspace evidence pack exposes cold candidates and hotspots honestly", WorkspaceEvidencePackExposesColdCandidatesAndHotspotsHonestly),
    ("Workspace evidence pack exposes root README identity as evidence honestly", WorkspaceEvidencePackExposesRootReadmeIdentityAsEvidenceHonestly),
    ("Workspace evidence pack maps project units honestly", WorkspaceEvidencePackMapsProjectUnitsHonestly),
    ("Workspace evidence pack applies scanner config unit overrides honestly", WorkspaceEvidencePackAppliesScannerConfigUnitOverridesHonestly),
    ("Workspace evidence pack maps run profiles honestly", WorkspaceEvidencePackMapsRunProfilesHonestly),
    ("Workspace evidence pack detects package json entry and run profiles honestly", WorkspaceEvidencePackDetectsPackageJsonEntryAndRunProfilesHonestly),
    ("Workspace evidence pack detects python pyproject entries honestly", WorkspaceEvidencePackDetectsPythonPyprojectEntriesHonestly),
    ("Workspace evidence pack does not promote readme narrative to entrypoint honestly", WorkspaceEvidencePackDoesNotPromoteReadmeNarrativeToEntrypointHonestly),
    ("Workspace evidence pack classifies source plus release topology honestly", WorkspaceEvidencePackClassifiesSourcePlusReleaseTopologyHonestly),
    ("Workspace evidence pack does not split root manifest from conventional source root honestly", WorkspaceEvidencePackDoesNotSplitRootManifestFromConventionalSourceRootHonestly),
    ("Workspace evidence pack classifies material only topology honestly", WorkspaceEvidencePackClassifiesMaterialOnlyTopologyHonestly),
    ("Workspace evidence pack keeps ignored dist visible as release output honestly", WorkspaceEvidencePackKeepsIgnoredDistVisibleAsReleaseOutputHonestly),
    ("Workspace evidence pack classifies low level source as legacy topology honestly", WorkspaceEvidencePackClassifiesLowLevelSourceAsLegacyTopologyHonestly),
    ("Workspace evidence pack classifies decompilation topology honestly", WorkspaceEvidencePackClassifiesDecompilationTopologyHonestly),
    ("Workspace evidence pack does not overclaim reverse topology from reference wording honestly", WorkspaceEvidencePackDoesNotOverclaimReverseTopologyFromReferenceWordingHonestly),
    ("Workspace evidence pack classifies unrelated roots as container topology honestly", WorkspaceEvidencePackClassifiesUnrelatedRootsAsContainerTopologyHonestly),
    ("Workspace evidence pack keeps container boundary stronger than nested decomp evidence honestly", WorkspaceEvidencePackKeepsContainerBoundaryStrongerThanNestedDecompEvidenceHonestly),
    ("Workspace evidence pack requires repeated structural support for runtime and platforms honestly", WorkspaceEvidencePackRequiresRepeatedStructuralSupportForRuntimeAndPlatformsHonestly),
    ("Workspace evidence pack keeps behavior and origin bounded on noisy mixed repo honestly", WorkspaceEvidencePackKeepsBehaviorAndOriginBoundedOnNoisyMixedRepoHonestly),
    ("Workspace evidence pack keeps service runtime bounded for test server roots honestly", WorkspaceEvidencePackKeepsServiceRuntimeBoundedForTestServerRootsHonestly),
    ("Workspace evidence pack keeps legacy scanner semantics deprecated honestly", WorkspaceEvidencePackKeepsLegacyScannerSemanticsDeprecatedHonestly),
    ("Workspace evidence pack requires non doc overlap for technical doc boosts honestly", WorkspaceEvidencePackRequiresNonDocOverlapForTechnicalDocBoostsHonestly),
    ("Workspace evidence pack extracts code edges and signatures honestly", WorkspaceEvidencePackExtractsCodeEdgesAndSignaturesHonestly),
    ("Workspace evidence pack annotates edge resolution honestly", WorkspaceEvidencePackAnnotatesEdgeResolutionHonestly),
    ("Workspace evidence pack improves Rust and Go code edges honestly", WorkspaceEvidencePackImprovesRustAndGoCodeEdgesHonestly),
    ("Workspace task scope resolver maps bounded scope honestly", WorkspaceTaskScopeResolverMapsBoundedScopeHonestly),
    ("Workspace evidence predicate registry maps scanner surfaces honestly", WorkspaceEvidencePredicateRegistryMapsScannerSurfacesHonestly),
    ("Scanner v2 plan forbids smoke repo specialization honestly", ScannerV2PlanForbidsSmokeRepoSpecializationHonestly),
    ("Workspace scanner fingerprint is provenance not content hash honestly", WorkspaceScannerFingerprintIsProvenanceNotContentHashHonestly),
    ("Workspace scanner reports performance budgets honestly", WorkspaceScannerReportsPerformanceBudgetsHonestly),
    ("Workspace evidence pack ranks Cargo default member entries honestly", WorkspaceEvidencePackRanksCargoDefaultMemberEntriesHonestly),
    ("Workspace evidence pack extracts dependency surface honestly", WorkspaceEvidencePackExtractsDependencySurfaceHonestly),
    ("Workspace evidence pack keeps binary hints bounded honestly", WorkspaceEvidencePackKeepsBinaryHintsBoundedHonestly),
    ("Workspace evidence pack keeps technical passport options bounded honestly", WorkspaceEvidencePackKeepsTechnicalPassportOptionsBoundedHonestly),
    ("Workspace import material interpretation builds fallback confidence slices honestly", WorkspaceImportMaterialInterpretationBuildsFallbackConfidenceSlicesHonestly),
    ("Workspace import material interpretation preserves scanner top entry honestly", WorkspaceImportMaterialInterpretationPreservesScannerTopEntryHonestly),
    ("Workspace import material interpretation preserves scanner module confidence honestly", WorkspaceImportMaterialInterpretationPreservesScannerModuleConfidenceHonestly),
    ("Workspace import preview labels package surface without main entry claim honestly", WorkspaceImportPreviewLabelsPackageSurfaceWithoutMainEntryClaimHonestly),
    ("Workspace import preview suppresses unsupported extra entries beside confirmed main honestly", WorkspaceImportPreviewSuppressesUnsupportedExtraEntriesBesideConfirmedMainHonestly),
    ("Workspace import material interpretation suppresses generic modules from weak cold evidence honestly", WorkspaceImportMaterialInterpretationSuppressesGenericModulesFromWeakColdEvidenceHonestly),
    ("Workspace import material interpretation degrades unsupported broad summary honestly", WorkspaceImportMaterialInterpretationDegradesUnsupportedBroadSummaryHonestly),
    ("Workspace import material interpretation filters unsupported narrative details honestly", WorkspaceImportMaterialInterpretationFiltersUnsupportedNarrativeDetailsHonestly),
    ("Workspace import material interpretation downgrades unsupported confidence claims honestly", WorkspaceImportMaterialInterpretationDowngradesUnsupportedConfidenceClaimsHonestly),
    ("Workspace import material interpretation suppresses unsupported custom layers honestly", WorkspaceImportMaterialInterpretationSuppressesUnsupportedCustomLayersHonestly),
    ("Workspace import material interpretation sanitizes unsupported diagram claims honestly", WorkspaceImportMaterialInterpretationSanitizesUnsupportedDiagramClaimsHonestly),
    ("Workspace import material interpretation keeps diagram title and notes coarse on weak evidence honestly", WorkspaceImportMaterialInterpretationKeepsDiagramTitleAndNotesCoarseOnWeakEvidenceHonestly),
    ("Workspace import material interpretation degrades unsupported stage claims honestly", WorkspaceImportMaterialInterpretationDegradesUnsupportedStageClaimsHonestly),
    ("Workspace import material interpretation suppresses unified narrative for multiple project container honestly", WorkspaceImportMaterialInterpretationSuppressesUnifiedNarrativeForMultipleProjectContainerHonestly),
    ("Workspace import material interpretation suppresses Terragrunt UI API drift honestly", WorkspaceImportMaterialInterpretationSuppressesTerragruntUiApiDriftHonestly),
    ("Workspace import material interpretation preserves cssDOOM web truth honestly", WorkspaceImportMaterialInterpretationPreservesCssDoomWebTruthHonestly),
    ("Workspace import material interpretation keeps x64dbg helper mains secondary honestly", WorkspaceImportMaterialInterpretationKeepsX64DbgHelperMainsSecondaryHonestly),
    ("Workspace import material interpretation suppresses codex layered inflation honestly", WorkspaceImportMaterialInterpretationSuppressesCodexLayeredInflationHonestly),
    ("Workspace import material interpretation suppresses Radare2 web UI drift honestly", WorkspaceImportMaterialInterpretationSuppressesRadare2WebUiDriftHonestly),
    ("Workspace import material prompt request demotes technical passport honestly", WorkspaceImportMaterialPromptRequestDemotesTechnicalPassportHonestly),
    ("Workspace material runtime front de-prioritizes noisy build logs honestly", WorkspaceMaterialRuntimeFrontDeprioritizesNoisyBuildLogsHonestly),
    ("Workspace material runtime front de-prioritizes procedural notes honestly", WorkspaceMaterialRuntimeFrontDeprioritizesProceduralNotesAsciiHonestly),
    ("Workspace material runtime front de-prioritizes bulk image assets honestly", WorkspaceMaterialRuntimeFrontDeprioritizesBulkImageAssetsHonestly),
    ("Pdf extraction service falls back honestly", PdfExtractionServiceFallsBackHonestly),
    ("Pdf extraction service prefers bundled pdftotext honestly", PdfExtractionServicePrefersBundledPdfToTextHonestly),
    ("Archive inspection service prefers bundled 7za honestly", ArchiveInspectionServicePrefersBundled7zaHonestly),
    ("Image inspection service uses windows image metadata honestly", ImageInspectionServiceUsesWindowsImageMetadataHonestly),
    ("External process runner drains stdout and stderr honestly", ExternalProcessRunnerDrainsStdoutAndStderrHonestly),
    ("Architecture diagram runtime renders bounded png honestly", ArchitectureDiagramRuntimeRendersBoundedPngHonestly),
    ("OpenRouter client fails fast on missing config honestly", OpenRouterClientFailsFastOnMissingConfigHonestly),
    ("OpenRouter configuration defaults import model honestly", OpenRouterConfigurationDefaultsImportModelHonestly),
    ("OpenRouter configuration can load local file honestly", OpenRouterConfigurationCanLoadLocalFileHonestly),
    ("Brave search runtime fails fast on missing config honestly", BraveSearchRuntimeFailsFastOnMissingConfigHonestly),
    ("Brave search runtime respects broker denial honestly", BraveSearchRuntimeRespectsBrokerDenialHonestly),
    ("Web search tool returns structured results through brave runtime honestly", WebSearchToolReturnsStructuredResultsThroughBraveRuntimeHonestly),
    ("Workspace import material interpreter runtime builds context-only result honestly", WorkspaceImportMaterialInterpreterRuntimeBuildsContextOnlyResultHonestly),
    ("Workspace evidence artifact runtime writes localized report legacy honestly", WorkspaceEvidenceArtifactRuntimeWritesLocalizedReportHonestly),
    ("Workspace evidence artifact runtime writes localized report honestly", WorkspaceEvidenceArtifactRuntimeWritesLocalizedReportUtf8Honestly),
    ("Workspace evidence artifact runtime writes readable utf8 json honestly", WorkspaceEvidenceArtifactRuntimeWritesReadableUtf8JsonHonestly),
    ("Workspace evidence artifact runtime writes cold scanner payloads honestly", WorkspaceEvidenceArtifactRuntimeWritesColdScannerPayloadsHonestly),
    ("Workspace evidence artifact runtime report does not re-inflate filtered modules honestly", WorkspaceEvidenceArtifactRuntimeReportDoesNotReinflateFilteredModulesHonestly),
    ("Workspace evidence artifact runtime attaches modules only through structured cold matches honestly", WorkspaceEvidenceArtifactRuntimeAttachesModulesOnlyThroughStructuredColdMatchesHonestly),
    ("Workspace evidence artifact runtime keeps bundle at workspace root when dot source root exists honestly", WorkspaceEvidenceArtifactRuntimeKeepsBundleAtWorkspaceRootWhenDotSourceRootExistsHonestly),
    ("Workspace evidence artifact runtime prefers git-backed nested project root honestly", WorkspaceEvidenceArtifactRuntimePrefersGitBackedNestedProjectRootHonestly),
    ("Workspace evidence artifact runtime keeps bundle at workspace root for multiple project container honestly", WorkspaceEvidenceArtifactRuntimeKeepsBundleAtWorkspaceRootForMultipleProjectContainerHonestly),
    ("Workspace evidence artifact runtime suppresses unified report projection for multiple project container honestly", WorkspaceEvidenceArtifactRuntimeSuppressesUnifiedReportProjectionForMultipleProjectContainerHonestly),
    ("Workspace evidence artifact runtime writes preview html for single project honestly", WorkspaceEvidenceArtifactRuntimeWritesPreviewHtmlForSingleProjectHonestly),
    ("Workspace evidence artifact runtime writes preview html warning for multiple project container honestly", WorkspaceEvidenceArtifactRuntimeWritesPreviewHtmlWarningForMultipleProjectContainerHonestly),
    ("Workspace evidence artifact runtime writes preview html warning for ambiguous container honestly", WorkspaceEvidenceArtifactRuntimeWritesPreviewHtmlWarningForAmbiguousContainerHonestly),
    ("Workspace evidence artifact runtime writes preview html from canonical docs honestly", WorkspaceEvidenceArtifactRuntimeWritesPreviewHtmlFromCanonicalDocsHonestly),
    ("Workspace evidence artifact runtime writes preview docs honestly", WorkspaceEvidenceArtifactRuntimeWritesPreviewDocsHonestly),
    ("Project document runtime writes bounded container project preview honestly", ProjectDocumentRuntimeWritesBoundedContainerProjectPreviewHonestly),
    ("Project document runtime preserves nonstandard topology in project preview honestly", ProjectDocumentRuntimePreservesNonstandardTopologyInProjectPreviewHonestly),
    ("Project document runtime promotes nonstandard topology preview honestly", ProjectDocumentRuntimePromotesNonstandardTopologyPreviewHonestly),
    ("Project document runtime keeps project preview identity stable on reimport honestly", ProjectDocumentRuntimeKeepsProjectPreviewIdentityStableOnReimportHonestly),
    ("Project document runtime writes observed canon preview honestly", ProjectDocumentRuntimeWritesObservedCanonPreviewHonestly),
    ("Project document runtime writes candidate direction preview honestly", ProjectDocumentRuntimeWritesCandidateDirectionPreviewHonestly),
    ("Project document runtime writes unknown-only direction without README honestly", ProjectDocumentRuntimeWritesUnknownOnlyDirectionWithoutReadmeHonestly),
    ("Project document runtime writes candidate roadmap preview honestly", ProjectDocumentRuntimeWritesCandidateRoadmapPreviewHonestly),
    ("Project document runtime writes unknown-only roadmap without git honestly", ProjectDocumentRuntimeWritesUnknownOnlyRoadmapWithoutGitHonestly),
    ("Project document source selector resolves import preview preview docs and canonical stages honestly", ProjectDocumentSourceSelectorResolvesStagesHonestly),
    ("Project document runtime confirms preview docs into 5 of 5 canonical docs honestly", ProjectDocumentRuntimeConfirmsPreviewDocsIntoFiveCanonicalDocsHonestly),
    ("Project document runtime promotes capsule without phantom project canon honestly", ProjectDocumentRuntimePromotesCapsuleWithoutPhantomProjectCanonHonestly),
    ("Project document runtime rejects preview doc with journal event honestly", ProjectDocumentRuntimeRejectsPreviewDocWithJournalEventHonestly),
    ("Project document runtime regenerates capsule v2 deterministically honestly", ProjectDocumentRuntimeRegeneratesCapsuleV2DeterministicallyHonestly),
    ("Workspace import material interpreter runtime preserves upstream failure honestly", WorkspaceImportMaterialInterpreterRuntimePreservesUpstreamFailureHonestly),
    ("Workspace scanner marks empty import missing honestly", WorkspaceScannerMarksEmptyImportMissingHonestly),
    ("Workspace scanner separates primary source roots from build roots honestly", WorkspaceScannerSeparatesPrimarySourceRootsFromBuildRootsHonestly),
    ("Workspace scanner classifies config only import honestly", WorkspaceScannerClassifiesConfigOnlyImportHonestly),
    ("Workspace scanner handles documents only import honestly", WorkspaceScannerHandlesDocumentsOnlyImportHonestly),
    ("Workspace scanner classifies non-source import honestly", WorkspaceScannerClassifiesNonSourceImportHonestly),
    ("Workspace scanner classifies mixed import honestly", WorkspaceScannerClassifiesMixedImportHonestly),
    ("Workspace scanner classifies assembler source import honestly", WorkspaceScannerClassifiesAssemblerSourceImportHonestly),
    ("Workspace scanner classifies assembler mixed import honestly", WorkspaceScannerClassifiesAssemblerMixedImportHonestly),
    ("Workspace scanner classifies binary import honestly", WorkspaceScannerClassifiesBinaryImportHonestly),
    ("Workspace scanner classifies source plus binaries as mixed honestly", WorkspaceScannerClassifiesSourcePlusBinariesAsMixedHonestly),
    ("Workspace scanner classifies scripting import honestly", WorkspaceScannerClassifiesScriptingImportHonestly),
    ("Workspace scanner recognizes manifest only import honestly", WorkspaceScannerRecognizesManifestOnlyImportHonestly),
    ("Workspace scanner classifies modern web source import honestly", WorkspaceScannerClassifiesModernWebSourceImportHonestly),
    ("Workspace scanner recognizes web config only import honestly", WorkspaceScannerRecognizesWebConfigOnlyImportHonestly),
    ("Workspace scanner recognizes infra marker only import honestly", WorkspaceScannerRecognizesInfraMarkerOnlyImportHonestly),
    ("Workspace scanner classifies infra markers plus docs honestly", WorkspaceScannerClassifiesInfraMarkersPlusDocsHonestly),
    ("Workspace scanner classifies sql import honestly", WorkspaceScannerClassifiesSqlImportHonestly),
    ("Workspace scanner recognizes data tooling config only import honestly", WorkspaceScannerRecognizesDataToolingConfigOnlyImportHonestly),
    ("Workspace baseline marks non-project import as partial", WorkspaceBaselineMarksNonProjectImportAsPartial),
    ("Acceptance guard allows safe apply for unchanged touched scope", AcceptanceGuardAllowsSafeApplyForUnchangedTouchedScope),
    ("Acceptance guard blocks touched file conflict", AcceptanceGuardBlocksTouchedFileConflict),
    ("Acceptance guard rejects sibling path prefix escape honestly", AcceptanceGuardRejectsSiblingPathPrefixEscapeHonestly),
    ("Staging task id path segment accepts safe ids honestly", StagingTaskIdPathSegmentAcceptsSafeIdsHonestly),
    ("Staging task id path segment rejects unsafe ids honestly", StagingTaskIdPathSegmentRejectsUnsafeIdsHonestly),
    ("Staging writer rejects sibling path prefix escape honestly", StagingWriterRejectsSiblingPathPrefixEscapeHonestly),
    ("Staging applier blocks hash drift honestly", StagingApplierBlocksHashDriftHonestly),
    ("Staging applier ignores manifest absolute staged path honestly", StagingApplierIgnoresManifestAbsoluteStagedPathHonestly),
    ("Acceptance evidence can be assembled from execution run result", AcceptanceEvidenceCanBeAssembledFromExecutionRunResult),
    ("Acceptance evidence factory assembles scanner and execution layers together", AcceptanceEvidenceFactoryAssemblesScannerAndExecutionLayersTogether),
    ("Acceptance evidence carries tool execution envelope automatically", AcceptanceEvidenceCarriesToolExecutionEnvelopeAutomatically),
    ("Acceptance evaluation factory produces safe apply for unchanged workspace", AcceptanceEvaluationFactoryProducesSafeApplyForUnchangedWorkspace),
    ("Acceptance evaluation factory produces conflict for touched file drift", AcceptanceEvaluationFactoryProducesConflictForTouchedFileDrift),
    ("Execution acceptance adapter evaluates produced runtime against workspace", ExecutionAcceptanceAdapterEvaluatesProducedRuntimeAgainstWorkspace),
    ("Execution runtime can observe acceptance after QC accept", ExecutionRuntimeCanObserveAcceptanceAfterQcAccept),
    ("Execution runtime preserves explicit runtime profile", ExecutionRuntimePreservesExplicitRuntimeProfile),
    ("Typed runtime package stays fixed and deterministic", TypedRuntimePackageStaysFixedAndDeterministic),
    ("Runtime profile catalog exposes default families deterministically", RuntimeProfileCatalogExposesDefaultFamiliesDeterministically),
    ("Runtime profile resolver prefers explicit profile over fallback selectors", RuntimeProfileResolverPrefersExplicitProfileOverFallbackSelectors),
    ("Runtime selection policy defaults to scoped local", RuntimeSelectionPolicyDefaultsToScopedLocal),
    ("Runtime selection policy blocks local unsafe outside trusted dev", RuntimeSelectionPolicyBlocksLocalUnsafeOutsideTrustedDev),
    ("Runtime selection policy allows container for heavier isolation", RuntimeSelectionPolicyAllowsContainerForHeavierIsolation),
    ("Runtime selection policy keeps remote and VM opt-in only", RuntimeSelectionPolicyKeepsRemoteAndVmOptInOnly),
    ("Isolation backend contracts stay runtime scoped honestly", IsolationBackendContractsStayRuntimeScopedHonestly),
    ("Runtime selection request builder creates default safe request", RuntimeSelectionRequestBuilderCreatesDefaultSafeRequest),
    ("Runtime selection request builder preserves launch context flags", RuntimeSelectionRequestBuilderPreservesLaunchContextFlags),
    ("Execution runtime begin uses runtime selection policy", ExecutionRuntimeBeginUsesRuntimeSelectionPolicy),
    ("Unified tool layer is UI-agnostic", UnifiedToolLayerIsUiAgnostic),
    ("Unified tool layer supports lead and worker roles", UnifiedToolLayerSupportsLeadAndWorkerRoles),
    ("Worker tool resolver keeps capability profiles bounded", WorkerToolResolverKeepsCapabilityProfilesBounded),
    ("Unified tool layer exposes visible worker tools deterministically", UnifiedToolLayerExposesVisibleWorkerToolsDeterministically),
    ("Unified tool layer keeps lead narrower than worker", UnifiedToolLayerKeepsLeadNarrowerThanWorker),
    ("Role tool resolver keeps QC and senior specialist bounded", RoleToolResolverKeepsQcAndSeniorSpecialistBounded),
    ("Tool execution envelope carries route and evidence", ToolExecutionEnvelopeCarriesRouteAndEvidence),
    ("Watchdog start and signals stay deterministic", WatchdogStartAndSignalsStayDeterministic),
    ("Watchdog evaluate returns bounded interruption reasons", WatchdogEvaluateReturnsBoundedInterruptionReasons),
    ("Execution runtime watchdog interruption is preserved in acceptance evidence", ExecutionRuntimeWatchdogInterruptionIsPreservedInAcceptanceEvidence),
    ("Unsupported input type is handled predictably", UnsupportedInputTypeIsHandledPredictably),
    ("Artifact inventory builds deterministically", ArtifactInventoryBuildsDeterministically),
    ("Candidate selection is deterministic and shortlist-bounded", CandidateSelectionIsDeterministicAndShortlistBounded),
    ("Retrieval layer is UI and LLM agnostic", RetrievalLayerIsUiAndLlmAgnostic),
    ("Scoped context is built predictably", ScopedContextIsBuiltPredictably),
    ("Execution flow transitions are deterministic", ExecutionFlowTransitionsAreDeterministic),
    ("Execution requires validated intent", ExecutionRequiresValidatedIntent),
    ("Execution cannot start without scoped context", ExecutionCannotStartWithoutScopedContext),
    ("Execution result must stay bound to task", ExecutionResultMustStayBoundToTask),
    ("QC cannot review missing result", QcCannotReviewMissingResult),
    ("Result lifecycle flow is deterministic", ResultLifecycleFlowIsDeterministic),
    ("Result lifecycle stages cannot be skipped", ResultLifecycleStagesCannotBeSkipped),
    ("Accepted result remains immutable through apply and commit flow", AcceptedResultRemainsImmutableThroughBoundaryFlow),
    ("Commit is always linked to apply", CommitIsAlwaysLinkedToApply),
    ("Shift state updates only through explicit commit step", ShiftStateUpdatesOnlyThroughExplicitCommitStep),
    ("Execution trace order is deterministic", ExecutionTraceOrderIsDeterministic),
    ("Trace is append-only", TraceIsAppendOnly),
    ("Commit is always reflected in trace", CommitIsAlwaysReflectedInTrace),
    ("Snapshot is consistent and deterministic", SnapshotIsConsistentAndDeterministic),
    ("Snapshot file persists checkpoint metadata", SnapshotFilePersistsCheckpointMetadata),
    ("Persistence bootstrap creates minimal .zavod layout", PersistenceBootstrapCreatesMinimalZavodLayout),
    ("Project state load save roundtrip is deterministic", ProjectStateLoadSaveRoundtripIsDeterministic),
    ("Cold start detection stays honest", ColdStartDetectionStaysHonest),
    ("Invalid persisted project meta fails fast", InvalidPersistedProjectMetaFailsFast),
    ("Snapshot file is persisted deterministically", SnapshotFileIsPersistedDeterministically),
    ("Bootstrap reports cold start for empty project", BootstrapReportsColdStartForEmptyProject),
    ("Bootstrap reports normal state for active shift", BootstrapReportsNormalStateForActiveShift),
    ("Bootstrap preserves fail fast for invalid meta", BootstrapPreservesFailFastForInvalidMeta),
    ("Bootstrap is idempotent across repeated startup", BootstrapIsIdempotentAcrossRepeatedStartup),
    ("Project entry selector routes cold start state to bootstrap mode", ProjectEntrySelectorRoutesColdStartStateToBootstrapMode),
    ("Project entry selector routes active shift state to resume mode", ProjectEntrySelectorRoutesActiveShiftStateToResumeMode),
    ("Project entry selector fails fast for active task without active shift", ProjectEntrySelectorFailsFastForActiveTaskWithoutActiveShift),
    ("Project entry resolver returns unified bootstrap result", ProjectEntryResolverReturnsUnifiedBootstrapResult),
    ("Project entry resolver returns unified resume result", ProjectEntryResolverReturnsUnifiedResumeResult),
    ("Active shift resume returns persisted active shift and task truth", ActiveShiftResumeReturnsPersistedActiveShiftAndTaskTruth),
    ("Active shift resume fails fast for mismatched active task binding", ActiveShiftResumeFailsFastForMismatchedActiveTaskBinding),
    ("Task intent validates candidate before canonical task creation", TaskIntentValidatesCandidateBeforeCanonicalTaskCreation),
    ("Task factory rejects non-validated intent", TaskFactoryRejectsNonValidatedIntent),
    ("Validated intent task applier binds task to shift and project state", ValidatedIntentTaskApplierBindsTaskToShiftAndProjectState),
    ("Validated intent task applier rejects occupied shift task slot", ValidatedIntentTaskApplierRejectsOccupiedShiftTaskSlot),
    ("Validated intent task applier rejects occupied project task slot", ValidatedIntentTaskApplierRejectsOccupiedProjectTaskSlot),
    ("Validated intent task applier rejects mismatched active shift binding", ValidatedIntentTaskApplierRejectsMismatchedActiveShiftBinding),
    ("First shift bootstrap creates active shift truth from cold start", FirstShiftBootstrapCreatesActiveShiftTruthFromColdStart),
    ("First shift bootstrap enables full cycle from cold start to snapshot", FirstShiftBootstrapEnablesFullCycleFromColdStartToSnapshot),
    ("Validated intent shift starter uses first shift seam for empty history", ValidatedIntentShiftStarterUsesFirstShiftSeamForEmptyHistory),
    ("Validated intent shift starter uses non-first seam for existing history", ValidatedIntentShiftStarterUsesNonFirstSeamForExistingHistory),
    ("Lead maps cold start to ColdStart mode", LeadMapsColdStartToColdStartMode),
    ("Lead maps valid idle state to Idle mode", LeadMapsValidIdleStateToIdleMode),
    ("Lead maps active shift state to ActiveWork mode", LeadMapsActiveShiftStateToActiveWorkMode),
    ("Planner maps ColdStart to EnterBootstrapFlow", PlannerMapsColdStartToEnterBootstrapFlow),
    ("Planner maps Idle to StayIdle", PlannerMapsIdleToStayIdle),
    ("Planner maps ActiveWork to ResumeActiveShift", PlannerMapsActiveWorkToResumeActiveShift),
    ("Planner ignores lead reason for logic", PlannerIgnoresLeadReasonForLogic),
    ("Router maps EnterBootstrapFlow to BootstrapScenario", RouterMapsEnterBootstrapFlowToBootstrapScenario),
    ("Router maps StayIdle to IdleScenario", RouterMapsStayIdleToIdleScenario),
    ("Router maps ResumeActiveShift to ActiveShiftScenario", RouterMapsResumeActiveShiftToActiveShiftScenario),
    ("Router ignores planning reason for logic", RouterIgnoresPlanningReasonForLogic),
    ("Presenter maps BootstrapScenario to presentation payload", PresenterMapsBootstrapScenarioToPresentationPayload),
    ("Presenter maps IdleScenario to presentation payload", PresenterMapsIdleScenarioToPresentationPayload),
    ("Presenter maps ActiveShiftScenario to presentation payload", PresenterMapsActiveShiftScenarioToPresentationPayload),
    ("Presenter ignores route reason for logic", PresenterIgnoresRouteReasonForLogic),
    ("BootstrapScenario maps to StartBootstrap action", BootstrapScenarioMapsToStartBootstrapAction),
    ("IdleScenario maps to StayIdle action", IdleScenarioMapsToStayIdleAction),
    ("ActiveShiftScenario maps to ResumeActiveShift action", ActiveShiftScenarioMapsToResumeActiveShiftAction),
    ("PrimaryActionLabel does not define action logic", PrimaryActionLabelDoesNotDefineActionLogic),
    ("StartBootstrap maps to StartBootstrapFlow intent", StartBootstrapMapsToStartBootstrapFlowIntent),
    ("StayIdle maps to StayIdle intent", StayIdleMapsToStayIdleIntent),
    ("ResumeActiveShift maps to ResumeActiveShift intent", ResumeActiveShiftMapsToResumeActiveShiftIntent),
    ("Presentation text does not define execution entry logic", PresentationTextDoesNotDefineExecutionEntryLogic),
    ("StartBootstrapFlow hands off to BootstrapSubsystem", StartBootstrapFlowHandsOffToBootstrapSubsystem),
    ("StayIdle hands off to IdleSubsystem", StayIdleHandsOffToIdleSubsystem),
    ("ResumeActiveShift hands off to ActiveShiftSubsystem", ResumeActiveShiftHandsOffToActiveShiftSubsystem),
    ("Upstream presentation text does not define handoff logic", UpstreamPresentationTextDoesNotDefineHandoffLogic),
    ("Dispatcher routes bootstrap target to bootstrap subsystem", DispatcherRoutesBootstrapTargetToBootstrapSubsystem),
    ("Dispatcher routes idle target to idle subsystem", DispatcherRoutesIdleTargetToIdleSubsystem),
    ("Dispatcher routes active shift target to active shift subsystem", DispatcherRoutesActiveShiftTargetToActiveShiftSubsystem),
    ("Dispatcher only routes and does not invoke unrelated subsystems", DispatcherOnlyRoutesToSelectedSubsystem),
    ("Bootstrap subsystem returns deterministic result for cold start", BootstrapSubsystemReturnsDeterministicResultForColdStart),
    ("Bootstrap subsystem returns deterministic result for valid state without active shift", BootstrapSubsystemReturnsDeterministicResultForValidStateWithoutActiveShift),
    ("Bootstrap subsystem returns deterministic result for active shift state", BootstrapSubsystemReturnsDeterministicResultForActiveShiftState),
    ("Active shift subsystem returns deterministic result when active shift is present", ActiveShiftSubsystemReturnsDeterministicResultWhenActiveShiftIsPresent),
    ("Active shift subsystem returns deterministic result when active shift is missing", ActiveShiftSubsystemReturnsDeterministicResultWhenActiveShiftIsMissing),
    ("Active shift subsystem rejects inconsistent active task without shift", ActiveShiftSubsystemRejectsInconsistentActiveTaskWithoutShift),
    ("Idle subsystem returns deterministic result when state is idle and consistent", IdleSubsystemReturnsDeterministicResultWhenStateIsIdleAndConsistent),
    ("Idle subsystem rejects active shift state", IdleSubsystemRejectsActiveShiftState),
    ("Idle subsystem rejects inconsistent active task without shift", IdleSubsystemRejectsInconsistentActiveTaskWithoutShift),
    ("Outcome layer maps NoOp deterministically", OutcomeLayerMapsNoOpDeterministically),
    ("Outcome layer maps Deferred deterministically", OutcomeLayerMapsDeferredDeterministically),
    ("Outcome layer maps Rejected deterministically", OutcomeLayerMapsRejectedDeterministically),
    ("Outcome layer preserves informative message", OutcomeLayerPreservesInformativeMessage),
    ("Execution pipeline returns deferred outcome for bootstrap target", ExecutionPipelineReturnsDeferredOutcomeForBootstrapTarget),
    ("Execution pipeline returns no-op outcome for idle target", ExecutionPipelineReturnsNoOpOutcomeForIdleTarget),
    ("Execution pipeline returns rejected outcome for active shift target", ExecutionPipelineReturnsRejectedOutcomeForActiveShiftTarget),
    ("Execution pipeline preserves message and single routed call", ExecutionPipelinePreservesMessageAndSingleRoutedCall),
    ("Execution pipeline returns deterministic outcome and record", ExecutionPipelineReturnsDeterministicOutcomeAndRecord),
    ("Execution record truthfully mirrors outcome", ExecutionRecordTruthfullyMirrorsOutcome),
    ("Closure candidate mirrors execution run result deterministically", ClosureCandidateMirrorsExecutionRunResultDeterministically),
    ("Closure candidate derives deferred follow-up state deterministically", ClosureCandidateDerivesDeferredFollowUpStateDeterministically),
    ("Closure candidate derives rejected state deterministically", ClosureCandidateDerivesRejectedStateDeterministically),
    ("Shift closure proposal mirrors closure candidate deterministically", ShiftClosureProposalMirrorsClosureCandidateDeterministically),
    ("Shift closure proposal keeps deferred execution open", ShiftClosureProposalKeepsDeferredExecutionOpen),
    ("Shift closure proposal marks no-op execution eligible to close", ShiftClosureProposalMarksNoOpExecutionEligibleToClose),
    ("Shift closure proposal keeps rejected execution open", ShiftClosureProposalKeepsRejectedExecutionOpen),
    ("Shift update candidate mirrors proposal deterministically", ShiftUpdateCandidateMirrorsProposalDeterministically),
    ("Shift update candidate marks keep-open deterministically", ShiftUpdateCandidateMarksKeepOpenDeterministically),
    ("Shift update candidate marks eligible-to-close deterministically", ShiftUpdateCandidateMarksEligibleToCloseDeterministically),
    ("Shift update candidate preserves rejected outcome fact", ShiftUpdateCandidatePreservesRejectedOutcomeFact),
    ("Shift update applier maps eligible-to-close candidate to WouldClose", ShiftUpdateApplierMapsEligibleToCloseCandidateToWouldClose),
    ("Shift update applier maps keep-open candidate to WouldKeepOpen", ShiftUpdateApplierMapsKeepOpenCandidateToWouldKeepOpen),
    ("Shift update applier maps rejected candidate to Rejected", ShiftUpdateApplierMapsRejectedCandidateToRejected),
    ("Shift update applier keeps neutral candidate as NoChange", ShiftUpdateApplierKeepsNeutralCandidateAsNoChange),
    ("Shift update decision allows apply for WouldClose", ShiftUpdateDecisionAllowsApplyForWouldClose),
    ("Shift update decision allows apply for WouldKeepOpen", ShiftUpdateDecisionAllowsApplyForWouldKeepOpen),
    ("Shift update decision denies apply for Rejected", ShiftUpdateDecisionDeniesApplyForRejected),
    ("Shift update decision denies apply for NoChange", ShiftUpdateDecisionDeniesApplyForNoChange),
    ("Project state mutator blocks direct execution close mutation", ProjectStateMutatorBlocksDirectExecutionCloseMutation),
    ("Project state mutator keeps direct execution keep-open path unchanged", ProjectStateMutatorKeepsDirectExecutionKeepOpenPathUnchanged),
    ("Project state mutator leaves state unchanged for deny-apply", ProjectStateMutatorLeavesStateUnchangedForDenyApply),
    ("Project state mutator does not write persistence", ProjectStateMutatorDoesNotWritePersistence),
    ("Task execution context binds active task deterministically", TaskExecutionContextBindsActiveTaskDeterministically),
    ("Task execution context rejects missing current task", TaskExecutionContextRejectsMissingCurrentTask),
    ("Task execution context rejects missing project active task", TaskExecutionContextRejectsMissingProjectActiveTask),
    ("Persistence decision allows persist for mutated state", PersistenceDecisionAllowsPersistForMutatedState),
    ("Persistence decision skips persist for unchanged state", PersistenceDecisionSkipsPersistForUnchangedState),
    ("Persistence decision does not write to disk", PersistenceDecisionDoesNotWriteToDisk),
    ("Save applier blocks direct project state persistence outside closure", SaveApplierBlocksDirectProjectStatePersistenceOutsideClosure),
    ("Save applier skips unchanged project state", SaveApplierSkipsUnchangedProjectState),
    ("Save applier blocked path does not write project state", SaveApplierBlockedPathDoesNotWriteProjectState),
    ("Save applier blocked path does not write snapshot capsule or task files", SaveApplierBlockedPathDoesNotWriteSnapshotCapsuleOrTaskFiles),
    ("Non-closable execution outcome does not mutate closure truth", NonClosableExecutionOutcomeDoesNotMutateClosureTruth),
    ("Successful confirmed closure persists canonical truth", SuccessfulConfirmedClosurePersistsCanonicalTruth),
    ("Accept result applies and completes task without closing shift", AcceptResultAppliesAndCompletesTaskWithoutClosingShift),
    ("Accept result blocks missing acceptance evaluation honestly", AcceptResultBlocksMissingAcceptanceEvaluationHonestly),
    ("Accept result still applies when observed acceptance is safe", AcceptResultStillAppliesWhenObservedAcceptanceIsSafe),
    ("Accept result is blocked when observed acceptance conflicts", AcceptResultIsBlockedWhenObservedAcceptanceConflicts),
    ("Soft checkpoint resolver ignores light step", SoftCheckpointResolverIgnoresLightStep),
    ("Soft checkpoint resolver detects semantic shift without project docs", SoftCheckpointResolverDetectsSemanticShiftWithoutProjectDocs),
    ("Soft checkpoint processor writes snapshot without mutating truth", SoftCheckpointProcessorWritesSnapshotWithoutMutatingTruth),
    ("Soft checkpoint processor is idempotent by dedupe key", SoftCheckpointProcessorIsIdempotentByDedupeKey),
    ("Hard checkpoint path short-circuits soft snapshot write", HardCheckpointPathShortCircuitsSoftSnapshotWrite),
    ("Proof scenario light step logs no soft snapshot", ProofScenarioLightStepLogsNoSoftSnapshot),
    ("Proof scenario semantic shift logs soft snapshot", ProofScenarioSemanticShiftLogsSoftSnapshot),
    ("Proof scenario hard checkpoint logs short circuit", ProofScenarioHardCheckpointLogsShortCircuit),
    ("Proof scenario repeated accept logs dedupe", ProofScenarioRepeatedAcceptLogsDedupe),
    ("Projects adapter streams partial updates and final render deterministically", ProjectsAdapterStreamsPartialUpdatesAndFinalRenderDeterministically),
    ("Conversation action visibility only shows in project non-user messages", ConversationActionVisibilityOnlyShowsInProjectNonUserMessages),
    ("Project state storage initializes shared and local persistence roots honestly", ProjectStateStorageInitializesSharedAndLocalPersistenceRootsHonestly),
    ("Resume stage storage persists under .zavod.local honestly", ResumeStageStoragePersistsUnderZavodLocalHonestly),
    ("Chats adapter persists local conversation separately from project truth honestly", ChatsAdapterPersistsLocalConversationSeparatelyFromProjectTruthHonestly),
    ("Conversation index tracks multiple chats independently honestly", ConversationIndexTracksMultipleChatsIndependentlyHonestly),
    ("Conversation storage falls back to legacy active chat file honestly", ConversationStorageFallsBackToLegacyActiveChatFileHonestly),
    ("Conversation storage windowing deduplicates logical items honestly", ConversationStorageWindowingDeduplicatesLogicalItemsHonestly),
    ("Chats adapter stores full logs outside timeline honestly", ChatsAdapterStoresFullLogsOutsideTimelineHonestly),
    ("Projects adapter stores artifacts as references honestly", ProjectsAdapterStoresArtifactsAsReferencesHonestly),
    ("Project conversations stay separate on the same engine honestly", ProjectConversationsStaySeparateOnTheSameEngineHonestly),
    ("Validated intent shift starter carries scope and acceptance honestly", ValidatedIntentShiftStarterCarriesScopeAndAcceptanceHonestly),
    ("Conversation composer draft store stages files honestly", ConversationComposerDraftStoreStagesFilesHonestly),
    ("Conversation composer draft store stages long text as artifact honestly", ConversationComposerDraftStoreStagesLongTextAsArtifactHonestly),
    ("Conversation log storage writes readable utf8 honestly", ConversationLogStorageWritesReadableUtf8Honestly),
    ("Chats web assets keep utf8 and plain text rendering honestly", ChatsWebAssetsKeepUtf8AndPlainTextRenderingHonestly),
    ("Chats runtime controller includes attachment content in execution request honestly", ChatsRuntimeControllerIncludesAttachmentContentInExecutionRequestHonestly),
    ("Projects flow consumes attachments before work-cycle send honestly", ProjectsFlowConsumesAttachmentsBeforeWorkCycleSendHonestly),
    ("Projects work cycle confirm preflight creates runtime-backed result honestly", ProjectsWorkCycleConfirmPreflightCreatesRuntimeBackedResultHonestly),
    ("Projects work cycle QC unavailable does not open result surface honestly", ProjectsWorkCycleQcUnavailableDoesNotOpenResultSurfaceHonestly),
    ("Projects work cycle blocks physical apply before acceptance gate honestly", ProjectsWorkCycleBlocksPhysicalApplyBeforeAcceptanceGateHonestly),
    ("Projects work cycle blocks truth apply when staging apply skips files honestly", ProjectsWorkCycleBlocksTruthApplyWhenStagingApplySkipsFilesHonestly),
    ("Projects work cycle accept result updates truth honestly", ProjectsWorkCycleAcceptResultUpdatesTruthHonestly),
    ("Project sage finds relevant history honestly", ProjectSageFindsRelevantHistoryHonestly),
    ("Project sage stays quiet without match honestly", ProjectSageStaysQuietWithoutMatchHonestly),
    ("Projects adapter persists local conversation separately from project truth honestly", ProjectsAdapterPersistsLocalConversationSeparatelyFromProjectTruthHonestly),
    ("Accepted shift closure keeps shared truth separate from local conversation honestly", AcceptedShiftClosureKeepsSharedTruthSeparateFromLocalConversationHonestly),
    ("Git ignore keeps .zavod.local out of shared commits honestly", GitIgnoreKeepsZavodLocalOutOfSharedCommitsHonestly),
    ("Accepted result cannot be applied twice", AcceptedResultCannotBeAppliedTwice),
    ("Accepted shift closure finalizes shift and clears active binding", AcceptedShiftClosureFinalizesShiftAndClearsActiveBinding),
    ("Post-accept persisted shift closes explicitly through truth-based accepted context", PostAcceptPersistedShiftClosesExplicitlyThroughTruthBasedAcceptedContext),
    ("Result abandon persists abandoned task and keeps shift active", ResultAbandonPersistsAbandonedTaskAndKeepsShiftActive),
    ("Result revision keeps active bindings and starts new execution cycle", ResultRevisionKeepsActiveBindingsAndStartsNewExecutionCycle),
    ("Accepted result cannot be abandoned after apply", AcceptedResultCannotBeAbandonedAfterApply),
    ("Accepted result cannot be returned for revision after apply reentry", AcceptedResultCannotBeReturnedForRevisionAfterApplyReentry),
    ("QC accept without result fails fast", QcAcceptWithoutResultFailsFast),
    ("Post-accept active shift discussion allows fresh ready intent reentry", PostAcceptActiveShiftDiscussionAllowsFreshReadyIntentReentry),
    ("Abandoned result restart keeps active shift truth", AbandonedResultRestartKeepsActiveShiftTruth),
    ("QC reject revision accept loop enables closure", QcRejectRevisionAcceptLoopEnablesClosure),
    ("Completed result revision restart starts new work cycle without QC reuse", CompletedResultRevisionRestartStartsNewWorkCycleWithoutQcReuse),
    ("In-progress cancel stops execution without producing result", InProgressCancelStopsExecutionWithoutProducingResult),
    ("QC reject forbids accept without new result", QcRejectForbidsAcceptWithoutNewResult),
    ("Multiple revision loops remain runtime only and append result history", MultipleRevisionLoopsRemainRuntimeOnlyAndAppendResultHistory),
    ("QC reject requires reason", QcRejectRequiresReason),
    ("QC reject reason is stored in runtime state", QcRejectReasonIsStoredInRuntimeState),
    ("New execution result clears previous reject reason", NewExecutionResultClearsPreviousRejectReason),
    ("Reject reason does not change closure eligibility on its own", RejectReasonDoesNotChangeClosureEligibilityOnItsOwn),
    ("Multiple revision loops update last reject reason", MultipleRevisionLoopsUpdateLastRejectReason),
    ("Execution attempts grow on each new result", ExecutionAttemptsGrowOnEachNewResult),
    ("Execution reject marks current attempt with reason", ExecutionRejectMarksCurrentAttemptWithReason),
    ("Execution accept marks current attempt as accepted", ExecutionAcceptMarksCurrentAttemptAsAccepted),
    ("New attempt does not overwrite previous attempts", NewAttemptDoesNotOverwritePreviousAttempts),
    ("Execution attempts remain runtime only", ExecutionAttemptsRemainRuntimeOnly),
    ("Multiple revision loops build execution attempt chain", MultipleRevisionLoopsBuildExecutionAttemptChain),
    ("Execution trace entry is built deterministically from run mutation and save", ExecutionTraceEntryIsBuiltDeterministicallyFromRunMutationAndSave),
    ("Interrupted execution builds deferred trace record with user reason", InterruptedExecutionBuildsDeferredTraceRecordWithUserReason),
    ("Execution trace preserves pipeline statuses", ExecutionTracePreservesPipelineStatuses),
    ("Execution trace is append-only", ExecutionTraceIsAppendOnly),
    ("Execution trace builder has no side effects", ExecutionTraceBuilderHasNoSideEffects),
    ("Shift trace entry is built deterministically from execution trace entry", ShiftTraceEntryIsBuiltDeterministicallyFromExecutionTraceEntry),
    ("Shift trace marks active shift target as relevant", ShiftTraceMarksActiveShiftTargetAsRelevant),
    ("Shift trace is append-only", ShiftTraceIsAppendOnly),
    ("Shift trace builder has no side effects", ShiftTraceBuilderHasNoSideEffects),
    ("Snapshot is built correctly from shift trace", SnapshotIsBuiltCorrectlyFromShiftTrace),
    ("Empty shift trace yields empty snapshot", EmptyShiftTraceYieldsEmptySnapshot),
    ("Snapshot only uses shift-relevant entries", SnapshotOnlyUsesShiftRelevantEntries),
    ("Snapshot builder has no side effects", SnapshotBuilderHasNoSideEffects),
    ("Capsule is built correctly from snapshot", CapsuleIsBuiltCorrectlyFromSnapshot),
    ("Empty snapshot yields empty capsule", EmptySnapshotYieldsEmptyCapsule),
    ("Capsule HasShiftActivity is derived honestly", CapsuleHasShiftActivityIsDerivedHonestly),
    ("Capsule summary line is deterministic", CapsuleSummaryLineIsDeterministic),
    ("Capsule builder has no side effects", CapsuleBuilderHasNoSideEffects),
    ("Entry pack is built correctly from capsule and snapshot", EntryPackIsBuiltCorrectlyFromCapsuleAndSnapshot),
    ("Empty capsule and snapshot yield empty entry pack", EmptyCapsuleAndSnapshotYieldEmptyEntryPack),
    ("Entry pack HasExecutionContext is derived honestly", EntryPackHasExecutionContextIsDerivedHonestly),
    ("Entry pack line is deterministic", EntryPackLineIsDeterministic),
    ("Entry pack builder has no side effects", EntryPackBuilderHasNoSideEffects),
    ("Task state is built correctly from entry pack", TaskStateIsBuiltCorrectlyFromEntryPack),
    ("Empty entry pack yields empty task state", EmptyEntryPackYieldsEmptyTaskState),
    ("Task state current fields reflect capsule truth", TaskStateCurrentFieldsReflectCapsuleTruth),
    ("Task state line is deterministic", TaskStateLineIsDeterministic),
    ("Task state builder has no side effects", TaskStateBuilderHasNoSideEffects),
    ("Task view is built correctly from execution task state", TaskViewIsBuiltCorrectlyFromExecutionTaskState),
    ("Empty task state yields empty task view", EmptyTaskStateYieldsEmptyTaskView),
    ("Task view current fields reflect task state truth", TaskViewCurrentFieldsReflectTaskStateTruth),
    ("Task view line is deterministic", TaskViewLineIsDeterministic),
    ("Task view builder has no side effects", TaskViewBuilderHasNoSideEffects),
    ("Task projection bundle is built correctly from task state and task view", TaskProjectionBundleIsBuiltCorrectlyFromTaskStateAndTaskView),
    ("Empty task state and task view yield empty task projection bundle", EmptyTaskStateAndTaskViewYieldEmptyTaskProjectionBundle),
    ("Task projection bundle flags reflect task state and task view truth", TaskProjectionBundleFlagsReflectTaskStateAndTaskViewTruth),
    ("Task projection bundle builder has no side effects", TaskProjectionBundleBuilderHasNoSideEffects),
    ("Core checkpoint bundle is built correctly from core layers", CoreCheckpointBundleIsBuiltCorrectlyFromCoreLayers),
    ("Empty core layers yield empty core checkpoint bundle", EmptyCoreLayersYieldEmptyCoreCheckpointBundle),
    ("Core checkpoint bundle flags stay consistent", CoreCheckpointBundleFlagsStayConsistent),
    ("Core checkpoint bundle builder has no side effects", CoreCheckpointBundleBuilderHasNoSideEffects),
    ("Step phase machine happy path yields consistent projection", StepPhaseMachineHappyPathYieldsConsistentProjection),
    ("Step phase machine QC reject path returns to execution revision", StepPhaseMachineQcRejectPathReturnsToExecutionRevision),
    ("Step phase machine result revision path reenters execution", StepPhaseMachineResultRevisionPathReentersExecution),
    ("Step phase machine return to lead path reopens discussion", StepPhaseMachineReturnToLeadPathReopensDiscussion),
    ("Step phase machine preflight cancel returns to discussion without active truth", StepPhaseMachinePreflightCancelReturnsToDiscussionWithoutActiveTruth),
    ("Step phase machine execution cancel becomes interrupted without completion", StepPhaseMachineExecutionCancelBecomesInterruptedWithoutCompletion),
    ("Step phase machine interrupted discussion keeps active truth without restart action", StepPhaseMachineInterruptedDiscussionKeepsActiveTruthWithoutRestartAction),
    ("Step phase machine revision intake can return to result ready", StepPhaseMachineRevisionIntakeCanReturnToResultReady),
    ("Step phase machine result abandon returns to chat-only discussion", StepPhaseMachineResultAbandonReturnsToChatOnlyDiscussion),
    ("Step phase machine result abandon is forbidden outside result phase", StepPhaseMachineResultAbandonIsForbiddenOutsideResultPhase),
    ("Step phase machine resume variants stay phase honest", StepPhaseMachineResumeVariantsStayPhaseHonest),
    ("Step phase machine forbidden transitions fail fast", StepPhaseMachineForbiddenTransitionsFailFast),
    ("Step phase projection matrix prevents impossible action sets", StepPhaseProjectionMatrixPreventsImpossibleActionSets),
    ("Resume stage normalizer keeps live running phase when runtime is active", ResumeStageNormalizerKeepsLiveRunningPhaseWhenRuntimeIsActive),
    ("Resume stage normalizer degrades running without runtime to interrupted", ResumeStageNormalizerDegradesRunningWithoutRuntimeToInterrupted),
    ("Resume stage normalizer keeps result review when backed by runtime", ResumeStageNormalizerKeepsResultReviewWhenBackedByRuntime),
    ("Resume stage normalizer keeps clean preflight without active truth", ResumeStageNormalizerKeepsCleanPreflightWithoutActiveTruth),
    ("Resume stage normalizer collapses dirty active discussion to reopened refinement", ResumeStageNormalizerCollapsesDirtyActiveDiscussionToReopenedRefinement),
    ("Resume stage normalizer keeps active discussion ready for re-entry", ResumeStageNormalizerKeepsActiveDiscussionReadyForReEntry),
    ("Resume stage normalizer keeps active shift empty discussion out of bootstrap", ResumeStageNormalizerKeepsActiveShiftEmptyDiscussionOutOfBootstrap),
    ("Legacy resume migration clears abandoned demo tail without touching accepted continuation", LegacyResumeMigrationClearsAbandonedDemoTailWithoutTouchingAcceptedContinuation),
    ("Orientation intent detector marks orientation requests", OrientationIntentDetectorMarksOrientationRequests),
    ("Orientation intent detector keeps mixed intent on product path", OrientationIntentDetectorKeepsMixedIntentOnProductPath),
    ("Orientation intent detector does not capture product requests", OrientationIntentDetectorDoesNotCaptureProductRequests),
    ("Orientation intent does not expose execution CTA", OrientationIntentDoesNotExposeExecutionCta),
    ("Orientation intent returns fallback response", OrientationIntentReturnsFallbackResponse),
    ("Orientation intent keeps ZAVOD persona without model leakage", OrientationIntentKeepsZavodPersonaWithoutModelLeakage),
    ("Orientation capability response keeps work context", OrientationCapabilityResponseKeepsWorkContext),
    ("Product intent classifier marks ready cases as ready", ProductIntentClassifierMarksReadyCasesAsReady),
    ("Product intent classifier keeps chatter out of ready", ProductIntentClassifierKeepsChatterOutOfReady),
    ("Product intent classifier handles borderline requests", ProductIntentClassifierHandlesBorderlineRequests),
    ("Product intent classifier keeps real human phrasing ready", ProductIntentClassifierKeepsHumanPhrasingReady),
    ("Product intent classifier keeps dirty mixed phrasing ready", ProductIntentClassifierKeepsDirtyMixedPhrasingReady),
    ("Product intent classifier normalizes noisy input", ProductIntentClassifierNormalizesNoisyInput),
    ("Product intent classifier noisy real-world inputs", ProductIntentClassifierNoisyRealWorldInputs),
    ("Product intent classifier is deterministic", ProductIntentClassifierIsDeterministic),
    ("Product ready draft exposes validate CTA through projection", ProductReadyDraftExposesValidateCtaThroughProjection),
    ("Product pipeline builds ready projection without UI events", ProductPipelineBuildsReadyProjectionWithoutUiEvents),
    ("Active shift ready discussion enters preflight without active task", ActiveShiftReadyDiscussionEntersPreflightWithoutActiveTask),
    ("Active shift preflight cancel preserves ready discussion without active task", ActiveShiftPreflightCancelPreservesReadyDiscussionWithoutActiveTask),
    ("Product ready draft can enter and cancel preflight without losing CTA", ProductReadyDraftCanEnterAndCancelPreflightWithoutLosingCta),
    ("Reopened discussion ready allows same-step validation entry", ReopenedDiscussionReadyAllowsSameStepValidationEntry),
    ("Same-step preflight cancel preserves active ready discussion", SameStepPreflightCancelPreservesActiveReadyDiscussion),
    ("Demo post-accept continuation restores ready discussion CTA", DemoPostAcceptContinuationRestoresReadyDiscussionCta),
    ("Demo session advances to second step after first accept", DemoSessionAdvancesToSecondStepAfterFirstAccept),
    ("Demo session reaches completion after second accept", DemoSessionReachesCompletionAfterSecondAccept),
    ("Demo session reset returns brand new shift to clean step 1", DemoSessionResetReturnsBrandNewShiftToCleanStepOne),
    ("Welcome selector R1 offers continue when active shift", WelcomeSelectorR1OffersContinueWhenActiveShift),
    ("Welcome selector R2 offers start cycle on canonical 5 of 5", WelcomeSelectorR2OffersStartCycleOnCanonical5Of5),
    ("Welcome selector R3 offers promote and author on partial canonical", WelcomeSelectorR3OffersPromoteAndAuthorOnPartialCanonical),
    ("Welcome selector R3 requires thin memory confirmation for start cycle", WelcomeSelectorR3RequiresThinMemoryConfirmationForStartCycle),
    ("Welcome selector R4 offers review and promote when preview only", WelcomeSelectorR4OffersReviewAndPromoteWhenPreviewOnly),
    ("Welcome selector R5 offers retry and author on empty state", WelcomeSelectorR5OffersRetryAndAuthorOnEmptyState),
    ("Welcome selector R6 overlays stale review when stale present", WelcomeSelectorR6OverlaysStaleReviewWhenStalePresent),
    ("Welcome selector caps output at 4 actions", WelcomeSelectorCapsOutputAt4Actions),
    ("Welcome selector pads below-minimum with project audit", WelcomeSelectorPadsBelowMinimumWithProjectAudit),
    ("Welcome selector is deterministic for identical input", WelcomeSelectorIsDeterministicForIdenticalInput),
    ("Work Packet input defaults preserve pre-B2 call shape", WorkPacketInputDefaultsPreservePreB2CallShape),
    ("Work Packet input carries canonical docs status when provided", WorkPacketInputCarriesCanonicalDocsStatusWhenProvided),
    ("Work Packet input carries first-cycle flag and preview status", WorkPacketInputCarriesFirstCycleFlagAndPreviewStatus),
    ("Work Packet metadata defaults are null or false", WorkPacketMetadataDefaultsAreNullOrFalse),
    ("Prompt request pipeline opens first-cycle lead packet honestly", PromptRequestPipelineOpensFirstCycleLeadPacketHonestly),
    ("Prompt request pipeline keeps first-cycle worker gated honestly", PromptRequestPipelineKeepsFirstCycleWorkerGatedHonestly),
    ("Lead agent prompt carries work packet status honestly", LeadAgentPromptCarriesWorkPacketStatusHonestly),
    ("Worker agent prompt carries work packet status honestly", WorkerAgentPromptCarriesWorkPacketStatusHonestly),
    ("QC agent prompt carries work packet status honestly", QcAgentPromptCarriesWorkPacketStatusHonestly),
    ("Canonical docs status counts canonical and at-least-preview honestly", CanonicalDocsStatusCountsCanonicalAndAtLeastPreviewHonestly),
    ("Work Packet builder maps selection to canonical status honestly", WorkPacketBuilderMapsSelectionToCanonicalStatusHonestly),
    ("Work Packet builder returns null preview status when 5 of 5 canonical", WorkPacketBuilderReturnsNullPreviewStatusWhen5Of5Canonical),
    ("Work Packet builder lists preview kinds when mixed", WorkPacketBuilderListsPreviewKindsWhenMixed),
    ("Work Packet builder produces honest warnings for absent and preview", WorkPacketBuilderProducesHonestWarningsForAbsentAndPreview)
};

var failures = new List<string>();

foreach (var test in tests)
{
    try
    {
        test.Run();
        Console.WriteLine($"[PASS] {test.Name}");
    }
    catch (Exception exception)
    {
        failures.Add($"{test.Name}: {exception.Message}");
        Console.WriteLine($"[FAIL] {test.Name}");
        Console.WriteLine(exception.Message);
    }
}

if (failures.Count > 0)
{
    Console.Error.WriteLine();
    Console.Error.WriteLine("Test run failed:");
    foreach (var failure in failures)
    {
        Console.Error.WriteLine($"- {failure}");
    }

    return 1;
}

Console.WriteLine();
Console.WriteLine($"All {tests.Length} backend invariants passed.");
return 0;

static void CanonicalAnchorOrderingIsDeterministic()
{
    var anchors = new[]
    {
        new PromptAnchor("A-CODE-002", PromptAnchorType.Code, "scope", "B.cs"),
        new PromptAnchor("A-TRUTH-001", PromptAnchorType.Truth, "canon", "Do not drift."),
        new PromptAnchor("A-TASK-001", PromptAnchorType.Task, "intent", "Validated intent."),
        new PromptAnchor("A-CONSTRAINT-001", PromptAnchorType.Constraint, "system", "Minimal sufficient solution."),
        new PromptAnchor("A-STATE-001", PromptAnchorType.State, "state", "Task is in review."),
        new PromptAnchor("A-CODE-001", PromptAnchorType.Code, "scope", "A.cs"),
        new PromptAnchor("A-DECISION-001", PromptAnchorType.Decision, "decision", "Use WinUI 3."),
        new PromptAnchor("A-ARTIFACT-001", PromptAnchorType.Artifact, "artifact", "Diff prepared.")
    };

    var serialized = PromptAnchorSerializer.Serialize(anchors);

    var order = serialized.Select(anchor => anchor.Type).ToArray();
    AssertSequenceEqual(
        new[] { "TASK", "TRUTH", "DECISION", "CODE", "CODE", "STATE", "ARTIFACT", "CONSTRAINT" },
        order,
        "Anchor type order must follow canonical ordering.");
    AssertEqual("A-CODE-001", serialized[3].Id, "Code anchors must be ordered deterministically by id.");
    AssertEqual("A-CODE-002", serialized[4].Id, "Code anchors must be ordered deterministically by id.");
}

static void InvalidInputFailsFast()
{
    var capsule = CreateCapsule();
    var task = CreateTaskState(ContextIntentState.Candidate, TaskStateStatus.Active, PromptRole.ShiftLead);
    var shift = CreateShiftState(task);
    var input = new PromptRequestInput(PromptRole.Worker, capsule, shift, task);

    AssertThrows<PromptRequestPipelineException>(
        () => PromptRequestPipeline.Execute(input),
        "Worker prompt assembly must fail for non-validated intent.");
}

static void MissingRequiredSectionFailsFast()
{
    var request = new PromptAssemblyRequest(
        PromptRole.Worker,
        PromptTruthMode.Anchored,
        new ShiftContextBlock("SHIFT-001", "Implement prompt flow", new[] { "Stay in scope" }, new[] { "TaskId: TASK-001" }),
        new TaskBlock(ContextIntentState.Validated, "Implement validated task", new[] { "Prompting/PromptAssembler.cs" }, new[] { "Prompt builds successfully" }),
        Array.Empty<PromptAnchor>(),
        null,
        new ValidatedIntentBlock(
            "Implement validated task",
            new ScopeBlock(new[] { "Prompting/PromptAssembler.cs" }, Array.Empty<string>()),
            new[] { "Prompt builds successfully" },
            Array.Empty<string>()));
    var packet = new PromptRequestPacket(
        PromptRole.Worker,
        PromptTruthMode.Anchored,
        request,
        new PromptPacketMetadata("SHIFT-001", "TASK-001", 0));

    AssertThrows<PromptAssemblyException>(
        () => PromptAssembler.Build(packet),
        "Assembler must fail when anchor pack is empty.");
}

static void IdenticalInputYieldsIdenticalSerializedOutput()
{
    var first = CreateWorkerPacket();
    var second = CreateWorkerPacket();

    var firstTransport = PromptTransportSerializer.Serialize(first);
    var secondTransport = PromptTransportSerializer.Serialize(second);

    AssertEqual(firstTransport.RoleCoreText, secondTransport.RoleCoreText, "Role core text must be deterministic.");
    AssertEqual(firstTransport.ShiftContextText, secondTransport.ShiftContextText, "Shift context text must be deterministic.");
    AssertEqual(firstTransport.TaskBlockText, secondTransport.TaskBlockText, "Task block text must be deterministic.");
    AssertEqual(firstTransport.AnchorPackText, secondTransport.AnchorPackText, "Anchor serialization must be deterministic.");
}

static void AssembledPromptContainsRequiredSections()
{
    var input = CreateWorkerInput();
    var result = PromptRequestPipeline.Execute(input);

    AssertContains(result.FinalPrompt, "[ROLE CORE]", "Prompt must include role core section.");
    AssertContains(result.FinalPrompt, "[SHIFT CONTEXT]", "Prompt must include shift context section.");
    AssertContains(result.FinalPrompt, "[TASK BLOCK]", "Prompt must include task block section.");
    AssertContains(result.FinalPrompt, "[ANCHORS]", "Prompt must include anchor section.");
}

static void RuntimeTruthModeIsPredictable()
{
    var capsule = CreateCapsule();

    var workerTask = CreateTaskState(ContextIntentState.Validated, TaskStateStatus.Active, PromptRole.Worker);
    var workerShift = CreateShiftState(workerTask);
    var workerResult = PromptRequestPipeline.Execute(new PromptRequestInput(PromptRole.Worker, capsule, workerShift, workerTask));
    AssertEqual(PromptTruthMode.Anchored, workerResult.TruthMode, "Worker on validated intent must be anchored.");

    var leadTask = CreateTaskState(ContextIntentState.Validated, TaskStateStatus.Active, PromptRole.ShiftLead);
    var leadShift = CreateShiftState(leadTask);
    var leadResult = PromptRequestPipeline.Execute(new PromptRequestInput(PromptRole.ShiftLead, capsule, leadShift, leadTask));
    AssertEqual(PromptTruthMode.Anchored, leadResult.TruthMode, "Shift Lead on canonical task runtime must use anchored truth mode.");

    var seniorTask = CreateTaskState(ContextIntentState.Validated, TaskStateStatus.Active, PromptRole.SeniorSpecialist);
    var seniorShift = CreateShiftState(seniorTask, openIssues: new[] { "Canon conflict detected" });
    var escalation = new EscalationContext("Resolve canon conflict", new[] { "Intent vs canon" }, new[] { "Need authority decision" });
    var seniorResult = PromptRequestPipeline.Execute(new PromptRequestInput(PromptRole.SeniorSpecialist, capsule, seniorShift, seniorTask, null, escalation));
    AssertEqual(PromptTruthMode.Anchored, seniorResult.TruthMode, "Senior escalation on canonical task runtime must use anchored truth mode.");
}

static void PromptRoleCoresLoadFromVersionedPromptFiles()
{
    var worker = PromptRoleCoreCatalog.Get(PromptRole.Worker);
    var lead = PromptRoleCoreCatalog.Get(PromptRole.ShiftLead);
    var qc = PromptRoleCoreCatalog.Get(PromptRole.Qc);
    var senior = PromptRoleCoreCatalog.Get(PromptRole.SeniorSpecialist);

    AssertEqual("Worker", worker.Role, "Worker core should load from stable prompt file.");
    AssertContains(worker.Rules[0], "Grounded Execution Only", "Worker file-backed core should preserve stable rules.");
    AssertEqual("Shift Lead", lead.Role, "Lead core should load from stable prompt file.");
    AssertContains(lead.ResponseContract[0], "single strict JSON object", "Lead file-backed core should preserve response contract.");
    AssertEqual("QC", qc.Role, "QC core should load from stable prompt file.");
    AssertContains(qc.Constraints[0], "validated intent required", "QC file-backed core should preserve constraints.");
    AssertEqual("Senior Specialist", senior.Role, "Senior core should load from stable prompt file.");
    AssertContains(senior.ResponseContract[0], "DECISION TYPE", "Senior file-backed core should preserve response contract.");
}

static void ImportSystemPromptLoadsFromVersionedPromptFile()
{
    var importPrompt = PromptSystemCatalog.GetImportSystemPrompt();

    AssertContains(importPrompt, "Import Materials Interpreter", "Import system prompt should come from stable versioned file.");
    AssertContains(importPrompt, "context-only", "Import system prompt should keep non-authoritative boundary explicit.");
    AssertContains(importPrompt, "do not create or mutate project truth", "Import system prompt should preserve truth boundary.");
}

static void PipelineIsTheOnlyPublicPromptEntryPath()
{
    var pipelineType = typeof(PromptRequestPipeline);
    var assemblerType = typeof(PromptRole).Assembly.GetType("zavod.Prompting.PromptAssembler")
        ?? throw new InvalidOperationException("PromptAssembler type must exist.");
    var orchestratorType = typeof(PromptRole).Assembly.GetType("zavod.Orchestration.PromptAssemblyOrchestrator")
        ?? throw new InvalidOperationException("PromptAssemblyOrchestrator type must exist.");

    AssertTrue(pipelineType.IsPublic, "PromptRequestPipeline must remain public.");
    AssertFalse(assemblerType.IsPublic, "PromptAssembler must not be public.");
    AssertFalse(orchestratorType.IsPublic, "PromptAssemblyOrchestrator must not be public.");

    var buildFromTransport = assemblerType.GetMethod("BuildFromTransport", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
    AssertTrue(buildFromTransport is not null, "BuildFromTransport must exist as internal helper.");
    AssertFalse(buildFromTransport!.IsPublic, "BuildFromTransport must not be public.");
}

static void ArtifactTypingIsDeterministic()
{
    var imageInput = new IntakeSourceInput("ART-IMG-001", "user_upload", "diagram.png", "image/png", ".png");
    var pdfInput = new IntakeSourceInput("ART-PDF-001", "user_upload", "brief.pdf", "application/pdf", ".pdf");
    var archiveInput = new IntakeSourceInput("ART-ARC-001", "user_upload", "assets.zip", "application/zip", ".zip");
    var textInput = new IntakeSourceInput("ART-TXT-001", "user_input", "notes", InlineText: "hello");
    var documentInput = new IntakeSourceInput("ART-DOC-001", "user_upload", "spec.docx", "application/vnd.openxmlformats-officedocument.wordprocessingml.document", ".docx");

    AssertEqual(IntakeArtifactType.Image, IntakeArtifactFactory.DetermineType(imageInput), "Image typing must be deterministic.");
    AssertEqual(IntakeArtifactType.Pdf, IntakeArtifactFactory.DetermineType(pdfInput), "Pdf typing must be deterministic.");
    AssertEqual(IntakeArtifactType.Archive, IntakeArtifactFactory.DetermineType(archiveInput), "Archive typing must be deterministic.");
    AssertEqual(IntakeArtifactType.Text, IntakeArtifactFactory.DetermineType(textInput), "Text typing must be deterministic.");
    AssertEqual(IntakeArtifactType.Document, IntakeArtifactFactory.DetermineType(documentInput), "Document typing must be deterministic.");
}

static void IdenticalInputMetadataYieldsDeterministicArtifactShape()
{
    var first = IntakeArtifactFactory.Normalize(new IntakeSourceInput(
        "ART-001",
        "user_upload",
        "report.pdf",
        "application/pdf",
        ".pdf",
        Metadata:
        [
            new ArtifactMetadataEntry("size", "100"),
            new ArtifactMetadataEntry("author", "Boris")
        ]));
    var second = IntakeArtifactFactory.Normalize(new IntakeSourceInput(
        "ART-001",
        "user_upload",
        "report.pdf",
        "application/pdf",
        ".pdf",
        Metadata:
        [
            new ArtifactMetadataEntry("author", "Boris"),
            new ArtifactMetadataEntry("size", "100")
        ]));

    AssertEqual(first.Type, second.Type, "Artifact type must stay deterministic.");
    AssertEqual(first.NormalizedContentReference, second.NormalizedContentReference, "Normalized reference must stay deterministic.");
    AssertSequenceEqual(
        first.Metadata.Select(entry => $"{entry.Key}={entry.Value}"),
        second.Metadata.Select(entry => $"{entry.Key}={entry.Value}"),
        "Metadata normalization must be deterministic.");
}

static void ToolsReturnStructuredResults()
{
    var root = CreateScratchWorkspace();
    try
    {
        var pdfPath = Path.Combine(root, "guide.pdf");
        File.WriteAllText(pdfPath, "placeholder");

        var runner = new FakeExternalProcessRunner(request =>
        {
            if (request.Purpose.StartsWith("pdf_extract:pdftotext", StringComparison.Ordinal))
            {
                return new ExternalProcessResult(0, "runtime extracted pdf text", string.Empty, false);
            }

            return new ExternalProcessResult(1, string.Empty, "unexpected backend call", false);
        });

        var layer = new UnifiedToolLayer(
            new DocumentImportTool(),
            new PdfReadTool(new PdfExtractionRuntimeService(runner)),
            new ArchiveTool(new ArchiveInspectionRuntimeService(runner)),
            new ImageIntakeTool(new ImageInspectionRuntimeService(runner)),
            new WebSearchTool(),
            new WorkspaceTool());

        var importResult = layer.ImportDocument(
            PromptRole.ShiftLead,
            new DocumentImportRequest(
                "REQ-IMPORT-001",
                new IntakeSourceInput("ART-PDF-002", "user_upload", "guide.pdf", "application/pdf", ".pdf", RawContentReference: pdfPath)));

        AssertTrue(importResult.Success, "Import result should succeed for supported input.");
        AssertTrue(importResult.ProducedArtifacts.Count == 1, "Import result must return produced artifacts.");
        AssertTrue(importResult.ExtractedItems.Count == 1, "Import result must return structured extracted items.");

        var pdfResult = layer.ReadPdf(
            PromptRole.Worker,
            new PdfReadRequest("REQ-PDF-001", importResult.ProducedArtifacts[0]));

        AssertTrue(pdfResult.Success, "Pdf tool should succeed for pdf artifact with runtime-backed content reference.");
        AssertTrue(pdfResult.ExtractedItems.Count == 1, "Pdf tool must return structured output item.");
        AssertTrue(pdfResult.Diagnostics is null, "Pdf tool should not emit diagnostics for valid input.");
        AssertContains(pdfResult.Summary, "bounded PDF text preview", "Pdf tool summary should come from runtime-backed extraction.");
    }
    finally
    {
        DeleteScratchWorkspace(root);
    }
}

static void UnifiedToolLayerIsUiAgnostic()
{
    var toolingTypes = typeof(IntakeArtifact).Assembly
        .GetTypes()
        .Where(type => type.Namespace == "zavod.Tooling")
        .ToArray();

    foreach (var type in toolingTypes)
    {
        AssertFalse(type.Namespace?.StartsWith("Microsoft.UI", StringComparison.Ordinal) == true, "Tooling namespace must not depend on Microsoft.UI.");

        foreach (var property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static))
        {
            var propertyNamespace = property.PropertyType.Namespace ?? string.Empty;
            AssertFalse(propertyNamespace.StartsWith("Microsoft.UI", StringComparison.Ordinal), $"Tooling property '{type.Name}.{property.Name}' must not use Microsoft.UI types.");
            AssertFalse(propertyNamespace.StartsWith("Windows.UI", StringComparison.Ordinal), $"Tooling property '{type.Name}.{property.Name}' must not use Windows.UI types.");
        }
    }
}

static void UnifiedToolLayerSupportsLeadAndWorkerRoles()
{
    var layer = UnifiedToolLayer.CreateDefault();
    var request = new DocumentImportRequest(
        "REQ-DOC-001",
        new IntakeSourceInput("ART-TXT-002", "user_input", "brief", InlineText: "Implement prompt core"));

    var leadResult = layer.ImportDocument(PromptRole.ShiftLead, request);
    var workerResult = layer.ImportDocument(PromptRole.Worker, request);

    AssertTrue(leadResult.Success, "Shift Lead must be able to use unified tool layer.");
    AssertTrue(workerResult.Success, "Worker must be able to use unified tool layer.");

    AssertThrows<ToolingException>(
        () => layer.ImportDocument(PromptRole.Qc, request),
        "QC must not be allowed through the lead/worker tool seam in v1.");
}

static void WorkerToolResolverKeepsCapabilityProfilesBounded()
{
    var readOnly = WorkerToolResolver.ListVisibleTools(WorkerCapabilityProfile.ReadOnly);
    var workspace = WorkerToolResolver.ListVisibleTools(WorkerCapabilityProfile.WorkspaceOperator);
    var external = WorkerToolResolver.ListVisibleTools(WorkerCapabilityProfile.ExternalBrokered);

    AssertTrue(readOnly.Any(tool => tool.ToolName == "workspace.inspect"), "Read-only profile must expose workspace inspection.");
    AssertFalse(readOnly.Any(tool => tool.ToolName == "intake.document.import"), "Read-only profile must not expose mutating intake tools.");
    AssertTrue(workspace.Any(tool => tool.ToolName == "intake.document.import"), "Workspace operator must expose bounded workspace tools.");
    AssertFalse(workspace.Any(tool => tool.ToolName == "web.search"), "Workspace operator must not expose brokered external tools.");
    AssertTrue(external.Any(tool => tool.ToolName == "web.search"), "External brokered profile must expose brokered external tools.");

    var webRoute = WorkerToolResolver.ResolveRequired(WorkerCapabilityProfile.ExternalBrokered, "web.search");
    AssertTrue(webRoute.Route.RequiresAdditionalApproval, "External web search must require additional approval in worker route.");
    AssertEqual(RuntimeAccessMode.DenyByDefault, webRoute.Route.RuntimeSubstrate.NetworkBroker.AccessMode, "Default runtime must stay deny-by-default for network access.");
}

static void UnifiedToolLayerExposesVisibleWorkerToolsDeterministically()
{
    var layer = UnifiedToolLayer.CreateDefault();

    var first = layer.ListVisibleWorkerTools(WorkerCapabilityProfile.WorkspaceOperator)
        .Select(tool => tool.ToolName)
        .ToArray();
    var second = layer.ListVisibleWorkerTools(WorkerCapabilityProfile.WorkspaceOperator)
        .Select(tool => tool.ToolName)
        .ToArray();

    AssertSequenceEqual(first, second, "Visible worker tools must be deterministic across repeated reads.");
    AssertTrue(first.Contains("workspace.inspect"), "Workspace operator visible set must include workspace inspection.");
    AssertTrue(first.Contains("intake.document.import"), "Workspace operator visible set must include document import.");
    AssertFalse(first.Contains("web.search"), "Workspace operator visible set must keep brokered external tools hidden.");
}

static void UnifiedToolLayerKeepsLeadNarrowerThanWorker()
{
    var layer = UnifiedToolLayer.CreateDefault();

    var leadVisible = layer.ListVisibleToolsForRole(PromptRole.ShiftLead)
        .Select(tool => tool.ToolName)
        .ToArray();
    var workerVisible = layer.ListVisibleToolsForRole(PromptRole.Worker)
        .Select(tool => tool.ToolName)
        .ToArray();

    AssertFalse(leadVisible.Contains("web.search"), "Shift Lead must not see brokered external tools in the default profile.");
    AssertTrue(workerVisible.Contains("web.search"), "Worker must retain brokered external tools in the default profile.");

    AssertThrows<ToolingException>(
        () => layer.PerformWebSearch(PromptRole.ShiftLead, new WebSearchRequest("REQ-WEB-LEAD-001", "zavod runtime")),
        "Shift Lead web search must be denied by the narrower capability profile.");
}

static void RoleToolResolverKeepsQcAndSeniorSpecialistBounded()
{
    var layer = UnifiedToolLayer.CreateDefault();

    var qcVisible = layer.ListVisibleToolsForRole(PromptRole.Qc)
        .Select(tool => tool.ToolName)
        .ToArray();
    var seniorVisible = layer.ListVisibleToolsForRole(PromptRole.SeniorSpecialist)
        .Select(tool => tool.ToolName)
        .ToArray();

    AssertTrue(qcVisible.Contains("workspace.inspect"), "QC must retain read-only workspace inspection.");
    AssertFalse(qcVisible.Contains("intake.document.import"), "QC must not see execution mutation tools.");
    AssertFalse(qcVisible.Contains("web.search"), "QC must not see external broker tools by default.");

    AssertTrue(seniorVisible.Contains("workspace.inspect"), "Senior Specialist must retain read-only workspace inspection.");
    AssertTrue(seniorVisible.Contains("web.search"), "Senior Specialist may use brokered external analysis tools.");
    AssertFalse(seniorVisible.Contains("intake.document.import"), "Senior Specialist must not silently become default Worker.");

    var workspaceResult = layer.InspectWorkspace(
        PromptRole.Qc,
        new WorkspaceInspectRequest("REQ-WS-QC-001", "C:\\Users\\Boris\\Documents\\Dev\\zavod", new[] { "Tooling" }));
    AssertTrue(workspaceResult.ExtractedItems.Count > 0, "QC workspace inspection must remain available through role tool resolver.");

    AssertThrows<ToolingException>(
        () => layer.ImportDocument(
            PromptRole.Qc,
            new DocumentImportRequest("REQ-QC-DOC-001", new IntakeSourceInput("ART-QC-001", "user_input", "note.txt", InlineText: "x"))),
        "QC must not execute mutation-oriented document import.");

    AssertThrows<ToolingException>(
        () => layer.ImportDocument(
            PromptRole.SeniorSpecialist,
            new DocumentImportRequest("REQ-SS-DOC-001", new IntakeSourceInput("ART-SS-001", "user_input", "note.txt", InlineText: "x"))),
        "Senior Specialist must not silently replace Worker execution.");
}

static void ToolExecutionEnvelopeCarriesRouteAndEvidence()
{
    var layer = UnifiedToolLayer.CreateDefault();
    var root = CreateScratchWorkspace();
    try
    {
        Directory.CreateDirectory(Path.Combine(root, "Tooling"));
        File.WriteAllText(Path.Combine(root, "Tooling", "tool.txt"), "workspace envelope fixture");
        File.WriteAllText(Path.Combine(root, "Tooling", "tool.cs"), "public sealed class ToolFixture {}");
        File.WriteAllText(Path.Combine(root, "Tooling", "tool.csproj"), "<Project />");

        var workspaceEnvelope = layer.InspectWorkspaceWithEnvelope(
            PromptRole.Qc,
            new WorkspaceInspectRequest("REQ-WS-ENV-001", root, new[] { "Tooling" }));

        AssertTrue(workspaceEnvelope.Result.Success, "Workspace envelope should preserve successful tool result.");
        AssertEqual("workspace.inspect", workspaceEnvelope.ResolvedTool.ToolName, "Envelope must preserve resolved tool identity.");
        AssertEqual(RoleCapabilityProfile.ReadOnly, workspaceEnvelope.ResolvedTool.Route.CapabilityProfile, "QC envelope must preserve read-only capability profile.");
        AssertContains(workspaceEnvelope.EvidenceSummary, "role=Qc", "Envelope evidence should include role.");
        AssertContains(workspaceEnvelope.EvidenceSummary, "tool=workspace.inspect", "Envelope evidence should include tool id.");
    }
    finally
    {
        DeleteScratchWorkspace(root);
    }

    var webEnvelope = layer.PerformWebSearchWithEnvelope(
        PromptRole.SeniorSpecialist,
        new WebSearchRequest("REQ-WEB-ENV-001", "zavod runtime"));

    AssertEqual("web.search", webEnvelope.ResolvedTool.ToolName, "Envelope must preserve external tool identity.");
    AssertEqual(RoleCapabilityProfile.AnalysisSupport, webEnvelope.ResolvedTool.Route.CapabilityProfile, "Senior Specialist envelope must preserve analysis-support capability profile.");
    AssertTrue(webEnvelope.ResolvedTool.Route.RequiresAdditionalApproval, "Brokered external tool should require additional approval in the route.");
    AssertContains(webEnvelope.EvidenceSummary, "approval=True", "Envelope evidence should preserve approval requirement.");
    AssertContains(webEnvelope.Summary, "success=False", "Envelope summary should reflect actual tool result.");
}

static void TypedRuntimePackageStaysFixedAndDeterministic()
{
    var first = TypedToolCatalog.ListAll().Select(static contract => contract.ToolName).ToArray();
    var second = TypedToolCatalog.ListAll().Select(static contract => contract.ToolName).ToArray();

    AssertSequenceEqual(first, second, "Typed runtime package must remain deterministic.");
    AssertSequenceEqual(
        new[]
        {
            "workspace.inspect",
            "intake.document.import",
            "intake.pdf.read",
            "intake.archive.inspect",
            "intake.image.inspect",
            "web.search"
        },
        first,
        "Typed runtime package must stay limited to the agreed v1 capability set.");

    var webPolicy = TypedToolCatalog.BuildGovernancePolicy("web.search");
    AssertTrue(webPolicy.Source.RequiresHostGovernance, "Brokered external tool must remain host-governed.");
    AssertEqual(ToolApprovalPolicy.HostApprovalRequired, webPolicy.ApprovalPolicy, "Brokered external tool must keep host approval.");
}

static void WatchdogStartAndSignalsStayDeterministic()
{
    var startedAt = new DateTimeOffset(2026, 04, 08, 12, 00, 00, TimeSpan.Zero);
    var heartbeatAt = startedAt.AddSeconds(30);
    var progressAt = heartbeatAt.AddSeconds(15);

    var snapshot = ExecutionWatchdog.Start(startedAt: startedAt);
    AssertEqual(startedAt, snapshot.StartedAt, "Watchdog start must preserve explicit start time.");
    AssertTrue(snapshot.LastHeartbeat is null, "Fresh watchdog must not invent heartbeat.");
    AssertTrue(snapshot.LastProgress is null, "Fresh watchdog must not invent progress.");

    snapshot = ExecutionWatchdog.RecordHeartbeat(
        snapshot,
        new RuntimeHeartbeat(heartbeatAt, "worker_loop", "Heartbeat observed."));
    AssertTrue(snapshot.LastHeartbeat is not null, "Recorded heartbeat must be preserved.");
    AssertEqual(heartbeatAt, snapshot.LastHeartbeat!.ObservedAt, "Heartbeat timestamp must be preserved.");
    AssertTrue(snapshot.LastProgress is null, "Heartbeat update must not invent progress.");

    snapshot = ExecutionWatchdog.RecordProgress(
        snapshot,
        new RuntimeProgressSignal(progressAt, "tool_result", "Progress observed."));
    AssertTrue(snapshot.LastProgress is not null, "Recorded progress must be preserved.");
    AssertEqual(progressAt, snapshot.LastProgress!.ObservedAt, "Progress timestamp must be preserved.");
    AssertEqual(heartbeatAt, snapshot.LastHeartbeat!.ObservedAt, "Progress update must not drop previous heartbeat.");
}

static void WatchdogEvaluateReturnsBoundedInterruptionReasons()
{
    var policy = new ExecutionWatchdogPolicy(
        MaxRuntime: TimeSpan.FromMinutes(1),
        MaxHeartbeatGap: TimeSpan.FromSeconds(20),
        MaxNoProgressGap: TimeSpan.FromSeconds(25),
        GracefulStopBeforeKill: true,
        "Test watchdog policy.");
    var startedAt = new DateTimeOffset(2026, 04, 08, 12, 00, 00, TimeSpan.Zero);

    var clean = ExecutionWatchdog.Start(policy, startedAt);
    AssertTrue(
        ExecutionWatchdog.Evaluate(clean, startedAt.AddSeconds(10)) is null,
        "Watchdog must not interrupt when thresholds are still within bounds.");

    var timeout = ExecutionWatchdog.Evaluate(clean, startedAt.AddMinutes(2));
    AssertTrue(timeout is not null, "Watchdog must interrupt when runtime exceeds max runtime.");
    AssertEqual(StopReason.TimeoutExceeded, timeout!.Reason, "Watchdog must classify runtime overflow as timeout.");

    var withHeartbeat = ExecutionWatchdog.RecordHeartbeat(
        clean,
        new RuntimeHeartbeat(startedAt.AddSeconds(5), "worker_loop", "Heartbeat observed."));
    var heartbeatLost = ExecutionWatchdog.Evaluate(withHeartbeat, startedAt.AddSeconds(30));
    AssertTrue(heartbeatLost is not null, "Watchdog must interrupt when heartbeat gap is exceeded.");
    AssertEqual(StopReason.HeartbeatLost, heartbeatLost!.Reason, "Watchdog must classify missing heartbeat honestly.");

    var withProgress = ExecutionWatchdog.RecordProgress(
        clean,
        new RuntimeProgressSignal(startedAt.AddSeconds(5), "worker_result", "Progress observed."));
    var noProgress = ExecutionWatchdog.Evaluate(withProgress, startedAt.AddSeconds(40));
    AssertTrue(noProgress is not null, "Watchdog must interrupt when progress gap is exceeded.");
    AssertEqual(StopReason.NoProgressObserved, noProgress!.Reason, "Watchdog must classify lack of progress honestly.");

    var policyViolation = ExecutionWatchdog.Evaluate(clean, startedAt.AddSeconds(1), policyViolationObserved: true);
    AssertTrue(policyViolation is not null, "Watchdog must interrupt on explicit policy violation.");
    AssertEqual(StopReason.PolicyViolation, policyViolation!.Reason, "Watchdog must classify explicit policy violation honestly.");
}

static void UnsupportedInputTypeIsHandledPredictably()
{
    var layer = UnifiedToolLayer.CreateDefault();
    var result = layer.ImportDocument(
        PromptRole.ShiftLead,
        new DocumentImportRequest(
            "REQ-UNKNOWN-001",
            new IntakeSourceInput("ART-UNK-001", "user_upload", "mystery.bin", "application/octet-stream", ".bin")));

    AssertFalse(result.Success, "Unsupported input must fail predictably.");
    AssertTrue(result.ProducedArtifacts.Count == 1, "Unsupported input must still produce deterministic artifact record.");
    AssertEqual(IntakeArtifactType.Unknown, result.ProducedArtifacts[0].Type, "Unsupported input must normalize to Unknown.");
    AssertEqual(IntakeArtifactStatus.Unsupported, result.ProducedArtifacts[0].Status, "Unsupported input must be marked Unsupported.");
    AssertTrue(result.Warnings.Any(warning => warning.Code == "UNSUPPORTED_ARTIFACT_TYPE"), "Unsupported input must emit structured warning.");
}

static void ArtifactInventoryBuildsDeterministically()
{
    var artifact = new IntakeArtifact(
        "ART-ARCHIVE-001",
        IntakeArtifactType.Archive,
        "user_upload",
        "bundle.zip",
        new[]
        {
            new ArtifactMetadataEntry("archive.entry.002.path", "docs/readme.md"),
            new ArtifactMetadataEntry("archive.entry.001.path", "src/MainWindow.xaml"),
            new ArtifactMetadataEntry("archive.entry.001.type", "file"),
            new ArtifactMetadataEntry("archive.entry.002.type", "file")
        },
        "normalized://archive/ART-ARCHIVE-001",
        IntakeArtifactStatus.Normalized);

    var first = ArtifactInventoryBuilder.Build(artifact);
    var second = ArtifactInventoryBuilder.Build(artifact);

    AssertEqual(first.ArtifactId, second.ArtifactId, "Inventory artifact id must be deterministic.");
    AssertSequenceEqual(
        first.Entries.Select(entry => entry.NameOrPath),
        second.Entries.Select(entry => entry.NameOrPath),
        "Inventory entry order must be deterministic.");
}

static void CandidateSelectionIsDeterministicAndShortlistBounded()
{
    var artifact = new IntakeArtifact(
        "ART-ARCHIVE-002",
        IntakeArtifactType.Archive,
        "user_upload",
        "bundle.zip",
        new[]
        {
            new ArtifactMetadataEntry("archive.entry.001.path", "src/App.xaml"),
            new ArtifactMetadataEntry("archive.entry.002.path", "src/MainWindow.xaml"),
            new ArtifactMetadataEntry("archive.entry.003.path", "docs/notes.txt")
        },
        "normalized://archive/ART-ARCHIVE-002",
        IntakeArtifactStatus.Normalized);

    var inventories = new[] { ArtifactInventoryBuilder.Build(artifact) };
    var request = new RetrievalRequest(
        new[] { artifact },
        new[] { "xaml", "MainWindow" },
        new RetrievalFilter(
            Extensions: new[] { ".xaml" },
            PathContains: new[] { "src/" },
            EntryTypes: new[] { ArtifactInventoryEntryType.File }),
        MaxCandidates: 1);

    var first = BasicCandidateSelector.Select(inventories, request);
    var second = BasicCandidateSelector.Select(inventories, request);

    AssertTrue(first.Candidates.Count == 1, "Shortlist must respect MaxCandidates.");
    AssertEqual("src/MainWindow.xaml", first.Candidates[0].Reference, "Most relevant candidate must be selected predictably.");
    AssertSequenceEqual(
        first.Candidates.Select(candidate => candidate.Reference),
        second.Candidates.Select(candidate => candidate.Reference),
        "Candidate selection must be deterministic.");
}

static void RetrievalLayerIsUiAndLlmAgnostic()
{
    var retrievalTypes = typeof(ArtifactInventory).Assembly
        .GetTypes()
        .Where(type => type.Namespace == "zavod.Retrieval")
        .ToArray();

    foreach (var type in retrievalTypes)
    {
        foreach (var property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static))
        {
            var propertyNamespace = property.PropertyType.Namespace ?? string.Empty;
            AssertFalse(propertyNamespace.StartsWith("Microsoft.UI", StringComparison.Ordinal), $"Retrieval property '{type.Name}.{property.Name}' must not use Microsoft.UI types.");
            AssertFalse(propertyNamespace.StartsWith("Windows.UI", StringComparison.Ordinal), $"Retrieval property '{type.Name}.{property.Name}' must not use Windows.UI types.");
            AssertFalse(propertyNamespace.StartsWith("OpenAI", StringComparison.OrdinalIgnoreCase), $"Retrieval property '{type.Name}.{property.Name}' must not use LLM SDK types.");
        }
    }
}

static void ScopedContextIsBuiltPredictably()
{
    var layer = UnifiedToolLayer.CreateDefault();
    var artifact = new IntakeArtifact(
        "ART-TEXT-001",
        IntakeArtifactType.Text,
        "user_input",
        "notes",
        new[]
        {
            new ArtifactMetadataEntry("inline_text", "Button should be blue.\n\nButton should stay aligned right.")
        },
        "normalized://text/ART-TEXT-001",
        IntakeArtifactStatus.Normalized);

    var retrieval = layer.Retrieve(
        PromptRole.ShiftLead,
        new RetrievalRequest(
            new[] { artifact },
            new[] { "blue", "right" },
            MaxCandidates: 2));

    var context = layer.BuildScopedContext(PromptRole.Worker, retrieval);

    AssertTrue(context.SelectedCandidates.Count > 0, "Scoped context must include selected candidates.");
    AssertTrue(context.SourceReferences.Count == context.SelectedCandidates.Count, "Scoped context references must align with selected candidates.");
    AssertContains(context.ContextSummary, "Selected", "Scoped context summary must come from retrieval summary.");
}

static void ExecutionFlowTransitionsAreDeterministic()
{
    var task = CreateTaskState(ContextIntentState.Validated, TaskStateStatus.Active, PromptRole.Worker);
    var session = new ExecutionSession("SESSION-001", PromptRole.ShiftLead, task.TaskId, "SHIFT-001", ExecutionSessionState.Initialized);
    var scopedContext = CreateScopedContext();

    var preparation = ExecutionCoordinator.PrepareTask(session, task, scopedContext);
    AssertEqual(ExecutionSessionState.TaskPrepared, preparation.Session.State, "PrepareTask must move session to TaskPrepared.");

    var inProgress = ExecutionCoordinator.StartExecution(preparation.Session, preparation.Binding);
    AssertEqual(ExecutionSessionState.InProgress, inProgress.State, "StartExecution must move session to InProgress.");

    var result = CreateWorkerExecutionResult(task.TaskId, WorkerExecutionStatus.Success);
    var produced = ExecutionCoordinator.SubmitResult(inProgress, result);
    AssertEqual(ExecutionSessionState.ResultProduced, produced.State, "SubmitResult must move successful execution to ResultProduced.");

    var underReview = ExecutionCoordinator.RequestReview(produced, result);
    AssertEqual(ExecutionSessionState.UnderReview, underReview.State, "RequestReview must move session to UnderReview.");

    var review = CreateQcReviewResult(result.ResultId, QCReviewStatus.Accepted);
    var completed = ExecutionCoordinator.ApplyDecision(underReview, result, review);
    AssertEqual(ExecutionSessionState.Completed, completed.State, "Accepted review must complete execution session.");
}

static void ExecutionRequiresValidatedIntent()
{
    var task = CreateTaskState(ContextIntentState.Candidate, TaskStateStatus.Active, PromptRole.Worker);
    var session = new ExecutionSession("SESSION-002", PromptRole.ShiftLead, task.TaskId, "SHIFT-001", ExecutionSessionState.Initialized);

    AssertThrows<ExecutionCoordinatorException>(
        () => ExecutionCoordinator.PrepareTask(session, task, CreateScopedContext()),
        "Execution must not prepare candidate intent.");
}

static void ExecutionCannotStartWithoutScopedContext()
{
    var task = CreateTaskState(ContextIntentState.Validated, TaskStateStatus.Active, PromptRole.Worker);
    var session = new ExecutionSession("SESSION-003", PromptRole.ShiftLead, task.TaskId, "SHIFT-001", ExecutionSessionState.Initialized);
    var emptyContext = new ScopedContext(Array.Empty<Candidate>(), Array.Empty<ScopedContextReference>(), Array.Empty<ScopedContextSnippet>(), "No context");

    AssertThrows<ExecutionCoordinatorException>(
        () => ExecutionCoordinator.PrepareTask(session, task, emptyContext),
        "Execution must not prepare without scoped context.");
}

static void ExecutionResultMustStayBoundToTask()
{
    var task = CreateTaskState(ContextIntentState.Validated, TaskStateStatus.Active, PromptRole.Worker);
    var session = new ExecutionSession("SESSION-004", PromptRole.ShiftLead, task.TaskId, "SHIFT-001", ExecutionSessionState.Initialized);
    var preparation = ExecutionCoordinator.PrepareTask(session, task, CreateScopedContext());
    var inProgress = ExecutionCoordinator.StartExecution(preparation.Session, preparation.Binding);
    var mismatchedResult = CreateWorkerExecutionResult("TASK-OTHER", WorkerExecutionStatus.Success);

    AssertThrows<ExecutionCoordinatorException>(
        () => ExecutionCoordinator.SubmitResult(inProgress, mismatchedResult),
        "Execution result must remain bound to session task.");
}

static void QcCannotReviewMissingResult()
{
    var task = CreateTaskState(ContextIntentState.Validated, TaskStateStatus.Active, PromptRole.Worker);
    var session = new ExecutionSession("SESSION-005", PromptRole.ShiftLead, task.TaskId, "SHIFT-001", ExecutionSessionState.Initialized);
    var preparation = ExecutionCoordinator.PrepareTask(session, task, CreateScopedContext());
    var inProgress = ExecutionCoordinator.StartExecution(preparation.Session, preparation.Binding);
    var result = CreateWorkerExecutionResult(task.TaskId, WorkerExecutionStatus.Success);

    AssertThrows<ExecutionCoordinatorException>(
        () => ExecutionCoordinator.RequestReview(inProgress, result),
        "QC must not review before result is registered in session.");
}

static void ResultLifecycleFlowIsDeterministic()
{
    var result = CreateWorkerExecutionResult("TASK-BOUNDARY-001", WorkerExecutionStatus.Success);
    var lifecycle = ResultCommitCoordinator.RegisterProducedResult(result);
    AssertEqual(ResultLifecycleStatus.Produced, lifecycle.Status, "Produced result must start in Produced lifecycle.");

    var review = CreateQcReviewResult(result.ResultId, QCReviewStatus.Accepted);
    var acceptance = ResultCommitCoordinator.AcceptResult(
        lifecycle,
        result,
        review,
        new DateTimeOffset(2026, 03, 28, 10, 00, 00, TimeSpan.Zero));

    AssertEqual(ResultLifecycleStatus.Reviewed, acceptance.ReviewedLifecycle.Status, "Accept flow must pass through Reviewed state.");
    AssertEqual(ResultLifecycleStatus.Accepted, acceptance.AcceptedLifecycle.Status, "Accepted lifecycle must be explicit.");

    var apply = ResultCommitCoordinator.ApplyAcceptedResult(
        acceptance.AcceptedLifecycle,
        acceptance.AcceptedResult,
        ApplyTarget.Codebase,
        new[]
        {
            new ApplyChange("Prompting/PromptAssembler.cs", "edit", "Apply deterministic rendering change.")
        });
    AssertEqual(ResultLifecycleStatus.Applied, apply.AppliedLifecycle.Status, "Apply must move lifecycle to Applied.");

    var commit = ResultCommitCoordinator.Commit(
        apply.AppliedLifecycle,
        apply.ApplyOperation,
        new DateTimeOffset(2026, 03, 28, 10, 05, 00, TimeSpan.Zero),
        "Commit accepted prompt change.",
        result.TaskId,
        new[] { "DECISION://qc/review", "ARTIFACT://result" });
    AssertEqual(ResultLifecycleStatus.Committed, commit.CommittedLifecycle.Status, "Commit must move lifecycle to Committed.");
}

static void ResultLifecycleStagesCannotBeSkipped()
{
    var result = CreateWorkerExecutionResult("TASK-BOUNDARY-002", WorkerExecutionStatus.Success);
    var lifecycle = ResultCommitCoordinator.RegisterProducedResult(result);

    AssertThrows<ResultCommitCoordinatorException>(
        () => ResultCommitCoordinator.ApplyAcceptedResult(
            lifecycle,
            new AcceptedResult("ACCEPTED-X", result.ResultId, result.TaskId, result.Summary, DateTimeOffset.UtcNow, "REVIEW-X", result.ProducedArtifacts),
            ApplyTarget.Codebase,
            new[] { new ApplyChange("file.cs", "edit", "change") }),
        "Apply must not be allowed before accepted lifecycle.");

    AssertThrows<ResultCommitCoordinatorException>(
        () => ResultCommitCoordinator.Commit(
            lifecycle,
            new ApplyOperation("APPLY-X", "ACCEPTED-X", ApplyTarget.Codebase, new[] { new ApplyChange("file.cs", "edit", "change") }, ApplyStatus.Applied),
            DateTimeOffset.UtcNow,
            "Invalid commit",
            result.TaskId,
            new[] { "DECISION://x" }),
        "Commit must not be allowed before applied lifecycle.");
}

static void AcceptedResultRemainsImmutableThroughBoundaryFlow()
{
    var result = CreateWorkerExecutionResult("TASK-BOUNDARY-003", WorkerExecutionStatus.Success);
    var lifecycle = ResultCommitCoordinator.RegisterProducedResult(result);
    var review = CreateQcReviewResult(result.ResultId, QCReviewStatus.Accepted);
    var acceptance = ResultCommitCoordinator.AcceptResult(lifecycle, result, review, new DateTimeOffset(2026, 03, 28, 11, 00, 00, TimeSpan.Zero));

    var acceptedBefore = acceptance.AcceptedResult;
    var apply = ResultCommitCoordinator.ApplyAcceptedResult(
        acceptance.AcceptedLifecycle,
        acceptance.AcceptedResult,
        ApplyTarget.Workspace,
        new[] { new ApplyChange("workspace", "sync", "Apply workspace state.") });
    var commit = ResultCommitCoordinator.Commit(
        apply.AppliedLifecycle,
        apply.ApplyOperation,
        new DateTimeOffset(2026, 03, 28, 11, 05, 00, TimeSpan.Zero),
        "Commit accepted workspace change.",
        result.TaskId,
        new[] { "DECISION://qc/review" });

    AssertEqual(acceptedBefore.AcceptedResultId, acceptance.AcceptedResult.AcceptedResultId, "Accepted result id must stay unchanged.");
    AssertEqual(acceptedBefore.Summary, acceptance.AcceptedResult.Summary, "Accepted result summary must stay unchanged.");
    AssertEqual(result.TaskId, commit.CommitRecord.LinkedTaskId, "Commit must stay linked to original task.");
}

static void CommitIsAlwaysLinkedToApply()
{
    var result = CreateWorkerExecutionResult("TASK-BOUNDARY-004", WorkerExecutionStatus.Success);
    var acceptance = ResultCommitCoordinator.AcceptResult(
        ResultCommitCoordinator.RegisterProducedResult(result),
        result,
        CreateQcReviewResult(result.ResultId, QCReviewStatus.Accepted),
        new DateTimeOffset(2026, 03, 28, 12, 00, 00, TimeSpan.Zero));
    var apply = ResultCommitCoordinator.ApplyAcceptedResult(
        acceptance.AcceptedLifecycle,
        acceptance.AcceptedResult,
        ApplyTarget.Document,
        new[] { new ApplyChange("doc.md", "edit", "Apply documentation update.") });
    var commit = ResultCommitCoordinator.Commit(
        apply.AppliedLifecycle,
        apply.ApplyOperation,
        new DateTimeOffset(2026, 03, 28, 12, 05, 00, TimeSpan.Zero),
        "Commit accepted document update.",
        result.TaskId,
        new[] { "DECISION://qc/review" });

    AssertEqual(apply.ApplyOperation.ApplyId, commit.CommitRecord.ApplyId, "Commit must always reference apply id.");
}

static void ShiftStateUpdatesOnlyThroughExplicitCommitStep()
{
    var result = CreateWorkerExecutionResult("TASK-BOUNDARY-005", WorkerExecutionStatus.Success);
    var acceptance = ResultCommitCoordinator.AcceptResult(
        ResultCommitCoordinator.RegisterProducedResult(result),
        result,
        CreateQcReviewResult(result.ResultId, QCReviewStatus.Accepted),
        new DateTimeOffset(2026, 03, 28, 13, 00, 00, TimeSpan.Zero));
    var apply = ResultCommitCoordinator.ApplyAcceptedResult(
        acceptance.AcceptedLifecycle,
        acceptance.AcceptedResult,
        ApplyTarget.Codebase,
        new[] { new ApplyChange("file.cs", "edit", "Apply code update.") });
    var commit = ResultCommitCoordinator.Commit(
        apply.AppliedLifecycle,
        apply.ApplyOperation,
        new DateTimeOffset(2026, 03, 28, 13, 05, 00, TimeSpan.Zero),
        "Commit accepted code update.",
        result.TaskId,
        new[] { "DECISION://qc/review" });

    var originalShift = CreateShiftState(CreateTaskState(ContextIntentState.Validated, TaskStateStatus.Active, PromptRole.Worker));
    AssertTrue(originalShift.AcceptedResults.Count == 1, "Fixture shift starts with one accepted result.");

    var updatedShift = ResultCommitCoordinator.UpdateShiftState(originalShift, commit.CommitRecord, PromptRole.ShiftLead);

    AssertTrue(updatedShift.AcceptedResults.Count == 2, "Shift state must change only after explicit update step.");
    AssertTrue(updatedShift.AcceptedResults.Any(value => value.Contains(commit.CommitRecord.CommitId, StringComparison.Ordinal)), "Committed result must be recorded in shift state.");
}

static void ExecutionTraceOrderIsDeterministic()
{
    var task = CreateTaskState(ContextIntentState.Validated, TaskStateStatus.Active, PromptRole.Worker);
    var session = new ExecutionSession("SESSION-TRACE-001", PromptRole.ShiftLead, task.TaskId, "SHIFT-001", ExecutionSessionState.Initialized);
    var preparation = ExecutionCoordinator.PrepareTask(session, task, CreateScopedContext());
    var inProgress = ExecutionCoordinator.StartExecution(preparation.Session, preparation.Binding);
    var result = CreateWorkerExecutionResult(task.TaskId, WorkerExecutionStatus.Success);
    var produced = ExecutionCoordinator.SubmitResult(inProgress, result);
    var underReview = ExecutionCoordinator.RequestReview(produced, result);
    var review = CreateQcReviewResult(result.ResultId, QCReviewStatus.Accepted);
    var acceptance = ResultCommitCoordinator.AcceptResult(ResultCommitCoordinator.RegisterProducedResult(result), result, review, new DateTimeOffset(2026, 03, 28, 14, 00, 00, TimeSpan.Zero));
    var apply = ResultCommitCoordinator.ApplyAcceptedResult(acceptance.AcceptedLifecycle, acceptance.AcceptedResult, ApplyTarget.Codebase, new[] { new ApplyChange("file.cs", "edit", "Apply change.") });
    var commit = ResultCommitCoordinator.Commit(apply.AppliedLifecycle, apply.ApplyOperation, new DateTimeOffset(2026, 03, 28, 14, 05, 00, TimeSpan.Zero), "Commit change.", task.TaskId, new[] { "DECISION://qc/review" });

    var trace = TraceRecorder.Start("TRACE-001", session.SessionId, task.TaskId);
    trace = TraceRecorder.RecordPreparation(trace, preparation, new DateTimeOffset(2026, 03, 28, 13, 55, 00, TimeSpan.Zero));
    trace = TraceRecorder.RecordExecutionStarted(trace, inProgress, new DateTimeOffset(2026, 03, 28, 13, 56, 00, TimeSpan.Zero));
    trace = TraceRecorder.RecordResult(trace, result, new DateTimeOffset(2026, 03, 28, 13, 57, 00, TimeSpan.Zero));
    trace = TraceRecorder.RecordReview(trace, review, task.TaskId, new DateTimeOffset(2026, 03, 28, 13, 58, 00, TimeSpan.Zero));
    trace = TraceRecorder.RecordApply(trace, apply.ApplyOperation, task.TaskId, new DateTimeOffset(2026, 03, 28, 13, 59, 00, TimeSpan.Zero));
    trace = TraceRecorder.RecordCommit(trace, commit.CommitRecord, new DateTimeOffset(2026, 03, 28, 14, 05, 00, TimeSpan.Zero));

    AssertSequenceEqual(
        new[]
        {
            ExecutionStepType.Prepare,
            ExecutionStepType.Execute,
            ExecutionStepType.Result,
            ExecutionStepType.Review,
            ExecutionStepType.Apply,
            ExecutionStepType.Commit
        },
        trace.Steps.Select(step => step.StepType),
        "Trace steps must remain in deterministic order.");
}

static void TraceIsAppendOnly()
{
    var trace = TraceRecorder.Start("TRACE-002", "SESSION-TRACE-002", "TASK-TRACE-002");
    var first = TraceRecorder.AppendStep(
        trace,
        ExecutionStepType.Prepare,
        new DateTimeOffset(2026, 03, 28, 15, 00, 00, TimeSpan.Zero),
        PromptRole.ShiftLead,
        "TASK-TRACE-002",
        "Prepared task.");
    var second = TraceRecorder.AppendStep(
        first,
        ExecutionStepType.Execute,
        new DateTimeOffset(2026, 03, 28, 15, 01, 00, TimeSpan.Zero),
        PromptRole.Worker,
        "SESSION-TRACE-002",
        "Started execution.");

    AssertTrue(trace.Steps.Count == 0, "Original trace must remain unchanged.");
    AssertTrue(first.Steps.Count == 1, "First append must create one-step trace.");
    AssertTrue(second.Steps.Count == 2, "Second append must create two-step trace.");
}

static void CommitIsAlwaysReflectedInTrace()
{
    var result = CreateWorkerExecutionResult("TASK-TRACE-003", WorkerExecutionStatus.Success);
    var commit = ResultCommitCoordinator.Commit(
        ResultCommitCoordinator.ApplyAcceptedResult(
            ResultCommitCoordinator.AcceptResult(
                ResultCommitCoordinator.RegisterProducedResult(result),
                result,
                CreateQcReviewResult(result.ResultId, QCReviewStatus.Accepted),
                new DateTimeOffset(2026, 03, 28, 16, 00, 00, TimeSpan.Zero)).AcceptedLifecycle,
            new AcceptedResult($"ACCEPTED-{result.ResultId}", result.ResultId, result.TaskId, result.Summary, new DateTimeOffset(2026, 03, 28, 16, 00, 00, TimeSpan.Zero), $"REVIEW-{result.ResultId}", result.ProducedArtifacts),
            ApplyTarget.Codebase,
            new[] { new ApplyChange("file.cs", "edit", "Apply change.") }).AppliedLifecycle,
        new ApplyOperation($"APPLY-ACCEPTED-{result.ResultId}", $"ACCEPTED-{result.ResultId}", ApplyTarget.Codebase, new[] { new ApplyChange("file.cs", "edit", "Apply change.") }, ApplyStatus.Applied),
        new DateTimeOffset(2026, 03, 28, 16, 05, 00, TimeSpan.Zero),
        "Commit boundary change.",
        result.TaskId,
        new[] { "DECISION://qc/review" });

    var trace = TraceRecorder.Start("TRACE-003", "SESSION-TRACE-003", result.TaskId);
    trace = TraceRecorder.RecordCommit(trace, commit.CommitRecord, new DateTimeOffset(2026, 03, 28, 16, 05, 00, TimeSpan.Zero));

    AssertTrue(trace.Steps.Any(step => step.StepType == ExecutionStepType.Commit && step.ReferenceId == commit.CommitRecord.CommitId), "Commit must always appear in trace.");
}

static void SnapshotIsConsistentAndDeterministic()
{
    var taskA = CreateTaskState(ContextIntentState.Validated, TaskStateStatus.Active, PromptRole.Worker);
    var taskB = new StateTaskState(
        "TASK-OPEN-002",
        ContextIntentState.Candidate,
        TaskStateStatus.Active,
        "Prepare retrieval narrowing",
        new[] { "Retrieval/BasicCandidateSelector.cs" },
        new[] { "Selector remains deterministic" },
        PromptRole.ShiftLead,
        PromptRole.ShiftLead,
        new DateTimeOffset(2026, 03, 28, 17, 00, 00, TimeSpan.Zero));
    var shift = new ShiftState(
        "SHIFT-SNAPSHOT-001",
        "Build execution history",
        taskA.TaskId,
        ShiftStateStatus.Active,
        new[] { taskA, taskB },
        new[] { "Pending review" },
        new[] { "COMMIT-OLD|task:TASK-OLD|Previous change" },
        new[] { "Keep trace deterministic", "No persistence yet" });

    var commits = new[]
    {
        new CommitRecord("COMMIT-002", "APPLY-002", new DateTimeOffset(2026, 03, 28, 17, 10, 00, TimeSpan.Zero), "Second commit", "TASK-OPEN-002", new[] { "DECISION://2" }),
        new CommitRecord("COMMIT-001", "APPLY-001", new DateTimeOffset(2026, 03, 28, 17, 05, 00, TimeSpan.Zero), "First commit", "TASK-OPEN-001", new[] { "DECISION://1" })
    };

    var first = TraceSnapshotBuilder.BuildSnapshot(
        shift,
        "execution://shift/SHIFT-001/task/TASK-OPEN-001/target/IdleSubsystem/outcome/NoOp",
        "task://TASK-OPEN-001",
        commits,
        shift.Tasks,
        new DateTimeOffset(2026, 03, 28, 17, 15, 00, TimeSpan.Zero));
    var second = TraceSnapshotBuilder.BuildSnapshot(
        shift,
        "execution://shift/SHIFT-001/task/TASK-OPEN-001/target/IdleSubsystem/outcome/NoOp",
        "task://TASK-OPEN-001",
        commits,
        shift.Tasks,
        new DateTimeOffset(2026, 03, 28, 17, 15, 00, TimeSpan.Zero));

    AssertEqual(first.SnapshotId, second.SnapshotId, "Snapshot id must be deterministic for same inputs and timestamp.");
    AssertSequenceEqual(first.Commits.Select(commit => commit.CommitId), second.Commits.Select(commit => commit.CommitId), "Snapshot commits must remain ordered deterministically.");
    AssertSequenceEqual(first.OpenTasks.Select(task => task.TaskId), second.OpenTasks.Select(task => task.TaskId), "Snapshot open tasks must remain ordered deterministically.");
}

static void PersistenceBootstrapCreatesMinimalZavodLayout()
{
    var workspaceRoot = CreateScratchWorkspace();
    try
    {
        var state = ProjectStateStorage.EnsureInitialized(workspaceRoot, "zavod-test", "ZAVOD Test");

        AssertTrue(Directory.Exists(Path.Combine(workspaceRoot, ".zavod")), ".zavod root must be created.");
        AssertTrue(Directory.Exists(Path.Combine(workspaceRoot, ".zavod", "project")), "Project truth directory must be created.");
        AssertTrue(Directory.Exists(Path.Combine(workspaceRoot, ".zavod", "shifts")), "Shifts directory must be created.");
        AssertTrue(Directory.Exists(Path.Combine(workspaceRoot, ".zavod", "snapshots")), "Snapshots directory must be created.");
        AssertTrue(File.Exists(Path.Combine(workspaceRoot, ".zavod", "meta", "project.json")), "Project meta file must be created.");
        AssertTrue(state.IsColdStart, "Freshly initialized state must start in cold start mode.");
        AssertEqual(Path.Combine(workspaceRoot, ".zavod", "project", "project.md"), state.TruthPointers.ProjectDocumentPath, "Project truth pointer must be deterministic.");
    }
    finally
    {
        DeleteScratchWorkspace(workspaceRoot);
    }
}

static void ProjectStateLoadSaveRoundtripIsDeterministic()
{
    var workspaceRoot = CreateScratchWorkspace();
    try
    {
        var initial = ProjectStateStorage.EnsureInitialized(workspaceRoot, "zavod-roundtrip", "ZAVOD Roundtrip");
        var updated = initial with { ActiveShiftId = "SHIFT-001", ActiveTaskId = "TASK-001" };

        var persisted = ProjectStateStorage.Save(updated);
        var reloaded = ProjectStateStorage.Load(workspaceRoot);

        AssertEqual("SHIFT-001", persisted.ActiveShiftId, "Saved state must persist active shift id.");
        AssertEqual("TASK-001", persisted.ActiveTaskId, "Saved state must persist active task id.");
        AssertEqual(persisted, reloaded, "Reloaded state must match persisted state.");
    }
    finally
    {
        DeleteScratchWorkspace(workspaceRoot);
    }
}

static void ColdStartDetectionStaysHonest()
{
    var workspaceRoot = CreateScratchWorkspace();
    try
    {
        var cold = ProjectStateStorage.EnsureInitialized(workspaceRoot, "zavod-cold", "ZAVOD Cold");
        AssertTrue(ProjectStateStorage.IsColdStart(cold), "State without active shift must be cold start.");

        var active = ProjectStateStorage.Save(cold with { ActiveShiftId = "SHIFT-ACTIVE" });
        AssertFalse(ProjectStateStorage.IsColdStart(active), "State with active shift must not be cold start.");
    }
    finally
    {
        DeleteScratchWorkspace(workspaceRoot);
    }
}

static void InvalidPersistedProjectMetaFailsFast()
{
    var workspaceRoot = CreateScratchWorkspace();
    try
    {
        ProjectStateStorage.EnsureInitialized(workspaceRoot, "zavod-invalid", "ZAVOD Invalid");

        var metaFilePath = Path.Combine(workspaceRoot, ".zavod", "meta", "project.json");
        File.WriteAllText(metaFilePath, "{ not-valid-json");

        var exception = AssertThrows<ZavodPersistenceException>(
            () => ProjectStateStorage.Load(workspaceRoot),
            "Broken project meta must fail fast.");

        AssertEqual("InvalidProjectMeta", exception.Code, "Broken project meta must report InvalidProjectMeta.");
    }
    finally
    {
        DeleteScratchWorkspace(workspaceRoot);
    }
}

static void SnapshotFileIsPersistedDeterministically()
{
    var workspaceRoot = CreateScratchWorkspace();
    try
    {
        ProjectStateStorage.EnsureInitialized(workspaceRoot, "zavod-snapshot", "ZAVOD Snapshot");

        var task = CreateTaskState(ContextIntentState.Validated, TaskStateStatus.Active, PromptRole.Worker);
        var shift = CreateShiftState(task);
        var commits = new[]
        {
            new CommitRecord("COMMIT-010", "APPLY-010", new DateTimeOffset(2026, 03, 28, 18, 00, 00, TimeSpan.Zero), "Persist snapshot", task.TaskId, new[] { "DECISION://snapshot" })
        };
        var snapshot = TraceSnapshotBuilder.BuildSnapshot(
            shift,
            "execution://shift/SHIFT-001/task/TASK-001/target/IdleSubsystem/outcome/NoOp",
            $"task://{task.TaskId}",
            commits,
            shift.Tasks,
            new DateTimeOffset(2026, 03, 28, 18, 05, 00, TimeSpan.Zero));

        var filePath = SnapshotStorage.Save(workspaceRoot, snapshot);
        var persisted = File.ReadAllText(filePath);

        AssertTrue(File.Exists(filePath), "Snapshot file must be written.");
        AssertContains(persisted, snapshot.SnapshotId, "Snapshot file must contain snapshot id.");
        AssertContains(persisted, "\"ShiftStateReference\": \"shift://SHIFT-001\"", "Snapshot file must contain deterministic shift reference.");
        AssertContains(persisted, "\"ExecutionReference\": \"execution://shift/SHIFT-001/task/TASK-001/target/IdleSubsystem/outcome/NoOp\"", "Snapshot file must contain execution reference.");
        AssertContains(persisted, $"\"TaskReference\": \"task://{task.TaskId}\"", "Snapshot file must contain task reference.");
    }
    finally
    {
        DeleteScratchWorkspace(workspaceRoot);
    }
}

static void SnapshotFilePersistsCheckpointMetadata()
{
    var workspaceRoot = CreateScratchWorkspace();
    try
    {
        ProjectStateStorage.EnsureInitialized(workspaceRoot, "zavod-snapshot-metadata", "ZAVOD Snapshot Metadata");

        var task = CreateTaskState(ContextIntentState.Validated, TaskStateStatus.Active, PromptRole.Worker);
        var shift = CreateShiftState(task);
        var snapshot = TraceSnapshotBuilder.BuildSnapshot(
            shift,
            "execution://shift/SHIFT-001/accepted-result/ACCEPTED-RESULT-001/commit/COMMIT-001",
            $"task://{task.TaskId}",
            new[]
            {
                new CommitRecord("COMMIT-001", "APPLY-001", new DateTimeOffset(2026, 03, 28, 18, 00, 00, TimeSpan.Zero), "Persist snapshot", task.TaskId, new[] { "DECISION://snapshot" })
            },
            shift.Tasks,
            new DateTimeOffset(2026, 03, 28, 18, 05, 00, TimeSpan.Zero),
            checkpointKind: "soft",
            triggerScore: 2,
            triggerReasons: new[] { "domain_shift" },
            dedupeKey: "soft-checkpoint:ACCEPTED-RESULT-001",
            snapshotId: "SNAPSHOT-SHIFT-001-SOFT-ACCEPTED-RESULT-001");

        var filePath = SnapshotStorage.Save(workspaceRoot, snapshot);
        var persisted = File.ReadAllText(filePath);

        AssertContains(persisted, "\"CheckpointKind\": \"soft\"", "Snapshot file must persist checkpoint kind.");
        AssertContains(persisted, "\"TriggerScore\": 2", "Snapshot file must persist trigger score.");
        AssertContains(persisted, "\"TriggerReasons\": [", "Snapshot file must persist trigger reasons.");
        AssertContains(persisted, "\"domain_shift\"", "Snapshot file must persist trigger reason values.");
        AssertContains(persisted, "\"DedupeKey\": \"soft-checkpoint:ACCEPTED-RESULT-001\"", "Snapshot file must persist dedupe key.");
    }
    finally
    {
        DeleteScratchWorkspace(workspaceRoot);
    }
}

static void BootstrapReportsColdStartForEmptyProject()
{
    var workspaceRoot = CreateScratchWorkspace();
    try
    {
        var result = ProjectBootstrap.Initialize(workspaceRoot);

        AssertTrue(result.IsColdStart, "Bootstrap must report cold start for empty project.");
        AssertTrue(result.HasValidState, "Bootstrap must report valid state after initialization.");
        AssertFalse(result.HasActiveShift, "Cold start project must not have active shift.");
        AssertTrue(Directory.Exists(Path.Combine(workspaceRoot, ".zavod")), "Bootstrap must initialize .zavod storage.");
    }
    finally
    {
        DeleteScratchWorkspace(workspaceRoot);
    }
}

static void BootstrapReportsNormalStateForActiveShift()
{
    var workspaceRoot = CreateScratchWorkspace();
    try
    {
        var initial = ProjectStateStorage.EnsureInitialized(workspaceRoot, "zavod-bootstrap", "ZAVOD Bootstrap");
        ProjectStateStorage.Save(initial with { ActiveShiftId = "SHIFT-BOOTSTRAP-001" });

        var result = ProjectBootstrap.Initialize(workspaceRoot);

        AssertFalse(result.IsColdStart, "Bootstrap must not report cold start when active shift exists.");
        AssertTrue(result.HasValidState, "Bootstrap must report valid state for persisted project.");
        AssertTrue(result.HasActiveShift, "Bootstrap must report active shift when state has active shift id.");
    }
    finally
    {
        DeleteScratchWorkspace(workspaceRoot);
    }
}

static void BootstrapPreservesFailFastForInvalidMeta()
{
    var workspaceRoot = CreateScratchWorkspace();
    try
    {
        ProjectStateStorage.EnsureInitialized(workspaceRoot, "zavod-bootstrap-invalid", "ZAVOD Bootstrap Invalid");
        File.WriteAllText(Path.Combine(workspaceRoot, ".zavod", "meta", "project.json"), "{ invalid");

        var exception = AssertThrows<ZavodPersistenceException>(
            () => ProjectBootstrap.Initialize(workspaceRoot),
            "Bootstrap must preserve fail-fast behavior for invalid meta.");

        AssertEqual("InvalidProjectMeta", exception.Code, "Bootstrap must surface InvalidProjectMeta for broken state.");
    }
    finally
    {
        DeleteScratchWorkspace(workspaceRoot);
    }
}

static void BootstrapIsIdempotentAcrossRepeatedStartup()
{
    var workspaceRoot = CreateScratchWorkspace();
    try
    {
        var first = ProjectBootstrap.Initialize(workspaceRoot);
        var metaFilePath = Path.Combine(workspaceRoot, ".zavod", "meta", "project.json");
        var metaAfterFirstRun = File.ReadAllText(metaFilePath);

        var second = ProjectBootstrap.Initialize(workspaceRoot);
        var metaAfterSecondRun = File.ReadAllText(metaFilePath);

        AssertEqual(first, second, "Bootstrap result must remain stable across repeated startup.");
        AssertEqual(metaAfterFirstRun, metaAfterSecondRun, "Repeated bootstrap must not rewrite initialized project meta.");
    }
    finally
    {
        DeleteScratchWorkspace(workspaceRoot);
    }
}

static void ProjectEntrySelectorRoutesColdStartStateToBootstrapMode()
{
    var projectState = CreateProjectState(activeShiftId: null, activeTaskId: null);

    var selection = ProjectEntrySelector.Select(projectState);

    AssertEqual(ProjectEntryMode.Bootstrap, selection.Mode, "Canonical entry selector must route state without active shift to bootstrap mode.");
    AssertEqual(projectState, selection.ProjectState, "Canonical entry selector must preserve input project state for bootstrap routing.");
    AssertTrue(selection.ResumeResult is null, "Bootstrap routing must not fabricate resume result.");
}

static void ProjectEntrySelectorRoutesActiveShiftStateToResumeMode()
{
    var workspaceRoot = CreateScratchWorkspace();
    try
    {
        var initial = ProjectStateStorage.EnsureInitialized(workspaceRoot, "zavod-entry-selector", "ZAVOD Entry Selector");
        var bootstrap = FirstShiftBootstrap.Create(
            initial,
            new FirstShiftBootstrapRequest(
                "Route project entry to resume",
                "Resume canonical task",
                new DateTimeOffset(2026, 03, 28, 18, 47, 00, TimeSpan.Zero)));

        var selection = ProjectEntrySelector.Select(ProjectStateStorage.Load(workspaceRoot));

        AssertEqual(ProjectEntryMode.Resume, selection.Mode, "Canonical entry selector must route active shift state to resume mode.");
        AssertTrue(selection.ResumeResult is not null, "Resume routing must carry explicit resume result.");
        AssertEqual(bootstrap.ProjectState.ActiveShiftId, selection.ResumeResult!.ShiftState.ShiftId, "Resume routing must point to persisted active shift truth.");
        AssertEqual(bootstrap.ProjectState.ActiveTaskId, selection.ResumeResult.TaskState.TaskId, "Resume routing must point to persisted active task truth.");
    }
    finally
    {
        DeleteScratchWorkspace(workspaceRoot);
    }
}

static void ProjectEntrySelectorFailsFastForActiveTaskWithoutActiveShift()
{
    var projectState = CreateProjectState(activeShiftId: null, activeTaskId: "TASK-001");

    AssertThrows<InvalidOperationException>(
        () => ProjectEntrySelector.Select(projectState),
        "Canonical entry selector must fail fast for active task without active shift.",
        static exception => exception.Message.Contains("no active shift", StringComparison.Ordinal));
}

static void ProjectEntryResolverReturnsUnifiedBootstrapResult()
{
    var projectState = CreateProjectState(activeShiftId: null, activeTaskId: null);

    var result = ProjectEntryResolver.Resolve(projectState);

    AssertEqual(ProjectEntryMode.Bootstrap, result.Mode, "Unified project entry result must preserve bootstrap mode.");
    AssertEqual(projectState, result.ProjectState, "Unified project entry result must preserve project state.");
    AssertTrue(result.IsBootstrapReady, "Bootstrap entry result must explicitly signal bootstrap readiness.");
    AssertTrue(result.ShiftState is null, "Bootstrap entry result must not fabricate shift truth.");
    AssertTrue(result.TaskState is null, "Bootstrap entry result must not fabricate task truth.");
    AssertTrue(result.ResumeResult is null, "Bootstrap entry result must not fabricate resume data.");
}

static void ProjectEntryResolverReturnsUnifiedResumeResult()
{
    var workspaceRoot = CreateScratchWorkspace();
    try
    {
        var initial = ProjectStateStorage.EnsureInitialized(workspaceRoot, "zavod-entry-result", "ZAVOD Entry Result");
        var bootstrap = FirstShiftBootstrap.Create(
            initial,
            new FirstShiftBootstrapRequest(
                "Build unified resume entry result",
                "Resume canonical task",
                new DateTimeOffset(2026, 03, 28, 18, 48, 00, TimeSpan.Zero)));

        var result = ProjectEntryResolver.Resolve(ProjectStateStorage.Load(workspaceRoot));

        AssertEqual(ProjectEntryMode.Resume, result.Mode, "Unified project entry result must preserve resume mode.");
        AssertEqual(bootstrap.ProjectState.ActiveShiftId, result.ProjectState.ActiveShiftId, "Unified project entry result must preserve project active shift.");
        AssertEqual(bootstrap.ProjectState.ActiveTaskId, result.ProjectState.ActiveTaskId, "Unified project entry result must preserve project active task.");
        AssertTrue(!result.IsBootstrapReady, "Resume entry result must not report bootstrap readiness.");
        AssertTrue(result.ShiftState is not null, "Resume entry result must carry active shift truth.");
        AssertTrue(result.TaskState is not null, "Resume entry result must carry active task truth.");
        AssertTrue(result.ResumeResult is not null, "Resume entry result must preserve explicit resume data.");
        AssertEqual(bootstrap.ShiftState.ShiftId, result.ShiftState!.ShiftId, "Resume entry result must expose persisted shift truth.");
        AssertEqual(bootstrap.Task!.TaskId, result.TaskState!.TaskId, "Resume entry result must expose persisted task truth.");
    }
    finally
    {
        DeleteScratchWorkspace(workspaceRoot);
    }
}

static void ActiveShiftResumeReturnsPersistedActiveShiftAndTaskTruth()
{
    var workspaceRoot = CreateScratchWorkspace();
    try
    {
        var initial = ProjectStateStorage.EnsureInitialized(workspaceRoot, "zavod-resume", "ZAVOD Resume");
        var bootstrap = FirstShiftBootstrap.Create(
            initial,
            new FirstShiftBootstrapRequest(
                "Resume existing active shift",
                "Resume canonical task",
                new DateTimeOffset(2026, 03, 28, 18, 45, 00, TimeSpan.Zero)));

        var resumed = ActiveShiftResume.Resume(ProjectStateStorage.Load(workspaceRoot));

        AssertEqual(bootstrap.ProjectState.ActiveShiftId, resumed.ProjectState.ActiveShiftId, "Canonical resume must preserve active shift binding.");
        AssertEqual(bootstrap.ProjectState.ActiveTaskId, resumed.ProjectState.ActiveTaskId, "Canonical resume must preserve active task binding.");
        AssertEqual(bootstrap.ShiftState.ShiftId, resumed.ShiftState.ShiftId, "Canonical resume must load persisted shift truth.");
        AssertEqual(bootstrap.ShiftState.CurrentTaskId, resumed.ShiftState.CurrentTaskId, "Canonical resume must preserve current shift task binding.");
        AssertEqual(bootstrap.Task!.TaskId, resumed.TaskState.TaskId, "Canonical resume must return persisted active task truth.");
    }
    finally
    {
        DeleteScratchWorkspace(workspaceRoot);
    }
}

static void ActiveShiftResumeFailsFastForMismatchedActiveTaskBinding()
{
    var workspaceRoot = CreateScratchWorkspace();
    try
    {
        var initial = ProjectStateStorage.EnsureInitialized(workspaceRoot, "zavod-resume-invalid", "ZAVOD Resume Invalid");
        var bootstrap = FirstShiftBootstrap.Create(
            initial,
            new FirstShiftBootstrapRequest(
                "Resume existing active shift",
                "Resume canonical task",
                new DateTimeOffset(2026, 03, 28, 18, 46, 00, TimeSpan.Zero)));

        var inconsistent = bootstrap.ProjectState with
        {
            ActiveTaskId = "TASK-999"
        };

        AssertThrows<InvalidOperationException>(
            () => ActiveShiftResume.Resume(inconsistent),
            "Canonical resume must fail fast for mismatched active task binding.",
            static exception => exception.Message.Contains("active task", StringComparison.Ordinal));
    }
    finally
    {
        DeleteScratchWorkspace(workspaceRoot);
    }
}

static void TaskIntentValidatesCandidateBeforeCanonicalTaskCreation()
{
    var candidate = TaskIntentFactory.CreateCandidate("Establish canonical task binding");
    var ready = candidate.MarkReadyForValidation();
    var validated = ready.Validate();
    var task = TaskStateFactory.CreateFromValidatedIntent(
        validated,
        "TASK-001",
        PromptRole.ShiftLead,
        PromptRole.Worker,
        new DateTimeOffset(2026, 03, 28, 18, 15, 00, TimeSpan.Zero));

    AssertEqual(ContextIntentState.Candidate, candidate.Status, "Initial intent must start as candidate.");
    AssertEqual(ContextIntentState.ReadyForValidation, ready.Status, "Intent must reach ready_for_validation before validation.");
    AssertEqual(ContextIntentState.Validated, validated.Status, "Intent validation must produce validated intent.");
    AssertEqual(ContextIntentState.Validated, task.IntentState, "Canonical task must preserve validated intent state.");
    AssertEqual("Establish canonical task binding", task.Description, "Canonical task must preserve validated intent description.");
}

static void TaskFactoryRejectsNonValidatedIntent()
{
    var candidate = TaskIntentFactory.CreateCandidate("Do not create task directly from candidate");

    AssertThrows<InvalidOperationException>(
        () => TaskStateFactory.CreateFromValidatedIntent(
            candidate,
            "TASK-001",
            PromptRole.ShiftLead,
            PromptRole.Worker,
            new DateTimeOffset(2026, 03, 28, 18, 16, 00, TimeSpan.Zero)),
        "Canonical task creation must reject non-validated intent.",
        static exception => exception.Message.Contains("validated intent", StringComparison.Ordinal));
}

static void ValidatedIntentTaskApplierBindsTaskToShiftAndProjectState()
{
    var projectState = CreateProjectState(activeShiftId: null, activeTaskId: null);
    var shiftState = new ShiftState(
        "SHIFT-001",
        "Canonical task application",
        null,
        ShiftStateStatus.Active,
        Array.Empty<StateTaskState>(),
        Array.Empty<string>(),
        Array.Empty<string>(),
        Array.Empty<string>());
    var intent = TaskIntentFactory.CreateCandidate("Bind validated intent to canonical task").MarkReadyForValidation().Validate();

    var result = ValidatedIntentTaskApplier.Apply(
        projectState,
        shiftState,
        intent,
        "TASK-001",
        new DateTimeOffset(2026, 03, 28, 18, 18, 00, TimeSpan.Zero));

    AssertEqual("SHIFT-001", result.ProjectState.ActiveShiftId, "Validated intent application must bind active shift in project state.");
    AssertEqual("TASK-001", result.ProjectState.ActiveTaskId, "Validated intent application must bind active task in project state.");
    AssertEqual("TASK-001", result.ShiftState.CurrentTaskId, "Validated intent application must bind current task in shift truth.");
    AssertEqual("TASK-001", result.Task.TaskId, "Validated intent application must create canonical task.");
    AssertEqual(ContextIntentState.Validated, result.Task.IntentState, "Validated intent application must preserve validated intent state.");
}

static void ValidatedIntentTaskApplierRejectsOccupiedShiftTaskSlot()
{
    var existingTask = new StateTaskState(
        "TASK-000",
        ContextIntentState.Validated,
        TaskStateStatus.Active,
        "Existing canonical task",
        Array.Empty<string>(),
        Array.Empty<string>(),
        PromptRole.ShiftLead,
        PromptRole.Worker,
        new DateTimeOffset(2026, 03, 28, 18, 18, 00, TimeSpan.Zero));
    var projectState = CreateProjectState(activeShiftId: "SHIFT-001", activeTaskId: null);
    var shiftState = new ShiftState(
        "SHIFT-001",
        "Canonical task application",
        "TASK-000",
        ShiftStateStatus.Active,
        new[] { existingTask },
        Array.Empty<string>(),
        Array.Empty<string>(),
        Array.Empty<string>());
    var intent = TaskIntentFactory.CreateCandidate("Bind validated intent to canonical task").MarkReadyForValidation().Validate();

    AssertThrows<InvalidOperationException>(
        () => ValidatedIntentTaskApplier.Apply(
            projectState,
            shiftState,
            intent,
            "TASK-001",
            new DateTimeOffset(2026, 03, 28, 18, 18, 00, TimeSpan.Zero)),
        "Canonical task application must reject occupied shift task slot.",
        static exception => exception.Message.Contains("empty shift task slot", StringComparison.Ordinal));
}

static void ValidatedIntentTaskApplierRejectsOccupiedProjectTaskSlot()
{
    var projectState = CreateProjectState(activeShiftId: "SHIFT-001", activeTaskId: "TASK-000");
    var shiftState = new ShiftState(
        "SHIFT-001",
        "Canonical task application",
        null,
        ShiftStateStatus.Active,
        Array.Empty<StateTaskState>(),
        Array.Empty<string>(),
        Array.Empty<string>(),
        Array.Empty<string>());
    var intent = TaskIntentFactory.CreateCandidate("Bind validated intent to canonical task").MarkReadyForValidation().Validate();

    AssertThrows<InvalidOperationException>(
        () => ValidatedIntentTaskApplier.Apply(
            projectState,
            shiftState,
            intent,
            "TASK-001",
            new DateTimeOffset(2026, 03, 28, 18, 18, 00, TimeSpan.Zero)),
        "Canonical task application must reject occupied project task slot.",
        static exception => exception.Message.Contains("empty project task slot", StringComparison.Ordinal));
}

static void ValidatedIntentTaskApplierRejectsMismatchedActiveShiftBinding()
{
    var projectState = CreateProjectState(activeShiftId: "SHIFT-999", activeTaskId: null);
    var shiftState = new ShiftState(
        "SHIFT-001",
        "Canonical task application",
        null,
        ShiftStateStatus.Active,
        Array.Empty<StateTaskState>(),
        Array.Empty<string>(),
        Array.Empty<string>(),
        Array.Empty<string>());
    var intent = TaskIntentFactory.CreateCandidate("Bind validated intent to canonical task").MarkReadyForValidation().Validate();

    AssertThrows<InvalidOperationException>(
        () => ValidatedIntentTaskApplier.Apply(
            projectState,
            shiftState,
            intent,
            "TASK-001",
            new DateTimeOffset(2026, 03, 28, 18, 18, 00, TimeSpan.Zero)),
        "Canonical task application must reject mismatched active shift binding.",
        static exception => exception.Message.Contains("active shift must match target shift", StringComparison.Ordinal));
}

static void FirstShiftBootstrapCreatesActiveShiftTruthFromColdStart()
{
    var workspaceRoot = CreateScratchWorkspace();
    try
    {
        var initial = ProjectStateStorage.EnsureInitialized(workspaceRoot, "zavod-bootstrap-shift", "ZAVOD Bootstrap Shift");
        var result = FirstShiftBootstrap.Create(
            initial,
            new FirstShiftBootstrapRequest(
                "Start the first canonical shift",
                "Establish the first canonical task",
                new DateTimeOffset(2026, 03, 28, 18, 20, 00, TimeSpan.Zero)));
        var reloadedState = ProjectStateStorage.Load(workspaceRoot);
        var reloadedShift = ShiftStateStorage.Load(workspaceRoot, "SHIFT-001");

        AssertEqual("SHIFT-001", result.ProjectState.ActiveShiftId, "First shift bootstrap must bind active shift in project state.");
        AssertEqual("TASK-001", result.ProjectState.ActiveTaskId, "First shift bootstrap must bind initial task in project state.");
        AssertEqual(ShiftStateStatus.Active, result.ShiftState.Status, "First shift bootstrap must create active shift.");
        AssertEqual("TASK-001", result.ShiftState.CurrentTaskId, "First shift bootstrap must bind initial task as current shift task.");
        AssertTrue(result.Intent is not null, "First shift bootstrap with description must create intent first.");
        AssertEqual(ContextIntentState.Validated, result.Intent!.Status, "First shift bootstrap must validate intent before task creation.");
        AssertTrue(result.Task is not null, "First shift bootstrap with task description must create initial task truth.");
        AssertEqual(TaskStateStatus.Active, result.Task!.Status, "Initial bootstrap task must start as active canonical task.");
        AssertEqual(result.Intent.Description, result.Task.Description, "Task must be created from validated intent description.");
        AssertTrue(File.Exists(result.ShiftFilePath), "First shift bootstrap must persist shift truth.");
        AssertEqual(result.ProjectState, reloadedState, "Persisted project state must match bootstrap result.");
        AssertEqual(result.ShiftState.ShiftId, reloadedShift.ShiftId, "Persisted shift truth must preserve shift id.");
        AssertEqual(result.ShiftState.Goal, reloadedShift.Goal, "Persisted shift truth must preserve shift goal.");
        AssertEqual(result.ShiftState.CurrentTaskId, reloadedShift.CurrentTaskId, "Persisted shift truth must preserve current task binding.");
        AssertEqual(result.ShiftState.Status, reloadedShift.Status, "Persisted shift truth must preserve status.");
        AssertSequenceEqual(result.ShiftState.Tasks.Select(static task => task.TaskId), reloadedShift.Tasks.Select(static task => task.TaskId), "Persisted shift truth must preserve task ids.");
    }
    finally
    {
        DeleteScratchWorkspace(workspaceRoot);
    }
}

static void FirstShiftBootstrapEnablesFullCycleFromColdStartToSnapshot()
{
    var workspaceRoot = CreateScratchWorkspace();
    try
    {
        var bootstrapState = ProjectBootstrap.Initialize(workspaceRoot);
        AssertTrue(bootstrapState.IsColdStart, "Empty project must enter bootstrap mode before first shift creation.");

        var initial = ProjectStateStorage.Load(workspaceRoot);
        var firstShift = FirstShiftBootstrap.Create(
            initial,
            new FirstShiftBootstrapRequest(
                "Run the first project shift",
                "Execute the first canonical task",
                new DateTimeOffset(2026, 03, 28, 18, 30, 00, TimeSpan.Zero)));

        var activeTask = firstShift.Task!;
        var activeShift = firstShift.ShiftState.UpdateTask(activeTask);
        var context = TaskExecutionContextBuilder.Build(firstShift.ProjectState, activeShift);
        var pipeline = new ExecutionPipeline(new ExecutionDispatcher(
            new RecordingBootstrapSubsystem(new SubsystemHandleResult(SubsystemHandleStatus.Deferred, "bootstrap deferred")),
            new RecordingIdleSubsystem(new SubsystemHandleResult(SubsystemHandleStatus.NoOp, "task completed")),
            new RecordingActiveShiftSubsystem(new SubsystemHandleResult(SubsystemHandleStatus.Rejected, "active rejected"))));

        var run = pipeline.Execute(context, ExecutionTarget.IdleSubsystem);
        var closure = ShiftClosureProcessor.Close(
            firstShift.ProjectState,
            activeShift,
            run,
            new DateTimeOffset(2026, 03, 28, 18, 40, 00, TimeSpan.Zero),
            isUserConfirmed: true);

        AssertEqual(ShiftClosureStatus.Completed, closure.Status, "Bootstrap-created shift must complete canonical closure path.");
        AssertEqual(TaskStateStatus.Completed, closure.Task.Status, "Bootstrap-created task must finalize through closure.");
        AssertTrue(closure.Snapshot is not null, "Bootstrap-created shift must produce canonical snapshot.");
        AssertEqual<string?>(null, closure.ProjectState.ActiveShiftId, "Closure must clear active shift after first full cycle.");
        AssertEqual<string?>(null, closure.ProjectState.ActiveTaskId, "Closure must clear active task after first full cycle.");
    }
    finally
    {
        DeleteScratchWorkspace(workspaceRoot);
    }
}

static void ValidatedIntentShiftStarterUsesFirstShiftSeamForEmptyHistory()
{
    var workspaceRoot = CreateScratchWorkspace();
    try
    {
        var initial = ProjectStateStorage.EnsureInitialized(workspaceRoot, "zavod-shift-starter-first", "ZAVOD Shift Starter First");
        var intent = TaskIntentFactory.CreateCandidate("Create first shift from validated intent").MarkReadyForValidation().Validate();

        var result = ValidatedIntentShiftStarter.Start(
            initial,
            intent,
            new DateTimeOffset(2026, 03, 28, 18, 41, 00, TimeSpan.Zero));

        AssertEqual("SHIFT-001", result.ShiftState.ShiftId, "Empty history must route validated intent into first shift seam.");
        AssertEqual("TASK-001", result.Task!.TaskId, "First shift seam must create initial task truth.");
        AssertEqual("SHIFT-001", result.ProjectState.ActiveShiftId, "First shift seam must activate the first shift.");
    }
    finally
    {
        DeleteScratchWorkspace(workspaceRoot);
    }
}

static void ValidatedIntentShiftStarterUsesNonFirstSeamForExistingHistory()
{
    var workspaceRoot = CreateScratchWorkspace();
    try
    {
        var initial = ProjectStateStorage.EnsureInitialized(workspaceRoot, "zavod-shift-starter-next", "ZAVOD Shift Starter Next");
        var firstShift = FirstShiftBootstrap.Create(
            initial,
            new FirstShiftBootstrapRequest(
                "Run the first shift",
                "Complete the first task",
                new DateTimeOffset(2026, 03, 28, 18, 42, 00, TimeSpan.Zero)));

        var run = new ExecutionRunResult(
            new ExecutionOutcome(ExecutionTarget.IdleSubsystem, ExecutionOutcomeStatus.NoOp, "First shift completed"),
            new ExecutionRecord(firstShift.ShiftState.ShiftId, firstShift.Task!.TaskId, ExecutionTarget.IdleSubsystem, ExecutionOutcomeStatus.NoOp, "First shift completed"));
        var closed = ShiftClosureProcessor.Close(
            firstShift.ProjectState,
            firstShift.ShiftState,
            run,
            new DateTimeOffset(2026, 03, 28, 18, 43, 00, TimeSpan.Zero),
            isUserConfirmed: true);

        var nextIntent = TaskIntentFactory.CreateCandidate("Create next shift from validated intent").MarkReadyForValidation().Validate();
        var nextShift = ValidatedIntentShiftStarter.Start(
            closed.ProjectState,
            nextIntent,
            new DateTimeOffset(2026, 03, 28, 18, 44, 00, TimeSpan.Zero));

        AssertEqual("SHIFT-002", nextShift.ShiftState.ShiftId, "Existing shift history must route into non-first shift seam.");
        AssertEqual("TASK-001", nextShift.Task!.TaskId, "Non-first seam must still create task from validated intent.");
        AssertEqual("SHIFT-002", nextShift.ProjectState.ActiveShiftId, "Non-first seam must activate the new shift.");
    }
    finally
    {
        DeleteScratchWorkspace(workspaceRoot);
    }
}

static void LeadMapsColdStartToColdStartMode()
{
    var result = ProjectLead.Evaluate(new BootstrapResult(
        IsColdStart: true,
        HasValidState: true,
        HasActiveShift: false));

    AssertEqual(LeadMode.ColdStart, result.Mode, "Cold start bootstrap state must map to ColdStart mode.");
    AssertContains(result.Reason, "cold start", "Cold start mode must explain the state.");
}

static void LeadMapsValidIdleStateToIdleMode()
{
    var result = ProjectLead.Evaluate(new BootstrapResult(
        IsColdStart: false,
        HasValidState: true,
        HasActiveShift: false));

    AssertEqual(LeadMode.Idle, result.Mode, "Valid state without active shift must map to Idle mode.");
    AssertContains(result.Reason, "активная смена отсутствует", "Idle mode must explain missing active shift.");
}

static void LeadMapsActiveShiftStateToActiveWorkMode()
{
    var result = ProjectLead.Evaluate(new BootstrapResult(
        IsColdStart: false,
        HasValidState: true,
        HasActiveShift: true));

    AssertEqual(LeadMode.ActiveWork, result.Mode, "State with active shift must map to ActiveWork mode.");
    AssertContains(result.Reason, "активная смена", "ActiveWork mode must mention active shift.");
}

static void PlannerMapsColdStartToEnterBootstrapFlow()
{
    var result = ProjectPlanner.Plan(new LeadResult(
        LeadMode.ColdStart,
        "Любое объяснение."));

    AssertEqual(NextAction.EnterBootstrapFlow, result.NextAction, "ColdStart mode must map to EnterBootstrapFlow.");
    AssertContains(result.Reason, "bootstrap flow", "Planning reason must explain bootstrap flow.");
}

static void PlannerMapsIdleToStayIdle()
{
    var result = ProjectPlanner.Plan(new LeadResult(
        LeadMode.Idle,
        "Любое объяснение."));

    AssertEqual(NextAction.StayIdle, result.NextAction, "Idle mode must map to StayIdle.");
    AssertContains(result.Reason, "idle", "Planning reason must explain idle scenario.");
}

static void PlannerMapsActiveWorkToResumeActiveShift()
{
    var result = ProjectPlanner.Plan(new LeadResult(
        LeadMode.ActiveWork,
        "Любое объяснение."));

    AssertEqual(NextAction.ResumeActiveShift, result.NextAction, "ActiveWork mode must map to ResumeActiveShift.");
    AssertContains(result.Reason, "активную смену", "Planning reason must explain active shift scenario.");
}

static void PlannerIgnoresLeadReasonForLogic()
{
    var first = ProjectPlanner.Plan(new LeadResult(
        LeadMode.Idle,
        "Один reason."));
    var second = ProjectPlanner.Plan(new LeadResult(
        LeadMode.Idle,
        "Совсем другой reason с шумом и ложными намёками на bootstrap."));

    AssertEqual(first.NextAction, second.NextAction, "Planner must use only lead mode for logic.");
}

static void RouterMapsEnterBootstrapFlowToBootstrapScenario()
{
    var result = ProjectRouter.Route(new PlanningResult(
        NextAction.EnterBootstrapFlow,
        "Любое объяснение."));

    AssertEqual(Scenario.BootstrapScenario, result.Scenario, "EnterBootstrapFlow must map to BootstrapScenario.");
    AssertContains(result.Reason, "bootstrap", "Routing reason must explain bootstrap scenario.");
}

static void RouterMapsStayIdleToIdleScenario()
{
    var result = ProjectRouter.Route(new PlanningResult(
        NextAction.StayIdle,
        "Любое объяснение."));

    AssertEqual(Scenario.IdleScenario, result.Scenario, "StayIdle must map to IdleScenario.");
    AssertContains(result.Reason, "idle", "Routing reason must explain idle scenario.");
}

static void RouterMapsResumeActiveShiftToActiveShiftScenario()
{
    var result = ProjectRouter.Route(new PlanningResult(
        NextAction.ResumeActiveShift,
        "Любое объяснение."));

    AssertEqual(Scenario.ActiveShiftScenario, result.Scenario, "ResumeActiveShift must map to ActiveShiftScenario.");
    AssertContains(result.Reason, "активной смены", "Routing reason must explain active shift scenario.");
}

static void RouterIgnoresPlanningReasonForLogic()
{
    var first = ProjectRouter.Route(new PlanningResult(
        NextAction.StayIdle,
        "Один reason."));
    var second = ProjectRouter.Route(new PlanningResult(
        NextAction.StayIdle,
        "Другой reason с шумом и ложными намёками на resume."));

    AssertEqual(first.Scenario, second.Scenario, "Router must use only planning action for logic.");
}

static void PresenterMapsBootstrapScenarioToPresentationPayload()
{
    var result = ScenarioPresenter.Present(new RouteResult(
        Scenario.BootstrapScenario,
        "Любое объяснение."));

    AssertEqual(Scenario.BootstrapScenario, result.Scenario, "BootstrapScenario must stay unchanged in presentation payload.");
    AssertEqual("Новый старт проекта", result.Title, "BootstrapScenario must produce correct title.");
    AssertContains(result.Description, "bootstrap-сценарию", "BootstrapScenario must produce correct description.");
    AssertEqual("Начать", result.PrimaryActionLabel, "BootstrapScenario must produce correct primary action.");
    AssertEqual(PrimaryAction.StartBootstrap, result.PrimaryAction, "BootstrapScenario must produce correct structured action.");
}

static void PresenterMapsIdleScenarioToPresentationPayload()
{
    var result = ScenarioPresenter.Present(new RouteResult(
        Scenario.IdleScenario,
        "Любое объяснение."));

    AssertEqual(Scenario.IdleScenario, result.Scenario, "IdleScenario must stay unchanged in presentation payload.");
    AssertEqual("Проект в ожидании", result.Title, "IdleScenario must produce correct title.");
    AssertContains(result.Description, "idle-состоянии", "IdleScenario must produce correct description.");
    AssertEqual("Остаться в ожидании", result.PrimaryActionLabel, "IdleScenario must produce correct primary action.");
    AssertEqual(PrimaryAction.StayIdle, result.PrimaryAction, "IdleScenario must produce correct structured action.");
}

static void PresenterMapsActiveShiftScenarioToPresentationPayload()
{
    var result = ScenarioPresenter.Present(new RouteResult(
        Scenario.ActiveShiftScenario,
        "Любое объяснение."));

    AssertEqual(Scenario.ActiveShiftScenario, result.Scenario, "ActiveShiftScenario must stay unchanged in presentation payload.");
    AssertEqual("Продолжение активной смены", result.Title, "ActiveShiftScenario must produce correct title.");
    AssertContains(result.Description, "активную смену", "ActiveShiftScenario must produce correct description.");
    AssertEqual("Продолжить", result.PrimaryActionLabel, "ActiveShiftScenario must produce correct primary action.");
    AssertEqual(PrimaryAction.ResumeActiveShift, result.PrimaryAction, "ActiveShiftScenario must produce correct structured action.");
}

static void PresenterIgnoresRouteReasonForLogic()
{
    var first = ScenarioPresenter.Present(new RouteResult(
        Scenario.IdleScenario,
        "Один reason."));
    var second = ScenarioPresenter.Present(new RouteResult(
        Scenario.IdleScenario,
        "Другой reason с шумом и ложными намёками на bootstrap."));

    AssertEqual(first.Scenario, second.Scenario, "Presenter must use only route scenario for logic.");
    AssertEqual(first.Title, second.Title, "Presenter title must depend only on scenario.");
    AssertEqual(first.Description, second.Description, "Presenter description must depend only on scenario.");
    AssertEqual(first.PrimaryActionLabel, second.PrimaryActionLabel, "Presenter action label must depend only on scenario.");
    AssertEqual(first.PrimaryAction, second.PrimaryAction, "Presenter action must depend only on scenario.");
}

static void BootstrapScenarioMapsToStartBootstrapAction()
{
    var result = ScenarioPresenter.Present(new RouteResult(
        Scenario.BootstrapScenario,
        "Любое объяснение."));

    AssertEqual(PrimaryAction.StartBootstrap, result.PrimaryAction, "BootstrapScenario must map to StartBootstrap.");
}

static void IdleScenarioMapsToStayIdleAction()
{
    var result = ScenarioPresenter.Present(new RouteResult(
        Scenario.IdleScenario,
        "Любое объяснение."));

    AssertEqual(PrimaryAction.StayIdle, result.PrimaryAction, "IdleScenario must map to StayIdle.");
}

static void ActiveShiftScenarioMapsToResumeActiveShiftAction()
{
    var result = ScenarioPresenter.Present(new RouteResult(
        Scenario.ActiveShiftScenario,
        "Любое объяснение."));

    AssertEqual(PrimaryAction.ResumeActiveShift, result.PrimaryAction, "ActiveShiftScenario must map to ResumeActiveShift.");
}

static void PrimaryActionLabelDoesNotDefineActionLogic()
{
    var original = ScenarioPresenter.Present(new RouteResult(
        Scenario.BootstrapScenario,
        "Любое объяснение."));
    var relabeled = original with { PrimaryActionLabel = "Совсем другой текст" };

    AssertEqual(PrimaryAction.StartBootstrap, original.PrimaryAction, "Structured action must match scenario.");
    AssertEqual(PrimaryAction.StartBootstrap, relabeled.PrimaryAction, "Changing label must not redefine structured action.");
}

static void StartBootstrapMapsToStartBootstrapFlowIntent()
{
    var presentation = ScenarioPresenter.Present(new RouteResult(
        Scenario.BootstrapScenario,
        "Любое объяснение."));

    var intent = ExecutionEntry.Enter(presentation);

    AssertEqual(ExecutionIntent.StartBootstrapFlow, intent, "StartBootstrap must map to StartBootstrapFlow.");
}

static void StayIdleMapsToStayIdleIntent()
{
    var presentation = ScenarioPresenter.Present(new RouteResult(
        Scenario.IdleScenario,
        "Любое объяснение."));

    var intent = ExecutionEntry.Enter(presentation);

    AssertEqual(ExecutionIntent.StayIdle, intent, "StayIdle must map to StayIdle.");
}

static void ResumeActiveShiftMapsToResumeActiveShiftIntent()
{
    var presentation = ScenarioPresenter.Present(new RouteResult(
        Scenario.ActiveShiftScenario,
        "Любое объяснение."));

    var intent = ExecutionEntry.Enter(presentation);

    AssertEqual(ExecutionIntent.ResumeActiveShift, intent, "ResumeActiveShift must map to ResumeActiveShift.");
}

static void PresentationTextDoesNotDefineExecutionEntryLogic()
{
    var original = ScenarioPresenter.Present(new RouteResult(
        Scenario.BootstrapScenario,
        "Один reason."));
    var rewritten = original with
    {
        Title = "Другой заголовок",
        Description = "Другое описание с намёками на idle.",
        PrimaryActionLabel = "Другой текст кнопки"
    };

    var first = ExecutionEntry.Enter(original);
    var second = ExecutionEntry.Enter(rewritten);

    AssertEqual(first, second, "Execution entry must use only structured primary action for logic.");
}

static void StartBootstrapFlowHandsOffToBootstrapSubsystem()
{
    var target = ExecutionHandoff.Handoff(ExecutionIntent.StartBootstrapFlow);

    AssertEqual(ExecutionTarget.BootstrapSubsystem, target, "StartBootstrapFlow must hand off to BootstrapSubsystem.");
}

static void StayIdleHandsOffToIdleSubsystem()
{
    var target = ExecutionHandoff.Handoff(ExecutionIntent.StayIdle);

    AssertEqual(ExecutionTarget.IdleSubsystem, target, "StayIdle must hand off to IdleSubsystem.");
}

static void ResumeActiveShiftHandsOffToActiveShiftSubsystem()
{
    var target = ExecutionHandoff.Handoff(ExecutionIntent.ResumeActiveShift);

    AssertEqual(ExecutionTarget.ActiveShiftSubsystem, target, "ResumeActiveShift must hand off to ActiveShiftSubsystem.");
}

static void UpstreamPresentationTextDoesNotDefineHandoffLogic()
{
    var originalPresentation = ScenarioPresenter.Present(new RouteResult(
        Scenario.BootstrapScenario,
        "Один reason."));
    var rewrittenPresentation = originalPresentation with
    {
        Title = "Другой заголовок",
        Description = "Другое описание с ложными намёками на idle.",
        PrimaryActionLabel = "Другой текст кнопки"
    };

    var firstIntent = ExecutionEntry.Enter(originalPresentation);
    var secondIntent = ExecutionEntry.Enter(rewrittenPresentation);
    var firstTarget = ExecutionHandoff.Handoff(firstIntent);
    var secondTarget = ExecutionHandoff.Handoff(secondIntent);

    AssertEqual(firstTarget, secondTarget, "Execution handoff must remain stable when only presentation text changes.");
}

static void DispatcherRoutesBootstrapTargetToBootstrapSubsystem()
{
    var bootstrapResult = new SubsystemHandleResult(SubsystemHandleStatus.Deferred, "bootstrap");
    var idleResult = new SubsystemHandleResult(SubsystemHandleStatus.NoOp, "idle");
    var activeShiftResult = new SubsystemHandleResult(SubsystemHandleStatus.Deferred, "active");
    var bootstrap = new RecordingBootstrapSubsystem(bootstrapResult);
    var idle = new RecordingIdleSubsystem(idleResult);
    var activeShift = new RecordingActiveShiftSubsystem(activeShiftResult);
    var dispatcher = new ExecutionDispatcher(bootstrap, idle, activeShift);

    var result = dispatcher.Dispatch(ExecutionTarget.BootstrapSubsystem);

    AssertEqual(1, bootstrap.CallCount, "Bootstrap target must call bootstrap subsystem exactly once.");
    AssertEqual(0, idle.CallCount, "Bootstrap target must not call idle subsystem.");
    AssertEqual(0, activeShift.CallCount, "Bootstrap target must not call active shift subsystem.");
    AssertEqual(bootstrapResult, result, "Dispatcher must return bootstrap subsystem result.");
}

static void DispatcherRoutesIdleTargetToIdleSubsystem()
{
    var bootstrapResult = new SubsystemHandleResult(SubsystemHandleStatus.Deferred, "bootstrap");
    var idleResult = new SubsystemHandleResult(SubsystemHandleStatus.NoOp, "idle");
    var activeShiftResult = new SubsystemHandleResult(SubsystemHandleStatus.Deferred, "active");
    var bootstrap = new RecordingBootstrapSubsystem(bootstrapResult);
    var idle = new RecordingIdleSubsystem(idleResult);
    var activeShift = new RecordingActiveShiftSubsystem(activeShiftResult);
    var dispatcher = new ExecutionDispatcher(bootstrap, idle, activeShift);

    var result = dispatcher.Dispatch(ExecutionTarget.IdleSubsystem);

    AssertEqual(0, bootstrap.CallCount, "Idle target must not call bootstrap subsystem.");
    AssertEqual(1, idle.CallCount, "Idle target must call idle subsystem exactly once.");
    AssertEqual(0, activeShift.CallCount, "Idle target must not call active shift subsystem.");
    AssertEqual(idleResult, result, "Dispatcher must return idle subsystem result.");
}

static void DispatcherRoutesActiveShiftTargetToActiveShiftSubsystem()
{
    var bootstrapResult = new SubsystemHandleResult(SubsystemHandleStatus.Deferred, "bootstrap");
    var idleResult = new SubsystemHandleResult(SubsystemHandleStatus.NoOp, "idle");
    var activeShiftResult = new SubsystemHandleResult(SubsystemHandleStatus.Deferred, "active");
    var bootstrap = new RecordingBootstrapSubsystem(bootstrapResult);
    var idle = new RecordingIdleSubsystem(idleResult);
    var activeShift = new RecordingActiveShiftSubsystem(activeShiftResult);
    var dispatcher = new ExecutionDispatcher(bootstrap, idle, activeShift);

    var result = dispatcher.Dispatch(ExecutionTarget.ActiveShiftSubsystem);

    AssertEqual(0, bootstrap.CallCount, "Active shift target must not call bootstrap subsystem.");
    AssertEqual(0, idle.CallCount, "Active shift target must not call idle subsystem.");
    AssertEqual(1, activeShift.CallCount, "Active shift target must call active shift subsystem exactly once.");
    AssertEqual(activeShiftResult, result, "Dispatcher must return active shift subsystem result.");
}

static void DispatcherOnlyRoutesToSelectedSubsystem()
{
    var bootstrap = new RecordingBootstrapSubsystem(new SubsystemHandleResult(SubsystemHandleStatus.Deferred, "bootstrap"));
    var idle = new RecordingIdleSubsystem(new SubsystemHandleResult(SubsystemHandleStatus.NoOp, "idle"));
    var activeShift = new RecordingActiveShiftSubsystem(new SubsystemHandleResult(SubsystemHandleStatus.Deferred, "active"));
    var dispatcher = new ExecutionDispatcher(bootstrap, idle, activeShift);

    var result = dispatcher.Dispatch(ExecutionTarget.IdleSubsystem);

    AssertEqual(1, bootstrap.CallCount + idle.CallCount + activeShift.CallCount, "Dispatcher must do only one routed subsystem call.");
    AssertEqual(SubsystemHandleStatus.NoOp, result.Status, "Dispatcher must surface the selected subsystem result without extra logic.");
}

static void BootstrapSubsystemReturnsDeterministicResultForColdStart()
{
    var bootstrap = new BootstrapSubsystem(new BootstrapResult(
        IsColdStart: true,
        HasValidState: true,
        HasActiveShift: false));

    var bootstrapResult = bootstrap.Handle();

    AssertEqual(SubsystemHandleStatus.Deferred, bootstrapResult.Status, "Bootstrap shell must return deterministic Deferred status.");
    AssertContains(bootstrapResult.Message ?? string.Empty, "Cold start detected", "Bootstrap cold start message must be informative.");
}

static void BootstrapSubsystemReturnsDeterministicResultForValidStateWithoutActiveShift()
{
    var bootstrap = new BootstrapSubsystem(new BootstrapResult(
        IsColdStart: false,
        HasValidState: true,
        HasActiveShift: false));

    var result = bootstrap.Handle();

    AssertEqual(SubsystemHandleStatus.Deferred, result.Status, "Bootstrap shell must remain Deferred for valid state without active shift.");
    AssertContains(result.Message ?? string.Empty, "without active shift", "Bootstrap valid-state message must be informative.");
}

static void BootstrapSubsystemReturnsDeterministicResultForActiveShiftState()
{
    var bootstrap = new BootstrapSubsystem(new BootstrapResult(
        IsColdStart: false,
        HasValidState: true,
        HasActiveShift: true));

    var result = bootstrap.Handle();

    AssertEqual(SubsystemHandleStatus.Deferred, result.Status, "Bootstrap shell must remain Deferred for active shift state.");
    AssertContains(result.Message ?? string.Empty, "Active shift already exists", "Bootstrap active-shift message must be informative.");
}

static void ActiveShiftSubsystemReturnsDeterministicResultWhenActiveShiftIsPresent()
{
    var activeShift = new ActiveShiftSubsystem(CreateProjectState(
        activeShiftId: "SHIFT-001",
        activeTaskId: null));

    var result = activeShift.Handle();

    AssertEqual(SubsystemHandleStatus.Deferred, result.Status, "Active shift shell must defer when active shift is present.");
    AssertContains(result.Message ?? string.Empty, "Active shift is present", "Active shift shell must return informative present-state message.");
}

static void ActiveShiftSubsystemReturnsDeterministicResultWhenActiveShiftIsMissing()
{
    var activeShift = new ActiveShiftSubsystem(CreateProjectState(
        activeShiftId: null,
        activeTaskId: null));

    var result = activeShift.Handle();

    AssertEqual(SubsystemHandleStatus.Rejected, result.Status, "Active shift shell must reject missing active shift.");
    AssertContains(result.Message ?? string.Empty, "No active shift is available", "Active shift shell must return informative missing-state message.");
}

static void ActiveShiftSubsystemRejectsInconsistentActiveTaskWithoutShift()
{
    var activeShift = new ActiveShiftSubsystem(CreateProjectState(
        activeShiftId: null,
        activeTaskId: "TASK-001"));

    var result = activeShift.Handle();

    AssertEqual(SubsystemHandleStatus.Rejected, result.Status, "Active shift shell must reject inconsistent task-without-shift state.");
    AssertContains(result.Message ?? string.Empty, "without active shift", "Active shift shell must describe inconsistent state.");
}

static void IdleSubsystemReturnsDeterministicResultWhenStateIsIdleAndConsistent()
{
    var idle = new IdleSubsystem(CreateProjectState(
        activeShiftId: null,
        activeTaskId: null));
    var idleResult = idle.Handle();

    AssertEqual(SubsystemHandleStatus.NoOp, idleResult.Status, "Idle subsystem must return NoOp for consistent idle state.");
    AssertContains(idleResult.Message ?? string.Empty, "idle and consistent", "Idle subsystem must describe consistent idle state.");
}

static void IdleSubsystemRejectsActiveShiftState()
{
    var idle = new IdleSubsystem(CreateProjectState(
        activeShiftId: "SHIFT-001",
        activeTaskId: null));

    var result = idle.Handle();

    AssertEqual(SubsystemHandleStatus.Rejected, result.Status, "Idle subsystem must reject state with active shift.");
    AssertContains(result.Message ?? string.Empty, "active shift exists", "Idle subsystem must describe active-shift conflict.");
}

static void IdleSubsystemRejectsInconsistentActiveTaskWithoutShift()
{
    var idle = new IdleSubsystem(CreateProjectState(
        activeShiftId: null,
        activeTaskId: "TASK-001"));

    var result = idle.Handle();

    AssertEqual(SubsystemHandleStatus.Rejected, result.Status, "Idle subsystem must reject inconsistent task-without-shift state.");
    AssertContains(result.Message ?? string.Empty, "active task exists without an active shift", "Idle subsystem must describe inconsistent idle state.");
}

static void OutcomeLayerMapsNoOpDeterministically()
{
    var outcome = ExecutionOutcomeBuilder.Build(
        ExecutionTarget.IdleSubsystem,
        new SubsystemHandleResult(SubsystemHandleStatus.NoOp, "idle"));

    AssertEqual(ExecutionTarget.IdleSubsystem, outcome.Target, "Outcome layer must preserve execution target.");
    AssertEqual(ExecutionOutcomeStatus.NoOp, outcome.Status, "NoOp subsystem result must map to NoOp execution outcome.");
}

static void OutcomeLayerMapsDeferredDeterministically()
{
    var outcome = ExecutionOutcomeBuilder.Build(
        ExecutionTarget.BootstrapSubsystem,
        new SubsystemHandleResult(SubsystemHandleStatus.Deferred, "bootstrap"));

    AssertEqual(ExecutionTarget.BootstrapSubsystem, outcome.Target, "Outcome layer must preserve execution target.");
    AssertEqual(ExecutionOutcomeStatus.Deferred, outcome.Status, "Deferred subsystem result must map to Deferred execution outcome.");
}

static void OutcomeLayerMapsRejectedDeterministically()
{
    var outcome = ExecutionOutcomeBuilder.Build(
        ExecutionTarget.ActiveShiftSubsystem,
        new SubsystemHandleResult(SubsystemHandleStatus.Rejected, "rejected"));

    AssertEqual(ExecutionTarget.ActiveShiftSubsystem, outcome.Target, "Outcome layer must preserve execution target.");
    AssertEqual(ExecutionOutcomeStatus.Rejected, outcome.Status, "Rejected subsystem result must map to Rejected execution outcome.");
}

static void OutcomeLayerPreservesInformativeMessage()
{
    var outcome = ExecutionOutcomeBuilder.Build(
        ExecutionTarget.BootstrapSubsystem,
        new SubsystemHandleResult(SubsystemHandleStatus.Deferred, "Informative deferred message."));

    AssertEqual("Informative deferred message.", outcome.Message, "Outcome layer must preserve subsystem informative message.");
}

static void ExecutionPipelineReturnsDeferredOutcomeForBootstrapTarget()
{
    var bootstrap = new RecordingBootstrapSubsystem(new SubsystemHandleResult(SubsystemHandleStatus.Deferred, "bootstrap deferred"));
    var idle = new RecordingIdleSubsystem(new SubsystemHandleResult(SubsystemHandleStatus.NoOp, "idle noop"));
    var activeShift = new RecordingActiveShiftSubsystem(new SubsystemHandleResult(SubsystemHandleStatus.Rejected, "active rejected"));
    var pipeline = new ExecutionPipeline(new ExecutionDispatcher(bootstrap, idle, activeShift));
    var context = CreateTaskExecutionContext();

    var run = pipeline.Execute(context, ExecutionTarget.BootstrapSubsystem);
    var outcome = run.Outcome;

    AssertEqual(ExecutionTarget.BootstrapSubsystem, outcome.Target, "Pipeline must preserve bootstrap target.");
    AssertEqual(ExecutionOutcomeStatus.Deferred, outcome.Status, "Bootstrap target must produce deferred outcome.");
    AssertEqual("bootstrap deferred", outcome.Message, "Pipeline must preserve bootstrap message.");
    AssertEqual("TASK-001", run.Record.TaskId, "Pipeline record must stay bound to task context.");
    AssertEqual("SHIFT-001", run.Record.ShiftId, "Pipeline record must stay bound to shift context.");
    AssertEqual(1, bootstrap.CallCount, "Pipeline must call bootstrap subsystem exactly once.");
    AssertEqual(0, idle.CallCount, "Pipeline must not call idle subsystem for bootstrap target.");
    AssertEqual(0, activeShift.CallCount, "Pipeline must not call active shift subsystem for bootstrap target.");
}

static void ExecutionPipelineReturnsNoOpOutcomeForIdleTarget()
{
    var bootstrap = new RecordingBootstrapSubsystem(new SubsystemHandleResult(SubsystemHandleStatus.Deferred, "bootstrap deferred"));
    var idle = new RecordingIdleSubsystem(new SubsystemHandleResult(SubsystemHandleStatus.NoOp, "idle noop"));
    var activeShift = new RecordingActiveShiftSubsystem(new SubsystemHandleResult(SubsystemHandleStatus.Rejected, "active rejected"));
    var pipeline = new ExecutionPipeline(new ExecutionDispatcher(bootstrap, idle, activeShift));
    var context = CreateTaskExecutionContext();

    var run = pipeline.Execute(context, ExecutionTarget.IdleSubsystem);
    var outcome = run.Outcome;

    AssertEqual(ExecutionTarget.IdleSubsystem, outcome.Target, "Pipeline must preserve idle target.");
    AssertEqual(ExecutionOutcomeStatus.NoOp, outcome.Status, "Idle target must produce no-op outcome.");
    AssertEqual("idle noop", outcome.Message, "Pipeline must preserve idle message.");
    AssertEqual(0, bootstrap.CallCount, "Pipeline must not call bootstrap subsystem for idle target.");
    AssertEqual(1, idle.CallCount, "Pipeline must call idle subsystem exactly once.");
    AssertEqual(0, activeShift.CallCount, "Pipeline must not call active shift subsystem for idle target.");
}

static void ExecutionPipelineReturnsRejectedOutcomeForActiveShiftTarget()
{
    var bootstrap = new RecordingBootstrapSubsystem(new SubsystemHandleResult(SubsystemHandleStatus.Deferred, "bootstrap deferred"));
    var idle = new RecordingIdleSubsystem(new SubsystemHandleResult(SubsystemHandleStatus.NoOp, "idle noop"));
    var activeShift = new RecordingActiveShiftSubsystem(new SubsystemHandleResult(SubsystemHandleStatus.Rejected, "active rejected"));
    var pipeline = new ExecutionPipeline(new ExecutionDispatcher(bootstrap, idle, activeShift));
    var context = CreateTaskExecutionContext();

    var run = pipeline.Execute(context, ExecutionTarget.ActiveShiftSubsystem);
    var outcome = run.Outcome;

    AssertEqual(ExecutionTarget.ActiveShiftSubsystem, outcome.Target, "Pipeline must preserve active shift target.");
    AssertEqual(ExecutionOutcomeStatus.Rejected, outcome.Status, "Active shift target must produce rejected outcome.");
    AssertEqual("active rejected", outcome.Message, "Pipeline must preserve active shift message.");
    AssertEqual(0, bootstrap.CallCount, "Pipeline must not call bootstrap subsystem for active shift target.");
    AssertEqual(0, idle.CallCount, "Pipeline must not call idle subsystem for active shift target.");
    AssertEqual(1, activeShift.CallCount, "Pipeline must call active shift subsystem exactly once.");
}

static void ExecutionPipelinePreservesMessageAndSingleRoutedCall()
{
    var bootstrap = new RecordingBootstrapSubsystem(new SubsystemHandleResult(SubsystemHandleStatus.Deferred, "bootstrap message"));
    var idle = new RecordingIdleSubsystem(new SubsystemHandleResult(SubsystemHandleStatus.NoOp, "idle message"));
    var activeShift = new RecordingActiveShiftSubsystem(new SubsystemHandleResult(SubsystemHandleStatus.Rejected, "active message"));
    var pipeline = new ExecutionPipeline(new ExecutionDispatcher(bootstrap, idle, activeShift));
    var context = CreateTaskExecutionContext();

    var run = pipeline.Execute(context, ExecutionTarget.IdleSubsystem);
    var outcome = run.Outcome;

    AssertEqual("idle message", outcome.Message, "Pipeline must preserve selected subsystem message.");
    AssertEqual(1, bootstrap.CallCount + idle.CallCount + activeShift.CallCount, "Pipeline must trigger only one routed subsystem call.");
}

static void ExecutionPipelineReturnsDeterministicOutcomeAndRecord()
{
    var bootstrap = new RecordingBootstrapSubsystem(new SubsystemHandleResult(SubsystemHandleStatus.Deferred, "bootstrap deferred"));
    var idle = new RecordingIdleSubsystem(new SubsystemHandleResult(SubsystemHandleStatus.NoOp, "idle noop"));
    var activeShift = new RecordingActiveShiftSubsystem(new SubsystemHandleResult(SubsystemHandleStatus.Rejected, "active rejected"));
    var pipeline = new ExecutionPipeline(new ExecutionDispatcher(bootstrap, idle, activeShift));
    var context = CreateTaskExecutionContext();

    var run = pipeline.Execute(context, ExecutionTarget.IdleSubsystem);

    AssertEqual(ExecutionTarget.IdleSubsystem, run.Outcome.Target, "Run outcome must preserve target.");
    AssertEqual(ExecutionTarget.IdleSubsystem, run.Record.Target, "Execution record must preserve target.");
    AssertEqual(ExecutionOutcomeStatus.NoOp, run.Outcome.Status, "Run outcome must preserve status.");
    AssertEqual(ExecutionOutcomeStatus.NoOp, run.Record.OutcomeStatus, "Execution record must preserve status.");
    AssertEqual("scoped-local-default", run.EffectiveRuntimeProfile.ProfileId, "Execution run result must expose default runtime profile.");
    AssertEqual(RuntimeFamily.ScopedLocalWorkspace, run.Record.EffectiveRuntimeProfile.Family, "Execution record must expose default runtime family.");
}

static void ExecutionRecordTruthfullyMirrorsOutcome()
{
    var bootstrap = new RecordingBootstrapSubsystem(new SubsystemHandleResult(SubsystemHandleStatus.Deferred, "bootstrap deferred"));
    var idle = new RecordingIdleSubsystem(new SubsystemHandleResult(SubsystemHandleStatus.NoOp, "idle noop"));
    var activeShift = new RecordingActiveShiftSubsystem(new SubsystemHandleResult(SubsystemHandleStatus.Rejected, "active rejected"));
    var pipeline = new ExecutionPipeline(new ExecutionDispatcher(bootstrap, idle, activeShift));
    var context = CreateTaskExecutionContext();

    var run = pipeline.Execute(context, ExecutionTarget.ActiveShiftSubsystem);

    AssertEqual(run.Outcome.Target, run.Record.Target, "Execution record target must mirror outcome target.");
    AssertEqual(run.Outcome.Status, run.Record.OutcomeStatus, "Execution record status must mirror outcome status.");
    AssertEqual(run.Outcome.Message, run.Record.Message, "Execution record message must mirror outcome message.");
    AssertEqual(run.EffectiveRuntimeProfile.ProfileId, run.Record.EffectiveRuntimeProfile.ProfileId, "Execution record runtime profile must mirror run result.");
}

static void ExecutionRuntimePreservesExplicitRuntimeProfile()
{
    var task = CreateTaskState(ContextIntentState.Validated, TaskStateStatus.Active, PromptRole.Worker);
    var shift = CreateShiftState(task) with { CurrentTaskId = task.TaskId };
    var runtimeProfile = new RuntimeProfile(
        "container-heavy",
        RuntimeFamily.Container,
        RuntimeIsolationLevel.Container,
        UsesSandbox: true,
        TrustedOnly: false,
        "Container runtime for heavy isolation.");

    var runtime = ExecutionRuntimeController.Begin(task, shift, runtimeProfile);
    runtime = ExecutionRuntimeController.ProduceResult(runtime);

    AssertEqual("container-heavy", runtime.RuntimeProfile.ProfileId, "Runtime state must preserve explicit runtime profile.");
    AssertEqual(RuntimeFamily.Container, runtime.RunResult.EffectiveRuntimeProfile.Family, "Runtime run result must preserve explicit runtime family.");
    AssertEqual(RuntimeIsolationLevel.Container, runtime.RunResult.EffectiveRuntimeProfile.Isolation, "Runtime run result must preserve explicit runtime isolation.");

    var interrupted = ExecutionRuntimeController.BuildInterruptedRunResult(runtime, "manual stop");
    AssertEqual("container-heavy", interrupted.EffectiveRuntimeProfile.ProfileId, "Interrupted run result must preserve explicit runtime profile.");
}

static void RuntimeProfileCatalogExposesDefaultFamiliesDeterministically()
{
    var allProfiles = RuntimeProfileCatalog.ListAll();

    AssertTrue(allProfiles.Count >= 5, "Runtime profile catalog should expose the baseline execution families.");
    AssertEqual("scoped-local-default", RuntimeProfileCatalog.Default.ProfileId, "Default runtime profile must stay scoped local.");
    AssertEqual("local-unsafe", RuntimeProfileCatalog.GetDefaultFor(RuntimeFamily.LocalUnsafe).ProfileId, "Local unsafe family should map predictably.");
    AssertEqual("container-heavy", RuntimeProfileCatalog.GetDefaultFor(RuntimeFamily.Container).ProfileId, "Container family should map predictably.");
    AssertEqual("vm-sandbox-hard", RuntimeProfileCatalog.GetDefaultFor(RuntimeFamily.VmOrSandbox).ProfileId, "VM family should map predictably.");
    AssertEqual("remote-ephemeral", RuntimeProfileCatalog.GetDefaultFor(RuntimeFamily.Remote).ProfileId, "Remote family should map predictably.");
}

static void RuntimeProfileResolverPrefersExplicitProfileOverFallbackSelectors()
{
    var explicitProfile = new RuntimeProfile(
        "custom-explicit",
        RuntimeFamily.Container,
        RuntimeIsolationLevel.Container,
        UsesSandbox: true,
        TrustedOnly: false,
        "Custom explicit runtime.");

    var resolvedExplicit = RuntimeProfileResolver.Resolve(
        explicitProfile,
        profileId: "local-unsafe",
        family: RuntimeFamily.Remote);
    var resolvedById = RuntimeProfileResolver.ResolveByProfileId("container-heavy");
    var resolvedByFamily = RuntimeProfileResolver.ResolveByFamily(RuntimeFamily.VmOrSandbox);
    var resolvedDefault = RuntimeProfileResolver.Resolve(null);

    AssertEqual("custom-explicit", resolvedExplicit.ProfileId, "Explicit runtime profile must win over fallback selectors.");
    AssertEqual("container-heavy", resolvedById.ProfileId, "Resolver must map known profile ids deterministically.");
    AssertEqual("vm-sandbox-hard", resolvedByFamily.ProfileId, "Resolver must map runtime families deterministically.");
    AssertEqual("scoped-local-default", resolvedDefault.ProfileId, "Resolver must fall back to scoped local default.");
}

static void RuntimeSelectionPolicyDefaultsToScopedLocal()
{
    var decision = RuntimeSelectionPolicy.Select(new RuntimeSelectionRequest());

    AssertTrue(decision.IsAllowed, "Default runtime policy should allow a default runtime.");
    AssertEqual("scoped-local-default", decision.Profile.ProfileId, "Default runtime policy must choose scoped local.");
}

static void RuntimeSelectionPolicyBlocksLocalUnsafeOutsideTrustedDev()
{
    var denied = RuntimeSelectionPolicy.Select(new RuntimeSelectionRequest(RequestedFamily: RuntimeFamily.LocalUnsafe));
    var allowed = RuntimeSelectionPolicy.Select(new RuntimeSelectionRequest(
        RequestedFamily: RuntimeFamily.LocalUnsafe,
        IsTrustedDevelopmentScenario: true));

    AssertFalse(denied.IsAllowed, "Local unsafe must be blocked outside trusted development.");
    AssertEqual("local-unsafe", denied.Profile.ProfileId, "Policy should classify the actual denied runtime.");
    AssertTrue(allowed.IsAllowed, "Local unsafe may be allowed for trusted development scenarios.");
}

static void RuntimeSelectionPolicyAllowsContainerForHeavierIsolation()
{
    var decision = RuntimeSelectionPolicy.Select(new RuntimeSelectionRequest(RequiresHeavierIsolation: true));

    AssertTrue(decision.IsAllowed, "Heavier isolation should select an allowed runtime in v1.");
    AssertEqual("container-heavy", decision.Profile.ProfileId, "Heavier isolation should route to container runtime.");
}

static void RuntimeSelectionPolicyKeepsRemoteAndVmOptInOnly()
{
    var remoteDenied = RuntimeSelectionPolicy.Select(new RuntimeSelectionRequest(RequestedFamily: RuntimeFamily.Remote));
    var vmDenied = RuntimeSelectionPolicy.Select(new RuntimeSelectionRequest(RequestedFamily: RuntimeFamily.VmOrSandbox));
    var remoteDefaultDenied = RuntimeSelectionPolicy.Select(new RuntimeSelectionRequest(RequiresDetachedExecution: true));
    var vmDefaultDenied = RuntimeSelectionPolicy.Select(new RuntimeSelectionRequest(RequiresHardIsolation: true));

    AssertFalse(remoteDenied.IsAllowed, "Remote runtime must not be allowed by default.");
    AssertFalse(vmDenied.IsAllowed, "VM runtime must not be allowed by default.");
    AssertFalse(remoteDefaultDenied.IsAllowed, "Detached execution should stay denied until remote family is enabled.");
    AssertFalse(vmDefaultDenied.IsAllowed, "Hard isolation should stay denied until VM or sandbox family is enabled.");
}

static void IsolationBackendContractsStayRuntimeScopedHonestly()
{
    var request = new IsolationBackendRequest(
        "container-cli",
        RuntimeIsolationLevel.Container,
        "C:\\workspace",
        "dotnet",
        new[] { "build" },
        new Dictionary<string, string> { ["DOTNET_CLI_HOME"] = "C:\\temp" });

    AssertEqual("container-cli", request.BackendId, "Isolation backend request should preserve backend identity.");
    AssertEqual(RuntimeIsolationLevel.Container, request.IsolationLevel, "Isolation backend request should remain in runtime isolation layer.");
    AssertEqual("dotnet", request.Command, "Isolation backend request should preserve CLI command boundary.");
}

static void RuntimeSelectionRequestBuilderCreatesDefaultSafeRequest()
{
    var request = RuntimeSelectionRequestBuilder.BuildDefault();

    AssertFalse(request.IsTrustedDevelopmentScenario, "Default runtime request must not assume trusted development.");
    AssertFalse(request.RequiresHeavierIsolation, "Default runtime request must not over-request heavier isolation.");
    AssertFalse(request.RequiresHardIsolation, "Default runtime request must not over-request hard isolation.");
    AssertFalse(request.RequiresDetachedExecution, "Default runtime request must not request detached execution.");
    AssertTrue(request.RequestedFamily is null, "Default runtime request should let policy choose the family.");
}

static void RuntimeSelectionRequestBuilderPreservesLaunchContextFlags()
{
    var profile = RuntimeProfileCatalog.GetRequired("container-heavy");
    var request = RuntimeSelectionRequestBuilder.Build(new RuntimeLaunchContext(
        IsTrustedDevelopmentScenario: true,
        RequiresHeavierIsolation: true,
        RequiresHardIsolation: false,
        RequiresDetachedExecution: false,
        RequestedFamily: RuntimeFamily.Container,
        RequestedProfileId: "container-heavy",
        ExplicitProfile: profile));

    AssertTrue(request.IsTrustedDevelopmentScenario, "Builder must preserve trusted-development flag.");
    AssertTrue(request.RequiresHeavierIsolation, "Builder must preserve heavier-isolation flag.");
    AssertEqual(RuntimeFamily.Container, request.RequestedFamily, "Builder must preserve requested family.");
    AssertEqual("container-heavy", request.RequestedProfileId, "Builder must preserve requested profile id.");
    AssertEqual("container-heavy", request.ExplicitProfile!.ProfileId, "Builder must preserve explicit runtime profile.");
}

static void ExecutionRuntimeBeginUsesRuntimeSelectionPolicy()
{
    var task = CreateTaskState(ContextIntentState.Validated, TaskStateStatus.Active, PromptRole.Worker);
    var shift = CreateShiftState(task) with { CurrentTaskId = task.TaskId };

    var defaultRuntime = ExecutionRuntimeController.Begin(task, shift, new RuntimeSelectionRequest());
    AssertEqual("scoped-local-default", defaultRuntime.RuntimeProfile.ProfileId, "Policy-aware begin must default to scoped local runtime.");

    AssertThrows<InvalidOperationException>(
        () => ExecutionRuntimeController.Begin(
            task,
            shift,
            new RuntimeSelectionRequest(RequestedFamily: RuntimeFamily.LocalUnsafe)),
        "Policy-aware begin must reject local unsafe outside trusted development.");
}

static void ClosureCandidateMirrorsExecutionRunResultDeterministically()
{
    var bootstrap = new RecordingBootstrapSubsystem(new SubsystemHandleResult(SubsystemHandleStatus.NoOp, "idle-like completion"));
    var idle = new RecordingIdleSubsystem(new SubsystemHandleResult(SubsystemHandleStatus.NoOp, "idle noop"));
    var activeShift = new RecordingActiveShiftSubsystem(new SubsystemHandleResult(SubsystemHandleStatus.Rejected, "active rejected"));
    var pipeline = new ExecutionPipeline(new ExecutionDispatcher(bootstrap, idle, activeShift));
    var context = CreateTaskExecutionContext();

    var run = pipeline.Execute(context, ExecutionTarget.IdleSubsystem);
    var candidate = ExecutionClosureBuilder.Build(run);

    AssertEqual(run.Record.Target, candidate.Target, "Closure candidate target must mirror execution record target.");
    AssertEqual(run.Record.OutcomeStatus, candidate.OutcomeStatus, "Closure candidate status must mirror execution record status.");
    AssertEqual(run.Record.Message, candidate.Message, "Closure candidate message must mirror execution record message.");
}

static void ClosureCandidateDerivesDeferredFollowUpStateDeterministically()
{
    var bootstrap = new RecordingBootstrapSubsystem(new SubsystemHandleResult(SubsystemHandleStatus.Deferred, "bootstrap deferred"));
    var idle = new RecordingIdleSubsystem(new SubsystemHandleResult(SubsystemHandleStatus.NoOp, "idle noop"));
    var activeShift = new RecordingActiveShiftSubsystem(new SubsystemHandleResult(SubsystemHandleStatus.Rejected, "active rejected"));
    var pipeline = new ExecutionPipeline(new ExecutionDispatcher(bootstrap, idle, activeShift));
    var context = CreateTaskExecutionContext();

    var run = pipeline.Execute(context, ExecutionTarget.BootstrapSubsystem);
    var candidate = ExecutionClosureBuilder.Build(run);

    AssertFalse(candidate.IsClosable, "Deferred execution must not be marked closable.");
    AssertTrue(candidate.RequiresFollowup, "Deferred execution must require follow-up.");
    AssertFalse(candidate.IsRejected, "Deferred execution must not be marked rejected.");
}

static void ClosureCandidateDerivesRejectedStateDeterministically()
{
    var bootstrap = new RecordingBootstrapSubsystem(new SubsystemHandleResult(SubsystemHandleStatus.Deferred, "bootstrap deferred"));
    var idle = new RecordingIdleSubsystem(new SubsystemHandleResult(SubsystemHandleStatus.NoOp, "idle noop"));
    var activeShift = new RecordingActiveShiftSubsystem(new SubsystemHandleResult(SubsystemHandleStatus.Rejected, "active rejected"));
    var pipeline = new ExecutionPipeline(new ExecutionDispatcher(bootstrap, idle, activeShift));
    var context = CreateTaskExecutionContext();

    var run = pipeline.Execute(context, ExecutionTarget.ActiveShiftSubsystem);
    var candidate = ExecutionClosureBuilder.Build(run);

    AssertFalse(candidate.IsClosable, "Rejected execution must remain non-closable in canonical closure flow.");
    AssertTrue(candidate.RequiresFollowup, "Rejected execution must stay on keep-open follow-up path.");
    AssertTrue(candidate.IsRejected, "Rejected execution must be marked rejected.");
}

static void ShiftClosureProposalMirrorsClosureCandidateDeterministically()
{
    var candidate = new ExecutionClosureCandidate(
        ExecutionTarget.IdleSubsystem,
        ExecutionOutcomeStatus.NoOp,
        "Idle noop",
        IsClosable: true,
        RequiresFollowup: false,
        IsRejected: false);

    var proposal = ShiftClosureProposalBuilder.Build(candidate);

    AssertEqual(candidate.Target, proposal.Target, "Shift closure proposal target must mirror closure candidate target.");
    AssertEqual(candidate.OutcomeStatus, proposal.OutcomeStatus, "Shift closure proposal status must mirror closure candidate status.");
    AssertEqual(candidate.Message, proposal.Message, "Shift closure proposal message must mirror closure candidate message.");
}

static void ShiftClosureProposalKeepsDeferredExecutionOpen()
{
    var candidate = new ExecutionClosureCandidate(
        ExecutionTarget.BootstrapSubsystem,
        ExecutionOutcomeStatus.Deferred,
        "Bootstrap deferred",
        IsClosable: false,
        RequiresFollowup: true,
        IsRejected: false);

    var proposal = ShiftClosureProposalBuilder.Build(candidate);

    AssertEqual(ProposedShiftEffect.KeepOpen, proposal.ProposedShiftEffect, "Deferred execution must propose keeping shift open.");
    AssertTrue(proposal.RequiresFollowup, "Deferred proposal must preserve follow-up requirement.");
    AssertFalse(proposal.IsClosable, "Deferred proposal must remain non-closable.");
}

static void ShiftClosureProposalMarksNoOpExecutionEligibleToClose()
{
    var candidate = new ExecutionClosureCandidate(
        ExecutionTarget.IdleSubsystem,
        ExecutionOutcomeStatus.NoOp,
        "Idle noop",
        IsClosable: true,
        RequiresFollowup: false,
        IsRejected: false);

    var proposal = ShiftClosureProposalBuilder.Build(candidate);

    AssertEqual(ProposedShiftEffect.EligibleToClose, proposal.ProposedShiftEffect, "NoOp closable execution must be eligible to close.");
    AssertTrue(proposal.IsClosable, "NoOp proposal must preserve closable state.");
}

static void ShiftClosureProposalKeepsRejectedExecutionOpen()
{
    var candidate = new ExecutionClosureCandidate(
        ExecutionTarget.ActiveShiftSubsystem,
        ExecutionOutcomeStatus.Rejected,
        "Rejected resume",
        IsClosable: true,
        RequiresFollowup: false,
        IsRejected: true);

    var proposal = ShiftClosureProposalBuilder.Build(candidate);

    AssertEqual(ProposedShiftEffect.KeepOpen, proposal.ProposedShiftEffect, "Rejected execution must not auto-close shift.");
    AssertTrue(proposal.IsClosable, "Rejected proposal must preserve closable fact from closure candidate.");
}

static void ShiftUpdateCandidateMirrorsProposalDeterministically()
{
    var proposal = new ShiftClosureProposal(
        ExecutionTarget.IdleSubsystem,
        ExecutionOutcomeStatus.NoOp,
        "Idle noop",
        ProposedShiftEffect.EligibleToClose,
        RequiresFollowup: false,
        IsClosable: true);

    var candidate = ShiftUpdateCandidateBuilder.Build(proposal);

    AssertEqual(proposal.Target, candidate.Target, "Shift update candidate target must mirror proposal target.");
    AssertEqual(proposal.OutcomeStatus, candidate.OutcomeStatus, "Shift update candidate status must mirror proposal status.");
    AssertEqual(proposal.Message, candidate.Message, "Shift update candidate message must mirror proposal message.");
    AssertEqual(proposal.ProposedShiftEffect, candidate.ProposedShiftEffect, "Shift update candidate effect must mirror proposal effect.");
}

static void ShiftUpdateCandidateMarksKeepOpenDeterministically()
{
    var proposal = new ShiftClosureProposal(
        ExecutionTarget.BootstrapSubsystem,
        ExecutionOutcomeStatus.Deferred,
        "Bootstrap deferred",
        ProposedShiftEffect.KeepOpen,
        RequiresFollowup: true,
        IsClosable: false);

    var candidate = ShiftUpdateCandidateBuilder.Build(proposal);

    AssertTrue(candidate.ShouldKeepShiftOpen, "KeepOpen proposal must mark shift update candidate as keep-open.");
    AssertFalse(candidate.IsEligibleToClose, "KeepOpen proposal must not be eligible to close.");
}

static void ShiftUpdateCandidateMarksEligibleToCloseDeterministically()
{
    var proposal = new ShiftClosureProposal(
        ExecutionTarget.IdleSubsystem,
        ExecutionOutcomeStatus.NoOp,
        "Idle noop",
        ProposedShiftEffect.EligibleToClose,
        RequiresFollowup: false,
        IsClosable: true);

    var candidate = ShiftUpdateCandidateBuilder.Build(proposal);

    AssertFalse(candidate.ShouldKeepShiftOpen, "EligibleToClose proposal must not mark keep-open.");
    AssertTrue(candidate.IsEligibleToClose, "EligibleToClose proposal must mark candidate eligible to close.");
}

static void ShiftUpdateCandidatePreservesRejectedOutcomeFact()
{
    var proposal = new ShiftClosureProposal(
        ExecutionTarget.ActiveShiftSubsystem,
        ExecutionOutcomeStatus.Rejected,
        "Rejected resume",
        ProposedShiftEffect.KeepOpen,
        RequiresFollowup: false,
        IsClosable: true);

    var candidate = ShiftUpdateCandidateBuilder.Build(proposal);

    AssertTrue(candidate.HasRejectedOutcome, "Rejected proposal must preserve rejected outcome fact.");
    AssertTrue(candidate.ShouldKeepShiftOpen, "Rejected keep-open proposal must keep shift open.");
}

static void ShiftUpdateApplierMapsEligibleToCloseCandidateToWouldClose()
{
    var candidate = new ShiftUpdateCandidate(
        ExecutionTarget.IdleSubsystem,
        ExecutionOutcomeStatus.NoOp,
        "Idle noop",
        ProposedShiftEffect.EligibleToClose,
        ShouldKeepShiftOpen: false,
        IsEligibleToClose: true,
        HasRejectedOutcome: false);

    var result = ShiftUpdateApplier.Apply(candidate);

    AssertEqual(ShiftUpdateStatus.WouldClose, result.Status, "Eligible-to-close candidate must map to WouldClose.");
    AssertEqual(candidate.Target, result.Target, "Shift update result must preserve target.");
    AssertEqual(candidate.OutcomeStatus, result.OutcomeStatus, "Shift update result must preserve outcome status.");
    AssertEqual(candidate.Message, result.Message, "Shift update result must preserve message.");
}

static void ShiftUpdateApplierMapsKeepOpenCandidateToWouldKeepOpen()
{
    var candidate = new ShiftUpdateCandidate(
        ExecutionTarget.BootstrapSubsystem,
        ExecutionOutcomeStatus.Deferred,
        "Bootstrap deferred",
        ProposedShiftEffect.KeepOpen,
        ShouldKeepShiftOpen: true,
        IsEligibleToClose: false,
        HasRejectedOutcome: false);

    var result = ShiftUpdateApplier.Apply(candidate);

    AssertEqual(ShiftUpdateStatus.WouldKeepOpen, result.Status, "Keep-open candidate must map to WouldKeepOpen.");
}

static void ShiftUpdateApplierMapsRejectedCandidateToRejected()
{
    var candidate = new ShiftUpdateCandidate(
        ExecutionTarget.ActiveShiftSubsystem,
        ExecutionOutcomeStatus.Rejected,
        "Rejected resume",
        ProposedShiftEffect.KeepOpen,
        ShouldKeepShiftOpen: true,
        IsEligibleToClose: false,
        HasRejectedOutcome: true);

    var result = ShiftUpdateApplier.Apply(candidate);

    AssertEqual(ShiftUpdateStatus.Rejected, result.Status, "Rejected candidate must map to Rejected status.");
}

static void ShiftUpdateApplierKeepsNeutralCandidateAsNoChange()
{
    var candidate = new ShiftUpdateCandidate(
        ExecutionTarget.IdleSubsystem,
        ExecutionOutcomeStatus.NoOp,
        "Neutral",
        ProposedShiftEffect.None,
        ShouldKeepShiftOpen: false,
        IsEligibleToClose: false,
        HasRejectedOutcome: false);

    var result = ShiftUpdateApplier.Apply(candidate);

    AssertEqual(ShiftUpdateStatus.NoChange, result.Status, "Neutral candidate must remain NoChange.");
}

static void ShiftUpdateDecisionAllowsApplyForWouldClose()
{
    var result = new ShiftUpdateResult(
        ExecutionTarget.IdleSubsystem,
        ExecutionOutcomeStatus.NoOp,
        "Would close",
        ShiftUpdateStatus.WouldClose);

    var decision = ShiftUpdateDecisionMaker.Decide(result);

    AssertEqual(ShiftUpdateDecisionStatus.AllowApply, decision.ApplyStatus, "WouldClose must allow apply.");
    AssertEqual(result.Target, decision.Target, "Decision must preserve target.");
    AssertEqual(result.OutcomeStatus, decision.OutcomeStatus, "Decision must preserve outcome status.");
}

static void ShiftUpdateDecisionAllowsApplyForWouldKeepOpen()
{
    var result = new ShiftUpdateResult(
        ExecutionTarget.BootstrapSubsystem,
        ExecutionOutcomeStatus.Deferred,
        "Would keep open",
        ShiftUpdateStatus.WouldKeepOpen);

    var decision = ShiftUpdateDecisionMaker.Decide(result);

    AssertEqual(ShiftUpdateDecisionStatus.AllowApply, decision.ApplyStatus, "WouldKeepOpen must allow apply.");
    AssertContains(decision.Reason ?? string.Empty, "remain open", "Allow-apply keep-open decision must be informative.");
}

static void ShiftUpdateDecisionDeniesApplyForRejected()
{
    var result = new ShiftUpdateResult(
        ExecutionTarget.ActiveShiftSubsystem,
        ExecutionOutcomeStatus.Rejected,
        "Rejected",
        ShiftUpdateStatus.Rejected);

    var decision = ShiftUpdateDecisionMaker.Decide(result);

    AssertEqual(ShiftUpdateDecisionStatus.DenyApply, decision.ApplyStatus, "Rejected must deny apply.");
    AssertContains(decision.Reason ?? string.Empty, "must not proceed", "Rejected deny decision must be informative.");
}

static void ShiftUpdateDecisionDeniesApplyForNoChange()
{
    var result = new ShiftUpdateResult(
        ExecutionTarget.IdleSubsystem,
        ExecutionOutcomeStatus.NoOp,
        "No change",
        ShiftUpdateStatus.NoChange);

    var decision = ShiftUpdateDecisionMaker.Decide(result);

    AssertEqual(ShiftUpdateDecisionStatus.DenyApply, decision.ApplyStatus, "NoChange must deny apply.");
    AssertContains(decision.Reason ?? string.Empty, "does not justify mutation", "NoChange deny decision must be informative.");
}

static void ProjectStateMutatorBlocksDirectExecutionCloseMutation()
{
    var state = CreateProjectState(
        activeShiftId: "SHIFT-001",
        activeTaskId: "TASK-001");
    var decision = new ShiftUpdateDecision(
        ExecutionTarget.IdleSubsystem,
        ExecutionOutcomeStatus.NoOp,
        ShiftUpdateDecisionStatus.AllowApply,
        "Would close");

    var result = ProjectStateMutator.Mutate(state, decision);

    AssertEqual(ProjectStateMutationStatus.Unchanged, result.Status, "Execution close path must not mutate canonical project state directly.");
    AssertEqual("SHIFT-001", result.State.ActiveShiftId, "Direct execution path must preserve active shift.");
    AssertEqual("TASK-001", result.State.ActiveTaskId, "Direct execution path must preserve active task.");
    AssertEqual("SHIFT-001", state.ActiveShiftId, "Original state must remain unchanged.");
    AssertEqual("TASK-001", state.ActiveTaskId, "Original state task must remain unchanged.");
}

static void ProjectStateMutatorKeepsDirectExecutionKeepOpenPathUnchanged()
{
    var state = CreateProjectState(
        activeShiftId: "SHIFT-001",
        activeTaskId: "TASK-001");
    var decision = new ShiftUpdateDecision(
        ExecutionTarget.BootstrapSubsystem,
        ExecutionOutcomeStatus.Deferred,
        ShiftUpdateDecisionStatus.AllowApply,
        "Would keep open");

    var result = ProjectStateMutator.Mutate(state, decision);

    AssertEqual(ProjectStateMutationStatus.Unchanged, result.Status, "Execution keep-open path must not mutate canonical project state.");
    AssertEqual("SHIFT-001", result.State.ActiveShiftId, "Keep-open must preserve active shift.");
    AssertEqual("TASK-001", result.State.ActiveTaskId, "Keep-open must preserve active task.");
}

static void ProjectStateMutatorLeavesStateUnchangedForDenyApply()
{
    var state = CreateProjectState(
        activeShiftId: "SHIFT-001",
        activeTaskId: "TASK-001");
    var decision = new ShiftUpdateDecision(
        ExecutionTarget.ActiveShiftSubsystem,
        ExecutionOutcomeStatus.Rejected,
        ShiftUpdateDecisionStatus.DenyApply,
        "Denied");

    var result = ProjectStateMutator.Mutate(state, decision);

    AssertEqual(ProjectStateMutationStatus.Unchanged, result.Status, "DenyApply must leave state unchanged.");
    AssertEqual(state, result.State, "DenyApply must return original state instance data unchanged.");
}

static void ProjectStateMutatorDoesNotWritePersistence()
{
    var workspaceRoot = CreateScratchWorkspace();
    try
    {
        var projectState = CreateProjectState(
            activeShiftId: "SHIFT-001",
            activeTaskId: "TASK-001",
            projectRoot: workspaceRoot);
        var decision = new ShiftUpdateDecision(
            ExecutionTarget.IdleSubsystem,
            ExecutionOutcomeStatus.NoOp,
            ShiftUpdateDecisionStatus.AllowApply,
            "Would close");
        var metaPath = Path.Combine(workspaceRoot, ".zavod", "meta", "project.json");

        _ = ProjectStateMutator.Mutate(projectState, decision);

        AssertFalse(File.Exists(metaPath), "In-memory mutator must not write persistence files.");
    }
    finally
    {
        DeleteScratchWorkspace(workspaceRoot);
    }
}

static void TaskExecutionContextBindsActiveTaskDeterministically()
{
    var projectState = CreateProjectState(activeShiftId: "SHIFT-001", activeTaskId: "TASK-001");
    var task = CreateTaskState(ContextIntentState.Validated, TaskStateStatus.Active, PromptRole.Worker);
    var shift = CreateShiftState(task) with { CurrentTaskId = task.TaskId };

    var context = TaskExecutionContextBuilder.Build(projectState, shift);

    AssertEqual("SHIFT-001", context.ShiftId, "Task execution context must preserve active shift id.");
    AssertEqual("TASK-001", context.TaskId, "Task execution context must preserve active task id.");
}

static void TaskExecutionContextRejectsMissingCurrentTask()
{
    var projectState = CreateProjectState(activeShiftId: "SHIFT-001", activeTaskId: "TASK-001");
    var task = CreateTaskState(ContextIntentState.Validated, TaskStateStatus.Active, PromptRole.Worker);
    var shift = CreateShiftState(task) with { CurrentTaskId = null };

    AssertThrows<InvalidOperationException>(
        () => TaskExecutionContextBuilder.Build(projectState, shift),
        "Task execution context must reject missing shift task binding.");
}

static void TaskExecutionContextRejectsMissingProjectActiveTask()
{
    var projectState = CreateProjectState(activeShiftId: "SHIFT-001", activeTaskId: null);
    var task = CreateTaskState(ContextIntentState.Validated, TaskStateStatus.Active, PromptRole.Worker);
    var shiftState = CreateShiftState(task) with { CurrentTaskId = task.TaskId };

    AssertThrows<InvalidOperationException>(
        () => TaskExecutionContextBuilder.Build(projectState, shiftState),
        "Task execution context must reject project state without active task binding.",
        static exception => exception.Message.Contains("active task", StringComparison.Ordinal));
}

static void PersistenceDecisionAllowsPersistForMutatedState()
{
    var state = CreateProjectState(
        activeShiftId: null,
        activeTaskId: null);
    var mutationResult = new ProjectStateMutationResult(
        state,
        ProjectStateMutationStatus.Mutated,
        "Mutated in memory");

    var decision = ProjectStatePersistenceDecisionMaker.Decide(mutationResult);

    AssertEqual(ProjectStatePersistenceDecisionStatus.Persist, decision.Status, "Mutated state must allow persistence.");
    AssertTrue(decision.ShouldPersist, "Mutated state must set ShouldPersist.");
    AssertEqual(state, decision.State, "Persistence decision must carry mutated state forward.");
}

static void PersistenceDecisionSkipsPersistForUnchangedState()
{
    var state = CreateProjectState(
        activeShiftId: "SHIFT-001",
        activeTaskId: "TASK-001");
    var mutationResult = new ProjectStateMutationResult(
        state,
        ProjectStateMutationStatus.Unchanged,
        "No mutation");

    var decision = ProjectStatePersistenceDecisionMaker.Decide(mutationResult);

    AssertEqual(ProjectStatePersistenceDecisionStatus.SkipPersist, decision.Status, "Unchanged state must skip persistence.");
    AssertFalse(decision.ShouldPersist, "Unchanged state must not set ShouldPersist.");
    AssertContains(decision.Reason ?? string.Empty, "should be skipped", "Skip-persist decision must be informative.");
}

static void PersistenceDecisionDoesNotWriteToDisk()
{
    var workspaceRoot = CreateScratchWorkspace();
    try
    {
        var state = CreateProjectState(
            activeShiftId: null,
            activeTaskId: null,
            projectRoot: workspaceRoot);
        var mutationResult = new ProjectStateMutationResult(
            state,
            ProjectStateMutationStatus.Mutated,
            "Mutated in memory");
        var metaPath = Path.Combine(workspaceRoot, ".zavod", "meta", "project.json");

        _ = ProjectStatePersistenceDecisionMaker.Decide(mutationResult);

        AssertFalse(File.Exists(metaPath), "Persistence decision layer must not write persistence files.");
    }
    finally
    {
        DeleteScratchWorkspace(workspaceRoot);
    }
}

static void SaveApplierBlocksDirectProjectStatePersistenceOutsideClosure()
{
    var workspaceRoot = CreateScratchWorkspace();
    try
    {
        var initial = ProjectStateStorage.EnsureInitialized(workspaceRoot, "zavod-save", "ZAVOD Save");
        var mutated = initial with { ActiveShiftId = "SHIFT-001", ActiveTaskId = "TASK-001" };
        var decision = new ProjectStatePersistenceDecision(
            mutated,
            ProjectStateMutationStatus.Mutated,
            ProjectStatePersistenceDecisionStatus.Persist,
            ShouldPersist: true,
            "Persist mutated state");

        var exception = AssertThrows<InvalidOperationException>(
            () => ProjectStateSaveApplier.Apply(decision),
            "Execution-side save applier must block direct canonical project state persistence.",
            static error => error.Message.Contains("ShiftClosureProcessor", StringComparison.Ordinal));

        AssertContains(exception.Message, "closure-path", "Blocked save seam must explain canonical closure requirement.");
        var reloaded = ProjectStateStorage.Load(workspaceRoot);
        AssertEqual(initial, reloaded, "Blocked execution-side save must leave persisted project state unchanged.");
    }
    finally
    {
        DeleteScratchWorkspace(workspaceRoot);
    }
}

static void SaveApplierSkipsUnchangedProjectState()
{
    var workspaceRoot = CreateScratchWorkspace();
    try
    {
        var initial = ProjectStateStorage.EnsureInitialized(workspaceRoot, "zavod-save-skip", "ZAVOD Save Skip");
        var metaPath = Path.Combine(workspaceRoot, ".zavod", "meta", "project.json");
        var before = File.ReadAllText(metaPath);
        var decision = new ProjectStatePersistenceDecision(
            initial,
            ProjectStateMutationStatus.Unchanged,
            ProjectStatePersistenceDecisionStatus.SkipPersist,
            ShouldPersist: false,
            "Skip unchanged state");

        var result = ProjectStateSaveApplier.Apply(decision);
        var after = File.ReadAllText(metaPath);

        AssertEqual(ProjectStateSaveStatus.Skipped, result.SaveStatus, "Unchanged state with skip decision must not be saved.");
        AssertFalse(result.WasPersisted, "Skipped save must mark WasPersisted=false.");
        AssertEqual(before, after, "Skip decision must not modify persisted meta file.");
    }
    finally
    {
        DeleteScratchWorkspace(workspaceRoot);
    }
}

static void SaveApplierBlockedPathDoesNotWriteProjectState()
{
    var workspaceRoot = CreateScratchWorkspace();
    try
    {
        var initial = ProjectStateStorage.EnsureInitialized(workspaceRoot, "zavod-reload", "ZAVOD Reload");
        var metaPath = Path.Combine(workspaceRoot, ".zavod", "meta", "project.json");
        var before = File.ReadAllText(metaPath);
        var mutated = initial with { ActiveShiftId = "SHIFT-RELOAD-001", ActiveTaskId = "TASK-RELOAD-001" };
        var decision = new ProjectStatePersistenceDecision(
            mutated,
            ProjectStateMutationStatus.Mutated,
            ProjectStatePersistenceDecisionStatus.Persist,
            ShouldPersist: true,
            "Persist for reload");

        _ = AssertThrows<InvalidOperationException>(
            () => ProjectStateSaveApplier.Apply(decision),
            "Execution-side save applier must not write project state outside closure.");
        var after = File.ReadAllText(metaPath);
        var reloaded = ProjectStateStorage.Load(workspaceRoot);

        AssertEqual(before, after, "Blocked execution-side save must not rewrite persisted meta.");
        AssertEqual(initial, reloaded, "Reloaded state must remain unchanged after blocked execution-side save.");
    }
    finally
    {
        DeleteScratchWorkspace(workspaceRoot);
    }
}

static void SaveApplierBlockedPathDoesNotWriteSnapshotCapsuleOrTaskFiles()
{
    var workspaceRoot = CreateScratchWorkspace();
    try
    {
        var initial = ProjectStateStorage.EnsureInitialized(workspaceRoot, "zavod-sidefx", "ZAVOD SideFX");
        var mutated = initial with { ActiveShiftId = "SHIFT-001", ActiveTaskId = "TASK-001" };
        var decision = new ProjectStatePersistenceDecision(
            mutated,
            ProjectStateMutationStatus.Mutated,
            ProjectStatePersistenceDecisionStatus.Persist,
            ShouldPersist: true,
            "Persist only state");

        _ = AssertThrows<InvalidOperationException>(
            () => ProjectStateSaveApplier.Apply(decision),
            "Blocked execution-side save must not create unrelated persistence side effects.");

        AssertFalse(Directory.Exists(Path.Combine(workspaceRoot, ".zavod", "tasks")), "Save applier must not create task storage.");
        AssertFalse(File.Exists(Path.Combine(workspaceRoot, ".zavod", "project", "capsule.md")), "Save applier must not write capsule document.");
        var snapshotsRoot = Path.Combine(workspaceRoot, ".zavod", "snapshots");
        AssertEqual(0, Directory.GetFiles(snapshotsRoot).Length, "Save applier must not write snapshot files.");
    }
    finally
    {
        DeleteScratchWorkspace(workspaceRoot);
    }
}

static void NonClosableExecutionOutcomeDoesNotMutateClosureTruth()
{
    var workspaceRoot = CreateScratchWorkspace();
    try
    {
        var initial = ProjectStateStorage.EnsureInitialized(workspaceRoot, "zavod-closure-non-closable", "ZAVOD Closure Non Closable");
        var activeState = ProjectStateStorage.Save(initial with { ActiveShiftId = "SHIFT-001", ActiveTaskId = "TASK-001" });
        var task = CreateTaskState(ContextIntentState.Validated, TaskStateStatus.Active, PromptRole.Worker);
        var shift = CreateShiftState(task) with { CurrentTaskId = task.TaskId };
        var context = TaskExecutionContextBuilder.Build(activeState, shift);
        var pipeline = new ExecutionPipeline(new ExecutionDispatcher(
            new RecordingBootstrapSubsystem(new SubsystemHandleResult(SubsystemHandleStatus.Deferred, "bootstrap deferred")),
            new RecordingIdleSubsystem(new SubsystemHandleResult(SubsystemHandleStatus.NoOp, "task completed")),
            new RecordingActiveShiftSubsystem(new SubsystemHandleResult(SubsystemHandleStatus.Rejected, "active rejected"))));

        var run = pipeline.Execute(context, ExecutionTarget.ActiveShiftSubsystem);
        var closure = ShiftClosureProcessor.Close(
            activeState,
            shift,
            run,
            new DateTimeOffset(2026, 03, 28, 18, 00, 00, TimeSpan.Zero),
            isUserConfirmed: true);
        var reloadedState = ProjectStateStorage.Load(workspaceRoot);

        AssertEqual(ShiftClosureStatus.Cancelled, closure.Status, "Non-closable execution outcome must not finalize canonical closure.");
        AssertEqual(TaskStateStatus.Active, closure.Task.Status, "Non-closable outcome must not complete task truth.");
        AssertEqual(ShiftStateStatus.Active, closure.Shift.Status, "Non-closable outcome must not complete shift truth.");
        AssertEqual("SHIFT-001", closure.ProjectState.ActiveShiftId, "Non-closable outcome must leave active shift bound.");
        AssertEqual("TASK-001", closure.ProjectState.ActiveTaskId, "Non-closable outcome must leave active task bound.");
        AssertTrue(closure.Snapshot is null, "Non-closable outcome must not create canonical snapshot.");
        AssertTrue(closure.ShiftFilePath is null, "Non-closable outcome must not persist updated shift truth.");
        AssertTrue(closure.SnapshotFilePath is null, "Non-closable outcome must not persist snapshot history.");
        AssertEqual(activeState, reloadedState, "Non-closable outcome must not mutate persisted project state.");
        AssertEqual(0, Directory.GetFiles(Path.Combine(workspaceRoot, ".zavod", "snapshots")).Length, "Non-closable outcome must not create snapshot files.");
    }
    finally
    {
        DeleteScratchWorkspace(workspaceRoot);
    }
}

static void SuccessfulConfirmedClosurePersistsCanonicalTruth()
{
    var workspaceRoot = CreateScratchWorkspace();
    try
    {
        var initial = ProjectStateStorage.EnsureInitialized(workspaceRoot, "zavod-closure-confirmed", "ZAVOD Closure Confirmed");
        var activeState = ProjectStateStorage.Save(initial with { ActiveShiftId = "SHIFT-001", ActiveTaskId = "TASK-001" });
        var task = CreateTaskState(ContextIntentState.Validated, TaskStateStatus.Active, PromptRole.Worker);
        var shift = CreateShiftState(task) with { CurrentTaskId = task.TaskId };
        var context = TaskExecutionContextBuilder.Build(activeState, shift);
        var pipeline = new ExecutionPipeline(new ExecutionDispatcher(
            new RecordingBootstrapSubsystem(new SubsystemHandleResult(SubsystemHandleStatus.Deferred, "bootstrap deferred")),
            new RecordingIdleSubsystem(new SubsystemHandleResult(SubsystemHandleStatus.NoOp, "task completed")),
            new RecordingActiveShiftSubsystem(new SubsystemHandleResult(SubsystemHandleStatus.Rejected, "active rejected"))));

        var run = pipeline.Execute(context, ExecutionTarget.IdleSubsystem);
        var closure = ShiftClosureProcessor.Close(
            activeState,
            shift,
            run,
            new DateTimeOffset(2026, 03, 28, 18, 05, 00, TimeSpan.Zero),
            isUserConfirmed: true);
        var reloadedState = ProjectStateStorage.Load(workspaceRoot);
        var reloadedShift = ShiftStateStorage.Load(workspaceRoot, "SHIFT-001");

        AssertEqual(ShiftClosureStatus.Completed, closure.Status, "Confirmed closable execution outcome must complete canonical closure.");
        AssertEqual(TaskStateStatus.Completed, closure.Task.Status, "Confirmed closure must complete task truth.");
        AssertEqual(ShiftStateStatus.Completed, closure.Shift.Status, "Confirmed closure must complete shift truth.");
        AssertEqual<string?>(null, closure.ProjectState.ActiveShiftId, "Confirmed closure must clear active shift in project state.");
        AssertEqual<string?>(null, closure.ProjectState.ActiveTaskId, "Confirmed closure must clear active task in project state.");
        AssertTrue(closure.Snapshot is not null, "Confirmed closure must create immutable snapshot.");
        AssertTrue(File.Exists(closure.ShiftFilePath!), "Confirmed closure must persist shift truth.");
        AssertTrue(File.Exists(closure.SnapshotFilePath!), "Confirmed closure must persist snapshot history.");
        AssertEqual(closure.ProjectState, reloadedState, "Persisted project state must match closure result.");
        AssertEqual(ShiftStateStatus.Completed, reloadedShift.Status, "Persisted shift truth must reflect completed lifecycle.");
        AssertEqual($"task://{task.TaskId}", closure.Snapshot!.TaskReference, "Canonical snapshot must reference finalized task.");
        AssertEqual(
            $"execution://shift/{run.Record.ShiftId}/task/{run.Record.TaskId}/target/{run.Record.Target}/outcome/{run.Record.OutcomeStatus}",
            closure.Snapshot.ExecutionReference,
            "Canonical snapshot must reference originating execution result.");
    }
    finally
    {
        DeleteScratchWorkspace(workspaceRoot);
    }
}

static void AcceptResultAppliesAndCompletesTaskWithoutClosingShift()
{
    var workspaceRoot = CreateScratchWorkspace();
    try
    {
        var initial = ProjectStateStorage.EnsureInitialized(workspaceRoot, "zavod-accept-without-close", "ZAVOD Accept Without Close");
        var activeState = ProjectStateStorage.Save(initial with { ActiveShiftId = "SHIFT-001", ActiveTaskId = "TASK-001" });
        var task = CreateTaskState(ContextIntentState.Validated, TaskStateStatus.Active, PromptRole.Worker);
        var shift = CreateShiftState(task) with { CurrentTaskId = task.TaskId };
        _ = ShiftStateStorage.Save(workspaceRoot, shift);
        EnsureProjectStructureForTest(workspaceRoot);

        var runtime = ExecutionRuntimeController.Begin(task, shift);
        runtime = ExecutionRuntimeController.ProduceResult(runtime);
        runtime = ExecutionRuntimeController.RequestQcReview(runtime);
        runtime = ExecutionRuntimeController.AcceptQcReview(runtime);
        runtime = ObserveSafeAcceptanceForApply(runtime, workspaceRoot);

        var applied = AcceptedResultApplyProcessor.Apply(
            activeState,
            shift,
            task,
            runtime,
            new DateTimeOffset(2026, 03, 31, 10, 00, 00, TimeSpan.Zero));

        var reloadedState = ProjectStateStorage.Load(workspaceRoot);
        var reloadedShift = ShiftStateStorage.Load(workspaceRoot, shift.ShiftId);
        var persistedTask = reloadedShift.Tasks.Single(candidate => candidate.TaskId == task.TaskId);

        AssertEqual(TaskStateStatus.Completed, applied.TaskState.Status, "Accept must complete current task truth.");
        AssertEqual(ShiftStateStatus.Active, applied.ShiftState.Status, "Accept must keep shift active.");
        AssertEqual<string?>("SHIFT-001", applied.ProjectState.ActiveShiftId, "Accept must preserve active shift binding.");
        AssertEqual<string?>(null, applied.ProjectState.ActiveTaskId, "Accept must clear active task binding only.");
        AssertEqual<string?>(null, applied.ShiftState.CurrentTaskId, "Accept must free current task slot for the next task.");
        AssertTrue(applied.ShiftState.AcceptedResults.Count > 0, "Accept must record committed accepted result in shift history.");
        AssertEqual(reloadedState, applied.ProjectState, "Persisted project state must match accepted-result apply.");
        AssertEqual(ShiftStateStatus.Active, reloadedShift.Status, "Persisted shift must remain active after accept.");
        AssertEqual(TaskStateStatus.Completed, persistedTask.Status, "Persisted task must be completed after accept.");
    }
    finally
    {
        DeleteScratchWorkspace(workspaceRoot);
    }
}

static ExecutionRuntimeState ObserveSafeAcceptanceForApply(ExecutionRuntimeState runtime, string workspaceRoot)
{
    EnsureRuntimeTouchedFilesExist(workspaceRoot, runtime);
    EnsureProjectStructureForTest(workspaceRoot);
    return ExecutionRuntimeController.ObserveAcceptanceAfterQc(
        runtime,
        workspaceRoot,
        new AcceptanceProcessEvidence(0, "ok", string.Empty, TimedOut: false, WasCanceled: false),
        "workspace unchanged");
}

static void EnsureProjectStructureForTest(string workspaceRoot)
{
    var projectPath = Path.Combine(workspaceRoot, "project.csproj");
    if (!File.Exists(projectPath))
    {
        File.WriteAllText(projectPath, "<Project />");
    }

    var programPath = Path.Combine(workspaceRoot, "Program.cs");
    if (!File.Exists(programPath))
    {
        File.WriteAllText(programPath, "class Program {}");
    }
}

static void EnsureRuntimeTouchedFilesExist(string workspaceRoot, ExecutionRuntimeState runtime)
{
    if (runtime.Result is null)
    {
        return;
    }

    foreach (var relativePath in runtime.Result.Modifications
        .Select(static modification => modification.Path)
        .Where(static path => !string.IsNullOrWhiteSpace(path))
        .Select(static path => path.Trim())
        .Distinct(StringComparer.OrdinalIgnoreCase))
    {
        var fullPath = Path.GetFullPath(Path.Combine(workspaceRoot, relativePath));
        if (!IsUnderRootForTest(workspaceRoot, fullPath))
        {
            throw new InvalidOperationException("Test fixture touched path escaped workspace root.");
        }

        var directory = Path.GetDirectoryName(fullPath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        if (!File.Exists(fullPath))
        {
            File.WriteAllText(fullPath, "test fixture touched file");
        }
    }
}

static bool IsUnderRootForTest(string root, string path)
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

static void AcceptResultBlocksMissingAcceptanceEvaluationHonestly()
{
    var workspaceRoot = CreateScratchWorkspace();
    try
    {
        var initial = ProjectStateStorage.EnsureInitialized(workspaceRoot, "zavod-accept-missing-gate", "ZAVOD Accept Missing Gate");
        var activeState = ProjectStateStorage.Save(initial with { ActiveShiftId = "SHIFT-001", ActiveTaskId = "TASK-001" });
        var task = CreateTaskState(ContextIntentState.Validated, TaskStateStatus.Active, PromptRole.Worker);
        var shift = CreateShiftState(task) with { CurrentTaskId = task.TaskId };
        _ = ShiftStateStorage.Save(workspaceRoot, shift);

        var runtime = ExecutionRuntimeController.Begin(task, shift);
        runtime = ExecutionRuntimeController.ProduceResult(runtime);
        runtime = ExecutionRuntimeController.RequestQcReview(runtime);
        runtime = ExecutionRuntimeController.AcceptQcReview(runtime);

        AssertThrows<InvalidOperationException>(
            () => AcceptedResultApplyProcessor.Apply(
                activeState,
                shift,
                task,
                runtime,
                new DateTimeOffset(2026, 03, 31, 10, 03, 00, TimeSpan.Zero)),
            "Accepted result apply must not move truth without an acceptance evaluation.");
    }
    finally
    {
        DeleteScratchWorkspace(workspaceRoot);
    }
}

static void AcceptResultStillAppliesWhenObservedAcceptanceIsSafe()
{
    var workspaceRoot = CreateScratchWorkspace();
    try
    {
        var initial = ProjectStateStorage.EnsureInitialized(workspaceRoot, "zavod-accept-safe-gate", "ZAVOD Accept Safe Gate");
        File.WriteAllText(Path.Combine(workspaceRoot, "Program.cs"), "class Program {}");
        File.WriteAllText(Path.Combine(workspaceRoot, "project.csproj"), "<Project />");
        var activeState = ProjectStateStorage.Save(initial with { ActiveShiftId = "SHIFT-001", ActiveTaskId = "TASK-001" });
        var task = CreateTaskState(ContextIntentState.Validated, TaskStateStatus.Active, PromptRole.Worker);
        var shift = CreateShiftState(task) with { CurrentTaskId = task.TaskId };
        _ = ShiftStateStorage.Save(workspaceRoot, shift);
        var runtime = ExecutionRuntimeController.Begin(task, shift);
        var providedResult = new WorkerExecutionResult(
            "RESULT-SAFE-GATE-001",
            task.TaskId,
            WorkerExecutionStatus.Success,
            "Worker prepared Program.cs update.",
            Array.Empty<IntakeArtifact>(),
            new[] { new WorkerExecutionModification("Program.cs", "update", "Adjusted entry point.") },
            Array.Empty<ToolWarning>());

        runtime = ExecutionRuntimeController.ProduceProvidedResult(runtime, providedResult);
        runtime = ExecutionRuntimeController.RequestQcReview(runtime);
        runtime = ExecutionRuntimeController.AcceptQcReview(runtime);
        runtime = ExecutionRuntimeController.ObserveAcceptanceAfterQc(
            runtime,
            workspaceRoot,
            new AcceptanceProcessEvidence(0, "ok", string.Empty, TimedOut: false, WasCanceled: false),
            "workspace unchanged");

        var applied = AcceptedResultApplyProcessor.Apply(
            activeState,
            shift,
            task,
            runtime,
            new DateTimeOffset(2026, 03, 31, 10, 05, 00, TimeSpan.Zero));

        AssertEqual(TaskStateStatus.Completed, applied.TaskState.Status, "Safe observed acceptance must still allow apply.");
        AssertEqual(ShiftStateStatus.Active, applied.ShiftState.Status, "Safe observed acceptance must keep shift active after accept.");
    }
    finally
    {
        DeleteScratchWorkspace(workspaceRoot);
    }
}

static void AcceptResultIsBlockedWhenObservedAcceptanceConflicts()
{
    var workspaceRoot = CreateScratchWorkspace();
    try
    {
        var initial = ProjectStateStorage.EnsureInitialized(workspaceRoot, "zavod-accept-conflict-gate", "ZAVOD Accept Conflict Gate");
        var codePath = Path.Combine(workspaceRoot, "Program.cs");
        File.WriteAllText(codePath, "class Program {}");
        File.WriteAllText(Path.Combine(workspaceRoot, "project.csproj"), "<Project />");
        var activeState = ProjectStateStorage.Save(initial with { ActiveShiftId = "SHIFT-001", ActiveTaskId = "TASK-001" });
        var task = CreateTaskState(ContextIntentState.Validated, TaskStateStatus.Active, PromptRole.Worker);
        var shift = CreateShiftState(task) with { CurrentTaskId = task.TaskId };
        _ = ShiftStateStorage.Save(workspaceRoot, shift);
        var runtime = ExecutionRuntimeController.Begin(task, shift);
        var providedResult = new WorkerExecutionResult(
            "RESULT-CONFLICT-GATE-001",
            task.TaskId,
            WorkerExecutionStatus.Success,
            "Worker prepared Program.cs update.",
            Array.Empty<IntakeArtifact>(),
            new[] { new WorkerExecutionModification("Program.cs", "update", "Adjusted entry point.") },
            Array.Empty<ToolWarning>());

        runtime = ExecutionRuntimeController.ProduceProvidedResult(runtime, providedResult);
        runtime = ExecutionRuntimeController.RequestQcReview(runtime);
        runtime = ExecutionRuntimeController.AcceptQcReview(runtime);
        var initialScan = WorkspaceScanner.Scan(new WorkspaceScanRequest(workspaceRoot));
        var baseline = WorkspaceBaselineBuilder.Build(initialScan);
        var executionBase = ExecutionBaseBuilder.Build(workspaceRoot, new[] { "Program.cs" }, runtime.Session.SessionId);
        File.AppendAllText(codePath, "\n// external change");
        var currentScan = WorkspaceScanner.Scan(new WorkspaceScanRequest(workspaceRoot));
        var evaluation = AcceptanceEvaluationFactory.CreateFromComponents(
            currentScan.State,
            baseline,
            executionBase,
            runtime.RuntimeSelection,
            runtime.RunResult,
            runtime.Result,
            new AcceptanceProcessEvidence(0, "ok", string.Empty, TimedOut: false, WasCanceled: false),
            "workspace changed after execution base");
        runtime = runtime with { AcceptanceEvaluation = evaluation };

        AssertThrows<InvalidOperationException>(
            () => AcceptedResultApplyProcessor.Apply(
                activeState,
                shift,
                task,
                runtime,
                new DateTimeOffset(2026, 03, 31, 10, 06, 00, TimeSpan.Zero)),
            "Blocked observed acceptance must stop apply before truth movement.");
    }
    finally
    {
        DeleteScratchWorkspace(workspaceRoot);
    }
}

static void SoftCheckpointResolverIgnoresLightStep()
{
    var previousTask = CreateCustomTaskState(
        "TASK-UI-000",
        TaskStateStatus.Completed,
        "Adjust button spacing on result screen",
        new[] { "MainWindow.xaml" });
    var currentTask = CreateCustomTaskState(
        "TASK-UI-001",
        TaskStateStatus.Completed,
        "Fix button alignment in result layout",
        new[] { "MainWindow.xaml" });
    var shift = new ShiftState(
        "SHIFT-001",
        "UI polish loop",
        null,
        ShiftStateStatus.Active,
        new[] { previousTask, currentTask },
        new[] { "Pending validation" },
        Array.Empty<string>(),
        new[] { "Stay within current UI layer" });
    var result = new WorkerExecutionResult(
        "RESULT-TASK-UI-001-001",
        currentTask.TaskId,
        WorkerExecutionStatus.Success,
        "Aligned the result button in XAML.",
        Array.Empty<IntakeArtifact>(),
        new[] { new WorkerExecutionModification("MainWindow.xaml", "edit", "Adjust result button alignment.") },
        Array.Empty<ToolWarning>());

    var signal = SoftCheckpointSignalResolverV1.Resolve(shift, currentTask, result);

    AssertFalse(signal.ShouldCreateSnapshot, "Ordinary same-domain UI step must not trigger soft checkpoint.");
    AssertEqual(0, signal.Score, "Ordinary same-domain UI step must keep zero soft checkpoint score.");
    AssertEqual(0, signal.Reasons.Count, "Ordinary same-domain UI step must not emit soft checkpoint reasons.");
}

static void SoftCheckpointResolverDetectsSemanticShiftWithoutProjectDocs()
{
    var previousTask = CreateCustomTaskState(
        "TASK-UI-000",
        TaskStateStatus.Completed,
        "Adjust button spacing on result screen",
        new[] { "MainWindow.xaml" });
    var currentTask = CreateCustomTaskState(
        "TASK-CORE-001",
        TaskStateStatus.Completed,
        "Adjust execution lifecycle state machine behavior",
        new[] { "Flow/StepPhaseMachine.cs" });
    var shift = new ShiftState(
        "SHIFT-001",
        "Semantic shift loop",
        null,
        ShiftStateStatus.Active,
        new[] { previousTask, currentTask },
        new[] { "Pending validation" },
        Array.Empty<string>(),
        new[] { "Keep current shift open" });
    var result = new WorkerExecutionResult(
        "RESULT-TASK-CORE-001-001",
        currentTask.TaskId,
        WorkerExecutionStatus.Success,
        "Adjusted lifecycle guards.",
        Array.Empty<IntakeArtifact>(),
        new[] { new WorkerExecutionModification("State/ResultAbandonProcessor.cs", "edit", "Adjust lifecycle guard.") },
        Array.Empty<ToolWarning>());

    var signal = SoftCheckpointSignalResolverV1.Resolve(shift, currentTask, result);

    AssertTrue(signal.ShouldCreateSnapshot, "Semantic shift from UI work to core lifecycle work must trigger soft checkpoint.");
    AssertTrue(signal.Score >= 2, "Domain shift must be strong enough to trigger soft checkpoint on its own.");
    AssertTrue(signal.Reasons.Contains("domain_shift", StringComparer.Ordinal), "Semantic shift v1 must emit the agreed domain-shift reason.");
}

static void SoftCheckpointProcessorWritesSnapshotWithoutMutatingTruth()
{
    var workspaceRoot = CreateScratchWorkspace();
    try
    {
        var initial = ProjectStateStorage.EnsureInitialized(workspaceRoot, "zavod-soft-checkpoint-write", "ZAVOD Soft Checkpoint Write");
        var previousTask = CreateCustomTaskState(
            "TASK-UI-000",
            TaskStateStatus.Completed,
            "Adjust result button spacing",
            new[] { "MainWindow.xaml" });
        var currentTask = CreateCustomTaskState(
            "TASK-CORE-001",
            TaskStateStatus.Active,
            "Adjust execution lifecycle state machine behavior",
            new[] { "Flow/StepPhaseMachine.cs" });
        var shift = new ShiftState(
            "SHIFT-001",
            "Continuous work shift",
            currentTask.TaskId,
            ShiftStateStatus.Active,
            new[] { previousTask, currentTask },
            new[] { "Pending review evidence" },
            Array.Empty<string>(),
            new[] { "Keep shift active" });
        var activeState = ProjectStateStorage.Save(initial with { ActiveShiftId = shift.ShiftId, ActiveTaskId = currentTask.TaskId });
        _ = ShiftStateStorage.Save(workspaceRoot, shift);

        var runtime = ExecutionRuntimeController.Begin(currentTask, shift);
        var providedResult = new WorkerExecutionResult(
            "RESULT-TASK-CORE-001-001",
            currentTask.TaskId,
            WorkerExecutionStatus.Success,
            "Adjusted lifecycle guards.",
            Array.Empty<IntakeArtifact>(),
            new[] { new WorkerExecutionModification("State/ResultAbandonProcessor.cs", "edit", "Adjust lifecycle guard.") },
            Array.Empty<ToolWarning>());
        runtime = ExecutionRuntimeController.ProduceProvidedResult(runtime, providedResult);
        runtime = ExecutionRuntimeController.RequestQcReview(runtime);
        runtime = ExecutionRuntimeController.AcceptQcReview(runtime);
        runtime = ObserveSafeAcceptanceForApply(runtime, workspaceRoot);

        var applied = AcceptedResultApplyProcessor.Apply(
            activeState,
            shift,
            currentTask,
            runtime,
            new DateTimeOffset(2026, 04, 05, 10, 00, 00, TimeSpan.Zero));
        var signal = SoftCheckpointSignalResolverV1.Resolve(applied.ShiftState, applied.TaskState, providedResult);
        var written = CheckpointSnapshotProcessor.WriteSoftCheckpoint(
            applied.ProjectState,
            applied.ShiftState,
            applied.AcceptedResult,
            applied.CommitRecord,
            signal.Score,
            signal.Reasons);
        var reloadedState = ProjectStateStorage.Load(workspaceRoot);
        var reloadedShift = ShiftStateStorage.Load(workspaceRoot, shift.ShiftId);

        AssertTrue(signal.ShouldCreateSnapshot, "Semantic shift fixture must produce a soft checkpoint signal.");
        AssertTrue(written.WasCreated, "First soft checkpoint write must create a snapshot.");
        AssertEqual("soft", written.Snapshot.CheckpointKind, "Soft checkpoint write must mark snapshot kind as soft.");
        AssertEqual(signal.Score, written.Snapshot.TriggerScore, "Soft checkpoint write must preserve trigger score.");
        AssertSequenceEqual(signal.Reasons, written.Snapshot.TriggerReasons ?? Array.Empty<string>(), "Soft checkpoint write must preserve trigger reasons.");
        AssertEqual(
            $"soft-checkpoint:{applied.AcceptedResult.AcceptedResultId}",
            written.Snapshot.DedupeKey,
            "Soft checkpoint write must use the agreed dedupe key format.");
        AssertEqual(applied.ProjectState, reloadedState, "Soft checkpoint write must not mutate project truth.");
        AssertEqual<string?>("SHIFT-001", reloadedState.ActiveShiftId, "Soft checkpoint write must keep active shift binding.");
        AssertEqual<string?>(null, reloadedState.ActiveTaskId, "Soft checkpoint write must keep active task cleared after accept.");
        AssertEqual(ShiftStateStatus.Active, reloadedShift.Status, "Soft checkpoint write must keep shift active.");
        AssertEqual(applied.ShiftState.ShiftId, reloadedShift.ShiftId, "Soft checkpoint write must keep shift identity unchanged.");
        AssertEqual(applied.ShiftState.CurrentTaskId, reloadedShift.CurrentTaskId, "Soft checkpoint write must not restore current task binding.");
        AssertSequenceEqual(applied.ShiftState.Tasks.Select(task => task.TaskId), reloadedShift.Tasks.Select(task => task.TaskId), "Soft checkpoint write must preserve task inventory.");
        AssertSequenceEqual(applied.ShiftState.AcceptedResults, reloadedShift.AcceptedResults, "Soft checkpoint write must preserve accepted result history.");
        AssertSequenceEqual(applied.ShiftState.OpenIssues, reloadedShift.OpenIssues, "Soft checkpoint write must preserve open issues.");
        AssertSequenceEqual(applied.ShiftState.Constraints, reloadedShift.Constraints, "Soft checkpoint write must preserve constraints.");
        AssertTrue(File.Exists(written.SnapshotFilePath), "Soft checkpoint write must persist snapshot file.");
    }
    finally
    {
        DeleteScratchWorkspace(workspaceRoot);
    }
}

static void SoftCheckpointProcessorIsIdempotentByDedupeKey()
{
    var workspaceRoot = CreateScratchWorkspace();
    try
    {
        var initial = ProjectStateStorage.EnsureInitialized(workspaceRoot, "zavod-soft-checkpoint-dedupe", "ZAVOD Soft Checkpoint Dedupe");
        var previousTask = CreateCustomTaskState(
            "TASK-UI-000",
            TaskStateStatus.Completed,
            "Adjust result button spacing",
            new[] { "MainWindow.xaml" });
        var currentTask = CreateCustomTaskState(
            "TASK-CORE-001",
            TaskStateStatus.Active,
            "Adjust execution lifecycle state machine behavior",
            new[] { "Flow/StepPhaseMachine.cs" });
        var shift = new ShiftState(
            "SHIFT-001",
            "Continuous work shift",
            currentTask.TaskId,
            ShiftStateStatus.Active,
            new[] { previousTask, currentTask },
            new[] { "Pending review evidence" },
            Array.Empty<string>(),
            new[] { "Keep shift active" });
        var activeState = ProjectStateStorage.Save(initial with { ActiveShiftId = shift.ShiftId, ActiveTaskId = currentTask.TaskId });
        _ = ShiftStateStorage.Save(workspaceRoot, shift);

        var runtime = ExecutionRuntimeController.Begin(currentTask, shift);
        var providedResult = new WorkerExecutionResult(
            "RESULT-TASK-CORE-001-001",
            currentTask.TaskId,
            WorkerExecutionStatus.Success,
            "Adjusted lifecycle guards.",
            Array.Empty<IntakeArtifact>(),
            new[] { new WorkerExecutionModification("State/ResultAbandonProcessor.cs", "edit", "Adjust lifecycle guard.") },
            Array.Empty<ToolWarning>());
        runtime = ExecutionRuntimeController.ProduceProvidedResult(runtime, providedResult);
        runtime = ExecutionRuntimeController.RequestQcReview(runtime);
        runtime = ExecutionRuntimeController.AcceptQcReview(runtime);
        runtime = ObserveSafeAcceptanceForApply(runtime, workspaceRoot);

        var applied = AcceptedResultApplyProcessor.Apply(
            activeState,
            shift,
            currentTask,
            runtime,
            new DateTimeOffset(2026, 04, 05, 10, 10, 00, TimeSpan.Zero));
        var signal = SoftCheckpointSignalResolverV1.Resolve(applied.ShiftState, applied.TaskState, providedResult);
        var first = CheckpointSnapshotProcessor.WriteSoftCheckpoint(
            applied.ProjectState,
            applied.ShiftState,
            applied.AcceptedResult,
            applied.CommitRecord,
            signal.Score,
            signal.Reasons);
        var second = CheckpointSnapshotProcessor.WriteSoftCheckpoint(
            applied.ProjectState,
            applied.ShiftState,
            applied.AcceptedResult,
            applied.CommitRecord,
            signal.Score,
            signal.Reasons);
        var snapshotFiles = Directory.GetFiles(Path.Combine(workspaceRoot, ".zavod", "snapshots"));

        AssertTrue(first.WasCreated, "First soft checkpoint write must create a snapshot.");
        AssertFalse(second.WasCreated, "Second soft checkpoint write for the same accepted result must be idempotent.");
        AssertEqual(first.SnapshotFilePath, second.SnapshotFilePath, "Idempotent soft checkpoint write must resolve to the same snapshot file.");
        AssertEqual(first.Snapshot.SnapshotId, second.Snapshot.SnapshotId, "Idempotent soft checkpoint write must preserve snapshot identity.");
        AssertEqual(1, snapshotFiles.Length, "Idempotent soft checkpoint write must not create duplicate files.");
    }
    finally
    {
        DeleteScratchWorkspace(workspaceRoot);
    }
}

static void HardCheckpointPathShortCircuitsSoftSnapshotWrite()
{
    var workspaceRoot = CreateScratchWorkspace();
    try
    {
        var initial = ProjectStateStorage.EnsureInitialized(workspaceRoot, "zavod-hard-short-circuit", "ZAVOD Hard Short Circuit");
        var previousTask = CreateCustomTaskState(
            "TASK-UI-000",
            TaskStateStatus.Completed,
            "Adjust result button spacing",
            new[] { "MainWindow.xaml" });
        var currentTask = CreateCustomTaskState(
            "TASK-CORE-001",
            TaskStateStatus.Active,
            "Adjust execution lifecycle state machine behavior",
            new[] { "project/roadmap.md" });
        var shift = new ShiftState(
            "SHIFT-001",
            "Continuous work shift",
            currentTask.TaskId,
            ShiftStateStatus.Active,
            new[] { previousTask, currentTask },
            new[] { "Pending review evidence" },
            Array.Empty<string>(),
            new[] { "Keep shift active" });
        var activeState = ProjectStateStorage.Save(initial with { ActiveShiftId = shift.ShiftId, ActiveTaskId = currentTask.TaskId });
        _ = ShiftStateStorage.Save(workspaceRoot, shift);

        var runtime = ExecutionRuntimeController.Begin(currentTask, shift);
        var providedResult = new WorkerExecutionResult(
            "RESULT-TASK-CORE-001-001",
            currentTask.TaskId,
            WorkerExecutionStatus.Success,
            "Adjusted roadmap direction.",
            Array.Empty<IntakeArtifact>(),
            new[] { new WorkerExecutionModification("project/roadmap.md", "edit", "Adjust roadmap direction.") },
            Array.Empty<ToolWarning>());
        runtime = ExecutionRuntimeController.ProduceProvidedResult(runtime, providedResult);
        runtime = ExecutionRuntimeController.RequestQcReview(runtime);
        runtime = ExecutionRuntimeController.AcceptQcReview(runtime);
        runtime = ObserveSafeAcceptanceForApply(runtime, workspaceRoot);

        var applied = AcceptedResultApplyProcessor.Apply(
            activeState,
            shift,
            currentTask,
            runtime,
            new DateTimeOffset(2026, 04, 05, 10, 20, 00, TimeSpan.Zero));
        var hardAcceptedResult = applied.AcceptedResult;
        var decisionSignal = DecisionSignalResolverV1.TryResolve(hardAcceptedResult);
        var isHard = CheckpointDetectorV1.IsCheckpoint(applied.TaskState, nextIntent: null, decisionSignal);
        var softSignal = SoftCheckpointSignalResolverV1.Resolve(applied.ShiftState, applied.TaskState, providedResult);

        AssertTrue(isHard, "Project-truth mutation must trigger hard checkpoint.");
        AssertTrue(softSignal.ShouldCreateSnapshot, "Fixture should also qualify for soft checkpoint, so hard short-circuit is meaningful.");

        var closure = ShiftClosureProcessor.CloseAcceptedShift(
            applied.ProjectState,
            applied.ShiftState,
            new DateTimeOffset(2026, 04, 05, 10, 21, 00, TimeSpan.Zero),
            "Hard checkpoint closure.");
        var snapshotFiles = Directory.GetFiles(Path.Combine(workspaceRoot, ".zavod", "snapshots"));

        AssertEqual(1, snapshotFiles.Length, "Hard checkpoint path must short-circuit soft snapshot write.");
        AssertEqual("closure", closure.Snapshot!.CheckpointKind, "Hard/closure snapshot must stay explicitly distinct from soft snapshots.");
        AssertEqual<string?>(null, closure.Snapshot.DedupeKey, "Closure snapshot must not reuse soft dedupe key.");
    }
    finally
    {
        DeleteScratchWorkspace(workspaceRoot);
    }
}

static void ProofScenarioLightStepLogsNoSoftSnapshot()
{
    var previousTask = CreateCustomTaskState(
        "TASK-UI-000",
        TaskStateStatus.Completed,
        "Adjust button spacing on result screen",
        new[] { "MainWindow.xaml" });
    var currentTask = CreateCustomTaskState(
        "TASK-UI-001",
        TaskStateStatus.Completed,
        "Fix button alignment in result layout",
        new[] { "MainWindow.xaml" });
    var shift = new ShiftState(
        "SHIFT-001",
        "UI polish loop",
        null,
        ShiftStateStatus.Active,
        new[] { previousTask, currentTask },
        new[] { "Pending validation" },
        Array.Empty<string>(),
        new[] { "Stay within current UI layer" });
    var result = new WorkerExecutionResult(
        "RESULT-TASK-UI-001-001",
        currentTask.TaskId,
        WorkerExecutionStatus.Success,
        "Aligned the result button in XAML.",
        Array.Empty<IntakeArtifact>(),
        new[] { new WorkerExecutionModification("MainWindow.xaml", "edit", "Adjust result button alignment.") },
        Array.Empty<ToolWarning>());
    var signal = SoftCheckpointSignalResolverV1.Resolve(shift, currentTask, result);

    Console.WriteLine("[SCENARIO] LIGHT_STEP");
    LogPostAcceptState("unchanged_by_soft", shift);

    AssertFalse(signal.ShouldCreateSnapshot, "Light step must not trigger soft snapshot.");
}

static void ProofScenarioSemanticShiftLogsSoftSnapshot()
{
    var workspaceRoot = CreateScratchWorkspace();
    try
    {
        var initial = ProjectStateStorage.EnsureInitialized(workspaceRoot, "zavod-proof-soft", "ZAVOD Proof Soft");
        var previousTask = CreateCustomTaskState(
            "TASK-UI-000",
            TaskStateStatus.Completed,
            "Adjust result button spacing",
            new[] { "MainWindow.xaml" });
        var currentTask = CreateCustomTaskState(
            "TASK-CORE-001",
            TaskStateStatus.Active,
            "Adjust execution lifecycle state machine behavior",
            new[] { "Flow/StepPhaseMachine.cs" });
        var shift = new ShiftState(
            "SHIFT-001",
            "Continuous work shift",
            currentTask.TaskId,
            ShiftStateStatus.Active,
            new[] { previousTask, currentTask },
            new[] { "Pending review evidence" },
            Array.Empty<string>(),
            new[] { "Keep shift active" });
        var activeState = ProjectStateStorage.Save(initial with { ActiveShiftId = shift.ShiftId, ActiveTaskId = currentTask.TaskId });
        _ = ShiftStateStorage.Save(workspaceRoot, shift);

        var runtime = ExecutionRuntimeController.Begin(currentTask, shift);
        var providedResult = new WorkerExecutionResult(
            "RESULT-TASK-CORE-001-001",
            currentTask.TaskId,
            WorkerExecutionStatus.Success,
            "Adjusted lifecycle guards.",
            Array.Empty<IntakeArtifact>(),
            new[] { new WorkerExecutionModification("State/ResultAbandonProcessor.cs", "edit", "Adjust lifecycle guard.") },
            Array.Empty<ToolWarning>());
        runtime = ExecutionRuntimeController.ProduceProvidedResult(runtime, providedResult);
        runtime = ExecutionRuntimeController.RequestQcReview(runtime);
        runtime = ExecutionRuntimeController.AcceptQcReview(runtime);
        runtime = ObserveSafeAcceptanceForApply(runtime, workspaceRoot);
        var applied = AcceptedResultApplyProcessor.Apply(
            activeState,
            shift,
            currentTask,
            runtime,
            new DateTimeOffset(2026, 04, 05, 11, 00, 00, TimeSpan.Zero));
        var signal = SoftCheckpointSignalResolverV1.Resolve(applied.ShiftState, applied.TaskState, providedResult);
        var written = CheckpointSnapshotProcessor.WriteSoftCheckpoint(
            applied.ProjectState,
            applied.ShiftState,
            applied.AcceptedResult,
            applied.CommitRecord,
            signal.Score,
            signal.Reasons);

        Console.WriteLine("[SCENARIO] SEMANTIC_SHIFT");
        LogPostAcceptState("unchanged_by_soft", applied.ShiftState, applied.ProjectState.ActiveShiftId, applied.ProjectState.ActiveTaskId);
        Console.WriteLine(
            $"[SNAPSHOT_EXAMPLE_SOFT] kind={written.Snapshot.CheckpointKind}; snapshotId={written.Snapshot.SnapshotId}; score={written.Snapshot.TriggerScore}; reasons={string.Join(",", written.Snapshot.TriggerReasons ?? Array.Empty<string>())}; dedupeKey={written.Snapshot.DedupeKey}");

        AssertTrue(written.WasCreated, "Semantic shift proof must create soft snapshot.");
    }
    finally
    {
        DeleteScratchWorkspace(workspaceRoot);
    }
}

static void ProofScenarioHardCheckpointLogsShortCircuit()
{
    var workspaceRoot = CreateScratchWorkspace();
    try
    {
        var initial = ProjectStateStorage.EnsureInitialized(workspaceRoot, "zavod-proof-hard", "ZAVOD Proof Hard");
        var previousTask = CreateCustomTaskState(
            "TASK-UI-000",
            TaskStateStatus.Completed,
            "Adjust result button spacing",
            new[] { "MainWindow.xaml" });
        var currentTask = CreateCustomTaskState(
            "TASK-CORE-001",
            TaskStateStatus.Active,
            "Adjust execution lifecycle state machine behavior",
            new[] { "project/roadmap.md" });
        var shift = new ShiftState(
            "SHIFT-001",
            "Continuous work shift",
            currentTask.TaskId,
            ShiftStateStatus.Active,
            new[] { previousTask, currentTask },
            new[] { "Pending review evidence" },
            Array.Empty<string>(),
            new[] { "Keep shift active" });
        var activeState = ProjectStateStorage.Save(initial with { ActiveShiftId = shift.ShiftId, ActiveTaskId = currentTask.TaskId });
        _ = ShiftStateStorage.Save(workspaceRoot, shift);

        var runtime = ExecutionRuntimeController.Begin(currentTask, shift);
        var providedResult = new WorkerExecutionResult(
            "RESULT-TASK-CORE-001-001",
            currentTask.TaskId,
            WorkerExecutionStatus.Success,
            "Adjusted roadmap direction.",
            Array.Empty<IntakeArtifact>(),
            new[] { new WorkerExecutionModification("project/roadmap.md", "edit", "Adjust roadmap direction.") },
            Array.Empty<ToolWarning>());
        runtime = ExecutionRuntimeController.ProduceProvidedResult(runtime, providedResult);
        runtime = ExecutionRuntimeController.RequestQcReview(runtime);
        runtime = ExecutionRuntimeController.AcceptQcReview(runtime);
        runtime = ObserveSafeAcceptanceForApply(runtime, workspaceRoot);
        var applied = AcceptedResultApplyProcessor.Apply(
            activeState,
            shift,
            currentTask,
            runtime,
            new DateTimeOffset(2026, 04, 05, 11, 10, 00, TimeSpan.Zero));
        var decisionSignal = DecisionSignalResolverV1.TryResolve(applied.AcceptedResult);
        var isHard = CheckpointDetectorV1.IsCheckpoint(applied.TaskState, nextIntent: null, decisionSignal);

        Console.WriteLine("[SCENARIO] HARD_CHECKPOINT");
        Console.WriteLine($"[HARD_SHORT_CIRCUIT] reason=decision_checkpoint; acceptedResultId={applied.AcceptedResult.AcceptedResultId}");

        AssertTrue(isHard, "Hard proof scenario must trigger hard checkpoint.");

        var closure = ShiftClosureProcessor.CloseAcceptedShift(
            applied.ProjectState,
            applied.ShiftState,
            new DateTimeOffset(2026, 04, 05, 11, 11, 00, TimeSpan.Zero),
            "Hard checkpoint closure.");

        LogPostAcceptState("changed_by_hard", closure.Shift, closure.ProjectState.ActiveShiftId, closure.ProjectState.ActiveTaskId);
        Console.WriteLine(
            $"[SNAPSHOT_EXAMPLE_CLOSURE] kind={closure.Snapshot!.CheckpointKind}; snapshotId={closure.Snapshot.SnapshotId}; score={closure.Snapshot.TriggerScore}; reasons={string.Join(",", closure.Snapshot.TriggerReasons ?? Array.Empty<string>())}; dedupeKey={closure.Snapshot.DedupeKey ?? "<null>"}");
    }
    finally
    {
        DeleteScratchWorkspace(workspaceRoot);
    }
}

static void ProofScenarioRepeatedAcceptLogsDedupe()
{
    var workspaceRoot = CreateScratchWorkspace();
    try
    {
        var initial = ProjectStateStorage.EnsureInitialized(workspaceRoot, "zavod-proof-dedupe", "ZAVOD Proof Dedupe");
        var previousTask = CreateCustomTaskState(
            "TASK-UI-000",
            TaskStateStatus.Completed,
            "Adjust result button spacing",
            new[] { "MainWindow.xaml" });
        var currentTask = CreateCustomTaskState(
            "TASK-CORE-001",
            TaskStateStatus.Active,
            "Adjust execution lifecycle state machine behavior",
            new[] { "Flow/StepPhaseMachine.cs" });
        var shift = new ShiftState(
            "SHIFT-001",
            "Continuous work shift",
            currentTask.TaskId,
            ShiftStateStatus.Active,
            new[] { previousTask, currentTask },
            new[] { "Pending review evidence" },
            Array.Empty<string>(),
            new[] { "Keep shift active" });
        var activeState = ProjectStateStorage.Save(initial with { ActiveShiftId = shift.ShiftId, ActiveTaskId = currentTask.TaskId });
        _ = ShiftStateStorage.Save(workspaceRoot, shift);

        var runtime = ExecutionRuntimeController.Begin(currentTask, shift);
        var providedResult = new WorkerExecutionResult(
            "RESULT-TASK-CORE-001-001",
            currentTask.TaskId,
            WorkerExecutionStatus.Success,
            "Adjusted lifecycle guards.",
            Array.Empty<IntakeArtifact>(),
            new[] { new WorkerExecutionModification("State/ResultAbandonProcessor.cs", "edit", "Adjust lifecycle guard.") },
            Array.Empty<ToolWarning>());
        runtime = ExecutionRuntimeController.ProduceProvidedResult(runtime, providedResult);
        runtime = ExecutionRuntimeController.RequestQcReview(runtime);
        runtime = ExecutionRuntimeController.AcceptQcReview(runtime);
        runtime = ObserveSafeAcceptanceForApply(runtime, workspaceRoot);
        var applied = AcceptedResultApplyProcessor.Apply(
            activeState,
            shift,
            currentTask,
            runtime,
            new DateTimeOffset(2026, 04, 05, 11, 20, 00, TimeSpan.Zero));
        var signal = SoftCheckpointSignalResolverV1.Resolve(applied.ShiftState, applied.TaskState, providedResult);

        Console.WriteLine("[SCENARIO] REPEATED_ACCEPT");
        var first = CheckpointSnapshotProcessor.WriteSoftCheckpoint(
            applied.ProjectState,
            applied.ShiftState,
            applied.AcceptedResult,
            applied.CommitRecord,
            signal.Score,
            signal.Reasons);
        var second = CheckpointSnapshotProcessor.WriteSoftCheckpoint(
            applied.ProjectState,
            applied.ShiftState,
            applied.AcceptedResult,
            applied.CommitRecord,
            signal.Score,
            signal.Reasons);
        LogPostAcceptState("unchanged_by_soft", applied.ShiftState, applied.ProjectState.ActiveShiftId, applied.ProjectState.ActiveTaskId);

        AssertTrue(first.WasCreated, "First repeated-accept proof write must create snapshot.");
        AssertFalse(second.WasCreated, "Second repeated-accept proof write must dedupe.");
    }
    finally
    {
        DeleteScratchWorkspace(workspaceRoot);
    }
}

static void AcceptedResultCannotBeAppliedTwice()
{
    var workspaceRoot = CreateScratchWorkspace();
    try
    {
        var initial = ProjectStateStorage.EnsureInitialized(workspaceRoot, "zavod-double-accept-guard", "ZAVOD Double Accept Guard");
        var activeState = ProjectStateStorage.Save(initial with { ActiveShiftId = "SHIFT-001", ActiveTaskId = "TASK-001" });
        var task = CreateTaskState(ContextIntentState.Validated, TaskStateStatus.Active, PromptRole.Worker);
        var shift = CreateShiftState(task) with { CurrentTaskId = task.TaskId };
        _ = ShiftStateStorage.Save(workspaceRoot, shift);

        var runtime = ExecutionRuntimeController.Begin(task, shift);
        runtime = ExecutionRuntimeController.ProduceResult(runtime);
        runtime = ExecutionRuntimeController.RequestQcReview(runtime);
        runtime = ExecutionRuntimeController.AcceptQcReview(runtime);
        runtime = ObserveSafeAcceptanceForApply(runtime, workspaceRoot);

        var applied = AcceptedResultApplyProcessor.Apply(
            activeState,
            shift,
            task,
            runtime,
            new DateTimeOffset(2026, 03, 31, 10, 10, 00, TimeSpan.Zero));

        AssertThrows<InvalidOperationException>(
            () => AcceptedResultApplyProcessor.Apply(
                applied.ProjectState,
                applied.ShiftState,
                applied.TaskState,
                runtime,
                new DateTimeOffset(2026, 03, 31, 10, 11, 00, TimeSpan.Zero)),
            "Accepted result apply must fail fast when the same result is applied twice.");
    }
    finally
    {
        DeleteScratchWorkspace(workspaceRoot);
    }
}

static void AcceptedShiftClosureFinalizesShiftAndClearsActiveBinding()
{
    var workspaceRoot = CreateScratchWorkspace();
    try
    {
        var initial = ProjectStateStorage.EnsureInitialized(workspaceRoot, "zavod-close-accepted-shift", "ZAVOD Close Accepted Shift");
        var activeState = ProjectStateStorage.Save(initial with { ActiveShiftId = "SHIFT-001", ActiveTaskId = null });
        var task = CreateTaskState(ContextIntentState.Validated, TaskStateStatus.Completed, PromptRole.Worker);
        var shift = CreateShiftState(task) with
        {
            CurrentTaskId = null,
            AcceptedResults = new[] { "COMMIT-001|task:TASK-001|Accepted change" }
        };
        _ = ShiftStateStorage.Save(workspaceRoot, shift);

        var closure = ShiftClosureProcessor.CloseAcceptedShift(
            activeState,
            shift,
            new DateTimeOffset(2026, 03, 31, 10, 30, 00, TimeSpan.Zero),
            "Close accepted shift.");

        var reloadedState = ProjectStateStorage.Load(workspaceRoot);
        var reloadedShift = ShiftStateStorage.Load(workspaceRoot, shift.ShiftId);

        AssertEqual(ShiftClosureStatus.Completed, closure.Status, "Accepted shift closure must complete successfully.");
        AssertEqual(ShiftStateStatus.Completed, closure.Shift.Status, "Accepted shift closure must finalize shift truth.");
        AssertEqual<string?>(null, closure.ProjectState.ActiveShiftId, "Accepted shift closure must clear active shift binding.");
        AssertEqual<string?>(null, closure.ProjectState.ActiveTaskId, "Accepted shift closure must keep active task empty.");
        AssertTrue(closure.Snapshot is not null, "Accepted shift closure must create snapshot.");
        AssertEqual(reloadedState, closure.ProjectState, "Persisted project state must match accepted shift closure.");
        AssertEqual(ShiftStateStatus.Completed, reloadedShift.Status, "Persisted shift must be completed after explicit close.");
    }
    finally
    {
        DeleteScratchWorkspace(workspaceRoot);
    }
}

static void PostAcceptPersistedShiftClosesExplicitlyThroughTruthBasedAcceptedContext()
{
    var workspaceRoot = CreateScratchWorkspace();
    try
    {
        var initial = ProjectStateStorage.EnsureInitialized(workspaceRoot, "zavod-post-accept-close", "ZAVOD Post Accept Close");
        var activeState = ProjectStateStorage.Save(initial with { ActiveShiftId = "SHIFT-001", ActiveTaskId = "TASK-001" });
        var task = CreateTaskState(ContextIntentState.Validated, TaskStateStatus.Active, PromptRole.Worker);
        var shift = CreateShiftState(task) with { CurrentTaskId = task.TaskId };
        _ = ShiftStateStorage.Save(workspaceRoot, shift);

        var runtime = ExecutionRuntimeController.Begin(task, shift);
        runtime = ExecutionRuntimeController.ProduceResult(runtime);
        runtime = ExecutionRuntimeController.RequestQcReview(runtime);
        runtime = ExecutionRuntimeController.AcceptQcReview(runtime);
        runtime = ObserveSafeAcceptanceForApply(runtime, workspaceRoot);

        _ = AcceptedResultApplyProcessor.Apply(
            activeState,
            shift,
            task,
            runtime,
            new DateTimeOffset(2026, 04, 01, 09, 00, 00, TimeSpan.Zero));

        var persistedProjectState = ProjectStateStorage.Load(workspaceRoot);
        AssertEqual<string?>("SHIFT-001", persistedProjectState.ActiveShiftId, "Post-accept persisted state must keep shift active until explicit close.");
        AssertEqual<string?>(null, persistedProjectState.ActiveTaskId, "Post-accept persisted state must clear only active task binding.");

        AssertTrue(
            AcceptedShiftClosureContextResolver.TryResolve(
                persistedProjectState,
                liveShiftState: null,
                shiftId => ShiftStateStorage.Load(workspaceRoot, shiftId),
                out var closureContext),
            "Persisted post-accept state must resolve accepted-shift closure context from truth.");

        var closure = ShiftClosureProcessor.CloseAcceptedShift(
            closureContext!.ProjectState,
            closureContext.ShiftState,
            new DateTimeOffset(2026, 04, 01, 09, 30, 00, TimeSpan.Zero),
            "Close shift after accepted first demo step.");

        var reloadedState = ProjectStateStorage.Load(workspaceRoot);
        var reloadedShift = ShiftStateStorage.Load(workspaceRoot, "SHIFT-001");
        var snapshotsRoot = Path.Combine(workspaceRoot, ".zavod", "snapshots");

        AssertEqual(ShiftClosureStatus.Completed, closure.Status, "Explicit post-accept close must complete successfully.");
        AssertEqual(ShiftStateStatus.Completed, reloadedShift.Status, "Explicit post-accept close must finalize shift truth.");
        AssertEqual<string?>(null, reloadedState.ActiveShiftId, "Explicit post-accept close must clear active shift binding.");
        AssertEqual<string?>(null, reloadedState.ActiveTaskId, "Explicit post-accept close must keep active task empty.");
        AssertTrue(closure.Snapshot is not null, "Explicit post-accept close must create snapshot.");
        AssertEqual(1, Directory.GetFiles(snapshotsRoot).Length, "Explicit post-accept close must persist exactly one snapshot.");
    }
    finally
    {
        DeleteScratchWorkspace(workspaceRoot);
    }
}

static void ResultAbandonPersistsAbandonedTaskAndKeepsShiftActive()
{
    var workspaceRoot = CreateScratchWorkspace();
    try
    {
        var initial = ProjectStateStorage.EnsureInitialized(workspaceRoot, "zavod-abandon-result", "ZAVOD Abandon Result");
        var activeState = ProjectStateStorage.Save(initial with { ActiveShiftId = "SHIFT-001", ActiveTaskId = "TASK-001" });
        var task = CreateTaskState(ContextIntentState.Validated, TaskStateStatus.Active, PromptRole.Worker);
        var shift = CreateShiftState(task) with { CurrentTaskId = task.TaskId };
        _ = ShiftStateStorage.Save(workspaceRoot, shift);

        var abandoned = ResultAbandonProcessor.Abandon(
            activeState,
            shift,
            task,
            new DateTimeOffset(2026, 03, 30, 12, 00, 00, TimeSpan.Zero));

        var reloadedState = ProjectStateStorage.Load(workspaceRoot);
        var reloadedShift = ShiftStateStorage.Load(workspaceRoot, shift.ShiftId);
        var persistedTask = reloadedShift.Tasks.Single(candidate => candidate.TaskId == task.TaskId);

        AssertEqual(TaskStateStatus.Abandoned, abandoned.TaskState.Status, "Result abandon must mark task as abandoned.");
        AssertEqual(ShiftStateStatus.Active, abandoned.ShiftState.Status, "Result abandon must keep shift active.");
        AssertEqual<string?>("SHIFT-001", abandoned.ProjectState.ActiveShiftId, "Result abandon must preserve active shift binding.");
        AssertEqual<string?>(null, abandoned.ProjectState.ActiveTaskId, "Result abandon must clear only active task binding.");
        AssertEqual(reloadedState, abandoned.ProjectState, "Persisted project state must match abandoned result mutation.");
        AssertEqual(TaskStateStatus.Abandoned, persistedTask.Status, "Persisted shift history must retain abandoned task truth.");
    }
    finally
    {
        DeleteScratchWorkspace(workspaceRoot);
    }
}

static void ResultRevisionKeepsActiveBindingsAndStartsNewExecutionCycle()
{
    var workspaceRoot = CreateScratchWorkspace();
    try
    {
        var initial = ProjectStateStorage.EnsureInitialized(workspaceRoot, "zavod-result-revision-cycle", "ZAVOD Result Revision Cycle");
        var activeState = ProjectStateStorage.Save(initial with { ActiveShiftId = "SHIFT-001", ActiveTaskId = "TASK-001" });
        var task = CreateTaskState(ContextIntentState.Validated, TaskStateStatus.Active, PromptRole.Worker);
        var shift = CreateShiftState(task) with { CurrentTaskId = task.TaskId };
        _ = ShiftStateStorage.Save(workspaceRoot, shift);

        var runtime = ExecutionRuntimeController.Begin(task, shift);
        runtime = ExecutionRuntimeController.ProduceResult(runtime);
        runtime = ExecutionRuntimeController.RequestQcReview(runtime);
        runtime = ExecutionRuntimeController.AcceptQcReview(runtime);

        var revisionIntake = StepPhaseMachine.ReturnForRevision(StepPhaseMachine.ResumeResult());
        var intakeProjection = StepPhaseProjectionBuilder.Build(revisionIntake);
        runtime = ExecutionRuntimeController.RestartCompletedResultForRevision(runtime, "Need one more pass.");
        var restartedPhase = StepPhaseMachine.StartRevisionCycle(revisionIntake);
        var restartedProjection = StepPhaseProjectionBuilder.Build(restartedPhase);

        AssertTrue(intakeProjection.ShowResult, "Revision intake must keep the previous result visible while revision is being requested.");
        AssertEqual(TaskStateStatus.Active, task.Status, "Result revision must keep task active for the next execution cycle.");
        AssertEqual(ShiftStateStatus.Active, shift.Status, "Result revision must keep the current shift active.");
        AssertEqual<string?>("TASK-001", activeState.ActiveTaskId, "Result revision must keep active task binding during the new execution cycle.");
        AssertEqual<string?>("SHIFT-001", activeState.ActiveShiftId, "Result revision must preserve active shift binding.");
        AssertEqual(ExecutionSessionState.InProgress, runtime.Session.State, "Result revision restart must start a new execution cycle.");
        AssertEqual(QCReviewStatus.NotStarted, runtime.QcStatus, "Result revision restart must require a fresh QC cycle.");
        AssertEqual<WorkerExecutionResult?>(null, runtime.Result, "Result revision restart must clear the current result before the new execution run.");
        AssertEqual(1, runtime.ResultHistory.Count, "Result revision restart must retain the previous result in history without faking a new one.");
        AssertEqual(SurfacePhase.Execution, restartedPhase.Phase, "Revision restart must return to execution phase.");
        AssertEqual(ExecutionSubphase.Running, restartedPhase.ExecutionSubphase, "Revision restart must resume running execution.");
        AssertTrue(restartedPhase.HasActiveShift, "Revision restart must preserve active shift phase truth.");
        AssertTrue(restartedPhase.HasActiveTask, "Revision restart must preserve active task phase truth.");
        AssertTrue(restartedProjection.ShowExecution, "Revision restart must expose the execution surface.");
        AssertFalse(restartedProjection.ShowResult, "Revision restart must clear revision intake visibility once the new execution cycle starts.");
    }
    finally
    {
        DeleteScratchWorkspace(workspaceRoot);
    }
}

static void AcceptedResultCannotBeAbandonedAfterApply()
{
    var workspaceRoot = CreateScratchWorkspace();
    try
    {
        var initial = ProjectStateStorage.EnsureInitialized(workspaceRoot, "zavod-accept-then-abandon-guard", "ZAVOD Accept Then Abandon Guard");
        var activeState = ProjectStateStorage.Save(initial with { ActiveShiftId = "SHIFT-001", ActiveTaskId = "TASK-001" });
        var task = CreateTaskState(ContextIntentState.Validated, TaskStateStatus.Active, PromptRole.Worker);
        var shift = CreateShiftState(task) with { CurrentTaskId = task.TaskId };
        _ = ShiftStateStorage.Save(workspaceRoot, shift);

        var runtime = ExecutionRuntimeController.Begin(task, shift);
        runtime = ExecutionRuntimeController.ProduceResult(runtime);
        runtime = ExecutionRuntimeController.RequestQcReview(runtime);
        runtime = ExecutionRuntimeController.AcceptQcReview(runtime);
        runtime = ObserveSafeAcceptanceForApply(runtime, workspaceRoot);

        var applied = AcceptedResultApplyProcessor.Apply(
            activeState,
            shift,
            task,
            runtime,
            new DateTimeOffset(2026, 03, 31, 10, 20, 00, TimeSpan.Zero));

        AssertThrows<InvalidOperationException>(
            () => ResultAbandonProcessor.Abandon(
                applied.ProjectState,
                applied.ShiftState,
                applied.TaskState,
                new DateTimeOffset(2026, 03, 31, 10, 21, 00, TimeSpan.Zero)),
            "Result abandon must fail fast after accepted result apply clears active task truth.");
    }
    finally
    {
        DeleteScratchWorkspace(workspaceRoot);
    }
}

static void AcceptedResultCannotBeReturnedForRevisionAfterApplyReentry()
{
    var postAcceptDiscussion = StepPhaseMachine.ResumeActiveShiftDiscussion();

    AssertThrows<InvalidOperationException>(
        () => StepPhaseMachine.ReturnForRevision(postAcceptDiscussion),
        "Result revision must fail fast after accepted result apply reentry leaves result phase.");
}

static void AbandonedResultRestartKeepsActiveShiftTruth()
{
    var workspaceRoot = CreateScratchWorkspace();
    try
    {
        var initial = ProjectStateStorage.EnsureInitialized(workspaceRoot, "zavod-abandon-restart", "ZAVOD Abandon Restart");
        var activeState = ProjectStateStorage.Save(initial with { ActiveShiftId = "SHIFT-001", ActiveTaskId = "TASK-001" });
        var task = CreateTaskState(ContextIntentState.Validated, TaskStateStatus.Active, PromptRole.Worker);
        var shift = CreateShiftState(task) with { CurrentTaskId = task.TaskId };
        _ = ShiftStateStorage.Save(workspaceRoot, shift);

        _ = ResultAbandonProcessor.Abandon(
            activeState,
            shift,
            task,
            new DateTimeOffset(2026, 03, 30, 12, 05, 00, TimeSpan.Zero));

        var reloadedState = ProjectStateStorage.Load(workspaceRoot);
        var persistedShift = ShiftStateStorage.Load(workspaceRoot, shift.ShiftId);
        var persistedTask = persistedShift.Tasks.Single(candidate => candidate.TaskId == task.TaskId);

        AssertEqual<string?>("SHIFT-001", reloadedState.ActiveShiftId, "Abandoned result restart must preserve active shift binding.");
        AssertEqual<string?>(null, reloadedState.ActiveTaskId, "Abandoned result restart must not revive an active task.");
        AssertEqual(ShiftStateStatus.Active, persistedShift.Status, "Abandoned result restart must keep the shift active.");
        AssertEqual(TaskStateStatus.Abandoned, persistedTask.Status, "Abandoned task history must remain persisted after restart.");
    }
    finally
    {
        DeleteScratchWorkspace(workspaceRoot);
    }
}

static void QcRejectRevisionAcceptLoopEnablesClosure()
{
    var task = CreateTaskState(ContextIntentState.Validated, TaskStateStatus.Active, PromptRole.Worker);
    var shift = CreateShiftState(task) with { CurrentTaskId = task.TaskId };

    var runtime = ExecutionRuntimeController.Begin(task, shift);
    runtime = ExecutionRuntimeController.ProduceResult(runtime);
    runtime = ExecutionRuntimeController.RequestQcReview(runtime);
    runtime = ExecutionRuntimeController.RejectQcReview(runtime, needsRevision: false, "Не пройдена проверка результата.");

    AssertEqual(ExecutionSessionState.ReturnedForRevision, runtime.Session.State, "Rejected QC must return execution to revision loop.");
    AssertEqual(QCReviewStatus.Rejected, runtime.QcStatus, "Rejected QC must stay visible in runtime-only QC status.");
    AssertEqual("Не пройдена проверка результата.", runtime.LastQcRejectReason, "Rejected QC must preserve last reject reason in runtime state.");
    AssertFalse(runtime.ClosureProposal.IsClosable, "Rejected QC must keep closure unavailable.");

    runtime = ExecutionRuntimeController.RestartRevision(runtime);
    AssertEqual(ExecutionSessionState.InProgress, runtime.Session.State, "Revision restart must return execution to InProgress.");
    AssertEqual(QCReviewStatus.NotStarted, runtime.QcStatus, "New revision loop must reset QC state.");
    AssertEqual("Не пройдена проверка результата.", runtime.LastQcRejectReason, "Restarting revision must keep last reject reason visible until a new result is produced.");

    runtime = ExecutionRuntimeController.ProduceResult(runtime);
    AssertEqual<string?>(null, runtime.LastQcRejectReason, "Producing a new result must clear previous reject reason.");
    runtime = ExecutionRuntimeController.RequestQcReview(runtime);
    runtime = ExecutionRuntimeController.AcceptQcReview(runtime);

    AssertEqual(ExecutionSessionState.Completed, runtime.Session.State, "Accepted revised result must complete runtime execution.");
    AssertEqual(QCReviewStatus.Accepted, runtime.QcStatus, "Accepted revised result must surface accepted QC state.");
    AssertTrue(runtime.ClosureProposal.IsClosable, "Accepted revised result must enable closure review.");
    AssertEqual(2, runtime.ResultHistory.Count, "Revision loop must append new runtime result history.");
}

static void CompletedResultRevisionRestartStartsNewWorkCycleWithoutQcReuse()
{
    var task = CreateTaskState(ContextIntentState.Validated, TaskStateStatus.Active, PromptRole.Worker);
    var shift = CreateShiftState(task) with { CurrentTaskId = task.TaskId };

    var runtime = ExecutionRuntimeController.Begin(task, shift);
    runtime = ExecutionRuntimeController.ProduceResult(runtime);
    runtime = ExecutionRuntimeController.RequestQcReview(runtime);
    runtime = ExecutionRuntimeController.AcceptQcReview(runtime);

    runtime = ExecutionRuntimeController.RestartCompletedResultForRevision(runtime, "Нужна доработка результата.");

    AssertEqual(ExecutionSessionState.InProgress, runtime.Session.State, "Completed result revision restart must start a new execution cycle.");
    AssertEqual(QCReviewStatus.NotStarted, runtime.QcStatus, "Completed result revision restart must reset QC state.");
    AssertEqual("Нужна доработка результата.", runtime.LastQcRejectReason, "Completed result revision restart must preserve revision reason.");
    AssertEqual<WorkerExecutionResult?>(null, runtime.Result, "Completed result revision restart must clear current result for the new cycle.");
    AssertFalse(runtime.ClosureProposal.IsClosable, "Completed result revision restart must block closure until a new result is produced.");

    var currentAttempt = runtime.Attempts.Single(attempt => attempt.AttemptIndex == runtime.CurrentAttemptIndex);
    AssertEqual(QCReviewStatus.NeedsRevision, currentAttempt.QcStatus, "Completed result revision restart must mark the current attempt as needs revision.");
    AssertEqual("Нужна доработка результата.", currentAttempt.RejectReason, "Completed result revision restart must keep the revision reason on the current attempt.");
}

static void QcAcceptWithoutResultFailsFast()
{
    var task = CreateTaskState(ContextIntentState.Validated, TaskStateStatus.Active, PromptRole.Worker);
    var shift = CreateShiftState(task) with { CurrentTaskId = task.TaskId };
    var runtime = ExecutionRuntimeController.Begin(task, shift);

    AssertThrows<InvalidOperationException>(
        () => ExecutionRuntimeController.AcceptQcReview(runtime),
        "QC acceptance must fail fast when no execution result exists.");
}

static void PostAcceptActiveShiftDiscussionAllowsFreshReadyIntentReentry()
{
    var discussion = StepPhaseMachine.RecordIntent(
        StepPhaseMachine.ResumeActiveShiftDiscussion(),
        ContextIntentState.ReadyForValidation);
    var discussionProjection = StepPhaseProjectionBuilder.Build(discussion);
    var preflight = StepPhaseMachine.EnterActiveShiftPreflight(discussion);
    var preflightProjection = StepPhaseProjectionBuilder.Build(preflight);

    AssertEqual(SurfacePhase.Discussion, discussion.Phase, "Post-accept reentry must return to discussion before a new task starts.");
    AssertTrue(discussion.HasActiveShift, "Post-accept reentry must preserve active shift truth.");
    AssertFalse(discussion.HasActiveTask, "Post-accept reentry must not resurrect the previous task.");
    AssertTrue(discussionProjection.CanStartIntentValidation, "Post-accept ready discussion must expose the validate CTA for the next intent.");
    AssertEqual(SurfacePhase.Execution, preflight.Phase, "Fresh post-accept intent must be able to enter execution preflight.");
    AssertEqual(ExecutionSubphase.Preflight, preflight.ExecutionSubphase, "Fresh post-accept intent must land in preflight.");
    AssertTrue(preflight.HasActiveShift, "Active shift truth must survive post-accept reentry into preflight.");
    AssertFalse(preflight.HasActiveTask, "Preflight after accept must still start without an active task.");
    AssertTrue(preflightProjection.CanConfirmPreflight, "Post-accept preflight must expose explicit execution start.");
}

static void InProgressCancelStopsExecutionWithoutProducingResult()
{
    var task = CreateTaskState(ContextIntentState.Validated, TaskStateStatus.Active, PromptRole.Worker);
    var shift = CreateShiftState(task) with { CurrentTaskId = task.TaskId };

    var runtime = ExecutionRuntimeController.Begin(task, shift);
    runtime = ExecutionRuntimeController.CancelInProgress(runtime, "user_cancelled");

    AssertEqual(ExecutionSessionState.Failed, runtime.Session.State, "In-progress cancel must stop runtime in failed state.");
    AssertEqual("user_cancelled", runtime.Session.FailureReason, "In-progress cancel must preserve cancel reason.");
    AssertEqual<WorkerExecutionResult?>(null, runtime.Result, "In-progress cancel must not fabricate result.");
    AssertEqual(0, runtime.ResultHistory.Count, "In-progress cancel must not append result history.");
    AssertEqual(QCReviewStatus.NotStarted, runtime.QcStatus, "In-progress cancel must not enter QC review.");
}

static void QcRejectForbidsAcceptWithoutNewResult()
{
    var task = CreateTaskState(ContextIntentState.Validated, TaskStateStatus.Active, PromptRole.Worker);
    var shift = CreateShiftState(task) with { CurrentTaskId = task.TaskId };

    var runtime = ExecutionRuntimeController.Begin(task, shift);
    runtime = ExecutionRuntimeController.ProduceResult(runtime);
    runtime = ExecutionRuntimeController.RequestQcReview(runtime);
    runtime = ExecutionRuntimeController.RejectQcReview(runtime, needsRevision: false, "Найдена ошибка в результате.");

    AssertThrows<InvalidOperationException>(
        () => ExecutionRuntimeController.AcceptQcReview(runtime),
        "Acceptance must stay forbidden after rejected QC until a new result is produced.");
    AssertFalse(runtime.ClosureProposal.IsClosable, "Rejected runtime state must not expose closure.");
}

static void MultipleRevisionLoopsRemainRuntimeOnlyAndAppendResultHistory()
{
    var task = CreateTaskState(ContextIntentState.Validated, TaskStateStatus.Active, PromptRole.Worker);
    var shift = CreateShiftState(task) with { CurrentTaskId = task.TaskId };

    var runtime = ExecutionRuntimeController.Begin(task, shift);

    for (var revision = 1; revision <= 3; revision++)
    {
        runtime = ExecutionRuntimeController.ProduceResult(runtime);
        AssertEqual(revision, runtime.ResultHistory.Count, "Each revision must append one runtime result.");
        AssertEqual(QCReviewStatus.NotStarted, runtime.QcStatus, "Producing a new result must reset QC state before review.");

        runtime = ExecutionRuntimeController.RequestQcReview(runtime);
        AssertEqual(QCReviewStatus.PendingReview, runtime.QcStatus, "Requesting QC must move runtime QC state to pending review.");

        if (revision < 3)
        {
            runtime = ExecutionRuntimeController.RejectQcReview(runtime, needsRevision: true, $"Нужна ревизия #{revision}.");
            AssertEqual(ExecutionSessionState.ReturnedForRevision, runtime.Session.State, "Needs-revision QC must return execution for another revision.");
            AssertEqual(QCReviewStatus.NeedsRevision, runtime.QcStatus, "Needs-revision decision must stay runtime-only QC status.");
            AssertEqual($"Нужна ревизия #{revision}.", runtime.LastQcRejectReason, "Needs-revision loop must keep the last reject reason in runtime state.");
            AssertFalse(runtime.ClosureProposal.IsClosable, "Needs-revision runtime state must keep closure blocked.");
            runtime = ExecutionRuntimeController.RestartRevision(runtime);
        }
        else
        {
            runtime = ExecutionRuntimeController.AcceptQcReview(runtime);
        }
    }

    AssertEqual(ExecutionSessionState.Completed, runtime.Session.State, "Final accepted revision must complete runtime execution.");
    AssertEqual(QCReviewStatus.Accepted, runtime.QcStatus, "Final accepted revision must expose accepted QC state.");
    AssertEqual<string?>(null, runtime.LastQcRejectReason, "Accepted final revision must clear stale reject reason.");
    AssertTrue(runtime.ClosureProposal.IsClosable, "Only final accepted revision may enable closure.");
}

static void QcRejectRequiresReason()
{
    var task = CreateTaskState(ContextIntentState.Validated, TaskStateStatus.Active, PromptRole.Worker);
    var shift = CreateShiftState(task) with { CurrentTaskId = task.TaskId };

    var runtime = ExecutionRuntimeController.Begin(task, shift);
    runtime = ExecutionRuntimeController.ProduceResult(runtime);
    runtime = ExecutionRuntimeController.RequestQcReview(runtime);

    AssertThrows<ArgumentException>(
        () => ExecutionRuntimeController.RejectQcReview(runtime, needsRevision: false, "   "),
        "QC reject must require non-empty reason.");
}

static void QcRejectReasonIsStoredInRuntimeState()
{
    var task = CreateTaskState(ContextIntentState.Validated, TaskStateStatus.Active, PromptRole.Worker);
    var shift = CreateShiftState(task) with { CurrentTaskId = task.TaskId };

    var runtime = ExecutionRuntimeController.Begin(task, shift);
    runtime = ExecutionRuntimeController.ProduceResult(runtime);
    runtime = ExecutionRuntimeController.RequestQcReview(runtime);
    runtime = ExecutionRuntimeController.RejectQcReview(runtime, needsRevision: false, "Нужно исправить нарушенный контракт.");

    AssertEqual("Нужно исправить нарушенный контракт.", runtime.LastQcRejectReason, "Runtime state must store last reject reason.");
    AssertEqual("Нужно исправить нарушенный контракт.", runtime.Review!.RejectReason, "QC review result must preserve reject reason.");
}

static void NewExecutionResultClearsPreviousRejectReason()
{
    var task = CreateTaskState(ContextIntentState.Validated, TaskStateStatus.Active, PromptRole.Worker);
    var shift = CreateShiftState(task) with { CurrentTaskId = task.TaskId };

    var runtime = ExecutionRuntimeController.Begin(task, shift);
    runtime = ExecutionRuntimeController.ProduceResult(runtime);
    runtime = ExecutionRuntimeController.RequestQcReview(runtime);
    runtime = ExecutionRuntimeController.RejectQcReview(runtime, needsRevision: false, "Требуется ещё одна правка.");
    runtime = ExecutionRuntimeController.RestartRevision(runtime);
    runtime = ExecutionRuntimeController.ProduceResult(runtime);

    AssertEqual(QCReviewStatus.NotStarted, runtime.QcStatus, "New execution result must reset QC state.");
    AssertEqual<string?>(null, runtime.LastQcRejectReason, "New execution result must clear previous reject reason.");
}

static void RejectReasonDoesNotChangeClosureEligibilityOnItsOwn()
{
    var task = CreateTaskState(ContextIntentState.Validated, TaskStateStatus.Active, PromptRole.Worker);
    var shift = CreateShiftState(task) with { CurrentTaskId = task.TaskId };

    var runtime = ExecutionRuntimeController.Begin(task, shift);
    runtime = ExecutionRuntimeController.ProduceResult(runtime);
    runtime = ExecutionRuntimeController.RequestQcReview(runtime);
    var rejected = ExecutionRuntimeController.RejectQcReview(runtime, needsRevision: false, "Причина отклонения.");
    var needsRevision = ExecutionRuntimeController.RejectQcReview(runtime, needsRevision: true, "Нужна отдельная ревизия.");

    AssertFalse(rejected.ClosureProposal.IsClosable, "Reject reason must not make closure eligible.");
    AssertFalse(needsRevision.ClosureProposal.IsClosable, "Needs-revision reason must not make closure eligible.");
}

static void MultipleRevisionLoopsUpdateLastRejectReason()
{
    var task = CreateTaskState(ContextIntentState.Validated, TaskStateStatus.Active, PromptRole.Worker);
    var shift = CreateShiftState(task) with { CurrentTaskId = task.TaskId };

    var runtime = ExecutionRuntimeController.Begin(task, shift);
    runtime = ExecutionRuntimeController.ProduceResult(runtime);
    runtime = ExecutionRuntimeController.RequestQcReview(runtime);
    runtime = ExecutionRuntimeController.RejectQcReview(runtime, needsRevision: false, "Первая причина.");
    AssertEqual("Первая причина.", runtime.LastQcRejectReason, "First revision loop must store first reject reason.");

    runtime = ExecutionRuntimeController.RestartRevision(runtime);
    runtime = ExecutionRuntimeController.ProduceResult(runtime);
    runtime = ExecutionRuntimeController.RequestQcReview(runtime);
    runtime = ExecutionRuntimeController.RejectQcReview(runtime, needsRevision: true, "Вторая причина.");

    AssertEqual("Вторая причина.", runtime.LastQcRejectReason, "Latest revision loop must replace last reject reason.");
    AssertEqual("Вторая причина.", runtime.Review!.RejectReason, "Latest QC review result must preserve latest reject reason.");
}

static void ExecutionAttemptsGrowOnEachNewResult()
{
    var task = CreateTaskState(ContextIntentState.Validated, TaskStateStatus.Active, PromptRole.Worker);
    var shift = CreateShiftState(task) with { CurrentTaskId = task.TaskId };

    var runtime = ExecutionRuntimeController.Begin(task, shift);
    AssertEqual(0, runtime.Attempts.Count, "Fresh runtime must start without attempts.");

    runtime = ExecutionRuntimeController.ProduceResult(runtime);
    AssertEqual(1, runtime.Attempts.Count, "First produced result must create first attempt.");
    AssertEqual(1, runtime.CurrentAttemptIndex, "Current attempt index must start at one.");

    runtime = ExecutionRuntimeController.RequestQcReview(runtime);
    runtime = ExecutionRuntimeController.RejectQcReview(runtime, needsRevision: false, "Первая попытка отклонена.");
    runtime = ExecutionRuntimeController.RestartRevision(runtime);
    runtime = ExecutionRuntimeController.ProduceResult(runtime);

    AssertEqual(2, runtime.Attempts.Count, "Each new produced result must append a new attempt.");
    AssertEqual(2, runtime.CurrentAttemptIndex, "Current attempt index must advance with each new result.");
}

static void ExecutionRejectMarksCurrentAttemptWithReason()
{
    var task = CreateTaskState(ContextIntentState.Validated, TaskStateStatus.Active, PromptRole.Worker);
    var shift = CreateShiftState(task) with { CurrentTaskId = task.TaskId };

    var runtime = ExecutionRuntimeController.Begin(task, shift);
    runtime = ExecutionRuntimeController.ProduceResult(runtime);
    runtime = ExecutionRuntimeController.RequestQcReview(runtime);
    runtime = ExecutionRuntimeController.RejectQcReview(runtime, needsRevision: false, "Требуется исправление контракта.");

    var attempt = runtime.Attempts.Single(attempt => attempt.AttemptIndex == runtime.CurrentAttemptIndex);
    AssertEqual(QCReviewStatus.Rejected, attempt.QcStatus, "Rejected QC must mark current attempt as rejected.");
    AssertEqual("Требуется исправление контракта.", attempt.RejectReason, "Rejected QC must preserve attempt reason.");
}

static void ExecutionAcceptMarksCurrentAttemptAsAccepted()
{
    var task = CreateTaskState(ContextIntentState.Validated, TaskStateStatus.Active, PromptRole.Worker);
    var shift = CreateShiftState(task) with { CurrentTaskId = task.TaskId };

    var runtime = ExecutionRuntimeController.Begin(task, shift);
    runtime = ExecutionRuntimeController.ProduceResult(runtime);
    runtime = ExecutionRuntimeController.RequestQcReview(runtime);
    runtime = ExecutionRuntimeController.AcceptQcReview(runtime);

    var attempt = runtime.Attempts.Single(attempt => attempt.AttemptIndex == runtime.CurrentAttemptIndex);
    AssertEqual(QCReviewStatus.Accepted, attempt.QcStatus, "Accepted QC must mark current attempt as accepted.");
    AssertEqual(ExecutionOutcomeStatus.NoOp, attempt.OutcomeStatus, "Accepted QC must mark current attempt as closure-ready runtime outcome.");
}

static void NewAttemptDoesNotOverwritePreviousAttempts()
{
    var task = CreateTaskState(ContextIntentState.Validated, TaskStateStatus.Active, PromptRole.Worker);
    var shift = CreateShiftState(task) with { CurrentTaskId = task.TaskId };

    var runtime = ExecutionRuntimeController.Begin(task, shift);
    runtime = ExecutionRuntimeController.ProduceResult(runtime);
    runtime = ExecutionRuntimeController.RequestQcReview(runtime);
    runtime = ExecutionRuntimeController.RejectQcReview(runtime, needsRevision: false, "Первая причина.");
    var firstAttempt = runtime.Attempts.Single(attempt => attempt.AttemptIndex == 1);

    runtime = ExecutionRuntimeController.RestartRevision(runtime);
    runtime = ExecutionRuntimeController.ProduceResult(runtime);

    AssertEqual("Первая причина.", runtime.Attempts.Single(attempt => attempt.AttemptIndex == 1).RejectReason, "Second attempt must not overwrite first attempt reason.");
    AssertEqual(firstAttempt, runtime.Attempts.Single(attempt => attempt.AttemptIndex == 1), "First attempt must stay unchanged after new attempt creation.");
    AssertEqual(2, runtime.Attempts.Count, "New attempt must append instead of overwrite.");
}

static void ExecutionAttemptsRemainRuntimeOnly()
{
    var workspaceRoot = CreateScratchWorkspace();
    try
    {
        var initial = ProjectStateStorage.EnsureInitialized(workspaceRoot, "zavod-runtime-attempts", "ZAVOD Runtime Attempts");
        var activeState = ProjectStateStorage.Save(initial with { ActiveShiftId = "SHIFT-001", ActiveTaskId = "TASK-001" });
        var task = CreateTaskState(ContextIntentState.Validated, TaskStateStatus.Active, PromptRole.Worker);
        var shift = CreateShiftState(task) with { CurrentTaskId = task.TaskId };
        _ = ShiftStateStorage.Save(workspaceRoot, shift);

        var runtime = ExecutionRuntimeController.Begin(task, shift);
        runtime = ExecutionRuntimeController.ProduceResult(runtime);
        runtime = ExecutionRuntimeController.RequestQcReview(runtime);
        runtime = ExecutionRuntimeController.RejectQcReview(runtime, needsRevision: false, "Runtime-only причина.");

        var reloadedState = ProjectStateStorage.Load(workspaceRoot);
        var persistedShift = ShiftStateStorage.Load(workspaceRoot, shift.ShiftId);

        AssertEqual(activeState, reloadedState, "Runtime attempts must not mutate persisted project state.");
        AssertEqual(ShiftStateStatus.Active, persistedShift.Status, "Runtime attempts must not mutate persisted shift truth.");
        AssertEqual(task.TaskId, persistedShift.CurrentTaskId, "Runtime attempts must not rebind persisted shift task.");
    }
    finally
    {
        DeleteScratchWorkspace(workspaceRoot);
    }
}

static void MultipleRevisionLoopsBuildExecutionAttemptChain()
{
    var task = CreateTaskState(ContextIntentState.Validated, TaskStateStatus.Active, PromptRole.Worker);
    var shift = CreateShiftState(task) with { CurrentTaskId = task.TaskId };

    var runtime = ExecutionRuntimeController.Begin(task, shift);
    runtime = ExecutionRuntimeController.ProduceResult(runtime);
    runtime = ExecutionRuntimeController.RequestQcReview(runtime);
    runtime = ExecutionRuntimeController.RejectQcReview(runtime, needsRevision: false, "Первая цепочка.");
    runtime = ExecutionRuntimeController.RestartRevision(runtime);
    runtime = ExecutionRuntimeController.ProduceResult(runtime);
    runtime = ExecutionRuntimeController.RequestQcReview(runtime);
    runtime = ExecutionRuntimeController.RejectQcReview(runtime, needsRevision: true, "Вторая цепочка.");
    runtime = ExecutionRuntimeController.RestartRevision(runtime);
    runtime = ExecutionRuntimeController.ProduceResult(runtime);
    runtime = ExecutionRuntimeController.RequestQcReview(runtime);
    runtime = ExecutionRuntimeController.AcceptQcReview(runtime);

    AssertEqual(3, runtime.Attempts.Count, "Multiple revisions must build full attempt chain.");
    AssertEqual(QCReviewStatus.Rejected, runtime.Attempts.Single(attempt => attempt.AttemptIndex == 1).QcStatus, "First attempt must stay rejected.");
    AssertEqual(QCReviewStatus.NeedsRevision, runtime.Attempts.Single(attempt => attempt.AttemptIndex == 2).QcStatus, "Second attempt must stay needs-revision.");
    AssertEqual(QCReviewStatus.Accepted, runtime.Attempts.Single(attempt => attempt.AttemptIndex == 3).QcStatus, "Last attempt must be accepted.");
    AssertTrue(runtime.ClosureProposal.IsClosable, "Closure must follow only the accepted final attempt.");
}

static void ExecutionTraceEntryIsBuiltDeterministicallyFromRunMutationAndSave()
{
    var workspaceRoot = CreateScratchWorkspace();
    try
    {
        var initial = ProjectStateStorage.EnsureInitialized(workspaceRoot, "zavod-trace", "ZAVOD Trace");
        var mutated = initial with { ActiveShiftId = "SHIFT-TRACE-001", ActiveTaskId = "TASK-TRACE-001" };
        var runResult = new ExecutionRunResult(
            new ExecutionOutcome(ExecutionTarget.IdleSubsystem, ExecutionOutcomeStatus.NoOp, "Idle noop"),
            new ExecutionRecord("SHIFT-TRACE-001", "TASK-TRACE-001", ExecutionTarget.IdleSubsystem, ExecutionOutcomeStatus.NoOp, "Idle noop"));
        var mutationResult = new ProjectStateMutationResult(mutated, ProjectStateMutationStatus.Mutated, "Mutated in memory");
        var saveResult = new ProjectStateSaveResult(
            mutated,
            ProjectStateSaveStatus.Skipped,
            WasPersisted: false,
            "Blocked outside closure.");

        var entry = ExecutionTraceBuilder.BuildEntry(runResult, mutationResult, saveResult);

        AssertEqual(ExecutionTarget.IdleSubsystem, entry.Target, "Trace entry must preserve execution target.");
        AssertEqual(ExecutionOutcomeStatus.NoOp, entry.OutcomeStatus, "Trace entry must preserve outcome status.");
        AssertEqual(ProjectStateMutationStatus.Mutated, entry.MutationStatus, "Trace entry must preserve mutation status.");
        AssertEqual(ProjectStateSaveStatus.Skipped, entry.SaveStatus, "Trace entry must preserve save status.");
        AssertEqual("Idle noop", entry.Message, "Trace entry must preserve execution message.");
    }
    finally
    {
        DeleteScratchWorkspace(workspaceRoot);
    }
}

static void InterruptedExecutionBuildsDeferredTraceRecordWithUserReason()
{
    var task = CreateTaskState(ContextIntentState.Validated, TaskStateStatus.Active, PromptRole.Worker);
    var shift = CreateShiftState(task) with { CurrentTaskId = task.TaskId };

    var runtime = ExecutionRuntimeController.Begin(task, shift);
    var interrupted = ExecutionRuntimeController.BuildInterruptedRunResult(runtime, "user_cancelled");

    AssertEqual(ExecutionTarget.ActiveShiftSubsystem, interrupted.Outcome.Target, "Interrupted execution must stay bound to the active shift subsystem.");
    AssertEqual(ExecutionOutcomeStatus.Deferred, interrupted.Outcome.Status, "Interrupted execution must remain deferred rather than completed.");
    AssertEqual(shift.ShiftId, interrupted.Record.ShiftId, "Interrupted execution trace must preserve shift identity.");
    AssertEqual(task.TaskId, interrupted.Record.TaskId, "Interrupted execution trace must preserve task identity.");
    AssertContains(interrupted.Record.Message ?? string.Empty, "user_cancelled", "Interrupted execution trace must include the user cancel reason.");
    AssertEqual(RuntimeIsolationLevel.ScopedWorkspace, interrupted.EffectiveRuntimeProfile.Isolation, "Interrupted execution must preserve runtime isolation.");
}

static void ExecutionTracePreservesPipelineStatuses()
{
    var runResult = new ExecutionRunResult(
        new ExecutionOutcome(ExecutionTarget.BootstrapSubsystem, ExecutionOutcomeStatus.Deferred, "Bootstrap deferred"),
        new ExecutionRecord("SHIFT-001", "TASK-001", ExecutionTarget.BootstrapSubsystem, ExecutionOutcomeStatus.Deferred, "Bootstrap deferred"));
    var mutationResult = new ProjectStateMutationResult(
        CreateProjectState(activeShiftId: "SHIFT-001", activeTaskId: "TASK-001"),
        ProjectStateMutationStatus.Unchanged,
        "Unchanged");
    var saveResult = new ProjectStateSaveResult(
        mutationResult.State,
        ProjectStateSaveStatus.Skipped,
        WasPersisted: false,
        "Skipped");

    var entry = ExecutionTraceBuilder.BuildEntry(runResult, mutationResult, saveResult);

    AssertEqual(ExecutionOutcomeStatus.Deferred, entry.OutcomeStatus, "Trace entry must preserve deferred outcome status.");
    AssertEqual(ProjectStateMutationStatus.Unchanged, entry.MutationStatus, "Trace entry must preserve unchanged mutation status.");
    AssertEqual(ProjectStateSaveStatus.Skipped, entry.SaveStatus, "Trace entry must preserve skipped save status.");
}

static void ExecutionTraceIsAppendOnly()
{
    var trace = ExecutionTraceBuilder.Start();
    var firstEntry = new ExecutionTraceEntry(
        ExecutionTarget.IdleSubsystem,
        ExecutionOutcomeStatus.NoOp,
        ProjectStateMutationStatus.Unchanged,
        ProjectStateSaveStatus.Skipped,
        "First");
    var secondEntry = new ExecutionTraceEntry(
        ExecutionTarget.BootstrapSubsystem,
        ExecutionOutcomeStatus.Deferred,
        ProjectStateMutationStatus.Unchanged,
        ProjectStateSaveStatus.Skipped,
        "Second");

    var first = ExecutionTraceBuilder.Append(trace, firstEntry);
    var second = ExecutionTraceBuilder.Append(first, secondEntry);

    AssertEqual(0, trace.Entries.Count, "Original trace must remain unchanged.");
    AssertEqual(1, first.Entries.Count, "First append must produce one-entry trace.");
    AssertEqual(2, second.Entries.Count, "Second append must produce two-entry trace.");
    AssertEqual("First", first.Entries[0].Message, "First append must preserve first entry.");
    AssertEqual("Second", second.Entries[1].Message, "Second append must preserve second entry order.");
}

static void ExecutionTraceBuilderHasNoSideEffects()
{
    var workspaceRoot = CreateScratchWorkspace();
    try
    {
        var runResult = new ExecutionRunResult(
            new ExecutionOutcome(ExecutionTarget.ActiveShiftSubsystem, ExecutionOutcomeStatus.Rejected, "Rejected"),
            new ExecutionRecord("SHIFT-001", "TASK-001", ExecutionTarget.ActiveShiftSubsystem, ExecutionOutcomeStatus.Rejected, "Rejected"));
        var mutationResult = new ProjectStateMutationResult(
            CreateProjectState(activeShiftId: "SHIFT-001", activeTaskId: "TASK-001", projectRoot: workspaceRoot),
            ProjectStateMutationStatus.Unchanged,
            "Unchanged");
        var saveResult = new ProjectStateSaveResult(
            mutationResult.State,
            ProjectStateSaveStatus.Skipped,
            WasPersisted: false,
            "Skipped");
        var metaPath = Path.Combine(workspaceRoot, ".zavod", "meta", "project.json");

        _ = ExecutionTraceBuilder.BuildEntry(runResult, mutationResult, saveResult);
        _ = ExecutionTraceBuilder.Append(ExecutionTraceBuilder.Start(), new ExecutionTraceEntry(
            ExecutionTarget.ActiveShiftSubsystem,
            ExecutionOutcomeStatus.Rejected,
            ProjectStateMutationStatus.Unchanged,
            ProjectStateSaveStatus.Skipped,
            "Rejected"));

        AssertFalse(File.Exists(metaPath), "Execution trace builder must not write persistence files.");
    }
    finally
    {
        DeleteScratchWorkspace(workspaceRoot);
    }
}

static void ShiftTraceEntryIsBuiltDeterministicallyFromExecutionTraceEntry()
{
    var executionEntry = new ExecutionTraceEntry(
        ExecutionTarget.IdleSubsystem,
        ExecutionOutcomeStatus.NoOp,
        ProjectStateMutationStatus.Unchanged,
        ProjectStateSaveStatus.Skipped,
        "Idle noop");

    var shiftEntry = ShiftTraceBuilder.BuildEntry(executionEntry);

    AssertEqual(executionEntry.Target, shiftEntry.Target, "Shift trace entry must preserve execution target.");
    AssertEqual(executionEntry.OutcomeStatus, shiftEntry.OutcomeStatus, "Shift trace entry must preserve outcome status.");
    AssertEqual(executionEntry.SaveStatus, shiftEntry.SaveStatus, "Shift trace entry must preserve save status.");
    AssertEqual(executionEntry.Message, shiftEntry.Message, "Shift trace entry must preserve message.");
    AssertFalse(shiftEntry.IsShiftRelevant, "Idle execution path must not be marked shift-relevant.");
}

static void ShiftTraceMarksActiveShiftTargetAsRelevant()
{
    var executionEntry = new ExecutionTraceEntry(
        ExecutionTarget.ActiveShiftSubsystem,
        ExecutionOutcomeStatus.Rejected,
        ProjectStateMutationStatus.Unchanged,
        ProjectStateSaveStatus.Skipped,
        "Rejected active shift");

    var shiftEntry = ShiftTraceBuilder.BuildEntry(executionEntry);

    AssertTrue(shiftEntry.IsShiftRelevant, "Active shift execution path must be marked shift-relevant.");
}

static void ShiftTraceIsAppendOnly()
{
    var trace = ShiftTraceBuilder.Start();
    var firstEntry = new ShiftTraceEntry(
        ExecutionTarget.IdleSubsystem,
        ExecutionOutcomeStatus.NoOp,
        ProjectStateSaveStatus.Skipped,
        false,
        "First");
    var secondEntry = new ShiftTraceEntry(
        ExecutionTarget.ActiveShiftSubsystem,
        ExecutionOutcomeStatus.Rejected,
        ProjectStateSaveStatus.Skipped,
        true,
        "Second");

    var first = ShiftTraceBuilder.Append(trace, firstEntry);
    var second = ShiftTraceBuilder.Append(first, secondEntry);

    AssertEqual(0, trace.Entries.Count, "Original shift trace must remain unchanged.");
    AssertEqual(1, first.Entries.Count, "First append must produce one-entry shift trace.");
    AssertEqual(2, second.Entries.Count, "Second append must produce two-entry shift trace.");
    AssertEqual("First", first.Entries[0].Message, "First append must preserve first shift trace entry.");
    AssertEqual("Second", second.Entries[1].Message, "Second append must preserve second shift trace entry order.");
}

static void ShiftTraceBuilderHasNoSideEffects()
{
    var workspaceRoot = CreateScratchWorkspace();
    try
    {
        var executionEntry = new ExecutionTraceEntry(
            ExecutionTarget.ActiveShiftSubsystem,
            ExecutionOutcomeStatus.Rejected,
            ProjectStateMutationStatus.Unchanged,
            ProjectStateSaveStatus.Skipped,
            "Rejected");
        var metaPath = Path.Combine(workspaceRoot, ".zavod", "meta", "project.json");

        _ = ShiftTraceBuilder.BuildEntry(executionEntry);
        _ = ShiftTraceBuilder.Append(ShiftTraceBuilder.Start(), new ShiftTraceEntry(
            ExecutionTarget.ActiveShiftSubsystem,
            ExecutionOutcomeStatus.Rejected,
            ProjectStateSaveStatus.Skipped,
            true,
            "Rejected"));

        AssertFalse(File.Exists(metaPath), "Shift trace builder must not write persistence files.");
    }
    finally
    {
        DeleteScratchWorkspace(workspaceRoot);
    }
}

static void SnapshotIsBuiltCorrectlyFromShiftTrace()
{
    var trace = ShiftTraceBuilder.Start();
    trace = ShiftTraceBuilder.Append(trace, new ShiftTraceEntry(
        ExecutionTarget.ActiveShiftSubsystem,
        ExecutionOutcomeStatus.Rejected,
        ProjectStateSaveStatus.Skipped,
        true,
        "Rejected active shift"));

    var snapshot = ExecutionSnapshotBuilder.Build(trace);

    AssertEqual(ExecutionTarget.ActiveShiftSubsystem, snapshot.LastExecutionTarget, "Snapshot must preserve last shift-relevant target.");
    AssertEqual(ExecutionOutcomeStatus.Rejected, snapshot.LastOutcomeStatus, "Snapshot must preserve last shift-relevant outcome status.");
    AssertEqual(ProjectStateSaveStatus.Skipped, snapshot.LastSaveStatus, "Snapshot must preserve last shift-relevant save status.");
    AssertEqual(1, snapshot.ShiftRelevantEntriesCount, "Snapshot must count shift-relevant entries.");
    AssertEqual("Rejected active shift", snapshot.LastMessage, "Snapshot must preserve last shift-relevant message.");
}

static void EmptyShiftTraceYieldsEmptySnapshot()
{
    var snapshot = ExecutionSnapshotBuilder.Build(ShiftTraceBuilder.Start());

    AssertEqual<ExecutionTarget?>(null, snapshot.LastExecutionTarget, "Empty shift trace must produce empty snapshot target.");
    AssertEqual<ExecutionOutcomeStatus?>(null, snapshot.LastOutcomeStatus, "Empty shift trace must produce empty snapshot outcome.");
    AssertEqual<ProjectStateSaveStatus?>(null, snapshot.LastSaveStatus, "Empty shift trace must produce empty snapshot save status.");
    AssertEqual(0, snapshot.ShiftRelevantEntriesCount, "Empty shift trace must have zero relevant entries.");
    AssertEqual<string?>(null, snapshot.LastMessage, "Empty shift trace must have no last message.");
}

static void SnapshotOnlyUsesShiftRelevantEntries()
{
    var trace = ShiftTraceBuilder.Start();
    trace = ShiftTraceBuilder.Append(trace, new ShiftTraceEntry(
        ExecutionTarget.IdleSubsystem,
        ExecutionOutcomeStatus.NoOp,
        ProjectStateSaveStatus.Skipped,
        false,
        "Idle"));
    trace = ShiftTraceBuilder.Append(trace, new ShiftTraceEntry(
        ExecutionTarget.ActiveShiftSubsystem,
        ExecutionOutcomeStatus.Deferred,
        ProjectStateSaveStatus.Persisted,
        true,
        "Active shift"));
    trace = ShiftTraceBuilder.Append(trace, new ShiftTraceEntry(
        ExecutionTarget.BootstrapSubsystem,
        ExecutionOutcomeStatus.Deferred,
        ProjectStateSaveStatus.Skipped,
        false,
        "Bootstrap"));

    var snapshot = ExecutionSnapshotBuilder.Build(trace);

    AssertEqual(ExecutionTarget.ActiveShiftSubsystem, snapshot.LastExecutionTarget, "Snapshot must ignore non-shift-relevant trailing entries.");
    AssertEqual(1, snapshot.ShiftRelevantEntriesCount, "Snapshot must count only shift-relevant entries.");
    AssertEqual("Active shift", snapshot.LastMessage, "Snapshot must use last shift-relevant message only.");
}

static void SnapshotBuilderHasNoSideEffects()
{
    var workspaceRoot = CreateScratchWorkspace();
    try
    {
        var trace = ShiftTraceBuilder.Start();
        trace = ShiftTraceBuilder.Append(trace, new ShiftTraceEntry(
            ExecutionTarget.ActiveShiftSubsystem,
            ExecutionOutcomeStatus.Deferred,
            ProjectStateSaveStatus.Skipped,
            true,
            "Deferred"));
        var metaPath = Path.Combine(workspaceRoot, ".zavod", "meta", "project.json");

        _ = ExecutionSnapshotBuilder.Build(trace);

        AssertFalse(File.Exists(metaPath), "Snapshot builder must not write persistence files.");
    }
    finally
    {
        DeleteScratchWorkspace(workspaceRoot);
    }
}

static void CapsuleIsBuiltCorrectlyFromSnapshot()
{
    var snapshot = new ExecutionRuntimeSnapshot(
        ExecutionTarget.ActiveShiftSubsystem,
        ExecutionOutcomeStatus.Rejected,
        ProjectStateSaveStatus.Skipped,
        ShiftRelevantEntriesCount: 2,
        LastMessage: "Rejected active shift");

    var capsule = ExecutionCapsuleBuilder.Build(snapshot);

    AssertEqual(snapshot.LastExecutionTarget, capsule.CurrentExecutionTarget, "Capsule must preserve current execution target.");
    AssertEqual(snapshot.LastOutcomeStatus, capsule.CurrentOutcomeStatus, "Capsule must preserve current outcome status.");
    AssertEqual(snapshot.LastSaveStatus, capsule.CurrentSaveStatus, "Capsule must preserve current save status.");
    AssertTrue(capsule.HasShiftActivity, "Capsule must report shift activity when snapshot has relevant entries.");
}

static void EmptySnapshotYieldsEmptyCapsule()
{
    var snapshot = new ExecutionRuntimeSnapshot(
        LastExecutionTarget: null,
        LastOutcomeStatus: null,
        LastSaveStatus: null,
        ShiftRelevantEntriesCount: 0,
        LastMessage: null);

    var capsule = ExecutionCapsuleBuilder.Build(snapshot);

    AssertEqual<ExecutionTarget?>(null, capsule.CurrentExecutionTarget, "Empty snapshot must produce empty capsule target.");
    AssertEqual<ExecutionOutcomeStatus?>(null, capsule.CurrentOutcomeStatus, "Empty snapshot must produce empty capsule outcome.");
    AssertEqual<ProjectStateSaveStatus?>(null, capsule.CurrentSaveStatus, "Empty snapshot must produce empty capsule save status.");
    AssertFalse(capsule.HasShiftActivity, "Empty snapshot must not report shift activity.");
    AssertEqual("No shift activity.", capsule.SummaryLine, "Empty snapshot must produce deterministic empty summary.");
}

static void CapsuleHasShiftActivityIsDerivedHonestly()
{
    var activeSnapshot = new ExecutionRuntimeSnapshot(
        ExecutionTarget.ActiveShiftSubsystem,
        ExecutionOutcomeStatus.Deferred,
        ProjectStateSaveStatus.Skipped,
        ShiftRelevantEntriesCount: 1,
        LastMessage: "Deferred");
    var emptySnapshot = new ExecutionRuntimeSnapshot(
        null,
        null,
        null,
        ShiftRelevantEntriesCount: 0,
        LastMessage: null);

    var activeCapsule = ExecutionCapsuleBuilder.Build(activeSnapshot);
    var emptyCapsule = ExecutionCapsuleBuilder.Build(emptySnapshot);

    AssertTrue(activeCapsule.HasShiftActivity, "Positive shift-relevant count must yield HasShiftActivity=true.");
    AssertFalse(emptyCapsule.HasShiftActivity, "Zero shift-relevant count must yield HasShiftActivity=false.");
}

static void CapsuleSummaryLineIsDeterministic()
{
    var snapshot = new ExecutionRuntimeSnapshot(
        ExecutionTarget.ActiveShiftSubsystem,
        ExecutionOutcomeStatus.Deferred,
        ProjectStateSaveStatus.Persisted,
        ShiftRelevantEntriesCount: 3,
        LastMessage: "Persisted deferred");

    var first = ExecutionCapsuleBuilder.Build(snapshot);
    var second = ExecutionCapsuleBuilder.Build(snapshot);

    AssertEqual(first.SummaryLine, second.SummaryLine, "Capsule summary line must be deterministic.");
    AssertContains(first.SummaryLine, "ActiveShiftSubsystem", "Capsule summary must include execution target.");
    AssertContains(first.SummaryLine, "Deferred", "Capsule summary must include outcome status.");
}

static void CapsuleBuilderHasNoSideEffects()
{
    var workspaceRoot = CreateScratchWorkspace();
    try
    {
        var snapshot = new ExecutionRuntimeSnapshot(
            ExecutionTarget.ActiveShiftSubsystem,
            ExecutionOutcomeStatus.Rejected,
            ProjectStateSaveStatus.Skipped,
            ShiftRelevantEntriesCount: 1,
            LastMessage: "Rejected");
        var metaPath = Path.Combine(workspaceRoot, ".zavod", "meta", "project.json");

        _ = ExecutionCapsuleBuilder.Build(snapshot);

        AssertFalse(File.Exists(metaPath), "Capsule builder must not write persistence files.");
    }
    finally
    {
        DeleteScratchWorkspace(workspaceRoot);
    }
}

static void EntryPackIsBuiltCorrectlyFromCapsuleAndSnapshot()
{
    var snapshot = new ExecutionRuntimeSnapshot(
        ExecutionTarget.ActiveShiftSubsystem,
        ExecutionOutcomeStatus.Deferred,
        ProjectStateSaveStatus.Persisted,
        ShiftRelevantEntriesCount: 2,
        LastMessage: "Persisted deferred");
    var capsule = new ExecutionRuntimeCapsule(
        ExecutionTarget.ActiveShiftSubsystem,
        ExecutionOutcomeStatus.Deferred,
        ProjectStateSaveStatus.Persisted,
        HasShiftActivity: true,
        SummaryLine: "Last shift activity: ActiveShiftSubsystem / Deferred / Persisted.");

    var entryPack = ExecutionEntryPackBuilder.Build(capsule, snapshot);

    AssertEqual(capsule, entryPack.Capsule, "Entry pack must preserve runtime capsule.");
    AssertEqual(snapshot, entryPack.Snapshot, "Entry pack must preserve runtime snapshot.");
    AssertTrue(entryPack.HasExecutionContext, "Entry pack must detect execution context when target and outcome are present.");
    AssertTrue(entryPack.HasShiftActivity, "Entry pack must preserve shift activity fact from capsule.");
}

static void EmptyCapsuleAndSnapshotYieldEmptyEntryPack()
{
    var snapshot = new ExecutionRuntimeSnapshot(
        LastExecutionTarget: null,
        LastOutcomeStatus: null,
        LastSaveStatus: null,
        ShiftRelevantEntriesCount: 0,
        LastMessage: null);
    var capsule = new ExecutionRuntimeCapsule(
        CurrentExecutionTarget: null,
        CurrentOutcomeStatus: null,
        CurrentSaveStatus: null,
        HasShiftActivity: false,
        SummaryLine: "No shift activity.");

    var entryPack = ExecutionEntryPackBuilder.Build(capsule, snapshot);

    AssertFalse(entryPack.HasExecutionContext, "Empty capsule must yield no execution context.");
    AssertFalse(entryPack.HasShiftActivity, "Empty capsule must yield no shift activity.");
    AssertEqual("No execution context. Shift activity: False.", entryPack.EntryLine, "Empty inputs must produce deterministic empty entry line.");
}

static void EntryPackHasExecutionContextIsDerivedHonestly()
{
    var contextualCapsule = new ExecutionRuntimeCapsule(
        ExecutionTarget.ActiveShiftSubsystem,
        ExecutionOutcomeStatus.Deferred,
        ProjectStateSaveStatus.Skipped,
        HasShiftActivity: true,
        SummaryLine: "Context present.");
    var emptyCapsule = new ExecutionRuntimeCapsule(
        CurrentExecutionTarget: null,
        CurrentOutcomeStatus: null,
        CurrentSaveStatus: null,
        HasShiftActivity: false,
        SummaryLine: "No shift activity.");
    var snapshot = new ExecutionRuntimeSnapshot(
        LastExecutionTarget: null,
        LastOutcomeStatus: null,
        LastSaveStatus: null,
        ShiftRelevantEntriesCount: 0,
        LastMessage: null);

    var contextualPack = ExecutionEntryPackBuilder.Build(contextualCapsule, snapshot);
    var emptyPack = ExecutionEntryPackBuilder.Build(emptyCapsule, snapshot);

    AssertTrue(contextualPack.HasExecutionContext, "Target and outcome must yield HasExecutionContext=true.");
    AssertFalse(emptyPack.HasExecutionContext, "Missing target/outcome must yield HasExecutionContext=false.");
}

static void EntryPackLineIsDeterministic()
{
    var snapshot = new ExecutionRuntimeSnapshot(
        ExecutionTarget.ActiveShiftSubsystem,
        ExecutionOutcomeStatus.Deferred,
        ProjectStateSaveStatus.Skipped,
        ShiftRelevantEntriesCount: 1,
        LastMessage: "Deferred");
    var capsule = new ExecutionRuntimeCapsule(
        ExecutionTarget.ActiveShiftSubsystem,
        ExecutionOutcomeStatus.Deferred,
        ProjectStateSaveStatus.Skipped,
        HasShiftActivity: true,
        SummaryLine: "Last shift activity: ActiveShiftSubsystem / Deferred / Skipped.");

    var first = ExecutionEntryPackBuilder.Build(capsule, snapshot);
    var second = ExecutionEntryPackBuilder.Build(capsule, snapshot);

    AssertEqual(first.EntryLine, second.EntryLine, "Entry line must be deterministic.");
    AssertContains(first.EntryLine, "ActiveShiftSubsystem", "Entry line must include execution target.");
    AssertContains(first.EntryLine, "Deferred", "Entry line must include outcome status.");
}

static void EntryPackBuilderHasNoSideEffects()
{
    var workspaceRoot = CreateScratchWorkspace();
    try
    {
        var snapshot = new ExecutionRuntimeSnapshot(
            ExecutionTarget.ActiveShiftSubsystem,
            ExecutionOutcomeStatus.Deferred,
            ProjectStateSaveStatus.Skipped,
            ShiftRelevantEntriesCount: 1,
            LastMessage: "Deferred");
        var capsule = new ExecutionRuntimeCapsule(
            ExecutionTarget.ActiveShiftSubsystem,
            ExecutionOutcomeStatus.Deferred,
            ProjectStateSaveStatus.Skipped,
            HasShiftActivity: true,
            SummaryLine: "Last shift activity: ActiveShiftSubsystem / Deferred / Skipped.");
        var metaPath = Path.Combine(workspaceRoot, ".zavod", "meta", "project.json");

        _ = ExecutionEntryPackBuilder.Build(capsule, snapshot);

        AssertFalse(File.Exists(metaPath), "Entry pack builder must not write persistence files.");
    }
    finally
    {
        DeleteScratchWorkspace(workspaceRoot);
    }
}

static void TaskStateIsBuiltCorrectlyFromEntryPack()
{
    var snapshot = new ExecutionRuntimeSnapshot(
        ExecutionTarget.ActiveShiftSubsystem,
        ExecutionOutcomeStatus.Deferred,
        ProjectStateSaveStatus.Persisted,
        ShiftRelevantEntriesCount: 2,
        LastMessage: "Persisted deferred");
    var capsule = new ExecutionRuntimeCapsule(
        ExecutionTarget.ActiveShiftSubsystem,
        ExecutionOutcomeStatus.Deferred,
        ProjectStateSaveStatus.Persisted,
        HasShiftActivity: true,
        SummaryLine: "Last shift activity: ActiveShiftSubsystem / Deferred / Persisted.");
    var entryPack = ExecutionEntryPackBuilder.Build(capsule, snapshot);

    var taskState = ExecutionTaskStateBuilder.Build(entryPack);

    AssertTrue(taskState.HasExecutionContext, "Task state must preserve execution context fact.");
    AssertTrue(taskState.HasShiftActivity, "Task state must preserve shift activity fact.");
    AssertEqual(ExecutionTarget.ActiveShiftSubsystem, taskState.CurrentExecutionTarget, "Task state must preserve current execution target.");
    AssertEqual(ExecutionOutcomeStatus.Deferred, taskState.CurrentOutcomeStatus, "Task state must preserve current outcome status.");
}

static void EmptyEntryPackYieldsEmptyTaskState()
{
    var snapshot = new ExecutionRuntimeSnapshot(
        LastExecutionTarget: null,
        LastOutcomeStatus: null,
        LastSaveStatus: null,
        ShiftRelevantEntriesCount: 0,
        LastMessage: null);
    var capsule = new ExecutionRuntimeCapsule(
        CurrentExecutionTarget: null,
        CurrentOutcomeStatus: null,
        CurrentSaveStatus: null,
        HasShiftActivity: false,
        SummaryLine: "No shift activity.");
    var entryPack = ExecutionEntryPackBuilder.Build(capsule, snapshot);

    var taskState = ExecutionTaskStateBuilder.Build(entryPack);

    AssertFalse(taskState.HasExecutionContext, "Empty entry pack must yield no execution context.");
    AssertFalse(taskState.HasShiftActivity, "Empty entry pack must yield no shift activity.");
    AssertEqual<ExecutionTarget?>(null, taskState.CurrentExecutionTarget, "Empty entry pack must yield empty current target.");
    AssertEqual<ExecutionOutcomeStatus?>(null, taskState.CurrentOutcomeStatus, "Empty entry pack must yield empty current outcome.");
    AssertEqual("No task execution context. Shift activity: False.", taskState.TaskLine, "Empty entry pack must produce deterministic empty task line.");
}

static void TaskStateCurrentFieldsReflectCapsuleTruth()
{
    var snapshot = new ExecutionRuntimeSnapshot(
        ExecutionTarget.ActiveShiftSubsystem,
        ExecutionOutcomeStatus.Rejected,
        ProjectStateSaveStatus.Skipped,
        ShiftRelevantEntriesCount: 1,
        LastMessage: "Rejected");
    var capsule = new ExecutionRuntimeCapsule(
        ExecutionTarget.ActiveShiftSubsystem,
        ExecutionOutcomeStatus.Rejected,
        ProjectStateSaveStatus.Skipped,
        HasShiftActivity: true,
        SummaryLine: "Last shift activity: ActiveShiftSubsystem / Rejected / Skipped.");
    var entryPack = ExecutionEntryPackBuilder.Build(capsule, snapshot);

    var taskState = ExecutionTaskStateBuilder.Build(entryPack);

    AssertEqual(capsule.CurrentExecutionTarget, taskState.CurrentExecutionTarget, "Task state current target must mirror capsule truth.");
    AssertEqual(capsule.CurrentOutcomeStatus, taskState.CurrentOutcomeStatus, "Task state current outcome must mirror capsule truth.");
}

static void TaskStateLineIsDeterministic()
{
    var snapshot = new ExecutionRuntimeSnapshot(
        ExecutionTarget.ActiveShiftSubsystem,
        ExecutionOutcomeStatus.Deferred,
        ProjectStateSaveStatus.Skipped,
        ShiftRelevantEntriesCount: 1,
        LastMessage: "Deferred");
    var capsule = new ExecutionRuntimeCapsule(
        ExecutionTarget.ActiveShiftSubsystem,
        ExecutionOutcomeStatus.Deferred,
        ProjectStateSaveStatus.Skipped,
        HasShiftActivity: true,
        SummaryLine: "Last shift activity: ActiveShiftSubsystem / Deferred / Skipped.");
    var entryPack = ExecutionEntryPackBuilder.Build(capsule, snapshot);

    var first = ExecutionTaskStateBuilder.Build(entryPack);
    var second = ExecutionTaskStateBuilder.Build(entryPack);

    AssertEqual(first.TaskLine, second.TaskLine, "Task line must be deterministic.");
    AssertContains(first.TaskLine, "ActiveShiftSubsystem", "Task line must include execution target.");
    AssertContains(first.TaskLine, "Deferred", "Task line must include outcome status.");
}

static void TaskStateBuilderHasNoSideEffects()
{
    var workspaceRoot = CreateScratchWorkspace();
    try
    {
        var snapshot = new ExecutionRuntimeSnapshot(
            ExecutionTarget.ActiveShiftSubsystem,
            ExecutionOutcomeStatus.Deferred,
            ProjectStateSaveStatus.Skipped,
            ShiftRelevantEntriesCount: 1,
            LastMessage: "Deferred");
        var capsule = new ExecutionRuntimeCapsule(
            ExecutionTarget.ActiveShiftSubsystem,
            ExecutionOutcomeStatus.Deferred,
            ProjectStateSaveStatus.Skipped,
            HasShiftActivity: true,
            SummaryLine: "Last shift activity: ActiveShiftSubsystem / Deferred / Skipped.");
        var entryPack = ExecutionEntryPackBuilder.Build(capsule, snapshot);
        var metaPath = Path.Combine(workspaceRoot, ".zavod", "meta", "project.json");

        _ = ExecutionTaskStateBuilder.Build(entryPack);

        AssertFalse(File.Exists(metaPath), "Task state builder must not write persistence files.");
    }
    finally
    {
        DeleteScratchWorkspace(workspaceRoot);
    }
}

static void TaskViewIsBuiltCorrectlyFromExecutionTaskState()
{
    var taskState = new ExecutionRuntimeTaskState(
        HasExecutionContext: true,
        HasShiftActivity: true,
        CurrentExecutionTarget: ExecutionTarget.ActiveShiftSubsystem,
        CurrentOutcomeStatus: ExecutionOutcomeStatus.Deferred,
        TaskLine: "Task context: ActiveShiftSubsystem / Deferred. Shift activity: True.");

    var taskView = ExecutionTaskViewBuilder.Build(taskState);

    AssertTrue(taskView.HasExecutionContext, "Task view must preserve execution context fact.");
    AssertTrue(taskView.HasShiftActivity, "Task view must preserve shift activity fact.");
    AssertEqual(ExecutionTarget.ActiveShiftSubsystem, taskView.CurrentExecutionTarget, "Task view must preserve current execution target.");
    AssertEqual(ExecutionOutcomeStatus.Deferred, taskView.CurrentOutcomeStatus, "Task view must preserve current outcome status.");
}

static void EmptyTaskStateYieldsEmptyTaskView()
{
    var taskState = new ExecutionRuntimeTaskState(
        HasExecutionContext: false,
        HasShiftActivity: false,
        CurrentExecutionTarget: null,
        CurrentOutcomeStatus: null,
        TaskLine: "No task execution context. Shift activity: False.");

    var taskView = ExecutionTaskViewBuilder.Build(taskState);

    AssertFalse(taskView.HasExecutionContext, "Empty task state must yield no execution context.");
    AssertFalse(taskView.HasShiftActivity, "Empty task state must yield no shift activity.");
    AssertEqual<ExecutionTarget?>(null, taskView.CurrentExecutionTarget, "Empty task state must yield empty current target.");
    AssertEqual<ExecutionOutcomeStatus?>(null, taskView.CurrentOutcomeStatus, "Empty task state must yield empty current outcome.");
    AssertEqual("No task view context. Shift activity: False.", taskView.ViewLine, "Empty task state must produce deterministic empty view line.");
}

static void TaskViewCurrentFieldsReflectTaskStateTruth()
{
    var taskState = new ExecutionRuntimeTaskState(
        HasExecutionContext: true,
        HasShiftActivity: true,
        CurrentExecutionTarget: ExecutionTarget.ActiveShiftSubsystem,
        CurrentOutcomeStatus: ExecutionOutcomeStatus.Rejected,
        TaskLine: "Task context: ActiveShiftSubsystem / Rejected. Shift activity: True.");

    var taskView = ExecutionTaskViewBuilder.Build(taskState);

    AssertEqual(taskState.CurrentExecutionTarget, taskView.CurrentExecutionTarget, "Task view current target must mirror task state truth.");
    AssertEqual(taskState.CurrentOutcomeStatus, taskView.CurrentOutcomeStatus, "Task view current outcome must mirror task state truth.");
}

static void TaskViewLineIsDeterministic()
{
    var taskState = new ExecutionRuntimeTaskState(
        HasExecutionContext: true,
        HasShiftActivity: true,
        CurrentExecutionTarget: ExecutionTarget.ActiveShiftSubsystem,
        CurrentOutcomeStatus: ExecutionOutcomeStatus.Deferred,
        TaskLine: "Task context: ActiveShiftSubsystem / Deferred. Shift activity: True.");

    var first = ExecutionTaskViewBuilder.Build(taskState);
    var second = ExecutionTaskViewBuilder.Build(taskState);

    AssertEqual(first.ViewLine, second.ViewLine, "Task view line must be deterministic.");
    AssertContains(first.ViewLine, "ActiveShiftSubsystem", "Task view line must include execution target.");
    AssertContains(first.ViewLine, "Deferred", "Task view line must include outcome status.");
}

static void TaskViewBuilderHasNoSideEffects()
{
    var workspaceRoot = CreateScratchWorkspace();
    try
    {
        var taskState = new ExecutionRuntimeTaskState(
            HasExecutionContext: true,
            HasShiftActivity: true,
            CurrentExecutionTarget: ExecutionTarget.ActiveShiftSubsystem,
            CurrentOutcomeStatus: ExecutionOutcomeStatus.Deferred,
            TaskLine: "Task context: ActiveShiftSubsystem / Deferred. Shift activity: True.");
        var metaPath = Path.Combine(workspaceRoot, ".zavod", "meta", "project.json");

        _ = ExecutionTaskViewBuilder.Build(taskState);

        AssertFalse(File.Exists(metaPath), "Task view builder must not write persistence files.");
    }
    finally
    {
        DeleteScratchWorkspace(workspaceRoot);
    }
}

static void TaskProjectionBundleIsBuiltCorrectlyFromTaskStateAndTaskView()
{
    var taskState = new ExecutionRuntimeTaskState(
        HasExecutionContext: true,
        HasShiftActivity: true,
        CurrentExecutionTarget: ExecutionTarget.ActiveShiftSubsystem,
        CurrentOutcomeStatus: ExecutionOutcomeStatus.Deferred,
        TaskLine: "Task context: ActiveShiftSubsystem / Deferred. Shift activity: True.");
    var taskView = new ExecutionRuntimeTaskView(
        HasExecutionContext: true,
        HasShiftActivity: true,
        CurrentExecutionTarget: ExecutionTarget.ActiveShiftSubsystem,
        CurrentOutcomeStatus: ExecutionOutcomeStatus.Deferred,
        ViewLine: "Task view: ActiveShiftSubsystem / Deferred. Shift activity: True.");

    var bundle = ExecutionTaskProjectionBundleBuilder.Build(taskState, taskView);

    AssertEqual(taskState, bundle.TaskState, "Task projection bundle must preserve task state.");
    AssertEqual(taskView, bundle.TaskView, "Task projection bundle must preserve task view.");
    AssertTrue(bundle.HasExecutionContext, "Task projection bundle must preserve execution context fact.");
    AssertTrue(bundle.HasShiftActivity, "Task projection bundle must preserve shift activity fact.");
}

static void EmptyTaskStateAndTaskViewYieldEmptyTaskProjectionBundle()
{
    var taskState = new ExecutionRuntimeTaskState(
        HasExecutionContext: false,
        HasShiftActivity: false,
        CurrentExecutionTarget: null,
        CurrentOutcomeStatus: null,
        TaskLine: "No task execution context. Shift activity: False.");
    var taskView = new ExecutionRuntimeTaskView(
        HasExecutionContext: false,
        HasShiftActivity: false,
        CurrentExecutionTarget: null,
        CurrentOutcomeStatus: null,
        ViewLine: "No task view context. Shift activity: False.");

    var bundle = ExecutionTaskProjectionBundleBuilder.Build(taskState, taskView);

    AssertFalse(bundle.HasExecutionContext, "Empty task projection bundle must yield no execution context.");
    AssertFalse(bundle.HasShiftActivity, "Empty task projection bundle must yield no shift activity.");
}

static void TaskProjectionBundleFlagsReflectTaskStateAndTaskViewTruth()
{
    var taskState = new ExecutionRuntimeTaskState(
        HasExecutionContext: true,
        HasShiftActivity: true,
        CurrentExecutionTarget: ExecutionTarget.ActiveShiftSubsystem,
        CurrentOutcomeStatus: ExecutionOutcomeStatus.Rejected,
        TaskLine: "Task context: ActiveShiftSubsystem / Rejected. Shift activity: True.");
    var taskView = new ExecutionRuntimeTaskView(
        HasExecutionContext: true,
        HasShiftActivity: true,
        CurrentExecutionTarget: ExecutionTarget.ActiveShiftSubsystem,
        CurrentOutcomeStatus: ExecutionOutcomeStatus.Rejected,
        ViewLine: "Task view: ActiveShiftSubsystem / Rejected. Shift activity: True.");

    var bundle = ExecutionTaskProjectionBundleBuilder.Build(taskState, taskView);

    AssertEqual(taskState.HasExecutionContext, bundle.HasExecutionContext, "Task projection bundle execution context must match task state truth.");
    AssertEqual(taskView.HasExecutionContext, bundle.HasExecutionContext, "Task projection bundle execution context must match task view truth.");
    AssertEqual(taskState.HasShiftActivity, bundle.HasShiftActivity, "Task projection bundle shift activity must match task state truth.");
    AssertEqual(taskView.HasShiftActivity, bundle.HasShiftActivity, "Task projection bundle shift activity must match task view truth.");
}

static void TaskProjectionBundleBuilderHasNoSideEffects()
{
    var workspaceRoot = CreateScratchWorkspace();
    try
    {
        var taskState = new ExecutionRuntimeTaskState(
            HasExecutionContext: true,
            HasShiftActivity: true,
            CurrentExecutionTarget: ExecutionTarget.ActiveShiftSubsystem,
            CurrentOutcomeStatus: ExecutionOutcomeStatus.Deferred,
            TaskLine: "Task context: ActiveShiftSubsystem / Deferred. Shift activity: True.");
        var taskView = new ExecutionRuntimeTaskView(
            HasExecutionContext: true,
            HasShiftActivity: true,
            CurrentExecutionTarget: ExecutionTarget.ActiveShiftSubsystem,
            CurrentOutcomeStatus: ExecutionOutcomeStatus.Deferred,
            ViewLine: "Task view: ActiveShiftSubsystem / Deferred. Shift activity: True.");
        var metaPath = Path.Combine(workspaceRoot, ".zavod", "meta", "project.json");

        _ = ExecutionTaskProjectionBundleBuilder.Build(taskState, taskView);

        AssertFalse(File.Exists(metaPath), "Task projection bundle builder must not write persistence files.");
    }
    finally
    {
        DeleteScratchWorkspace(workspaceRoot);
    }
}

static void CoreCheckpointBundleIsBuiltCorrectlyFromCoreLayers()
{
    var projectState = CreateProjectState(activeShiftId: "SHIFT-001", activeTaskId: "TASK-001");
    var snapshot = new ExecutionRuntimeSnapshot(
        ExecutionTarget.ActiveShiftSubsystem,
        ExecutionOutcomeStatus.Deferred,
        ProjectStateSaveStatus.Persisted,
        ShiftRelevantEntriesCount: 2,
        LastMessage: "Persisted deferred");
    var capsule = new ExecutionRuntimeCapsule(
        ExecutionTarget.ActiveShiftSubsystem,
        ExecutionOutcomeStatus.Deferred,
        ProjectStateSaveStatus.Persisted,
        HasShiftActivity: true,
        SummaryLine: "Last shift activity: ActiveShiftSubsystem / Deferred / Persisted.");
    var entryPack = ExecutionEntryPackBuilder.Build(capsule, snapshot);
    var taskState = ExecutionTaskStateBuilder.Build(entryPack);
    var taskView = ExecutionTaskViewBuilder.Build(taskState);
    var taskProjectionBundle = ExecutionTaskProjectionBundleBuilder.Build(taskState, taskView);

    var checkpoint = ExecutionCoreCheckpointBundleBuilder.Build(
        projectState,
        snapshot,
        capsule,
        entryPack,
        taskProjectionBundle);

    AssertEqual(projectState, checkpoint.ProjectState, "Core checkpoint bundle must preserve project state.");
    AssertEqual(snapshot, checkpoint.Snapshot, "Core checkpoint bundle must preserve snapshot.");
    AssertEqual(capsule, checkpoint.Capsule, "Core checkpoint bundle must preserve capsule.");
    AssertEqual(entryPack, checkpoint.EntryPack, "Core checkpoint bundle must preserve entry pack.");
    AssertEqual(taskProjectionBundle, checkpoint.TaskProjectionBundle, "Core checkpoint bundle must preserve task projection bundle.");
    AssertTrue(checkpoint.HasExecutionContext, "Core checkpoint bundle must preserve execution context fact.");
    AssertTrue(checkpoint.HasShiftActivity, "Core checkpoint bundle must preserve shift activity fact.");
}

static void EmptyCoreLayersYieldEmptyCoreCheckpointBundle()
{
    var projectState = CreateProjectState(activeShiftId: null, activeTaskId: null);
    var snapshot = new ExecutionRuntimeSnapshot(
        LastExecutionTarget: null,
        LastOutcomeStatus: null,
        LastSaveStatus: null,
        ShiftRelevantEntriesCount: 0,
        LastMessage: null);
    var capsule = new ExecutionRuntimeCapsule(
        CurrentExecutionTarget: null,
        CurrentOutcomeStatus: null,
        CurrentSaveStatus: null,
        HasShiftActivity: false,
        SummaryLine: "No shift activity.");
    var entryPack = ExecutionEntryPackBuilder.Build(capsule, snapshot);
    var taskState = ExecutionTaskStateBuilder.Build(entryPack);
    var taskView = ExecutionTaskViewBuilder.Build(taskState);
    var taskProjectionBundle = ExecutionTaskProjectionBundleBuilder.Build(taskState, taskView);

    var checkpoint = ExecutionCoreCheckpointBundleBuilder.Build(
        projectState,
        snapshot,
        capsule,
        entryPack,
        taskProjectionBundle);

    AssertFalse(checkpoint.HasExecutionContext, "Empty core checkpoint bundle must yield no execution context.");
    AssertFalse(checkpoint.HasShiftActivity, "Empty core checkpoint bundle must yield no shift activity.");
}

static void CoreCheckpointBundleFlagsStayConsistent()
{
    var projectState = CreateProjectState(activeShiftId: "SHIFT-001", activeTaskId: "TASK-001");
    var snapshot = new ExecutionRuntimeSnapshot(
        ExecutionTarget.ActiveShiftSubsystem,
        ExecutionOutcomeStatus.Rejected,
        ProjectStateSaveStatus.Skipped,
        ShiftRelevantEntriesCount: 1,
        LastMessage: "Rejected");
    var capsule = new ExecutionRuntimeCapsule(
        ExecutionTarget.ActiveShiftSubsystem,
        ExecutionOutcomeStatus.Rejected,
        ProjectStateSaveStatus.Skipped,
        HasShiftActivity: true,
        SummaryLine: "Last shift activity: ActiveShiftSubsystem / Rejected / Skipped.");
    var entryPack = ExecutionEntryPackBuilder.Build(capsule, snapshot);
    var taskState = ExecutionTaskStateBuilder.Build(entryPack);
    var taskView = ExecutionTaskViewBuilder.Build(taskState);
    var taskProjectionBundle = ExecutionTaskProjectionBundleBuilder.Build(taskState, taskView);

    var checkpoint = ExecutionCoreCheckpointBundleBuilder.Build(
        projectState,
        snapshot,
        capsule,
        entryPack,
        taskProjectionBundle);

    AssertEqual(entryPack.HasExecutionContext, checkpoint.HasExecutionContext, "Core checkpoint execution context must match entry pack truth.");
    AssertEqual(taskProjectionBundle.HasExecutionContext, checkpoint.HasExecutionContext, "Core checkpoint execution context must match task projection truth.");
    AssertEqual(capsule.HasShiftActivity, checkpoint.HasShiftActivity, "Core checkpoint shift activity must match capsule truth.");
    AssertEqual(entryPack.HasShiftActivity, checkpoint.HasShiftActivity, "Core checkpoint shift activity must match entry pack truth.");
    AssertEqual(taskProjectionBundle.HasShiftActivity, checkpoint.HasShiftActivity, "Core checkpoint shift activity must match task projection truth.");
}

static void CoreCheckpointBundleBuilderHasNoSideEffects()
{
    var workspaceRoot = CreateScratchWorkspace();
    try
    {
        var projectState = CreateProjectState(activeShiftId: "SHIFT-001", activeTaskId: "TASK-001", projectRoot: workspaceRoot);
        var snapshot = new ExecutionRuntimeSnapshot(
            ExecutionTarget.ActiveShiftSubsystem,
            ExecutionOutcomeStatus.Deferred,
            ProjectStateSaveStatus.Persisted,
            ShiftRelevantEntriesCount: 1,
            LastMessage: "Deferred");
        var capsule = new ExecutionRuntimeCapsule(
            ExecutionTarget.ActiveShiftSubsystem,
            ExecutionOutcomeStatus.Deferred,
            ProjectStateSaveStatus.Persisted,
            HasShiftActivity: true,
            SummaryLine: "Last shift activity: ActiveShiftSubsystem / Deferred / Persisted.");
        var entryPack = ExecutionEntryPackBuilder.Build(capsule, snapshot);
        var taskState = ExecutionTaskStateBuilder.Build(entryPack);
        var taskView = ExecutionTaskViewBuilder.Build(taskState);
        var taskProjectionBundle = ExecutionTaskProjectionBundleBuilder.Build(taskState, taskView);
        var metaPath = Path.Combine(workspaceRoot, ".zavod", "meta", "project.json");

        _ = ExecutionCoreCheckpointBundleBuilder.Build(
            projectState,
            snapshot,
            capsule,
            entryPack,
            taskProjectionBundle);

        AssertFalse(File.Exists(metaPath), "Core checkpoint bundle builder must not write persistence files.");
    }
    finally
    {
        DeleteScratchWorkspace(workspaceRoot);
    }
}

static PromptRequestInput CreateWorkerInput()
{
    var capsule = CreateCapsule();
    var task = CreateTaskState(ContextIntentState.Validated, TaskStateStatus.Active, PromptRole.Worker);
    var shift = CreateShiftState(task);
    return new PromptRequestInput(PromptRole.Worker, capsule, shift, task);
}

static PromptRequestPacket CreateWorkerPacket()
{
    var input = CreateWorkerInput();
    var shiftContext = ShiftContextBuilder.Build(input.ShiftState, input.TaskState);
    var projectedContext = ShiftContextBuilder.ProjectForRole(shiftContext, input.Capsule, input.Role);
    var anchors = PromptAnchorProvider.Build(
        input.Role,
        input.Capsule,
        projectedContext,
        $"Validated intent: {input.TaskState.Description}",
        null);

    var request = new PromptAssemblyRequest(
        input.Role,
        PromptTruthMode.Anchored,
        PromptContextAdapter.ToPromptShiftContext(projectedContext),
        new TaskBlock(input.TaskState.IntentState, $"Implement validated task: {input.TaskState.Description}", input.TaskState.Scope, input.TaskState.AcceptanceCriteria),
        anchors,
        null,
        new ValidatedIntentBlock(
            input.TaskState.Description,
            new ScopeBlock(input.TaskState.Scope, Array.Empty<string>()),
            input.TaskState.AcceptanceCriteria,
            Array.Empty<string>()),
        null,
        null);

    return new PromptRequestPacket(
        input.Role,
        PromptTruthMode.Anchored,
        request,
        new PromptPacketMetadata(input.ShiftState.ShiftId, input.TaskState.TaskId, anchors.Count));
}

static ContextCapsule CreateCapsule()
{
    return ContextCapsuleBuilder.Build(new CapsuleSourceInput(
        "ZAVOD",
        "Structured execution tooling",
        "Execution core",
        new[] { "Project truth is authoritative.", "Apply only after review." },
        new[] { "No chat truth.", "No direct project mutation outside apply." },
        Array.Empty<string>(),
        new[] { "Prompt pipeline hardening", "Execution backbone" }));
}

static StateTaskState CreateTaskState(ContextIntentState intentState, TaskStateStatus status, PromptRole assignedRole)
{
    return new StateTaskState(
        "TASK-001",
        intentState,
        status,
        "Implement prompt assembly flow",
        new[] { "Prompting/PromptAssembler.cs", "Orchestration/PromptRequestPipeline.cs" },
        new[] { "Prompt is deterministic", "Invalid packets fail fast" },
        PromptRole.ShiftLead,
        assignedRole,
        new DateTimeOffset(2026, 03, 27, 10, 00, 00, TimeSpan.Zero));
}

static StateTaskState CreateCustomTaskState(
    string taskId,
    TaskStateStatus status,
    string description,
    IReadOnlyList<string> scope,
    PromptRole assignedRole = PromptRole.Worker)
{
    return new StateTaskState(
        taskId,
        ContextIntentState.Validated,
        status,
        description,
        scope,
        new[] { "Keep execution deterministic" },
        PromptRole.ShiftLead,
        assignedRole,
        new DateTimeOffset(2026, 03, 27, 10, 00, 00, TimeSpan.Zero));
}

static ShiftState CreateShiftState(StateTaskState taskState, IReadOnlyList<string>? openIssues = null)
{
    return new ShiftState(
        "SHIFT-001",
        "Close prompt pipeline",
        taskState.TaskId,
        ShiftStateStatus.Active,
        new[] { taskState },
        openIssues ?? new[] { "Pending review evidence" },
        new[] { "Previous accepted packet serializer" },
        new[] { "Stay within prompt infrastructure" });
}

static void LogPostAcceptState(string projectStateChange, ShiftState shiftState, string? activeShiftId = "SHIFT-001", string? activeTaskId = null)
{
    Console.WriteLine(
        $"[POST_ACCEPT_STATE] ProjectState={projectStateChange}; ShiftStatus={shiftState.Status}; ActiveShiftId={activeShiftId ?? "<null>"}; ActiveTaskId={activeTaskId ?? "<null>"}; lifecyclePhase=unchanged");
}

static ScopedContext CreateScopedContext()
{
    var candidate = new Candidate(
        "CAND-001",
        "ART-001",
        "src/MainWindow.xaml",
        12,
        new[] { "hint:button", "extension:.xaml" },
        "Primary button layout");

    return new ScopedContext(
        new[] { candidate },
        new[] { new ScopedContextReference("ART-001", "src/MainWindow.xaml", "MainWindow.xaml") },
        new[] { new ScopedContextSnippet(candidate.CandidateId, "Primary button layout") },
        "Selected 1 candidate from 1 inventory entry.");
}

static TaskExecutionContext CreateTaskExecutionContext(string taskId = "TASK-001", string shiftId = "SHIFT-001")
{
    return new TaskExecutionContext(shiftId, taskId);
}

static void StepPhaseMachineHappyPathYieldsConsistentProjection()
{
    var discussion = StepPhaseMachine.StartDiscussion();
    var ready = StepPhaseMachine.RecordIntent(discussion, ContextIntentState.ReadyForValidation);
    var preflight = StepPhaseMachine.EnterPreflight(ready);
    var running = StepPhaseMachine.ConfirmPreflight(preflight);
    var qc = StepPhaseMachine.MoveToQc(running);
    var result = StepPhaseMachine.AcceptQc(qc);
    var completed = StepPhaseMachine.ConfirmCompletion(result);

    var discussionProjection = StepPhaseProjectionBuilder.Build(ready);
    var preflightProjection = StepPhaseProjectionBuilder.Build(preflight);
    var runningProjection = StepPhaseProjectionBuilder.Build(running);
    var resultProjection = StepPhaseProjectionBuilder.Build(result);
    var completedProjection = StepPhaseProjectionBuilder.Build(completed);

    AssertTrue(discussionProjection.ShowChat, "Discussion phase must show chat.");
    AssertFalse(discussionProjection.ShowExecution, "Discussion phase must not show execution.");
    AssertTrue(discussionProjection.CanStartIntentValidation, "Discussion ready phase must expose intent validation.");

    AssertTrue(preflightProjection.ShowExecution, "Preflight must show execution surface.");
    AssertFalse(preflightProjection.ShowResult, "Preflight must not show result surface.");
    AssertTrue(preflightProjection.CanConfirmPreflight, "Preflight must allow explicit execution start.");
    AssertTrue(preflightProjection.CanClarifyPreflight, "Preflight must allow clarification before execution.");

    AssertTrue(runningProjection.CanCancelExecution, "Running execution must allow cancel.");
    AssertFalse(runningProjection.CanSendChat, "Running execution must freeze chat sending.");

    AssertTrue(resultProjection.ShowResult, "Accepted QC result must show result surface.");
    AssertTrue(resultProjection.CanAcceptResult, "Result phase must allow final accept.");
    AssertTrue(resultProjection.CanReturnForRevision, "Result phase must allow revision return.");
    AssertTrue(resultProjection.CanReturnToLead, "Result phase must allow return to lead.");

    AssertEqual(SurfacePhase.Completed, completedProjection.Phase, "Completion must end in Completed phase.");
    AssertTrue(completedProjection.ShowChat, "Completed phase must still show chat.");
    AssertFalse(completedProjection.ShowExecution, "Completed phase must not show execution surface.");
    AssertFalse(completedProjection.ShowResult, "Completed phase must not show result surface.");
}

static void StepPhaseMachineQcRejectPathReturnsToExecutionRevision()
{
    var qc = StepPhaseMachine.MoveToQc(
        StepPhaseMachine.ConfirmPreflight(
            StepPhaseMachine.EnterPreflight(
                StepPhaseMachine.RecordIntent(
                    StepPhaseMachine.StartDiscussion(),
                    ContextIntentState.ReadyForValidation))));

    var revision = StepPhaseMachine.RejectQc(qc);
    var revisionProjection = StepPhaseProjectionBuilder.Build(revision);

    AssertEqual(SurfacePhase.Execution, revision.Phase, "QC reject must return to execution phase.");
    AssertEqual(ExecutionSubphase.Revision, revision.ExecutionSubphase, "QC reject must produce revision execution subphase.");
    AssertTrue(revisionProjection.ShowExecution, "Revision state must keep execution surface visible.");
    AssertFalse(revisionProjection.ShowResult, "Revision state must not keep result surface visible.");
    AssertTrue(revisionProjection.CanCancelExecution, "Revision state must still allow execution cancel.");
}

static void StepPhaseMachineResultRevisionPathReentersExecution()
{
    var result = StepPhaseMachine.AcceptQc(
        StepPhaseMachine.MoveToQc(
            StepPhaseMachine.ConfirmPreflight(
                StepPhaseMachine.EnterPreflight(
                    StepPhaseMachine.RecordIntent(
                        StepPhaseMachine.StartDiscussion(),
                        ContextIntentState.ReadyForValidation)))));

    var revision = StepPhaseMachine.ReturnForRevision(result);
    var revisionProjection = StepPhaseProjectionBuilder.Build(revision);

    AssertEqual(SurfacePhase.Execution, revision.Phase, "Result revision must return to execution phase.");
    AssertEqual(ExecutionSubphase.Revision, revision.ExecutionSubphase, "Result revision must become execution revision subphase.");
    AssertEqual(ResultSubphase.RevisionRequested, revision.ResultSubphase, "Result revision must preserve result context while revision note is being prepared.");
    AssertTrue(revisionProjection.ShowExecution, "Revision return must show execution surface.");
    AssertTrue(revisionProjection.ShowResult, "Revision return must keep result surface visible during revision intake.");
    AssertContains(revisionProjection.StatusTextKey, "ExecutionRevisionRequested", "Revision projection must resolve revision status key.");
}

static void StepPhaseMachineReturnToLeadPathReopensDiscussion()
{
    var clarifiedPreflight = StepPhaseMachine.ApplyClarification(
        StepPhaseMachine.EnterPreflight(
            StepPhaseMachine.RecordIntent(
                StepPhaseMachine.StartDiscussion(),
                ContextIntentState.ReadyForValidation)));
    var clarifiedProjection = StepPhaseProjectionBuilder.Build(clarifiedPreflight);
    var result = StepPhaseMachine.AcceptQc(
        StepPhaseMachine.MoveToQc(
            StepPhaseMachine.ConfirmPreflight(clarifiedPreflight)));

    var reopened = StepPhaseMachine.ReturnToLead(result);
    var projection = StepPhaseProjectionBuilder.Build(reopened);

    AssertContains(clarifiedProjection.StatusTextKey, "ExecutionPreflightClarified", "Clarified preflight must expose clarified status key.");
    AssertContains(clarifiedProjection.PrimaryHintKey, "ExecutionPreflightClarified", "Clarified preflight must expose clarified primary hint key.");
    AssertEqual(SurfacePhase.Discussion, reopened.Phase, "Return to lead must reopen discussion.");
    AssertEqual(DiscussionSubphase.Reopened, reopened.DiscussionSubphase, "Return to lead must mark reopened context.");
    AssertEqual(ContextIntentState.Refining, reopened.IntentState, "Return to lead must reopen context in refining mode.");
    AssertTrue(projection.ShowChat, "Reopened discussion must show chat.");
    AssertFalse(projection.ShowExecution, "Reopened discussion must hide execution.");
    AssertTrue(projection.CanSendChat, "Reopened discussion must allow chat input.");
    AssertFalse(reopened.HasActiveTask, "Return to lead must not keep active task bound in the abstract phase contract.");
}

static void StepPhaseMachinePreflightCancelReturnsToDiscussionWithoutActiveTruth()
{
    var preflight = StepPhaseMachine.EnterPreflight(
        StepPhaseMachine.RecordIntent(
            StepPhaseMachine.StartDiscussion(),
            ContextIntentState.ReadyForValidation));

    var discussion = StepPhaseMachine.CancelPreflight(preflight);
    var projection = StepPhaseProjectionBuilder.Build(discussion);

    AssertEqual(SurfacePhase.Discussion, discussion.Phase, "Preflight cancel must return to discussion.");
    AssertEqual(DiscussionSubphase.Ready, discussion.DiscussionSubphase, "Preflight cancel must preserve ready discussion context.");
    AssertFalse(discussion.HasActiveShift, "Preflight cancel must not apply active shift truth.");
    AssertFalse(discussion.HasActiveTask, "Preflight cancel must not apply active task truth.");
    AssertTrue(projection.CanStartIntentValidation, "Returned discussion must still allow validation.");
}

static void StepPhaseMachineExecutionCancelBecomesInterruptedWithoutCompletion()
{
    var running = StepPhaseMachine.ConfirmPreflight(
        StepPhaseMachine.EnterPreflight(
            StepPhaseMachine.RecordIntent(
                StepPhaseMachine.StartDiscussion(),
                ContextIntentState.ReadyForValidation)));

    var interrupted = StepPhaseMachine.CancelExecution(running);
    var projection = StepPhaseProjectionBuilder.Build(interrupted);

    AssertEqual(SurfacePhase.Execution, interrupted.Phase, "Execution cancel must stay inside execution phase contract.");
    AssertEqual(ExecutionSubphase.Interrupted, interrupted.ExecutionSubphase, "Execution cancel must enter interrupted subphase.");
    AssertTrue(interrupted.HasActiveShift, "Interrupted execution must preserve active shift binding.");
    AssertTrue(interrupted.HasActiveTask, "Interrupted execution must preserve active task binding.");
    AssertFalse(projection.CanAcceptResult, "Interrupted execution must not fake result acceptance.");
    AssertFalse(projection.CanSendChat, "Interrupted execution must not reopen chat until the user explicitly chooses task editing.");
    AssertTrue(projection.CanResumeExecution, "Interrupted execution must expose an explicit resume path.");
}

static void StepPhaseMachineInterruptedDiscussionKeepsActiveTruthWithoutRestartAction()
{
    var interrupted = StepPhaseMachine.CancelExecution(
        StepPhaseMachine.ConfirmPreflight(
            StepPhaseMachine.EnterPreflight(
                StepPhaseMachine.RecordIntent(
                    StepPhaseMachine.StartDiscussion(),
                    ContextIntentState.ReadyForValidation))));

    var reopened = StepPhaseMachine.OpenInterruptedDiscussion(interrupted);
    var projection = StepPhaseProjectionBuilder.Build(reopened);

    AssertEqual(SurfacePhase.Discussion, reopened.Phase, "Interrupted task editing must reopen discussion surface.");
    AssertEqual(DiscussionSubphase.Reopened, reopened.DiscussionSubphase, "Interrupted task editing must use reopened discussion subphase.");
    AssertTrue(reopened.HasActiveShift, "Interrupted task editing must preserve active shift truth.");
    AssertTrue(reopened.HasActiveTask, "Interrupted task editing must preserve active task truth.");
    AssertTrue(projection.CanSendChat, "Reopened interrupted discussion must allow chat.");
    AssertFalse(projection.CanStartIntentValidation, "Reopened interrupted discussion must not expose fresh start validation while active truth is still alive.");
}

static void StepPhaseMachineRevisionIntakeCanReturnToResultReady()
{
    var revisionIntake = StepPhaseMachine.ReturnForRevision(
        StepPhaseMachine.AcceptQc(
            StepPhaseMachine.MoveToQc(
                StepPhaseMachine.ConfirmPreflight(
                    StepPhaseMachine.EnterPreflight(
                        StepPhaseMachine.RecordIntent(
                            StepPhaseMachine.StartDiscussion(),
                            ContextIntentState.ReadyForValidation))))));

    var result = StepPhaseMachine.ReturnToResultFromRevisionIntake(revisionIntake);
    var projection = StepPhaseProjectionBuilder.Build(result);

    AssertEqual(SurfacePhase.Result, result.Phase, "Revision intake return must restore result phase.");
    AssertEqual(ResultSubphase.Ready, result.ResultSubphase, "Revision intake return must restore ready result subphase.");
    AssertEqual(ExecutionSubphase.None, result.ExecutionSubphase, "Revision intake return must clear execution revision subphase.");
    AssertTrue(projection.ShowResult, "Returned result must show result surface.");
    AssertTrue(projection.ShowExecution, "Returned result must keep the execution surface visible under the current three-surface UI contract.");
}

static void StepPhaseMachineResultAbandonReturnsToChatOnlyDiscussion()
{
    var resultReady = StepPhaseMachine.AcceptQc(
        StepPhaseMachine.MoveToQc(
            StepPhaseMachine.ConfirmPreflight(
                StepPhaseMachine.EnterPreflight(
                    StepPhaseMachine.RecordIntent(
                        StepPhaseMachine.StartDiscussion(),
                        ContextIntentState.ReadyForValidation)))));

    var abandoned = StepPhaseMachine.AbandonResult(resultReady);
    var projection = StepPhaseProjectionBuilder.Build(abandoned);

    AssertEqual(SurfacePhase.Discussion, abandoned.Phase, "Result abandon must return UI to discussion phase.");
    AssertEqual(DiscussionSubphase.Idle, abandoned.DiscussionSubphase, "Result abandon must return to chat-only idle discussion.");
    AssertTrue(abandoned.HasActiveShift, "Result abandon must preserve active shift phase fact.");
    AssertFalse(abandoned.HasActiveTask, "Result abandon must clear active task phase fact.");
    AssertTrue(projection.ShowChat, "Result abandon must keep chat visible.");
    AssertFalse(projection.ShowExecution, "Result abandon must hide execution surface.");
    AssertFalse(projection.ShowResult, "Result abandon must hide result surface.");
}

static void StepPhaseMachineResultAbandonIsForbiddenOutsideResultPhase()
{
    var running = StepPhaseMachine.ConfirmPreflight(
        StepPhaseMachine.EnterPreflight(
            StepPhaseMachine.RecordIntent(
                StepPhaseMachine.StartDiscussion(),
                ContextIntentState.ReadyForValidation)));

    AssertThrows<InvalidOperationException>(
        () => StepPhaseMachine.AbandonResult(running),
        "Result abandon must fail fast outside result phase.");
}

static void StepPhaseMachineResumeVariantsStayPhaseHonest()
{
    var discussion = StepPhaseProjectionBuilder.Build(StepPhaseMachine.ResumeDiscussion());
    var work = StepPhaseProjectionBuilder.Build(StepPhaseMachine.ResumeWork());
    var result = StepPhaseProjectionBuilder.Build(StepPhaseMachine.ResumeResult());
    var interrupted = StepPhaseProjectionBuilder.Build(StepPhaseMachine.ResumeInterrupted());

    AssertEqual(SurfacePhase.Discussion, discussion.Phase, "Persisted idle discussion must resume to discussion phase.");
    AssertFalse(discussion.ShowExecution, "Persisted discussion must not show execution.");

    AssertEqual(SurfacePhase.Execution, work.Phase, "Persisted work must resume to execution phase.");
    AssertEqual(ExecutionSubphase.Running, work.ExecutionSubphase, "Persisted work must resume to running execution.");

    AssertEqual(SurfacePhase.Result, result.Phase, "Persisted result must resume to result phase.");
    AssertTrue(result.ShowResult, "Persisted result must show result surface.");

    AssertEqual(ExecutionSubphase.Interrupted, interrupted.ExecutionSubphase, "Interrupted resume must stay interrupted.");
    AssertFalse(interrupted.CanCancelExecution, "Interrupted resume must not fake live running controls.");
}

static void StepPhaseMachineForbiddenTransitionsFailFast()
{
    var discussion = StepPhaseMachine.StartDiscussion();
    var running = StepPhaseMachine.ConfirmPreflight(
        StepPhaseMachine.EnterPreflight(
            StepPhaseMachine.RecordIntent(
                StepPhaseMachine.StartDiscussion(),
                ContextIntentState.ReadyForValidation)));

    AssertThrows<InvalidOperationException>(
        () => StepPhaseMachine.AcceptQc(discussion),
        "Discussion must not jump directly to accepted result.");

    AssertThrows<InvalidOperationException>(
        () => StepPhaseMachine.ConfirmCompletion(discussion),
        "Discussion must not confirm completion.");

    AssertThrows<InvalidOperationException>(
        () => StepPhaseMachine.EnterPreflight(discussion),
        "Discussion must not enter preflight without ready intent.");

    AssertThrows<InvalidOperationException>(
        () => StepPhaseMachine.RecordIntent(running, ContextIntentState.ReadyForValidation),
        "Execution phase must not restart fresh discussion intent path.");
}

static void StepPhaseProjectionMatrixPreventsImpossibleActionSets()
{
    var states = new[]
    {
        StepPhaseMachine.StartDiscussion(),
        StepPhaseMachine.RecordIntent(StepPhaseMachine.StartDiscussion(), ContextIntentState.Candidate),
        StepPhaseMachine.RecordIntent(StepPhaseMachine.StartDiscussion(), ContextIntentState.ReadyForValidation),
        StepPhaseMachine.EnterPreflight(
            StepPhaseMachine.RecordIntent(
                StepPhaseMachine.StartDiscussion(),
                ContextIntentState.ReadyForValidation)),
        StepPhaseMachine.ConfirmPreflight(
            StepPhaseMachine.EnterPreflight(
                StepPhaseMachine.RecordIntent(
                    StepPhaseMachine.StartDiscussion(),
                    ContextIntentState.ReadyForValidation))),
        StepPhaseMachine.MoveToQc(
            StepPhaseMachine.ConfirmPreflight(
                StepPhaseMachine.EnterPreflight(
                    StepPhaseMachine.RecordIntent(
                        StepPhaseMachine.StartDiscussion(),
                        ContextIntentState.ReadyForValidation)))),
        StepPhaseMachine.AcceptQc(
            StepPhaseMachine.MoveToQc(
                StepPhaseMachine.ConfirmPreflight(
                    StepPhaseMachine.EnterPreflight(
                        StepPhaseMachine.RecordIntent(
                            StepPhaseMachine.StartDiscussion(),
                            ContextIntentState.ReadyForValidation))))),
        StepPhaseMachine.CancelExecution(
            StepPhaseMachine.ConfirmPreflight(
                StepPhaseMachine.EnterPreflight(
                    StepPhaseMachine.RecordIntent(
                        StepPhaseMachine.StartDiscussion(),
                        ContextIntentState.ReadyForValidation))))
    };

    foreach (var state in states)
    {
        var projection = StepPhaseProjectionBuilder.Build(state);
        AssertFalse(
            projection.CanSendChat && projection.ShowExecution && state.Phase != SurfacePhase.Discussion,
            $"State '{state.Phase}/{state.ExecutionSubphase}/{state.ResultSubphase}' must not allow live chat while active execution/result surfaces are primary.");

        AssertFalse(
            projection.CanStartIntentValidation && state.Phase != SurfacePhase.Discussion,
            $"State '{state.Phase}/{state.ExecutionSubphase}/{state.ResultSubphase}' must not expose intent validation outside discussion.");

        AssertFalse(
            projection.ShowResult && state.Phase != SurfacePhase.Result,
            $"State '{state.Phase}/{state.ExecutionSubphase}/{state.ResultSubphase}' must not expose result surface before result phase.");

        AssertFalse(
            projection.CanAcceptResult && !projection.ShowResult,
            $"State '{state.Phase}/{state.ExecutionSubphase}/{state.ResultSubphase}' must not expose result acceptance without result surface.");

        AssertFalse(
            projection.StatusTextKey.Contains("DiscussionReady", StringComparison.Ordinal) && !projection.CanStartIntentValidation,
            $"State '{state.Phase}/{state.ExecutionSubphase}/{state.ResultSubphase}' must not promise ready validation without visible validation action.");
    }
}

static void ResumeStageNormalizerDegradesRunningWithoutRuntimeToInterrupted()
{
    var snapshot = new ResumeStageSnapshot(
        Version: "1.0",
        PhaseState: StepPhaseMachine.ResumeWork(),
        IntentState: ContextIntentState.Validated,
        IntentSummary: "Running without runtime",
        IsExecutionPreflightActive: false,
        IsPreflightClarificationActive: false,
        IsResultAccepted: false,
        ExecutionRefinement: null,
        PreflightClarificationText: string.Empty,
        RevisionIntakeText: string.Empty,
        RuntimeState: null,
        DemoState: null);

    var normalized = ResumeStageNormalizer.Normalize(snapshot, hasActiveWork: true)!;

    AssertEqual(SurfacePhase.Execution, normalized.PhaseState.Phase, "Running without runtime must stay in execution recovery.");
    AssertEqual(ExecutionSubphase.Interrupted, normalized.PhaseState.ExecutionSubphase, "Running without runtime must degrade to interrupted.");
    AssertFalse(normalized.IsExecutionPreflightActive, "Interrupted recovery must clear preflight tail.");
    AssertEqual<ExecutionRuntimeState?>(null, normalized.RuntimeState, "Interrupted recovery must not keep phantom runtime.");
}

static void ResumeStageNormalizerKeepsLiveRunningPhaseWhenRuntimeIsActive()
{
    var task = CreateTaskState(ContextIntentState.Validated, TaskStateStatus.Active, PromptRole.Worker);
    var shift = CreateShiftState(task) with { CurrentTaskId = task.TaskId };
    var runtime = ExecutionRuntimeController.Begin(task, shift);
    var snapshot = new ResumeStageSnapshot(
        Version: "1.0",
        PhaseState: StepPhaseMachine.ResumeWork(),
        IntentState: ContextIntentState.Validated,
        IntentSummary: "Live running",
        IsExecutionPreflightActive: false,
        IsPreflightClarificationActive: false,
        IsResultAccepted: false,
        ExecutionRefinement: null,
        PreflightClarificationText: string.Empty,
        RevisionIntakeText: string.Empty,
        RuntimeState: runtime,
        DemoState: null);

    var normalized = ResumeStageNormalizer.Normalize(snapshot, hasActiveWork: true, preserveLiveRuntimePhase: true)!;

    AssertEqual(SurfacePhase.Execution, normalized.PhaseState.Phase, "Live running must stay in execution phase.");
    AssertEqual(ExecutionSubphase.Running, normalized.PhaseState.ExecutionSubphase, "Live running must keep running subphase.");
    AssertTrue(normalized.RuntimeState is not null, "Live running must keep active runtime.");
}

static void ResumeStageNormalizerKeepsResultReviewWhenBackedByRuntime()
{
    var task = CreateTaskState(ContextIntentState.Validated, TaskStateStatus.Active, PromptRole.Worker);
    var shift = CreateShiftState(task) with { CurrentTaskId = task.TaskId };
    var runtime = ExecutionRuntimeController.Begin(task, shift);
    runtime = ExecutionRuntimeController.ProduceResult(runtime);
    runtime = ExecutionRuntimeController.RequestQcReview(runtime);
    runtime = ExecutionRuntimeController.AcceptQcReview(runtime);

    var snapshot = new ResumeStageSnapshot(
        Version: "1.0",
        PhaseState: StepPhaseMachine.ResumeResult() with { HasClarification = true },
        IntentState: ContextIntentState.Validated,
        IntentSummary: "Result review",
        IsExecutionPreflightActive: true,
        IsPreflightClarificationActive: true,
        IsResultAccepted: false,
        ExecutionRefinement: "stale-tail",
        PreflightClarificationText: "stale-preflight",
        RevisionIntakeText: "stale-revision",
        RuntimeState: runtime,
        DemoState: null);

    var normalized = ResumeStageNormalizer.Normalize(snapshot, hasActiveWork: true)!;

    AssertEqual(SurfacePhase.Result, normalized.PhaseState.Phase, "Runtime-backed result review must stay result.");
    AssertEqual(ResultSubphase.Ready, normalized.PhaseState.ResultSubphase, "Runtime-backed result review must stay ready.");
    AssertFalse(normalized.IsExecutionPreflightActive, "Result review must clear preflight presentation tail.");
    AssertEqual(string.Empty, normalized.PreflightClarificationText, "Result review must clear stale preflight draft.");
    AssertEqual(string.Empty, normalized.RevisionIntakeText, "Result review must clear stale revision draft.");
}

static void ResumeStageNormalizerKeepsCleanPreflightWithoutActiveTruth()
{
    var snapshot = new ResumeStageSnapshot(
        Version: "1.0",
        PhaseState: new StepPhaseState(
            SurfacePhase.Execution,
            DiscussionSubphase.None,
            ExecutionSubphase.Preflight,
            ResultSubphase.RevisionRequested,
            ContextIntentState.ReadyForValidation,
            HasActiveShift: true,
            HasActiveTask: true,
            HasClarification: true,
            HasReopenedContext: true),
        IntentState: ContextIntentState.ReadyForValidation,
        IntentSummary: "Agreement",
        IsExecutionPreflightActive: true,
        IsPreflightClarificationActive: true,
        IsResultAccepted: false,
        ExecutionRefinement: "keep-me",
        PreflightClarificationText: "clarification",
        RevisionIntakeText: "stale-revision",
        RuntimeState: null,
        DemoState: null);

    var normalized = ResumeStageNormalizer.Normalize(snapshot, hasActiveWork: false)!;

    AssertEqual(SurfacePhase.Execution, normalized.PhaseState.Phase, "Clean preflight must stay execution preflight.");
    AssertEqual(ExecutionSubphase.Preflight, normalized.PhaseState.ExecutionSubphase, "Clean preflight must preserve preflight subphase.");
    AssertFalse(normalized.PhaseState.HasActiveShift, "Preflight without active truth must clear active shift fact.");
    AssertFalse(normalized.PhaseState.HasActiveTask, "Preflight without active truth must clear active task fact.");
    AssertEqual(string.Empty, normalized.RevisionIntakeText, "Preflight must clear stale revision draft.");
}

static void ResumeStageNormalizerCollapsesDirtyActiveDiscussionToReopenedRefinement()
{
    var snapshot = new ResumeStageSnapshot(
        Version: "1.0",
        PhaseState: new StepPhaseState(
            SurfacePhase.Discussion,
            DiscussionSubphase.Ready,
            ExecutionSubphase.Running,
            ResultSubphase.RevisionRequested,
            ContextIntentState.Validated,
            HasActiveShift: true,
            HasActiveTask: true,
            HasClarification: true,
            HasReopenedContext: true),
        IntentState: ContextIntentState.Validated,
        IntentSummary: "Dirty reopened discussion",
        IsExecutionPreflightActive: true,
        IsPreflightClarificationActive: true,
        IsResultAccepted: false,
        ExecutionRefinement: "keep refinement",
        PreflightClarificationText: "stale-preflight",
        RevisionIntakeText: "stale-revision",
        RuntimeState: null,
        DemoState: null);

    var normalized = ResumeStageNormalizer.Normalize(snapshot, hasActiveWork: true)!;

    AssertEqual(SurfacePhase.Discussion, normalized.PhaseState.Phase, "Dirty active discussion must stay discussion.");
    AssertEqual(DiscussionSubphase.Reopened, normalized.PhaseState.DiscussionSubphase, "Dirty active discussion must collapse to reopened discussion.");
    AssertEqual(ContextIntentState.Refining, normalized.IntentState, "Dirty active discussion must become refinement of the current step.");
    AssertFalse(normalized.IsExecutionPreflightActive, "Dirty active discussion must clear preflight tail.");
    AssertEqual(string.Empty, normalized.PreflightClarificationText, "Dirty active discussion must clear stale preflight draft.");
    AssertEqual(string.Empty, normalized.RevisionIntakeText, "Dirty active discussion must clear stale revision draft.");
}

static void ResumeStageNormalizerKeepsActiveDiscussionReadyForReEntry()
{
    var snapshot = new ResumeStageSnapshot(
        Version: "1.0",
        PhaseState: new StepPhaseState(
            SurfacePhase.Discussion,
            DiscussionSubphase.Ready,
            ExecutionSubphase.None,
            ResultSubphase.None,
            ContextIntentState.ReadyForValidation,
            HasActiveShift: true,
            HasActiveTask: true,
            HasClarification: true,
            HasReopenedContext: true),
        IntentState: ContextIntentState.ReadyForValidation,
        IntentSummary: "Refined active step",
        IsExecutionPreflightActive: false,
        IsPreflightClarificationActive: false,
        IsResultAccepted: false,
        ExecutionRefinement: "keep refinement",
        PreflightClarificationText: string.Empty,
        RevisionIntakeText: string.Empty,
        RuntimeState: null,
        DemoState: null);

    var normalized = ResumeStageNormalizer.Normalize(snapshot, hasActiveWork: true)!;

    AssertEqual(SurfacePhase.Discussion, normalized.PhaseState.Phase, "Ready active discussion must stay discussion.");
    AssertEqual(DiscussionSubphase.Ready, normalized.PhaseState.DiscussionSubphase, "Ready active discussion must preserve ready re-entry state.");
    AssertEqual(ContextIntentState.ReadyForValidation, normalized.IntentState, "Ready active discussion must preserve ready-for-validation intent.");
    AssertTrue(normalized.PhaseState.HasActiveTask, "Ready active discussion must preserve active task binding.");
}

static void ResumeStageNormalizerKeepsActiveShiftEmptyDiscussionOutOfBootstrap()
{
    var snapshot = new ResumeStageSnapshot(
        Version: "1.0",
        PhaseState: StepPhaseMachine.ResumeActiveShiftDiscussion(),
        IntentState: ContextIntentState.None,
        IntentSummary: "Активного намерения нет.",
        IsExecutionPreflightActive: false,
        IsPreflightClarificationActive: false,
        IsResultAccepted: false,
        ExecutionRefinement: null,
        PreflightClarificationText: string.Empty,
        RevisionIntakeText: string.Empty,
        RuntimeState: null,
        DemoState: null);

    var normalized = ResumeStageNormalizer.Normalize(snapshot, hasActiveWork: false, hasActiveShift: true)!;
    var projection = StepPhaseProjectionBuilder.Build(normalized.PhaseState);

    AssertEqual(SurfacePhase.Discussion, normalized.PhaseState.Phase, "Active shift without task must stay in discussion.");
    AssertEqual(DiscussionSubphase.Idle, normalized.PhaseState.DiscussionSubphase, "Active shift without task must stay in empty discussion.");
    AssertTrue(normalized.PhaseState.HasActiveShift, "Active shift without task must preserve shift truth.");
    AssertFalse(normalized.PhaseState.HasActiveTask, "Active shift without task must not fabricate active task truth.");
    AssertEqual(ContextIntentState.None, normalized.IntentState, "Active shift without task must not invent intent.");
    AssertTrue(projection.ShowChat, "Active shift without task must keep chat visible.");
    AssertFalse(projection.ShowExecution, "Active shift without task must not reopen execution surface.");
    AssertFalse(projection.ShowResult, "Active shift without task must not reopen result surface.");
}

static void LegacyResumeMigrationClearsAbandonedDemoTailWithoutTouchingAcceptedContinuation()
{
    var abandonedShift = new ShiftState(
        "SHIFT-001",
        "Demo shift",
        CurrentTaskId: null,
        ShiftStateStatus.Active,
        new[]
        {
            CreateTaskState(ContextIntentState.Validated, TaskStateStatus.Abandoned, PromptRole.Worker)
        },
        Array.Empty<string>(),
        Array.Empty<string>(),
        Array.Empty<string>());

    var projectState = CreateProjectState(activeShiftId: "SHIFT-001", activeTaskId: null);
    var abandonedSnapshot = new ResumeStageSnapshot(
        Version: "1.0",
        PhaseState: new StepPhaseState(
            SurfacePhase.Result,
            DiscussionSubphase.None,
            ExecutionSubphase.None,
            ResultSubphase.Ready,
            ContextIntentState.Validated,
            HasActiveShift: true,
            HasActiveTask: false,
            HasClarification: false,
            HasReopenedContext: false),
        IntentState: ContextIntentState.ReadyForValidation,
        IntentSummary: "stale legacy tail",
        IsExecutionPreflightActive: true,
        IsPreflightClarificationActive: true,
        IsResultAccepted: true,
        ExecutionRefinement: "stale",
        PreflightClarificationText: "stale",
        RevisionIntakeText: "stale",
        RuntimeState: null,
        DemoState: new DemoResumeState(0, 0));

    var abandonedMigration = LegacyResumeStateMigrator.Normalize(projectState, abandonedShift, abandonedSnapshot);

    AssertTrue(abandonedMigration.SuppressDemoDrafts, "Legacy abandoned resume must suppress stale demo drafts.");
    AssertTrue(abandonedMigration.WasMigrated, "Legacy abandoned resume must be rewritten.");
    AssertEqual(SurfacePhase.Discussion, abandonedMigration.Snapshot!.PhaseState.Phase, "Legacy abandoned resume must return to discussion.");
    AssertEqual(DiscussionSubphase.Idle, abandonedMigration.Snapshot.PhaseState.DiscussionSubphase, "Legacy abandoned resume must become empty active-shift discussion.");
    AssertEqual(ContextIntentState.None, abandonedMigration.Snapshot.IntentState, "Legacy abandoned resume must clear stale intent.");
    AssertEqual(string.Empty, abandonedMigration.Snapshot.IntentSummary, "Legacy abandoned resume must clear stale summary.");

    var acceptedShift = abandonedShift with
    {
        Tasks = new[]
        {
            CreateTaskState(ContextIntentState.Validated, TaskStateStatus.Completed, PromptRole.Worker)
        },
        AcceptedResults = new[] { "accepted" }
    };
    var acceptedSnapshot = abandonedSnapshot with
    {
        PhaseState = DemoDiscussionPhaseSeed.BuildContinuation(isReadyForValidation: true),
        IntentState = ContextIntentState.ReadyForValidation,
        IntentSummary = "step 2",
        IsExecutionPreflightActive = false,
        IsPreflightClarificationActive = false,
        IsResultAccepted = false,
        ExecutionRefinement = null,
        PreflightClarificationText = string.Empty,
        RevisionIntakeText = string.Empty,
        DemoState = new DemoResumeState(1, 0)
    };

    var acceptedMigration = LegacyResumeStateMigrator.Normalize(projectState, acceptedShift, acceptedSnapshot);

    AssertFalse(acceptedMigration.SuppressDemoDrafts, "Accepted continuation must keep demo drafts available.");
    AssertFalse(acceptedMigration.WasMigrated, "Accepted continuation must not be rewritten by abandon migration.");
    AssertEqual(acceptedSnapshot, acceptedMigration.Snapshot, "Accepted continuation snapshot must remain unchanged.");
}

static void ProductReadyDraftExposesValidateCtaThroughProjection()
{
    var classification = ProductIntentClassifier.Classify("исправь кнопку в project home");
    LogIntentClassification("исправь кнопку в project home", classification);
    var discussion = StepPhaseMachine.RecordIntent(StepPhaseMachine.StartDiscussion(), classification.FinalState);
    var projection = StepPhaseProjectionBuilder.Build(discussion);

    AssertEqual(ContextIntentState.ReadyForValidation, classification.FinalState, "Classifier must mark the draft as ready.");
    AssertTrue(projection.CanStartIntentValidation, "Ready product draft must expose validate CTA through projection.");
}

static void OrientationIntentDetectorMarksOrientationRequests()
{
    var orientationRequests = new[]
    {
        "\u043a\u0442\u043e \u0442\u044b",
        "\u0447\u0442\u043e \u044d\u0442\u043e",
        "\u0447\u0442\u043e \u0442\u044b \u0443\u043c\u0435\u0435\u0448\u044c",
        "\u0433\u0434\u0435 \u044f",
        "where am i",
        "what is this",
        "what are you",
        "what can you do",
        "what does this do"
    };

    foreach (var text in orientationRequests)
    {
        AssertTrue(OrientationIntentDetector.IsOrientationRequest(text), $"Orientation detector must catch orientation request: {text}");
        AssertTrue(OrientationIntentDetector.ShouldHandleAsOrientation(text), $"Pure orientation request must stay on orientation path: {text}");
    }
}

static void OrientationIntentDetectorKeepsMixedIntentOnProductPath()
{
    var mixedIntentRequests = new[]
    {
        "\u0447\u0442\u043e \u044d\u0442\u043e \u0437\u0430 \u044d\u043a\u0440\u0430\u043d \u0438 \u0438\u0441\u043f\u0440\u0430\u0432\u044c \u043a\u043d\u043e\u043f\u043a\u0443 \u0441\u043f\u0440\u0430\u0432\u0430",
        "\u043a\u0442\u043e \u0442\u044b \u0438 \u043f\u043e\u0447\u0435\u043c\u0443 layout \u0441\u043b\u043e\u043c\u0430\u043d",
        "where am i and fix this button",
        "\u0447\u0442\u043e \u044d\u0442\u043e \u0437\u0430 \u043f\u0440\u043e\u0433\u0440\u0430\u043c\u043c\u0430, \u0434\u043e\u0431\u0430\u0432\u044c \u043a\u043d\u043e\u043f\u043a\u0443",
        "who are you and update the screen"
    };

    foreach (var text in mixedIntentRequests)
    {
        var classification = ProductIntentClassifier.Classify(text);
        LogIntentClassification(text, classification);
        var discussion = StepPhaseMachine.RecordIntent(StepPhaseMachine.StartDiscussion(), classification.FinalState);
        var projection = StepPhaseProjectionBuilder.Build(discussion);

        AssertTrue(OrientationIntentDetector.IsOrientationRequest(text), $"Mixed request may still contain orientation wording: {text}");
        AssertFalse(OrientationIntentDetector.ShouldHandleAsOrientation(text), $"Mixed request with product intent must stay on product path: {text}");
        AssertTrue(classification.DetectedAction.Detected || classification.DetectedTarget.Detected, $"Mixed request must still expose product signals: {text}");
        AssertEqual(
            classification.FinalState == ContextIntentState.ReadyForValidation,
            projection.CanStartIntentValidation,
            $"CTA must follow product readiness and not orientation wording: {text}");
    }
}

static void OrientationIntentDetectorDoesNotCaptureProductRequests()
{
    AssertFalse(OrientationIntentDetector.IsOrientationRequest("\u0438\u0441\u043f\u0440\u0430\u0432\u044c \u043a\u043d\u043e\u043f\u043a\u0443"), "Product request must not become orientation.");
    AssertFalse(OrientationIntentDetector.ShouldHandleAsOrientation("\u0438\u0441\u043f\u0440\u0430\u0432\u044c \u043a\u043d\u043e\u043f\u043a\u0443"), "Product request must not be handled by orientation policy.");
}

static void OrientationIntentDoesNotExposeExecutionCta()
{
    var discussion = StepPhaseMachine.RecordIntent(StepPhaseMachine.StartDiscussion(), ContextIntentState.Orientation);
    var projection = StepPhaseProjectionBuilder.Build(discussion);

    AssertEqual(ContextIntentState.Orientation, discussion.IntentState, "Orientation discussion must preserve orientation state.");
    AssertFalse(projection.CanStartIntentValidation, "Orientation intent must never expose validate CTA.");
    AssertThrows<InvalidOperationException>(
        () => StepPhaseMachine.EnterPreflight(discussion),
        "Orientation intent must not enter execution preflight.");
}

static void OrientationIntentReturnsFallbackResponse()
{
    AssertEqual(
        "\u042f \u0432\u0435\u0434\u0443 \u0440\u0430\u0431\u043e\u0442\u0443 \u0432 ZAVOD \u0438 \u043f\u043e\u043c\u043e\u0433\u0430\u044e \u0444\u043e\u0440\u043c\u0438\u0440\u043e\u0432\u0430\u0442\u044c \u0438 \u0437\u0430\u043f\u0443\u0441\u043a\u0430\u0442\u044c \u0437\u0430\u0434\u0430\u0447\u0438.",
        OrientationIntentResponder.Respond("\u0447\u0442\u043e \u044d\u0442\u043e"),
        "Orientation intent must return the documented fallback response.");
}

static void OrientationIntentKeepsZavodPersonaWithoutModelLeakage()
{
    var russianResponse = OrientationIntentResponder.Respond("\u043a\u0442\u043e \u0442\u044b");
    var englishResponse = OrientationIntentResponder.Respond("what are you");

    AssertContains(russianResponse, "ZAVOD", "Russian orientation response must keep product persona.");
    AssertContains(englishResponse, "ZAVOD", "English orientation response must keep product persona.");
    AssertFalse(russianResponse.Contains("ChatGPT", StringComparison.OrdinalIgnoreCase), "Russian orientation response must not leak ChatGPT identity.");
    AssertFalse(englishResponse.Contains("ChatGPT", StringComparison.OrdinalIgnoreCase), "English orientation response must not leak ChatGPT identity.");
    AssertFalse(russianResponse.Contains("OpenAI", StringComparison.OrdinalIgnoreCase), "Russian orientation response must not leak OpenAI identity.");
    AssertFalse(englishResponse.Contains("OpenAI", StringComparison.OrdinalIgnoreCase), "English orientation response must not leak OpenAI identity.");
}

static void OrientationCapabilityResponseKeepsWorkContext()
{
    var russianResponse = OrientationIntentResponder.Respond("\u0447\u0442\u043e \u0442\u044b \u0443\u043c\u0435\u0435\u0448\u044c");
    var englishResponse = OrientationIntentResponder.Respond("what can you do");

    AssertContains(russianResponse, "ZAVOD", "Capability response must stay inside product identity.");
    AssertContains(englishResponse, "ZAVOD", "Capability response must stay inside product identity.");
    AssertFalse(russianResponse.Contains("ChatGPT", StringComparison.OrdinalIgnoreCase), "Capability response must not leak ChatGPT identity.");
    AssertFalse(englishResponse.Contains("ChatGPT", StringComparison.OrdinalIgnoreCase), "Capability response must not leak ChatGPT identity.");
    AssertFalse(russianResponse.Contains("OpenAI", StringComparison.OrdinalIgnoreCase), "Capability response must not leak OpenAI identity.");
    AssertFalse(englishResponse.Contains("OpenAI", StringComparison.OrdinalIgnoreCase), "Capability response must not leak OpenAI identity.");
    AssertTrue(
        russianResponse.Contains("\u0437\u0430\u0434\u0430\u0447", StringComparison.OrdinalIgnoreCase)
        || russianResponse.Contains("\u0448\u0430\u0433", StringComparison.OrdinalIgnoreCase),
        "Russian capability response must gently return the user to work context.");
    AssertTrue(
        englishResponse.Contains("task", StringComparison.OrdinalIgnoreCase)
        || englishResponse.Contains("step", StringComparison.OrdinalIgnoreCase),
        "English capability response must gently return the user to work context.");
}

static void ActiveShiftReadyDiscussionEntersPreflightWithoutActiveTask()
{
    var ready = new StepPhaseState(
        SurfacePhase.Discussion,
        DiscussionSubphase.Ready,
        ExecutionSubphase.None,
        ResultSubphase.None,
        ContextIntentState.ReadyForValidation,
        HasActiveShift: true,
        HasActiveTask: false,
        HasClarification: false,
        HasReopenedContext: false);

    var preflight = StepPhaseMachine.EnterActiveShiftPreflight(ready);

    AssertEqual(SurfacePhase.Execution, preflight.Phase, "Active shift ready discussion must enter execution preflight.");
    AssertEqual(ExecutionSubphase.Preflight, preflight.ExecutionSubphase, "Active shift ready discussion must produce preflight.");
    AssertTrue(preflight.HasActiveShift, "Preflight must preserve active shift truth.");
    AssertFalse(preflight.HasActiveTask, "Preflight must not invent active task truth.");
}

static void ProductIntentClassifierMarksReadyCasesAsReady()
{
    var readyCases = new[]
    {
        "\u0438\u0441\u043f\u0440\u0430\u0432\u044c \u043a\u043d\u043e\u043f\u043a\u0443 \u0432 project home",
        "\u0434\u043e\u0431\u0430\u0432\u044c \u0441\u0438\u043d\u0438\u0439 \u0444\u043e\u043d \u043d\u0430 \u044d\u043a\u0440\u0430\u043d \u0440\u0435\u0437\u0443\u043b\u044c\u0442\u0430\u0442\u0430",
        "update button layout in xaml"
    };

    foreach (var text in readyCases)
    {
        var classification = ProductIntentClassifier.Classify(text);
        LogIntentClassification(text, classification);
        AssertTrue(classification.DetectedAction.Detected, $"Ready request must detect action: {text}");
        AssertTrue(classification.DetectedTarget.Detected, $"Ready request must detect target: {text}");
        AssertEqual(0, classification.DetectedBlockers.Count, $"Ready request must stay free of blockers: {text}");
        AssertTrue(classification.Score >= 2, $"Ready request must reach ready score: {text}");
        AssertEqual(ContextIntentState.ReadyForValidation, classification.FinalState, $"Ready request must become ready: {text}");
    }
}

static void ProductIntentClassifierKeepsChatterOutOfReady()
{
    var notReadyCases = new[]
    {
        "\u043a\u0430\u043a \u044d\u0442\u043e \u0440\u0430\u0431\u043e\u0442\u0430\u0435\u0442?",
        "\u0447\u0442\u043e \u0434\u0443\u043c\u0430\u0435\u0448\u044c?",
        "\u0434\u0430\u0432\u0430\u0439 \u043f\u043e\u0434\u0443\u043c\u0430\u0435\u043c",
        "ok",
        "\u043f\u0440\u0438\u0432\u0435\u0442"
    };

    foreach (var text in notReadyCases)
    {
        var classification = ProductIntentClassifier.Classify(text);
        LogIntentClassification(text, classification);
        AssertFalse(classification.DetectedAction.Detected, $"Casual or abstract chat must not detect action: {text}");
        AssertFalse(classification.DetectedTarget.Detected, $"Casual or abstract chat must not detect target: {text}");
        AssertTrue(classification.DetectedBlockers.Count > 0, $"Casual or abstract chat must explain why it is blocked: {text}");
        AssertTrue(classification.DetectedBlockers.Any(static blocker => blocker.StartsWith("hard:", StringComparison.Ordinal)), $"Casual or abstract chat must surface hard blockers: {text}");
        AssertFalse(classification.IntentOverride, $"Casual or abstract chat must not enter ready override: {text}");
        AssertNotEqual(ContextIntentState.ReadyForValidation, classification.FinalState, $"Casual or abstract chat must stay out of ready: {text}");
    }
}

static void ProductIntentClassifierHandlesBorderlineRequests()
{
    var borderlineCases = new[]
    {
        "\u0445\u043e\u0447\u0443 \u0447\u0442\u043e\u0431\u044b \u0441\u043f\u0440\u0430\u0432\u0430 \u0431\u044b\u043b\u0430 \u0441\u0438\u043d\u044f\u044f \u043a\u043d\u043e\u043f\u043a\u0430",
        "\u043d\u0443\u0436\u043d\u043e \u043f\u0435\u0440\u0435\u0434\u0435\u043b\u0430\u0442\u044c \u044d\u043a\u0440\u0430\u043d \u0440\u0435\u0437\u0443\u043b\u044c\u0442\u0430\u0442\u0430",
        "\u043d\u0430\u0434\u043e \u043f\u043e\u043f\u0440\u0430\u0432\u0438\u0442\u044c \u0440\u0430\u0437\u043c\u0435\u0442\u043a\u0443"
    };

    foreach (var text in borderlineCases)
    {
        var classification = ProductIntentClassifier.Classify(text);
        LogIntentClassification(text, classification);
        AssertTrue(classification.DetectedTarget.Detected, $"Borderline request must still detect target: {text}");
        AssertTrue(classification.Score >= 2, $"Borderline request must still reach ready score: {text}");
        AssertEqual(ContextIntentState.ReadyForValidation, classification.FinalState, $"Borderline but concrete request must still become ready: {text}");
    }
}

static void ProductIntentClassifierKeepsHumanPhrasingReady()
{
    var readyCases = new[]
    {
        "\u043c\u043e\u0436\u0435\u0448\u044c \u0438\u0441\u043f\u0440\u0430\u0432\u0438\u0442\u044c \u043a\u043d\u043e\u043f\u043a\u0443 \u0441\u043f\u0440\u0430\u0432\u0430?",
        "\u043f\u043e\u0436\u0430\u043b\u0443\u0439\u0441\u0442\u0430 \u0434\u043e\u0431\u0430\u0432\u044c \u0441\u0438\u043d\u0438\u0439 \u0444\u043e\u043d",
        "\u043d\u0430\u0434\u043e \u0431\u044b \u043f\u043e\u043f\u0440\u0430\u0432\u0438\u0442\u044c layout",
        "can you fix button alignment?",
        "maybe we should fix the button"
    };

    foreach (var text in readyCases)
    {
        var classification = ProductIntentClassifier.Classify(text);
        LogIntentClassification(text, classification);
        AssertTrue(classification.DetectedAction.Detected, $"Human phrasing must still detect action: {text}");
        AssertTrue(classification.DetectedTarget.Detected, $"Human phrasing must still detect target: {text}");
        AssertTrue(classification.IntentOverride, $"Human phrasing with concrete intent must use ready override: {text}");
        AssertTrue(classification.DetectedBlockers.Count == 0 || classification.DetectedBlockers.Any(static blocker => blocker.StartsWith("soft:", StringComparison.Ordinal)), $"Human phrasing must only keep soft blockers when ready: {text}");
        AssertEqual(ContextIntentState.ReadyForValidation, classification.FinalState, $"Human phrasing must stay ready: {text}");
    }
}

static void ProductIntentClassifierKeepsDirtyMixedPhrasingReady()
{
    var readyCases = new[]
    {
        "\u044d\u044d \u043f\u043e\u043f\u0440\u0430\u0432\u044c \u043a\u043d\u043e\u043f\u043a\u0443 \u0441\u043f\u0440\u0430\u0432\u0430 \u043f\u043b\u0438\u0437",
        "\u0431\u043b\u0438\u043d \u0442\u0443\u0442 \u043a\u043d\u043e\u043f\u043a\u0430 \u043a\u0440\u0438\u0432\u0430\u044f \u0438\u0441\u043f\u0440\u0430\u0432\u044c",
        "fix pls this button"
    };

    foreach (var text in readyCases)
    {
        var classification = ProductIntentClassifier.Classify(text);
        LogIntentClassification(text, classification);
        AssertTrue(classification.DetectedAction.Detected, $"Dirty phrasing must still detect action: {text}");
        AssertTrue(classification.DetectedTarget.Detected, $"Dirty phrasing must still detect target: {text}");
        AssertTrue(classification.IntentOverride, $"Dirty phrasing with concrete intent must use ready override: {text}");
        AssertTrue(classification.DetectedBlockers.Count == 0 || classification.DetectedBlockers.Any(static blocker => blocker.StartsWith("soft:", StringComparison.Ordinal)), $"Dirty phrasing must only keep soft blockers when ready: {text}");
        AssertEqual(ContextIntentState.ReadyForValidation, classification.FinalState, $"Dirty phrasing must still become ready: {text}");
    }
}

static void ProductIntentClassifierNormalizesNoisyInput()
{
    var noisyPairs = new (string Noisy, string Clean)[]
    {
        ("ПЛИИИЗ исправь кнопку", "плиз исправь кнопку"),
        ("   fix    button   ", "fix button"),
        ("поправь     layout", "поправь layout")
    };

    foreach (var (noisy, clean) in noisyPairs)
    {
        var noisyClassification = ProductIntentClassifier.Classify(noisy);
        var cleanClassification = ProductIntentClassifier.Classify(clean);
        LogIntentClassification(noisy, noisyClassification);
        LogIntentClassification(clean, cleanClassification);

        AssertEqual(ProductIntentClassifier.NormalizeInput(clean), noisyClassification.NormalizedText, $"Noisy input must normalize to the clean form: {noisy}");
        AssertEqual(cleanClassification.NormalizedText, noisyClassification.NormalizedText, $"Noisy and clean input must share normalized text: {noisy}");
        AssertEqual(cleanClassification.DetectedAction.Detected, noisyClassification.DetectedAction.Detected, $"Noisy input must preserve action detection: {noisy}");
        AssertSequenceEqual(cleanClassification.DetectedAction.Matches, noisyClassification.DetectedAction.Matches, $"Noisy input must preserve action matches: {noisy}");
        AssertEqual(cleanClassification.DetectedTarget.Detected, noisyClassification.DetectedTarget.Detected, $"Noisy input must preserve target detection: {noisy}");
        AssertSequenceEqual(cleanClassification.DetectedTarget.Matches, noisyClassification.DetectedTarget.Matches, $"Noisy input must preserve target matches: {noisy}");
        AssertSequenceEqual(cleanClassification.DetectedBlockers, noisyClassification.DetectedBlockers, $"Noisy input must preserve blocker matches: {noisy}");
        AssertEqual(cleanClassification.Score, noisyClassification.Score, $"Noisy input must preserve score: {noisy}");
        AssertEqual(cleanClassification.IntentOverride, noisyClassification.IntentOverride, $"Noisy input must preserve override: {noisy}");
        AssertEqual(cleanClassification.FinalState, noisyClassification.FinalState, $"Noisy input must preserve final state: {noisy}");
    }
}

static void ProductIntentClassifierNoisyRealWorldInputs()
{
    var supportedInV1 = new[]
    {
        "\u043f\u043b\u0438\u0438\u0438\u0437 \u0438\u0441\u043f\u0440\u0430\u0432\u044c \u043a\u043d\u043e\u043f\u043a\u0443",
        "fix    button",
        "\u044d\u044d \u043f\u043e\u043f\u0440\u0430\u0432\u044c layout \u0441\u043f\u0440\u0430\u0432\u0430",
        "can you fix button?",
        "\u043c\u043e\u0436\u0435\u0448\u044c \u043f\u043e\u043f\u0440\u0430\u0432\u0438\u0442\u044c xaml?"
    };

    var mixedSupportedWithoutExtraLogic = new[]
    {
        "fix \u043a\u043d\u043e\u043f\u043a\u0443 \u0441\u043f\u0440\u0430\u0432\u0430",
        "update \u044d\u043a\u0440\u0430\u043d \u0440\u0435\u0437\u0443\u043b\u044c\u0442\u0430\u0442\u0430",
        "\u043f\u043e\u043f\u0440\u0430\u0432\u044c button layout",
        "\u0434\u043e\u0431\u0430\u0432\u044c background \u043d\u0430 screen"
    };

    var intentionallyUnsupportedInV1 = new[]
    {
        "a k\u0430\u043a tebe ta\u043akaya knopka",
        "sdelai knopku sprava",
        "\u0438\u0441\u043f\u0440a\u0432\u044c \u043a\u043do\u043f\u043a\u0443",
        "\u043f0\u043f\u0440\u0430\u0432\u044c \u043a\u043d0\u043f\u043a\u0443",
        "fix \u043a\u043do\u043f\u043a\u0443 pls"
    };

    var dangerousFalsePositives = new[]
    {
        "\u0447\u0442\u043e \u0434\u0443\u043c\u0430\u0435\u0448\u044c?",
        "a kak eto rabotaet?",
        "maybe later",
        "\u043d\u0443 \u0442\u0430\u043a\u043e\u0435",
        "ok"
    };

    foreach (var text in supportedInV1)
    {
        var classification = ProductIntentClassifier.Classify(text);
        LogIntentClassification(text, classification);
        AssertEqual(ContextIntentState.ReadyForValidation, classification.FinalState, $"Noisy but supported v1 input must stay ready: {text}");
    }

    foreach (var text in mixedSupportedWithoutExtraLogic)
    {
        var classification = ProductIntentClassifier.Classify(text);
        LogIntentClassification(text, classification);
        AssertEqual(ContextIntentState.ReadyForValidation, classification.FinalState, $"Mixed RU/EN input should already work in v1 without extra recovery logic: {text}");
    }

    foreach (var text in intentionallyUnsupportedInV1)
    {
        var classification = ProductIntentClassifier.Classify(text);
        LogIntentClassification(text, classification);
        AssertNotEqual(ContextIntentState.ReadyForValidation, classification.FinalState, $"Broken-layout or pseudo-translit input must stay intentionally unsupported in v1: {text}");
    }

    foreach (var text in dangerousFalsePositives)
    {
        var classification = ProductIntentClassifier.Classify(text);
        LogIntentClassification(text, classification);
        AssertNotEqual(ContextIntentState.ReadyForValidation, classification.FinalState, $"Dangerous false-positive must not become ready: {text}");
    }
}

static void ProductIntentClassifierIsDeterministic()
{
    const string text = "fix button layout";
    var first = ProductIntentClassifier.Classify(text);
    var second = ProductIntentClassifier.Classify(text);
    LogIntentClassification(text, first);
    LogIntentClassification(text, second);

    AssertEqual(first.NormalizedText, second.NormalizedText, "Classifier must deterministically preserve normalized text.");
    AssertEqual(first.DetectedAction.Detected, second.DetectedAction.Detected, "Classifier must deterministically detect action.");
    AssertSequenceEqual(first.DetectedAction.Matches, second.DetectedAction.Matches, "Classifier must deterministically preserve action matches.");
    AssertEqual(first.DetectedTarget.Detected, second.DetectedTarget.Detected, "Classifier must deterministically detect target.");
    AssertSequenceEqual(first.DetectedTarget.Matches, second.DetectedTarget.Matches, "Classifier must deterministically preserve target matches.");
    AssertSequenceEqual(first.DetectedBlockers, second.DetectedBlockers, "Classifier must deterministically preserve blocker matches.");
    AssertEqual(first.Score, second.Score, "Classifier must deterministically preserve score.");
    AssertEqual(first.IntentOverride, second.IntentOverride, "Classifier must deterministically preserve override.");
    AssertEqual(first.FinalState, second.FinalState, "Classifier must deterministically preserve final state.");
}

static void ProductPipelineBuildsReadyProjectionWithoutUiEvents()
{
    var draft = "\u0434\u043e\u0431\u0430\u0432\u044c \u0441\u0438\u043d\u0438\u0439 \u0444\u043e\u043d \u043d\u0430 \u044d\u043a\u0440\u0430\u043d \u0440\u0435\u0437\u0443\u043b\u044c\u0442\u0430\u0442\u0430";
    var classification = ProductIntentClassifier.Classify(draft);
    LogIntentClassification(draft, classification);
    var discussion = StepPhaseMachine.RecordIntent(StepPhaseMachine.ResumeActiveShiftDiscussion(), classification.FinalState);
    var projection = StepPhaseProjectionBuilder.Build(discussion);

    AssertEqual(ContextIntentState.ReadyForValidation, classification.FinalState, "Direct classifier evaluation must produce ready intent.");
    AssertTrue(classification.DetectedAction.Detected, "Direct classifier evaluation must explain action detection.");
    AssertTrue(classification.DetectedTarget.Detected, "Direct classifier evaluation must explain target detection.");
    AssertTrue(discussion.HasActiveShift, "Direct sync path must preserve active shift truth.");
    AssertFalse(discussion.HasActiveTask, "Direct sync path must keep task slot empty.");
    AssertTrue(projection.CanStartIntentValidation, "Ready direct-sync draft must expose validate CTA.");
}

static void ProductReadyDraftCanEnterAndCancelPreflightWithoutLosingCta()
{
    var draft = "update button layout in xaml";
    var classification = ProductIntentClassifier.Classify(draft);
    LogIntentClassification(draft, classification);
    var discussion = StepPhaseMachine.RecordIntent(StepPhaseMachine.ResumeActiveShiftDiscussion(), classification.FinalState);
    var preflight = StepPhaseMachine.EnterActiveShiftPreflight(discussion);
    var cancelled = StepPhaseMachine.CancelPreflight(preflight);
    var projection = StepPhaseProjectionBuilder.Build(cancelled);

    AssertEqual(ContextIntentState.ReadyForValidation, classification.FinalState, "Preflight path must start from ready product intent.");
    AssertEqual(SurfacePhase.Execution, preflight.Phase, "Ready active-shift discussion must enter execution preflight.");
    AssertEqual(ExecutionSubphase.Preflight, preflight.ExecutionSubphase, "Ready active-shift discussion must land in preflight.");
    AssertEqual(SurfacePhase.Discussion, cancelled.Phase, "Cancelled preflight must return to discussion.");
    AssertEqual(DiscussionSubphase.Ready, cancelled.DiscussionSubphase, "Cancelled preflight must preserve ready discussion.");
    AssertTrue(projection.CanStartIntentValidation, "Ready CTA must still be available after preflight cancel.");
}

static void ActiveShiftPreflightCancelPreservesReadyDiscussionWithoutActiveTask()
{
    var ready = new StepPhaseState(
        SurfacePhase.Discussion,
        DiscussionSubphase.Ready,
        ExecutionSubphase.None,
        ResultSubphase.None,
        ContextIntentState.ReadyForValidation,
        HasActiveShift: true,
        HasActiveTask: false,
        HasClarification: false,
        HasReopenedContext: false);

    var cancelled = StepPhaseMachine.CancelPreflight(StepPhaseMachine.EnterActiveShiftPreflight(ready));
    var projection = StepPhaseProjectionBuilder.Build(cancelled);

    AssertEqual(SurfacePhase.Discussion, cancelled.Phase, "Cancelled active-shift preflight must return to discussion.");
    AssertEqual(DiscussionSubphase.Ready, cancelled.DiscussionSubphase, "Cancelled active-shift preflight must preserve ready discussion.");
    AssertTrue(cancelled.HasActiveShift, "Cancelled active-shift preflight must preserve active shift truth.");
    AssertFalse(cancelled.HasActiveTask, "Cancelled active-shift preflight must preserve no-active-task truth.");
    AssertTrue(projection.CanStartIntentValidation, "Returned active-shift ready discussion must expose validate CTA.");
}

static void ReopenedDiscussionReadyAllowsSameStepValidationEntry()
{
    var reopenedReady = new StepPhaseState(
        SurfacePhase.Discussion,
        DiscussionSubphase.Ready,
        ExecutionSubphase.None,
        ResultSubphase.None,
        ContextIntentState.ReadyForValidation,
        HasActiveShift: true,
        HasActiveTask: true,
        HasClarification: false,
        HasReopenedContext: true);

    var projection = StepPhaseProjectionBuilder.Build(reopenedReady);
    var preflight = StepPhaseMachine.EnterReopenedPreflight(reopenedReady);

    AssertTrue(projection.CanStartIntentValidation, "Ready reopened discussion must expose same-step validation entry.");
    AssertEqual(SurfacePhase.Execution, preflight.Phase, "Same-step validation entry must move into execution preflight.");
    AssertEqual(ExecutionSubphase.Preflight, preflight.ExecutionSubphase, "Same-step validation entry must produce preflight.");
    AssertTrue(preflight.HasActiveTask, "Same-step preflight must preserve active task truth.");
}

static void SameStepPreflightCancelPreservesActiveReadyDiscussion()
{
    var reopenedReady = new StepPhaseState(
        SurfacePhase.Discussion,
        DiscussionSubphase.Ready,
        ExecutionSubphase.None,
        ResultSubphase.None,
        ContextIntentState.ReadyForValidation,
        HasActiveShift: true,
        HasActiveTask: true,
        HasClarification: true,
        HasReopenedContext: true);

    var cancelled = StepPhaseMachine.CancelPreflight(StepPhaseMachine.EnterReopenedPreflight(reopenedReady));

    AssertEqual(SurfacePhase.Discussion, cancelled.Phase, "Cancelled same-step preflight must return to discussion.");
    AssertEqual(DiscussionSubphase.Ready, cancelled.DiscussionSubphase, "Cancelled same-step preflight must preserve ready discussion.");
    AssertTrue(cancelled.HasActiveTask, "Cancelled same-step preflight must preserve active task truth.");
    AssertEqual(ContextIntentState.ReadyForValidation, cancelled.IntentState, "Cancelled same-step preflight must preserve ready intent.");
}

static void DemoPostAcceptContinuationRestoresReadyDiscussionCta()
{
    var seeded = DemoDiscussionPhaseSeed.BuildContinuation(isReadyForValidation: true);
    var projection = StepPhaseProjectionBuilder.Build(seeded);

    AssertEqual(SurfacePhase.Discussion, seeded.Phase, "Demo post-accept continuation must remain in discussion phase.");
    AssertEqual(ContextIntentState.ReadyForValidation, seeded.IntentState, "Demo post-accept continuation must carry ready intent.");
    AssertTrue(seeded.HasActiveShift, "Demo continuation must preserve active shift truth.");
    AssertFalse(seeded.HasActiveTask, "Demo continuation must not fabricate active task truth.");
    AssertTrue(projection.CanStartIntentValidation, "Ready demo continuation must expose the primary CTA.");
}

static void DemoSessionAdvancesToSecondStepAfterFirstAccept()
{
    var demo = new DemoSessionState(DemoScenarioSeed.CreateV1());

    AssertEqual("делаем синюю кнопку в main.qml", demo.ChatDraft, "Demo must start with step 1 chat draft.");
    AssertEqual("хочу тёмно-синюю", demo.ClarifyDraft, "Demo must expose step 1 clarify draft.");

    demo.AdvanceAfterAccept();

    AssertEqual("добавим под кнопкой короткую подпись 'Start session'", demo.ChatDraft, "Demo must advance to step 2 after first accept.");
    AssertEqual("сделать подпись светло-серой и чуть меньше", demo.ClarifyDraft, "Demo step 2 clarify draft must become active.");
}

static void DemoSessionReachesCompletionAfterSecondAccept()
{
    var demo = new DemoSessionState(DemoScenarioSeed.CreateV1());

    demo.AdvanceAfterAccept();
    demo.AdvanceAfterAccept();

    AssertTrue(demo.IsComplete, "Demo must report completion after the second accepted step.");
    AssertEqual("demo завершено", demo.ChatDraft, "Completed demo must expose the completion draft.");
    AssertFalse(demo.AutoAdvanceResult, "Completed demo must not schedule more auto-result transitions.");
}

static void DemoSessionResetReturnsBrandNewShiftToCleanStepOne()
{
    var demo = new DemoSessionState(DemoScenarioSeed.CreateV1());
    var baseline = new DemoSessionState(DemoScenarioSeed.CreateV1());

    demo.AdvanceAfterAccept();
    AssertEqual(1, demo.CurrentStepIndex, "Control check: same-shift continuation must reach step 2 before reset.");
    AssertEqual(0, demo.CurrentCycleIndex, "Control check: same-shift continuation must start step 2 from its first cycle.");

    demo.ResetToStart();

    AssertEqual(0, demo.CurrentStepIndex, "Brand new shift reset must return demo session to step 1.");
    AssertEqual(0, demo.CurrentCycleIndex, "Brand new shift reset must clear cycle continuation state.");
    AssertEqual(baseline.ChatDraft, demo.ChatDraft, "Brand new shift must restart from clean demo step 1 draft.");
    AssertEqual(baseline.ClarifyDraft, demo.ClarifyDraft, "Brand new shift must restore step 1 clarify draft.");
}

static void ProjectsAdapterStreamsPartialUpdatesAndFinalRenderDeterministically()
{
    var adapter = new ProjectsAdapter();
    var item = adapter.AddMessageAsync(
        ConversationItemKind.Lead,
        "Shift Lead",
        string.Empty,
        isStreaming: true).GetAwaiter().GetResult();
    var createdState = item.RenderState;
    var createdStreaming = item.IsStreaming;

    Console.WriteLine($"STREAM_CHECK created streaming={item.IsStreaming} state={item.RenderState} text='{item.Text}' blocks={item.Blocks.Count}");

    adapter.AppendStreamingAsync(item, "Partial line").GetAwaiter().GetResult();
    var partial1Text = item.Text;
    Console.WriteLine($"STREAM_CHECK partial1 streaming={item.IsStreaming} state={item.RenderState} text='{item.Text}' blocks={item.Blocks.Count}");

    adapter.AppendStreamingAsync(item, " completed.\n").GetAwaiter().GetResult();
    var partial2Blocks = item.Blocks.Count;
    Console.WriteLine($"STREAM_CHECK partial2 streaming={item.IsStreaming} state={item.RenderState} text='{item.Text.Replace("\n", "\\n", StringComparison.Ordinal)}' blocks={item.Blocks.Count}");

    adapter.CompleteStreamingAsync(item, "# Final title\n\n- one\n- two").GetAwaiter().GetResult();
    var blockTypes = string.Join(",", item.Blocks.Select(static block => block.GetType().Name));
    Console.WriteLine($"STREAM_CHECK final streaming={item.IsStreaming} state={item.RenderState} text='{item.Text.Replace("\n", "\\n", StringComparison.Ordinal)}' blocks={item.Blocks.Count} blockTypes={blockTypes}");

    AssertTrue(createdStreaming, "Streaming item must start in streaming mode.");
    AssertEqual(MessageRenderState.Streaming, createdState, "Streaming item must start in streaming state.");
    AssertTrue(partial1Text.Contains("Partial line", StringComparison.Ordinal), "Partial update must immediately update current text.");
    AssertTrue(partial2Blocks > 0, "Logical flush must produce parsed blocks during streaming.");
    AssertFalse(item.IsStreaming, "Completed item must leave streaming mode.");
    AssertEqual(MessageRenderState.Final, item.RenderState, "Completed item must end in final render state.");
    AssertEqual(2, item.Blocks.Count, "Final markdown render must produce heading plus list blocks.");
}

static void ConversationActionVisibilityOnlyShowsInProjectNonUserMessages()
{
    var projectLead = new ConversationItemViewModel(
        "lead-1",
        ConversationItemKind.Lead,
        "Shift Lead",
        "Lead message",
        DateTimeOffset.UtcNow,
        metadata: new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["mode"] = "project",
            ["file-path"] = "src/ui/main.qml"
        },
        metadataActions: new[]
        {
            new ConversationMetadataAction("open-file", "open_file", "Открыть файл", isPrimary: true)
        });

    var projectUser = new ConversationItemViewModel(
        "user-1",
        ConversationItemKind.User,
        "User",
        "User message",
        DateTimeOffset.UtcNow,
        metadata: new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["mode"] = "project"
        },
        metadataActions: new[]
        {
            new ConversationMetadataAction("show-metadata", "show_metadata", "Show Metadata")
        });

    var chatLead = new ConversationItemViewModel(
        "chat-1",
        ConversationItemKind.Lead,
        "Assistant",
        "Chat message",
        DateTimeOffset.UtcNow,
        metadata: new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["mode"] = "chat"
        },
        metadataActions: new[]
        {
            new ConversationMetadataAction("show-metadata", "show_metadata", "Show Metadata")
        });

    AssertTrue(ConversationActionVisibility.ShouldShow(projectLead), "Project non-user message must show action row.");
    AssertFalse(ConversationActionVisibility.ShouldShow(projectUser), "Project user message must hide action row.");
    AssertFalse(ConversationActionVisibility.ShouldShow(chatLead), "Chat mode message must hide action row.");
}

static void ProjectStateStorageInitializesSharedAndLocalPersistenceRootsHonestly()
{
    var workspaceRoot = CreateScratchWorkspace();
    try
    {
        _ = ProjectStateStorage.EnsureInitialized(workspaceRoot, "zavod-local-layout", "ZAVOD Local Layout");

        AssertTrue(Directory.Exists(Path.Combine(workspaceRoot, ".zavod", "project")), "Shared truth root must stay under .zavod/project.");
        AssertTrue(Directory.Exists(Path.Combine(workspaceRoot, ".zavod", "meta")), "Shared meta root must stay under .zavod/meta.");
        AssertTrue(Directory.Exists(Path.Combine(workspaceRoot, ".zavod.local", "conversations")), "Local conversations root must be initialized under .zavod.local.");
        AssertTrue(Directory.Exists(Path.Combine(workspaceRoot, ".zavod.local", "runtime")), "Local runtime root must be initialized under .zavod.local.");
        AssertTrue(Directory.Exists(Path.Combine(workspaceRoot, ".zavod.local", "cache")), "Local cache root must be initialized under .zavod.local.");
        AssertTrue(Directory.Exists(Path.Combine(workspaceRoot, ".zavod.local", "previews")), "Local previews root must be initialized under .zavod.local.");
        AssertTrue(Directory.Exists(Path.Combine(workspaceRoot, ".zavod.local", "resume")), "Local resume root must be initialized under .zavod.local.");
        AssertTrue(Directory.Exists(Path.Combine(workspaceRoot, ".zavod.local", "attachments")), "Local attachments root must be initialized under .zavod.local.");
        AssertTrue(Directory.Exists(Path.Combine(workspaceRoot, ".zavod.local", "meta")), "Local meta root must be initialized under .zavod.local.");
    }
    finally
    {
        DeleteScratchWorkspace(workspaceRoot);
    }
}

static void ResumeStageStoragePersistsUnderZavodLocalHonestly()
{
    var workspaceRoot = CreateScratchWorkspace();
    try
    {
        _ = ProjectStateStorage.EnsureInitialized(workspaceRoot, "zavod-resume-local", "ZAVOD Resume Local");
        var snapshot = new ResumeStageSnapshot(
            Version: "1.0",
            PhaseState: StepPhaseMachine.StartDiscussion(),
            IntentState: ContextIntentState.ReadyForValidation,
            IntentSummary: "Resume summary",
            IsExecutionPreflightActive: false,
            IsPreflightClarificationActive: false,
            IsResultAccepted: false,
            ExecutionRefinement: null,
            PreflightClarificationText: string.Empty,
            RevisionIntakeText: string.Empty,
            RuntimeState: null,
            DemoState: null);

        ResumeStageStorage.Save(workspaceRoot, snapshot);

        var localPath = Path.Combine(workspaceRoot, ".zavod.local", "resume", "resume-stage.json");
        var sharedPath = Path.Combine(workspaceRoot, ".zavod", "meta", "resume-stage.json");
        var restored = ResumeStageStorage.Load(workspaceRoot);

        AssertTrue(File.Exists(localPath), "Resume snapshot must persist under .zavod.local/resume.");
        AssertFalse(File.Exists(sharedPath), "Resume snapshot must not persist under shared .zavod/meta anymore.");
        AssertTrue(restored is not null, "Resume snapshot must load back from local storage.");
        AssertEqual("Resume summary", restored!.IntentSummary, "Local resume snapshot must roundtrip deterministically.");
    }
    finally
    {
        DeleteScratchWorkspace(workspaceRoot);
    }
}

static void ChatsAdapterPersistsLocalConversationSeparatelyFromProjectTruthHonestly()
{
    var workspaceRoot = CreateScratchWorkspace();
    try
    {
        _ = ProjectStateStorage.EnsureInitialized(workspaceRoot, "zavod-chat-local", "ZAVOD Chat Local");
        const string conversationId = "chat-local-001";
        var adapter = new ChatsAdapter(storage: ConversationLogStorage.ForChatConversation(workspaceRoot, conversationId));

        _ = adapter.AddMessageAsync(ConversationItemKind.User, "User", "Chat hello").GetAwaiter().GetResult();

        var localPath = Path.Combine(workspaceRoot, ".zavod.local", "conversations", $"{conversationId}.jsonl");
        var sharedPath = Path.Combine(workspaceRoot, ".zavod", "conversations", "chats-active.jsonl");
        var restored = new ChatsAdapter(storage: ConversationLogStorage.ForChatConversation(workspaceRoot, conversationId));
        var restoredCount = restored.RestorePersistedAsync().GetAwaiter().GetResult();

        AssertTrue(File.Exists(localPath), "Chats history must persist under .zavod.local/conversations.");
        AssertFalse(File.Exists(sharedPath), "Chats history must not leak into shared .zavod.");
        AssertEqual(1, restoredCount, "Chats adapter must restore one persisted local conversation item.");
        AssertEqual("Chat hello", restored.Items[0].Text, "Chats adapter must restore persisted text honestly.");
    }
    finally
    {
        DeleteScratchWorkspace(workspaceRoot);
    }
}

static void ConversationIndexTracksMultipleChatsIndependentlyHonestly()
{
    var workspaceRoot = CreateScratchWorkspace();
    try
    {
        _ = ProjectStateStorage.EnsureInitialized(workspaceRoot, "zavod-chat-index", "ZAVOD Chat Index");
        var firstStorage = ConversationLogStorage.ForChatConversation(workspaceRoot, "chat-a");
        var secondStorage = ConversationLogStorage.ForChatConversation(workspaceRoot, "chat-b");

        firstStorage.Append(
            new ConversationLogSnapshot(
                "msg-a",
                new DateTimeOffset(2026, 04, 15, 10, 00, 00, TimeSpan.Zero),
                "User",
                ConversationItemKind.User.ToString(),
                "First chat",
                "First chat",
                StepId: null,
                Phase: null,
                Attachments: Array.Empty<string>(),
                Source: "chats",
                Adapter: "chats",
                IsStreaming: false,
                Metadata: null),
            "append");
        secondStorage.Append(
            new ConversationLogSnapshot(
                "msg-b",
                new DateTimeOffset(2026, 04, 15, 11, 00, 00, TimeSpan.Zero),
                "User",
                ConversationItemKind.User.ToString(),
                "Second chat",
                "Second chat",
                StepId: null,
                Phase: null,
                Attachments: Array.Empty<string>(),
                Source: "chats",
                Adapter: "chats",
                IsStreaming: false,
                Metadata: null),
            "append");

        ConversationIndexStorage.Upsert(workspaceRoot, new ConversationIndexEntry("chat-a", "chat", null, "Alpha", new DateTimeOffset(2026, 04, 15, 10, 00, 00, TimeSpan.Zero)));
        ConversationIndexStorage.Upsert(workspaceRoot, new ConversationIndexEntry("chat-b", "chat", null, "Beta", new DateTimeOffset(2026, 04, 15, 11, 00, 00, TimeSpan.Zero)));

        var indexPath = Path.Combine(workspaceRoot, ".zavod.local", "index.json");
        var index = ConversationIndexStorage.Load(workspaceRoot);
        var firstPath = Path.Combine(workspaceRoot, ".zavod.local", "conversations", "chat-a.jsonl");
        var secondPath = Path.Combine(workspaceRoot, ".zavod.local", "conversations", "chat-b.jsonl");

        AssertTrue(File.Exists(firstPath), "First chat must persist into its own conversation file.");
        AssertTrue(File.Exists(secondPath), "Second chat must persist into its own conversation file.");
        AssertTrue(File.Exists(indexPath), "Conversation index must persist under .zavod.local/index.json.");
        AssertEqual(2, index.Count, "Conversation index must track both chat conversations.");
        AssertEqual("chat-b", index[0].ConversationId, "Most recently updated chat must sort first in the index.");
        AssertEqual("chat-a", index[1].ConversationId, "Older chat must remain independently addressable in the index.");
        AssertContains(File.ReadAllText(firstPath), "First chat", "First chat file must keep only its own conversation payload.");
        AssertContains(File.ReadAllText(secondPath), "Second chat", "Second chat file must keep only its own conversation payload.");
    }
    finally
    {
        DeleteScratchWorkspace(workspaceRoot);
    }
}

static void ConversationStorageFallsBackToLegacyActiveChatFileHonestly()
{
    var workspaceRoot = CreateScratchWorkspace();
    try
    {
        _ = ProjectStateStorage.EnsureInitialized(workspaceRoot, "zavod-chat-fallback", "ZAVOD Chat Fallback");
        var legacyStorage = ConversationLogStorage.ForChats(workspaceRoot);
        legacyStorage.Append(
            new ConversationLogSnapshot(
                "legacy-msg",
                new DateTimeOffset(2026, 04, 15, 12, 00, 00, TimeSpan.Zero),
                "User",
                ConversationItemKind.User.ToString(),
                "Legacy chat",
                "Legacy chat",
                StepId: null,
                Phase: null,
                Attachments: Array.Empty<string>(),
                Source: "chats",
                Adapter: "chats",
                IsStreaming: false,
                Metadata: null),
            "append");

        var routedStorage = ConversationLogStorage.ForChatConversation(workspaceRoot, "chat-routed", fallbackFileName: "chats-active.jsonl");
        var restored = routedStorage.LoadLatest();

        AssertEqual(1, restored.Count, "Conversation-specific storage must read legacy chat history as fallback when its own file does not exist.");
        AssertEqual("Legacy chat", restored[0].Text, "Fallback read must keep the legacy record payload intact.");
        AssertFalse(File.Exists(Path.Combine(workspaceRoot, ".zavod.local", "conversations", "chat-routed.jsonl")), "Fallback read alone must not materialize a new routed conversation file.");
    }
    finally
    {
        DeleteScratchWorkspace(workspaceRoot);
    }
}

static void ConversationStorageWindowingDeduplicatesLogicalItemsHonestly()
{
    var workspaceRoot = CreateScratchWorkspace();
    try
    {
        _ = ProjectStateStorage.EnsureInitialized(workspaceRoot, "zavod-chat-windowing", "ZAVOD Chat Windowing");
        var storage = ConversationLogStorage.ForChatConversation(workspaceRoot, "chat-windowing");

        storage.Append(
            new ConversationLogSnapshot(
                "msg-001",
                new DateTimeOffset(2026, 04, 16, 9, 00, 00, TimeSpan.Zero),
                "User",
                ConversationItemKind.User.ToString(),
                "draft",
                "draft",
                StepId: null,
                Phase: null,
                Attachments: Array.Empty<string>(),
                Source: "chats",
                Adapter: "chats",
                IsStreaming: false,
                Metadata: null),
            "append");
        storage.Append(
            new ConversationLogSnapshot(
                "msg-001",
                new DateTimeOffset(2026, 04, 16, 9, 01, 00, TimeSpan.Zero),
                "User",
                ConversationItemKind.User.ToString(),
                "final",
                "final",
                StepId: null,
                Phase: null,
                Attachments: Array.Empty<string>(),
                Source: "chats",
                Adapter: "chats",
                IsStreaming: false,
                Metadata: null),
            "final");
        storage.Append(
            new ConversationLogSnapshot(
                "msg-002",
                new DateTimeOffset(2026, 04, 16, 9, 02, 00, TimeSpan.Zero),
                "Assistant",
                ConversationItemKind.Assistant.ToString(),
                "assistant",
                "assistant",
                StepId: null,
                Phase: null,
                Attachments: Array.Empty<string>(),
                Source: "chats",
                Adapter: "chats",
                IsStreaming: false,
                Metadata: null),
            "append");

        var latestWindow = storage.LoadLatestWindow(12);
        var olderWindow = storage.LoadWindowBefore(3, 12);

        AssertEqual(2, latestWindow.TotalCount, "Logical total count must ignore duplicate raw event lines for the same message id.");
        AssertEqual(2, latestWindow.Snapshots.Count, "Latest window must contain only deduplicated logical items.");
        AssertEqual("final", latestWindow.Snapshots[0].Text, "Latest window must keep the final logical state of the duplicated message.");
        AssertEqual(2, olderWindow.TotalCount, "Older window total count must also be based on logical deduplicated items.");
        AssertEqual(2, olderWindow.WindowEndSeq, "Older window must use logical sequence numbers.");
    }
    finally
    {
        DeleteScratchWorkspace(workspaceRoot);
    }
}

static void ChatsAdapterStoresFullLogsOutsideTimelineHonestly()
{
    var workspaceRoot = CreateScratchWorkspace();
    try
    {
        _ = ProjectStateStorage.EnsureInitialized(workspaceRoot, "zavod-chat-logs", "ZAVOD Chat Logs");
        const string conversationId = "chat-log-001";
        var adapter = new ChatsAdapter(
            storage: ConversationLogStorage.ForChatConversation(workspaceRoot, conversationId),
            artifactStorage: new ConversationArtifactStorage(workspaceRoot));

        var fullLog = string.Join(
            Environment.NewLine,
            Enumerable.Range(1, 20).Select(index => $"line-{index:D2}: heavy runtime output"));
        var item = adapter.AddLogAsync("Runtime", fullLog).GetAwaiter().GetResult();
        var restored = new ChatsAdapter(
            storage: ConversationLogStorage.ForChatConversation(workspaceRoot, conversationId),
            artifactStorage: new ConversationArtifactStorage(workspaceRoot));
        var restoredCount = restored.RestorePersistedAsync().GetAwaiter().GetResult();
        var itemMetadata = item.Metadata;
        AssertTrue(itemMetadata is not null, "Log timeline item must keep reference metadata.");
        if (itemMetadata is null)
        {
            throw new InvalidOperationException("Log reference metadata is missing.");
        }

        var referencePath = itemMetadata["reference-path"];

        AssertEqual(ConversationItemKind.Log, item.Kind, "Log spill must keep log kind on the timeline item.");
        AssertTrue(File.Exists(referencePath), "Full log payload must be stored as a separate .log artifact file.");
        AssertContains(referencePath, Path.Combine(".zavod.local", "artifacts", "logs"), "Log payload must live under .zavod.local/artifacts/logs.");
        AssertContains(File.ReadAllText(referencePath), "line-01: heavy runtime output", "Stored log file must preserve the full payload.");
        AssertContains(File.ReadAllText(referencePath), "line-20: heavy runtime output", "Stored log file must keep the tail as well.");
        AssertFalse(string.Equals(item.Text, fullLog, StringComparison.Ordinal), "Timeline log item must not keep the full heavy payload inline.");
        AssertContains(item.Text, "line-20: heavy runtime output", "Timeline log item must keep a readable tail preview.");
        AssertEqual(1, restoredCount, "Persisted conversation must restore the lightweight log item.");
        AssertEqual("log", restored.Items[0].Metadata!["payload-kind"], "Restored log item must keep reference metadata.");
    }
    finally
    {
        DeleteScratchWorkspace(workspaceRoot);
    }
}

static void ProjectsAdapterStoresArtifactsAsReferencesHonestly()
{
    var workspaceRoot = CreateScratchWorkspace();
    try
    {
        const string projectId = "zavod-project-artifacts";
        _ = ProjectStateStorage.EnsureInitialized(workspaceRoot, projectId, "ZAVOD Project Artifacts");
        var conversationId = ConversationRouting.GetProjectConversationId(projectId);
        var adapter = new ProjectsAdapter(
            storage: ConversationLogStorage.ForProjectConversation(workspaceRoot, conversationId),
            artifactStorage: new ConversationArtifactStorage(workspaceRoot));

        var fullArtifact = "# Canonical plan" + Environment.NewLine + Environment.NewLine + string.Join(Environment.NewLine, Enumerable.Range(1, 16).Select(index => $"- item {index:D2}"));
        var item = adapter.AddArtifactAsync(
            "Shift Lead",
            "Plan Draft",
            fullArtifact,
            "md",
            preview: "Plan draft prepared for review.").GetAwaiter().GetResult();
        var restored = new ProjectsAdapter(
            storage: ConversationLogStorage.ForProjectConversation(workspaceRoot, conversationId),
            artifactStorage: new ConversationArtifactStorage(workspaceRoot));
        var restoredCount = restored.RestorePersistedAsync().GetAwaiter().GetResult();
        var itemMetadata = item.Metadata;
        AssertTrue(itemMetadata is not null, "Artifact timeline item must keep reference metadata.");
        if (itemMetadata is null)
        {
            throw new InvalidOperationException("Artifact reference metadata is missing.");
        }

        var referencePath = itemMetadata["reference-path"];

        AssertEqual(ConversationItemKind.Artifact, item.Kind, "Artifact spill must keep artifact kind on the timeline item.");
        AssertEqual("Plan draft prepared for review.", item.Text, "Timeline artifact item must keep only the short preview.");
        AssertTrue(File.Exists(referencePath), "Artifact payload must be stored as a separate artifact file.");
        AssertContains(referencePath, Path.Combine(".zavod.local", "artifacts"), "Artifact payload must live under .zavod.local/artifacts.");
        AssertContains(referencePath, ".md", "Artifact payload must preserve the requested extension.");
        AssertContains(File.ReadAllText(referencePath), "# Canonical plan", "Stored artifact file must preserve the full payload.");
        AssertEqual(1, restoredCount, "Persisted project conversation must restore the lightweight artifact item.");
        var restoredMetadata = restored.Items[0].Metadata;
        AssertTrue(restoredMetadata is not null, "Restored artifact item must keep reference metadata.");
        if (restoredMetadata is null)
        {
            throw new InvalidOperationException("Restored artifact metadata is missing.");
        }

        AssertEqual("artifact", restoredMetadata["payload-kind"], "Restored artifact item must keep artifact reference metadata.");
        AssertEqual("Plan Draft", restoredMetadata["reference-label"], "Restored artifact item must keep the artifact label.");
        AssertEqual("Plan draft prepared for review.", restored.Items[0].Text, "Restored artifact item must stay lightweight on the timeline.");
    }
    finally
    {
        DeleteScratchWorkspace(workspaceRoot);
    }
}

static void ProjectConversationsStaySeparateOnTheSameEngineHonestly()
{
    var workspaceRoot = CreateScratchWorkspace();
    try
    {
        const string projectId = "zavod-project-conversations";
        _ = ProjectStateStorage.EnsureInitialized(workspaceRoot, projectId, "ZAVOD Project Conversations");
        var artifactStorage = new ConversationArtifactStorage(workspaceRoot);
        var conversationA = "project-conv-a";
        var conversationB = "project-conv-b";
        var metadataA = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["mode"] = "project",
            ["phase"] = "discussion",
            ["step-id"] = "TASK-001"
        };
        var metadataB = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["mode"] = "project",
            ["phase"] = "discussion",
            ["step-id"] = "TASK-002"
        };

        var adapterA = new ProjectsAdapter(
            storage: ConversationLogStorage.ForProjectConversation(workspaceRoot, conversationA),
            artifactStorage: artifactStorage);
        var adapterB = new ProjectsAdapter(
            storage: ConversationLogStorage.ForProjectConversation(workspaceRoot, conversationB),
            artifactStorage: artifactStorage);

        _ = adapterA.AddMessageAsync(ConversationItemKind.User, "User", "Task one", metadata: metadataA).GetAwaiter().GetResult();
        _ = adapterA.AddMessageAsync(ConversationItemKind.Status, "QC", "Preflight opened.", metadata: metadataA).GetAwaiter().GetResult();
        _ = adapterA.AddLogAsync("Worker", "build-a-line-1\nbuild-a-line-2", preview: "build-a-preview", metadata: metadataA).GetAwaiter().GetResult();
        _ = adapterA.AddArtifactAsync("Worker", "Execution brief", "# Brief A", "md", preview: "brief-a-preview", metadata: metadataA).GetAwaiter().GetResult();

        _ = adapterB.AddMessageAsync(ConversationItemKind.User, "User", "Task two", metadata: metadataB).GetAwaiter().GetResult();
        _ = adapterB.AddMessageAsync(ConversationItemKind.Lead, "Shift Lead", "Lead response", metadata: metadataB).GetAwaiter().GetResult();

        ConversationIndexStorage.Upsert(workspaceRoot, new ConversationIndexEntry(conversationA, "project", projectId, "Task one", new DateTimeOffset(2026, 04, 15, 15, 00, 00, TimeSpan.Zero)));
        ConversationIndexStorage.Upsert(workspaceRoot, new ConversationIndexEntry(conversationB, "project", projectId, "Task two", new DateTimeOffset(2026, 04, 15, 16, 00, 00, TimeSpan.Zero)));

        var restoredA = new ProjectsAdapter(
            storage: ConversationLogStorage.ForProjectConversation(workspaceRoot, conversationA),
            artifactStorage: artifactStorage);
        var restoredB = new ProjectsAdapter(
            storage: ConversationLogStorage.ForProjectConversation(workspaceRoot, conversationB),
            artifactStorage: artifactStorage);
        var restoredACount = restoredA.RestorePersistedAsync().GetAwaiter().GetResult();
        var restoredBCount = restoredB.RestorePersistedAsync().GetAwaiter().GetResult();
        var indexEntries = ConversationIndexStorage.Load(workspaceRoot)
            .Where(entry => string.Equals(entry.Mode, "project", StringComparison.Ordinal)
                && string.Equals(entry.ProjectId, projectId, StringComparison.Ordinal))
            .ToArray();

        AssertEqual(4, restoredACount, "First project conversation must restore message, status, log, and artifact through the same engine.");
        AssertEqual(2, restoredBCount, "Second project conversation must remain independent inside the same project.");
        AssertEqual(ConversationItemKind.Status, restoredA.Items[1].Kind, "Project engine must preserve status items.");
        AssertEqual(ConversationItemKind.Log, restoredA.Items[2].Kind, "Project engine must preserve log items.");
        AssertEqual(ConversationItemKind.Artifact, restoredA.Items[3].Kind, "Project engine must preserve artifact items.");
        AssertEqual("Task two", restoredB.Items[0].Text, "Second project conversation must restore only its own message timeline.");
        AssertEqual(2, indexEntries.Length, "Project index must support multiple conversations under one project id.");
        AssertEqual(conversationB, indexEntries[0].ConversationId, "Most recently updated project conversation must sort first.");
    }
    finally
    {
        DeleteScratchWorkspace(workspaceRoot);
    }
}

static void ProjectSageFindsRelevantHistoryHonestly()
{
    var workspaceRoot = CreateScratchWorkspace();
    try
    {
        const string projectId = "zavod-project-sage";
        var projectState = ProjectStateStorage.EnsureInitialized(workspaceRoot, projectId, "ZAVOD Project Sage");
        var storage = ConversationLogStorage.ForProjectConversation(workspaceRoot, "project-sage-conv-001");
        storage.Append(
            new ConversationLogSnapshot(
                "sage-msg-001",
                new DateTimeOffset(2026, 04, 15, 10, 00, 00, TimeSpan.Zero),
                "User",
                ConversationItemKind.User.ToString(),
                "We tried button layout with an extra layer and it failed in the shell.",
                "We tried button layout with an extra layer and it failed in the shell.",
                StepId: "TASK-001",
                Phase: "discussion",
                Attachments: Array.Empty<string>(),
                Source: "projects",
                Adapter: "projects",
                IsStreaming: false,
                Metadata: new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["mode"] = "project"
                }),
            "append");
        ConversationIndexStorage.Upsert(
            workspaceRoot,
            new ConversationIndexEntry(
                "project-sage-conv-001",
                "project",
                projectId,
                "Button layout failure",
                new DateTimeOffset(2026, 04, 15, 10, 00, 00, TimeSpan.Zero)));

        var task = CreateCustomTaskState(
            "TASK-002",
            TaskStateStatus.Abandoned,
            "Button layout rework with a new layer was abandoned after shell duplication.",
            new[] { "MainWindow.xaml" });
        var shift = CreateShiftState(task, new[] { "Earlier attempt failed due to shell duplication." }) with
        {
            AcceptedResults = new[] { "Similar issue was solved via inline patch without new layers." },
            Constraints = new[] { "Do not introduce new layers in the shell." }
        };
        _ = ShiftStateStorage.Save(workspaceRoot, shift);

        File.WriteAllText(
            projectState.TruthPointers.ProjectDocumentPath,
            "# Project" + Environment.NewLine + Environment.NewLine + "- Prefer existing renderer and avoid extra shell layers.");

        var sage = new ProjectSageService();
        var constraintAdvisory = sage.BuildLeadAdvisory(workspaceRoot, projectId, "Need to fix button layout but maybe add a new layer.");
        var historyAdvisory = sage.BuildLeadAdvisory(workspaceRoot, projectId, "Maybe the inline patch approach can solve the shell duplication again.");

        AssertTrue(constraintAdvisory.HasNotes, "Project Sage should return advisory notes when similar history exists.");
        AssertTrue(constraintAdvisory.Notes.Any(note => note.Contains("Constraint:", StringComparison.Ordinal) && note.Contains("new layers", StringComparison.OrdinalIgnoreCase)), "Project Sage should surface relevant constraints.");
        AssertTrue(
            historyAdvisory.Notes.Any(note =>
                note.Contains("Earlier conversation:", StringComparison.Ordinal)
                || note.Contains("Accepted before:", StringComparison.Ordinal)
                || note.Contains("Abandoned earlier:", StringComparison.Ordinal)
                || note.Contains("Project doc:", StringComparison.Ordinal)),
            "Project Sage should surface relevant project history.");
    }
    finally
    {
        DeleteScratchWorkspace(workspaceRoot);
    }
}

static void ProjectSageStaysQuietWithoutMatchHonestly()
{
    var workspaceRoot = CreateScratchWorkspace();
    try
    {
        const string projectId = "zavod-project-sage-quiet";
        _ = ProjectStateStorage.EnsureInitialized(workspaceRoot, projectId, "ZAVOD Project Sage Quiet");
        var storage = ConversationLogStorage.ForProjectConversation(workspaceRoot, "project-sage-conv-quiet");
        storage.Append(
            new ConversationLogSnapshot(
                "sage-msg-quiet",
                new DateTimeOffset(2026, 04, 15, 11, 00, 00, TimeSpan.Zero),
                "User",
                ConversationItemKind.User.ToString(),
                "Archive parser failed on malformed zip metadata.",
                "Archive parser failed on malformed zip metadata.",
                StepId: "TASK-010",
                Phase: "discussion",
                Attachments: Array.Empty<string>(),
                Source: "projects",
                Adapter: "projects",
                IsStreaming: false,
                Metadata: null),
            "append");
        ConversationIndexStorage.Upsert(
            workspaceRoot,
            new ConversationIndexEntry(
                "project-sage-conv-quiet",
                "project",
                projectId,
                "Archive parser",
                new DateTimeOffset(2026, 04, 15, 11, 00, 00, TimeSpan.Zero)));

        var sage = new ProjectSageService();
        var advisory = sage.BuildWorkerAdvisory(workspaceRoot, projectId, "Need a color pass for the dashboard hero.");

        AssertFalse(advisory.HasNotes, "Project Sage must stay optional and quiet when no relevant history matches.");
    }
    finally
    {
        DeleteScratchWorkspace(workspaceRoot);
    }
}

static void ValidatedIntentShiftStarterCarriesScopeAndAcceptanceHonestly()
{
    var workspaceRoot = CreateScratchWorkspace();
    try
    {
        var projectState = ProjectStateStorage.EnsureInitialized(workspaceRoot, "zavod-scope-accept", "ZAVOD Scope Accept");
        var intent = TaskIntentFactory
            .CreateCandidate("Implement the shell stabilization task.")
            .MarkReadyForValidation()
            .Validate();

        var started = ValidatedIntentShiftStarter.Start(
            projectState,
            intent,
            new DateTimeOffset(2026, 04, 16, 10, 00, 00, TimeSpan.Zero),
            scope: new[] { Path.Combine(workspaceRoot, ".zavod", "project", "project.md") },
            acceptanceCriteria: new[] { "Produce evidence-backed output.", "QC decision required." });

        AssertEqual(1, started.Task!.Scope.Count, "First shift task must keep the provided scope.");
        AssertEqual(2, started.Task.AcceptanceCriteria.Count, "First shift task must keep the provided acceptance criteria.");
        AssertContains(started.Task.Scope[0], "project.md", "First shift task scope must preserve the provided project document path.");
    }
    finally
    {
        DeleteScratchWorkspace(workspaceRoot);
    }
}

static void ConversationComposerDraftStoreStagesFilesHonestly()
{
    var workspaceRoot = CreateScratchWorkspace();
    try
    {
        _ = ProjectStateStorage.EnsureInitialized(workspaceRoot, "zavod-composer-files", "ZAVOD Composer Files");
        var artifactStorage = new ConversationArtifactStorage(workspaceRoot);
        var drafts = new ConversationComposerDraftStore(artifactStorage);
        var sourcePath = Path.Combine(workspaceRoot, "sample.pdf");
        File.WriteAllText(sourcePath, "fake pdf content");

        var staged = drafts.StageFiles("conv-001", "proj-001", new[] { sourcePath });

        AssertEqual(1, staged.Count, "File staging must create one pending draft item.");
        AssertTrue(File.Exists(staged[0].Reference.FilePath), "Staged file artifact must be copied into conversation-owned storage.");
        AssertContains(staged[0].Reference.FilePath, Path.Combine(".zavod.local", "artifacts", "conversations", "conv-001"), "Staged file must live under the conversation-scoped artifact root.");
        AssertEqual("proj-001", staged[0].ProjectId, "Pending draft must preserve project grouping metadata.");
        AssertTrue(drafts.RemoveDraft("conv-001", staged[0].DraftId), "Pending draft removal must succeed.");
        AssertFalse(File.Exists(staged[0].Reference.FilePath), "Removing pending draft must clean up the staged file.");
    }
    finally
    {
        DeleteScratchWorkspace(workspaceRoot);
    }
}

static void ConversationComposerDraftStoreStagesLongTextAsArtifactHonestly()
{
    var workspaceRoot = CreateScratchWorkspace();
    try
    {
        _ = ProjectStateStorage.EnsureInitialized(workspaceRoot, "zavod-composer-text", "ZAVOD Composer Text");
        var artifactStorage = new ConversationArtifactStorage(workspaceRoot);
        var drafts = new ConversationComposerDraftStore(artifactStorage);
        var longText = string.Join(Environment.NewLine, Enumerable.Range(1, 60).Select(index => $"line {index:D2} of long pasted text"));

        var staged = drafts.StageLongTextArtifact("conv-002", "proj-002", longText);

        AssertTrue(ConversationComposerDraftStore.ShouldBecomeArtifact(longText), "Long pasted text must cross the artifact conversion threshold.");
        AssertTrue(staged is not null, "Long pasted text must create a pending text artifact.");
        AssertEqual("user_paste", staged!.Origin, "Text artifact staging must preserve paste origin.");
        AssertEqual("text", staged.IntakeType, "Text artifact staging must classify the payload as text.");
        AssertTrue(File.Exists(staged.Reference.FilePath), "Text artifact staging must persist the pasted payload into conversation-owned artifact storage.");
        AssertContains(File.ReadAllText(staged.Reference.FilePath), "line 60 of long pasted text", "Persisted text artifact must preserve the full pasted payload.");
    }
    finally
    {
        DeleteScratchWorkspace(workspaceRoot);
    }
}

static void ConversationLogStorageWritesReadableUtf8Honestly()
{
    var workspaceRoot = CreateScratchWorkspace();
    try
    {
        _ = ProjectStateStorage.EnsureInitialized(workspaceRoot, "zavod-conversation-utf8", "ZAVOD Conversation UTF8");
        var storage = ConversationLogStorage.ForChatConversation(workspaceRoot, "chat-utf8-001");
        storage.Append(
            new ConversationLogSnapshot(
                "utf8-msg-001",
                new DateTimeOffset(2026, 04, 15, 18, 00, 00, TimeSpan.Zero),
                "User",
                ConversationItemKind.User.ToString(),
                "Привет, мир. Это проверка UTF-8.",
                "Привет, мир. Это проверка UTF-8.",
                StepId: null,
                Phase: null,
                Attachments: Array.Empty<string>(),
                Source: "chats",
                Adapter: "chats",
                IsStreaming: false,
                Metadata: null),
            "append");

        var fileText = File.ReadAllText(storage.FilePath, Encoding.UTF8);
        var restored = storage.LoadLatest();

        AssertContains(fileText, "Привет, мир. Это проверка UTF-8.", "Conversation log jsonl should keep Cyrillic text readable in UTF-8.");
        AssertFalse(fileText.Contains("\\u041f", StringComparison.Ordinal), "Conversation log jsonl should not re-escape Cyrillic into unicode sequences.");
        AssertEqual("Привет, мир. Это проверка UTF-8.", restored[0].Text, "Conversation log restore must preserve Cyrillic text exactly.");
    }
    finally
    {
        DeleteScratchWorkspace(workspaceRoot);
    }
}

static void ChatsWebAssetsKeepUtf8AndPlainTextRenderingHonestly()
{
    var repoRoot = Directory.GetCurrentDirectory();
    var htmlPath = Path.Combine(repoRoot, "UI", "Web", "Chats", "chats.surface.html");
    var jsPath = Path.Combine(repoRoot, "UI", "Web", "Chats", "chats.bridge.js");
    var html = File.ReadAllText(htmlPath, Encoding.UTF8);
    var js = File.ReadAllText(jsPath, Encoding.UTF8);

    AssertContains(html, "<meta charset=\"utf-8\">", "Chats WebView html should declare UTF-8 explicitly.");
    AssertContains(js, "appendPlainTextWithBreaks", "Chats bridge should render assistant raw text through a plain-text helper.");
    AssertFalse(js.Contains("p.innerHTML = escapeHtml", StringComparison.Ordinal), "Chats bridge should not use innerHTML for raw assistant text rendering.");
}

static void ChatsRuntimeControllerIncludesAttachmentContentInExecutionRequestHonestly()
{
    var workspaceRoot = CreateScratchWorkspace();
    try
    {
        _ = ProjectStateStorage.EnsureInitialized(workspaceRoot, "zavod-chat-runtime-utf8", "ZAVOD Chat Runtime UTF8");
        var openRouter = new FakeOpenRouterExecutionClient(request =>
        {
            var attachmentContent = request.Attachments is { Count: > 0 }
                ? request.Attachments[0].Content
                : string.Empty;
            var reply = attachmentContent.Contains("строки 50", StringComparison.Ordinal)
                ? "Текст о стабилизации UTF-8 и длинном кириллическом вложении."
                : "Вложение не дошло до исполнения.";
            return new OpenRouterExecutionResponse(true, reply, "openrouter/test", 200, null, "ok");
        });
        var controller = new ChatsRuntimeController(workspaceRoot, openRouter);
        controller.EnsureInitializedAsync().GetAwaiter().GetResult();

        var longText = string.Join(
            Environment.NewLine,
            Enumerable.Range(1, 50).Select(index => $"Это длинный кириллический текст строки {index:D2} про стабилизацию UTF-8."));

        AssertTrue(controller.StageLongTextArtifact(longText), "Chats runtime should stage long Cyrillic text as an attachment artifact.");
        AssertTrue(controller.SendMessageAsync("о чем этот текст?").GetAwaiter().GetResult(), "Chats runtime should accept a user question with staged artifact context.");
        AssertTrue(openRouter.LastRequest is not null, "Chats runtime should issue an OpenRouter execution request.");
        AssertTrue(openRouter.LastRequest!.Attachments is not null && openRouter.LastRequest.Attachments.Count == 1, "Chats runtime request should carry the staged artifact as attachment input.");
        AssertContains(openRouter.LastRequest.Attachments![0].Content, "строки 50", "Execution attachment content should include the full Cyrillic artifact payload.");

        var snapshot = controller.BuildSnapshot();
        AssertContains(snapshot.Messages[^1].Text, "стабилизации UTF-8", "Assistant reply should be able to depend on the attached Cyrillic content.");
    }
    finally
    {
        DeleteScratchWorkspace(workspaceRoot);
    }
}

#pragma warning disable CS8321 // Legacy draft tests are not part of the active invariant suite yet.
static void ProjectsWorkCycleRevisionCarriesAttachmentContentHonestly()
{
    var workspaceRoot = CreateScratchWorkspace();
    try
    {
        const string projectId = "zavod-project-revision-artifacts";
        _ = ProjectStateStorage.EnsureInitialized(workspaceRoot, projectId, "ZAVOD Project Revision Artifacts");
        var conversationId = ConversationRouting.GetProjectConversationId(projectId);
        var artifactStorage = new ConversationArtifactStorage(workspaceRoot);
        var adapter = new ProjectsAdapter(
            storage: ConversationLogStorage.ForProjectConversation(workspaceRoot, conversationId),
            artifactStorage: artifactStorage);
        var controller = new WorkCycleActionController(
            workspaceRoot,
            () => adapter,
            () => Task.CompletedTask,
            () => { });

        _ = controller.SendProjectsMessageAsync("Подготовь краткий вывод по материалам.").GetAwaiter().GetResult();
        _ = controller.EnterWorkAsync().GetAwaiter().GetResult();
        _ = controller.ConfirmPreflightAsync().GetAwaiter().GetResult();
        _ = controller.RequestRevisionAsync().GetAwaiter().GetResult();

        var attachmentText = string.Join(
            Environment.NewLine,
            Enumerable.Range(1, 45).Select(index => $"Кириллический файл строки {index:D2}: стабилизация UTF-8."));
        var reference = artifactStorage.SaveConversationTextArtifact(conversationId, "Pasted text", attachmentText, "txt");
        var draft = new ConversationComposerDraftItem(
            "draft-revision-001",
            conversationId,
            projectId,
            "user_paste",
            "text",
            "Pasted text",
            reference.Preview,
            "utf8 text artifact",
            attachmentText.Length,
            reference);

        AssertTrue(
            controller.SendProjectsMessageAsync(new ConversationComposerSubmission(conversationId, "о чем этот текст?", new[] { draft }, projectId)).GetAwaiter().GetResult(),
            "Revision flow should accept text plus staged artifact submission together.");

        var artifactItem = adapter.Items.LastOrDefault(item => item.Kind == ConversationItemKind.Artifact);
        AssertTrue(artifactItem is not null, "Revision flow should emit a worker artifact item.");
        var artifactMetadata = artifactItem!.Metadata;
        AssertTrue(artifactMetadata is not null && artifactMetadata.TryGetValue("reference-path", out _), "Worker artifact should keep its persisted payload reference.");
        if (artifactMetadata is null || !artifactMetadata.TryGetValue("reference-path", out var artifactPath))
        {
            throw new InvalidOperationException("Worker artifact reference path is missing.");
        }

        var artifactMarkdown = File.ReadAllText(artifactPath, Encoding.UTF8);
        AssertContains(artifactMarkdown, "attached.content", "Revision artifact should record attached content as execution evidence.");
        AssertContains(artifactMarkdown, "Кириллический файл строки 45", "Revision artifact should preserve attached Cyrillic content in UTF-8.");
    }
    finally
    {
        DeleteScratchWorkspace(workspaceRoot);
    }
}

static void ProjectsWorkCycleRevisionCarriesAttachmentContentAsciiHonestly()
{
    var workspaceRoot = CreateScratchWorkspace();
    try
    {
        const string projectId = "zavod-project-revision-artifacts-ascii";
        _ = ProjectStateStorage.EnsureInitialized(workspaceRoot, projectId, "ZAVOD Project Revision Artifacts ASCII");
        var conversationId = ConversationRouting.GetProjectConversationId(projectId);
        var artifactStorage = new ConversationArtifactStorage(workspaceRoot);
        var adapter = new ProjectsAdapter(
            storage: ConversationLogStorage.ForProjectConversation(workspaceRoot, conversationId),
            artifactStorage: artifactStorage);
        var controller = new WorkCycleActionController(
            workspaceRoot,
            () => adapter,
            () => Task.CompletedTask,
            () => { });

        AssertTrue(controller.SendProjectsMessageAsync("Fix button layout without adding new layers.").GetAwaiter().GetResult(), "Revision setup should start from a ready discussion message.");
        AssertTrue(controller.EnterWorkAsync().GetAwaiter().GetResult(), "Revision setup should enter preflight.");
        AssertTrue(controller.ConfirmPreflightAsync().GetAwaiter().GetResult(), "Revision setup should produce a runtime-backed result.");
        AssertTrue(controller.RequestRevisionAsync().GetAwaiter().GetResult(), "Revision setup should enter revision intake.");

        var attachmentText = string.Join(
            Environment.NewLine,
            Enumerable.Range(1, 45).Select(index => $"Кириллический файл строки {index:D2}: стабилизация UTF-8."));
        var reference = artifactStorage.SaveConversationTextArtifact(conversationId, "Pasted text", attachmentText, "txt");
        var draft = new ConversationComposerDraftItem(
            "draft-revision-ascii-001",
            conversationId,
            projectId,
            "user_paste",
            "text",
            "Pasted text",
            reference.Preview,
            "utf8 text artifact",
            attachmentText.Length,
            reference);

        AssertTrue(
            controller.SendProjectsMessageAsync(new ConversationComposerSubmission(conversationId, "What is this text about?", new[] { draft }, projectId)).GetAwaiter().GetResult(),
            "Revision flow should accept text plus staged artifact submission together.");

        var artifactItem = adapter.Items.LastOrDefault(item => item.Kind == ConversationItemKind.Artifact);
        AssertTrue(artifactItem is not null, "Revision flow should emit a worker artifact item.");
        var artifactMetadata = artifactItem!.Metadata;
        AssertTrue(artifactMetadata is not null && artifactMetadata.TryGetValue("reference-path", out _), "Worker artifact should keep its persisted payload reference.");
        if (artifactMetadata is null || !artifactMetadata.TryGetValue("reference-path", out var artifactPath))
        {
            throw new InvalidOperationException("Worker artifact reference path is missing.");
        }

        var artifactMarkdown = File.ReadAllText(artifactPath, Encoding.UTF8);
        AssertContains(artifactMarkdown, "attached.content", "Revision artifact should record attached content as execution evidence.");
        AssertContains(artifactMarkdown, "Кириллический файл строки 45", "Revision artifact should preserve attached Cyrillic content in UTF-8.");
    }
    finally
    {
        DeleteScratchWorkspace(workspaceRoot);
    }
}
#pragma warning restore CS8321

static void ProjectsFlowConsumesAttachmentsBeforeWorkCycleSendHonestly()
{
    var workspaceRoot = CreateScratchWorkspace();
    try
    {
        const string projectId = "zavod-project-flow-artifacts";
        var controller = new ProjectsRuntimeController(workspaceRoot);
        controller.EnsureInitializedAsync(projectId, "ZAVOD Project Flow Artifacts").GetAwaiter().GetResult();

        var longText = string.Join(
            Environment.NewLine,
            Enumerable.Range(1, 50).Select(index => $"Это длинный кириллический текст строки {index:D2} про стабилизацию UTF-8."));

        AssertTrue(controller.StageLongTextArtifact(longText), "Projects flow should stage long pasted text as an artifact.");
        var submission = controller.ConsumeComposerSubmissionAsync("Summarize the attached text.").GetAwaiter().GetResult();

        AssertTrue(submission.HasText, "Projects flow should preserve the typed text in the consumed submission.");
        AssertEqual(1, submission.Attachments.Count, "Projects flow should pass staged artifacts into the consumed submission.");
        AssertTrue(controller.ActiveAdapter.Items.Any(item => item.Kind == ConversationItemKind.Artifact), "Projects flow should append the artifact reference before work-cycle execution starts.");

        var workCycle = new WorkCycleActionController(
            workspaceRoot,
            () => controller.ActiveAdapter,
            () => Task.CompletedTask,
            () => { });

        AssertTrue(workCycle.SendProjectsMessageAsync(submission).GetAwaiter().GetResult(), "Work-cycle controller should accept the combined text plus artifact submission.");
    }
    finally
    {
        DeleteScratchWorkspace(workspaceRoot);
    }
}

static void ProjectsWorkCycleConfirmPreflightCreatesRuntimeBackedResultHonestly()
{
    var workspaceRoot = CreateScratchWorkspace();
    try
    {
        const string projectId = "zavod-project-runtime-flow";
        _ = ProjectStateStorage.EnsureInitialized(workspaceRoot, projectId, "ZAVOD Project Runtime Flow");
        var conversationId = ConversationRouting.GetProjectConversationId(projectId);
        var adapter = new ProjectsAdapter(
            storage: ConversationLogStorage.ForProjectConversation(workspaceRoot, conversationId),
            artifactStorage: new ConversationArtifactStorage(workspaceRoot));
        var workerRuntime = new WorkerAgentRuntime(clientFactory: _ => new FakeOpenRouterExecutionClient(_ => new OpenRouterExecutionResponse(
            true,
            """
            {
              "status": "success",
              "summary": "Prepared a bounded runtime-backed execution result.",
              "plan": ["Inspect task", "Produce bounded result"],
              "actions": ["Produced deterministic test result"],
              "modifications": [],
              "edits": [],
              "blockers": [],
              "risks": [],
              "warnings": []
            }
            """,
            "openrouter/test",
            200,
            null,
            "ok")));
        var qcRuntime = new QcAgentRuntime(clientFactory: _ => new FakeOpenRouterExecutionClient(_ => new OpenRouterExecutionResponse(
            true,
            """
            {
              "decision": "ACCEPT",
              "rationale": "The deterministic worker result is reviewable and bounded.",
              "issues": [],
              "next_action": "Surface the result to the user."
            }
            """,
            "openrouter/test",
            200,
            null,
            "ok")));
        var controller = new WorkCycleActionController(
            workspaceRoot,
            () => adapter,
            () => Task.CompletedTask,
            () => { },
            workerAgentRuntime: workerRuntime,
            qcAgentRuntime: qcRuntime);

        _ = controller.SendProjectsMessageAsync("Fix button layout without adding new layers.").GetAwaiter().GetResult();
        _ = controller.EnterWorkAsync().GetAwaiter().GetResult();
        _ = controller.ConfirmPreflightAsync().GetAwaiter().GetResult();

        var resume = ResumeStageStorage.Load(workspaceRoot);

        AssertTrue(resume is not null, "Projects work cycle must persist a resume snapshot.");
        AssertTrue(resume!.RuntimeState is not null, "Confirm preflight must persist typed execution runtime state.");
        AssertEqual(SurfacePhase.Result, resume.PhaseState.Phase, "Confirm preflight must move the phase into a result-ready state after runtime-backed result production.");
        AssertTrue(adapter.Items.Any(item => item.Kind == ConversationItemKind.Log), "Projects flow must emit worker log item from runtime-backed execution.");
        AssertTrue(adapter.Items.Any(item => item.Kind == ConversationItemKind.Artifact), "Projects flow must emit worker artifact item from runtime-backed execution.");
        AssertTrue(adapter.Items.Any(item => item.Kind == ConversationItemKind.Status && item.Text.Contains("QC accepted", StringComparison.Ordinal)), "Projects flow must emit QC status derived from runtime review.");
    }
    finally
    {
        DeleteScratchWorkspace(workspaceRoot);
    }
}

static void ProjectsWorkCycleQcUnavailableDoesNotOpenResultSurfaceHonestly()
{
    var workspaceRoot = CreateScratchWorkspace();
    try
    {
        const string projectId = "zavod-project-qc-unavailable-flow";
        _ = ProjectStateStorage.EnsureInitialized(workspaceRoot, projectId, "ZAVOD Project QC Unavailable Flow");
        var conversationId = ConversationRouting.GetProjectConversationId(projectId);
        var adapter = new ProjectsAdapter(
            storage: ConversationLogStorage.ForProjectConversation(workspaceRoot, conversationId),
            artifactStorage: new ConversationArtifactStorage(workspaceRoot));
        var workerRuntime = new WorkerAgentRuntime(clientFactory: _ => new FakeOpenRouterExecutionClient(_ => new OpenRouterExecutionResponse(
            true,
            """
            {
              "status": "success",
              "summary": "Prepared a bounded runtime-backed execution result.",
              "plan": ["Inspect task", "Produce bounded result"],
              "actions": ["Produced deterministic test result"],
              "modifications": [],
              "edits": [],
              "blockers": [],
              "risks": [],
              "warnings": []
            }
            """,
            "openrouter/test",
            200,
            null,
            "ok")));
        var qcRuntime = new QcAgentRuntime(clientFactory: _ => new FakeOpenRouterExecutionClient(_ => new OpenRouterExecutionResponse(
            true,
            "not-json",
            "openrouter/test",
            200,
            null,
            "ok")));
        var controller = new WorkCycleActionController(
            workspaceRoot,
            () => adapter,
            () => Task.CompletedTask,
            () => { },
            workerAgentRuntime: workerRuntime,
            qcAgentRuntime: qcRuntime);

        _ = controller.SendProjectsMessageAsync("Fix button layout without adding new layers.").GetAwaiter().GetResult();
        _ = controller.EnterWorkAsync().GetAwaiter().GetResult();
        _ = controller.ConfirmPreflightAsync().GetAwaiter().GetResult();

        var resume = ResumeStageStorage.Load(workspaceRoot);

        AssertTrue(resume is not null, "QC unavailable flow must persist a resume snapshot.");
        AssertTrue(resume!.RuntimeState is not null, "QC unavailable flow must keep runtime state for retry/revision.");
        AssertEqual(SurfacePhase.Execution, resume.PhaseState.Phase, "QC unavailable must not open the result surface.");
        AssertEqual(ExecutionSubphase.Revision, resume.PhaseState.ExecutionSubphase, "QC unavailable must return execution to revision.");
        AssertEqual(ResultSubphase.RevisionRequested, resume.PhaseState.ResultSubphase, "QC unavailable must request revision intake.");
        AssertEqual(QCReviewStatus.NotStarted, resume.RuntimeState!.QcStatus, "Revision restart after unavailable QC must require fresh QC.");
        AssertTrue(resume.RuntimeState.Result is null, "Revision restart after unavailable QC must not keep an accepted current result.");
        AssertFalse(
            adapter.Items.Any(item => item.Kind == ConversationItemKind.Status && item.Text.Contains("QC accepted", StringComparison.OrdinalIgnoreCase)),
            "QC unavailable must not emit an accepted-result status.");
        AssertTrue(
            adapter.Items.Any(item => item.Text.Contains("Result was not accepted", StringComparison.OrdinalIgnoreCase)),
            "QC unavailable must tell the user the result was not accepted.");
    }
    finally
    {
        DeleteScratchWorkspace(workspaceRoot);
    }
}

static void ProjectsWorkCycleBlocksPhysicalApplyBeforeAcceptanceGateHonestly()
{
    var workspaceRoot = CreateScratchWorkspace();
    try
    {
        const string projectId = "zavod-project-acceptance-gate-before-staging";
        _ = ProjectStateStorage.EnsureInitialized(workspaceRoot, projectId, "ZAVOD Project Acceptance Gate Before Staging");
        var targetPath = Path.Combine(workspaceRoot, "src", "File.txt");
        Directory.CreateDirectory(Path.GetDirectoryName(targetPath)!);
        File.WriteAllText(targetPath, "original", Encoding.UTF8);

        var conversationId = ConversationRouting.GetProjectConversationId(projectId);
        var adapter = new ProjectsAdapter(
            storage: ConversationLogStorage.ForProjectConversation(workspaceRoot, conversationId),
            artifactStorage: new ConversationArtifactStorage(workspaceRoot));
        var workerRuntime = new WorkerAgentRuntime(clientFactory: _ => new FakeOpenRouterExecutionClient(_ => new OpenRouterExecutionResponse(
            true,
            """
            {
              "status": "success",
              "summary": "Prepared a staged file edit.",
              "plan": ["Inspect task", "Stage bounded edit"],
              "actions": ["Staged deterministic file edit"],
              "modifications": [{"path": "src/File.txt", "kind": "edit", "summary": "Update file text"}],
              "edits": [{"path": "src/File.txt", "operation": "write_full", "content": "staged"}],
              "blockers": [],
              "risks": [],
              "warnings": []
            }
            """,
            "openrouter/test",
            200,
            null,
            "ok")));
        var qcRuntime = new QcAgentRuntime(clientFactory: _ => new FakeOpenRouterExecutionClient(_ => new OpenRouterExecutionResponse(
            true,
            """
            {
              "decision": "ACCEPT",
              "rationale": "The staged edit is bounded.",
              "issues": [],
              "next_action": "Surface the result to the user."
            }
            """,
            "openrouter/test",
            200,
            null,
            "ok")));
        var controller = new WorkCycleActionController(
            workspaceRoot,
            () => adapter,
            () => Task.CompletedTask,
            () => { },
            workerAgentRuntime: workerRuntime,
            qcAgentRuntime: qcRuntime);

        AssertTrue(controller.SendProjectsMessageAsync("Fix button layout without adding new layers.").GetAwaiter().GetResult(), "Work cycle should accept the task intent.");
        AssertTrue(controller.EnterWorkAsync().GetAwaiter().GetResult(), "Work cycle should enter preflight.");
        AssertTrue(controller.ConfirmPreflightAsync().GetAwaiter().GetResult(), "Work cycle should stage a worker result and enter result review.");

        var accepted = controller.AcceptResultAsync().GetAwaiter().GetResult();

        AssertFalse(accepted, "AcceptResult must stop before physical apply when acceptance evaluation is missing.");
        AssertEqual("original", File.ReadAllText(targetPath, Encoding.UTF8), "Missing acceptance evaluation must not physically apply staged files.");
        AssertTrue(
            adapter.Items.Any(item => item.Text.Contains("Accepted result apply blocked", StringComparison.OrdinalIgnoreCase)),
            "Blocked apply should be visible in the project conversation.");
    }
    finally
    {
        DeleteScratchWorkspace(workspaceRoot);
    }
}

static void ProjectsWorkCycleBlocksTruthApplyWhenStagingApplySkipsFilesHonestly()
{
    var workspaceRoot = CreateScratchWorkspace();
    try
    {
        const string projectId = "zavod-project-staging-skip-before-truth";
        _ = ProjectStateStorage.EnsureInitialized(workspaceRoot, projectId, "ZAVOD Project Staging Skip Before Truth");
        var targetPath = Path.Combine(workspaceRoot, "src", "File.txt");
        Directory.CreateDirectory(Path.GetDirectoryName(targetPath)!);
        File.WriteAllText(targetPath, "original", Encoding.UTF8);
        EnsureProjectStructureForTest(workspaceRoot);

        var conversationId = ConversationRouting.GetProjectConversationId(projectId);
        var adapter = new ProjectsAdapter(
            storage: ConversationLogStorage.ForProjectConversation(workspaceRoot, conversationId),
            artifactStorage: new ConversationArtifactStorage(workspaceRoot));
        var workerRuntime = new WorkerAgentRuntime(clientFactory: _ => new FakeOpenRouterExecutionClient(_ => new OpenRouterExecutionResponse(
            true,
            """
            {
              "status": "success",
              "summary": "Prepared a staged file edit.",
              "plan": ["Inspect task", "Stage bounded edit"],
              "actions": ["Staged deterministic file edit"],
              "modifications": [{"path": "src/File.txt", "kind": "edit", "summary": "Update file text"}],
              "edits": [{"path": "src/File.txt", "operation": "write_full", "content": "staged"}],
              "blockers": [],
              "risks": [],
              "warnings": []
            }
            """,
            "openrouter/test",
            200,
            null,
            "ok")));
        var qcRuntime = new QcAgentRuntime(clientFactory: _ => new FakeOpenRouterExecutionClient(_ => new OpenRouterExecutionResponse(
            true,
            """
            {
              "decision": "ACCEPT",
              "rationale": "The staged edit is bounded.",
              "issues": [],
              "next_action": "Surface the result to the user."
            }
            """,
            "openrouter/test",
            200,
            null,
            "ok")));
        var controller = new WorkCycleActionController(
            workspaceRoot,
            () => adapter,
            () => Task.CompletedTask,
            () => { },
            workerAgentRuntime: workerRuntime,
            qcAgentRuntime: qcRuntime);

        AssertTrue(controller.SendProjectsMessageAsync("Fix button layout without adding new layers.").GetAwaiter().GetResult(), "Work cycle should accept the task intent.");
        AssertTrue(controller.EnterWorkAsync().GetAwaiter().GetResult(), "Work cycle should enter preflight.");
        AssertTrue(controller.ConfirmPreflightAsync().GetAwaiter().GetResult(), "Work cycle should stage a worker result and enter result review.");

        var projectStateBeforeAccept = ProjectStateStorage.Load(workspaceRoot);
        var taskId = projectStateBeforeAccept.ActiveTaskId ?? throw new InvalidOperationException("Test expected active task.");
        var resume = ResumeStageStorage.Load(workspaceRoot) ?? throw new InvalidOperationException("Test expected resume snapshot.");
        var acceptedRuntime = ExecutionRuntimeController.ObserveAcceptanceAfterQc(
            resume.RuntimeState ?? throw new InvalidOperationException("Test expected runtime state."),
            workspaceRoot,
            new AcceptanceProcessEvidence(0, "ok", string.Empty, TimedOut: false, WasCanceled: false),
            "workspace unchanged");
        WorkCycleActionController.SaveWorkCycleSnapshot(
            workspaceRoot,
            StepPhaseMachine.ResumeResult(),
            resume.IntentSummary,
            isClarificationActive: false,
            clarificationDraft: string.Empty,
            runtimeState: acceptedRuntime);

        var stagedFile = Path.Combine(workspaceRoot, ".zavod.local", "staging", taskId, "attempt-01", "src", "File.txt");
        File.Delete(stagedFile);

        var accepted = controller.AcceptResultAsync().GetAwaiter().GetResult();
        var projectStateAfterAccept = ProjectStateStorage.Load(workspaceRoot);

        AssertFalse(accepted, "AcceptResult must stop truth movement when physical staging apply skipped a file.");
        AssertEqual("original", File.ReadAllText(targetPath, Encoding.UTF8), "Skipped physical apply must preserve the project file.");
        AssertEqual<string?>(taskId, projectStateAfterAccept.ActiveTaskId, "Skipped physical apply must not clear active task truth.");
        AssertTrue(
            adapter.Items.Any(item => item.Text.Contains("Staging apply did not apply all files", StringComparison.OrdinalIgnoreCase)),
            "Skipped physical apply should be visible in the project conversation.");
    }
    finally
    {
        DeleteScratchWorkspace(workspaceRoot);
    }
}

static void ProjectsWorkCycleAcceptResultUpdatesTruthHonestly()
{
    var workspaceRoot = CreateScratchWorkspace();
    try
    {
        const string projectId = "zavod-project-accept-flow";
        _ = ProjectStateStorage.EnsureInitialized(workspaceRoot, projectId, "ZAVOD Project Accept Flow");
        var conversationId = ConversationRouting.GetProjectConversationId(projectId);
        var adapter = new ProjectsAdapter(
            storage: ConversationLogStorage.ForProjectConversation(workspaceRoot, conversationId),
            artifactStorage: new ConversationArtifactStorage(workspaceRoot));
        var controller = new WorkCycleActionController(
            workspaceRoot,
            () => adapter,
            () => Task.CompletedTask,
            () => { });

        _ = controller.SendProjectsMessageAsync("Implement the shell stabilization path.").GetAwaiter().GetResult();
        _ = controller.EnterWorkAsync().GetAwaiter().GetResult();
        _ = controller.ConfirmPreflightAsync().GetAwaiter().GetResult();
        _ = controller.AcceptResultAsync().GetAwaiter().GetResult();

        var projectState = ProjectStateStorage.Load(workspaceRoot);
        var resume = ResumeStageStorage.Load(workspaceRoot);

        AssertEqual<string?>(null, projectState.ActiveTaskId, "Accept result must clear the active task binding in project truth.");
        AssertTrue(resume is not null && resume.RuntimeState is null, "After apply, Projects work cycle must clear runtime state from resume snapshot.");
    }
    finally
    {
        DeleteScratchWorkspace(workspaceRoot);
    }
}

static void ProjectsAdapterPersistsLocalConversationSeparatelyFromProjectTruthHonestly()
{
    var workspaceRoot = CreateScratchWorkspace();
    try
    {
        const string projectId = "zavod-project-local";
        _ = ProjectStateStorage.EnsureInitialized(workspaceRoot, projectId, "ZAVOD Project Local");
        var conversationId = ConversationRouting.GetProjectConversationId(projectId);
        var adapter = new ProjectsAdapter(storage: ConversationLogStorage.ForProjectConversation(workspaceRoot, conversationId));
        var metadata = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["mode"] = "project",
            ["role"] = "Shift Lead",
            ["kind"] = ConversationItemKind.Lead.ToString(),
            ["phase"] = SurfacePhase.Discussion.ToString(),
            ["step-id"] = "TASK-LOCAL-001",
            ["file-path"] = "src/ui/main.qml"
        };

        _ = adapter.AddMessageAsync(
            ConversationItemKind.Lead,
            "Shift Lead",
            "Project hello",
            metadata: metadata,
            metadataActions: null).GetAwaiter().GetResult();

        var localPath = Path.Combine(workspaceRoot, ".zavod.local", "conversations", $"{conversationId}.jsonl");
        var sharedPath = Path.Combine(workspaceRoot, ".zavod", "conversations", "projects-active.jsonl");
        var restored = new ProjectsAdapter(storage: ConversationLogStorage.ForProjectConversation(workspaceRoot, conversationId));
        var restoredCount = restored.RestorePersistedAsync().GetAwaiter().GetResult();

        AssertTrue(File.Exists(localPath), "Projects conversation must persist under .zavod.local/conversations.");
        AssertFalse(File.Exists(sharedPath), "Projects conversation must not leak into shared .zavod.");
        AssertEqual(1, restoredCount, "Projects adapter must restore one persisted local conversation item.");
        AssertEqual("Project hello", restored.Items[0].Text, "Projects adapter must restore persisted project text honestly.");
        var restoredMetadata = restored.Items[0].Metadata;
        AssertTrue(restoredMetadata is not null &&
                   restoredMetadata.TryGetValue("step-id", out var stepId) &&
                   string.Equals(stepId, "TASK-LOCAL-001", StringComparison.Ordinal), "Projects adapter must preserve local step metadata for resume.");
    }
    finally
    {
        DeleteScratchWorkspace(workspaceRoot);
    }
}

static void AcceptedShiftClosureKeepsSharedTruthSeparateFromLocalConversationHonestly()
{
    var workspaceRoot = CreateScratchWorkspace();
    try
    {
        const string projectId = "zavod-close-local-split";
        var initial = ProjectStateStorage.EnsureInitialized(workspaceRoot, projectId, "ZAVOD Close Local Split");
        var activeState = ProjectStateStorage.Save(initial with { ActiveShiftId = "SHIFT-001", ActiveTaskId = null });
        var task = CreateTaskState(ContextIntentState.Validated, TaskStateStatus.Completed, PromptRole.Worker);
        var shift = CreateShiftState(task) with
        {
            CurrentTaskId = null,
            AcceptedResults = new[] { "COMMIT-001|task:TASK-001|Accepted change" }
        };
        _ = ShiftStateStorage.Save(workspaceRoot, shift);

        var conversationId = ConversationRouting.GetProjectConversationId(projectId);
        var adapter = new ProjectsAdapter(storage: ConversationLogStorage.ForProjectConversation(workspaceRoot, conversationId));
        _ = adapter.AddMessageAsync(ConversationItemKind.Lead, "Shift Lead", "Local closure context").GetAwaiter().GetResult();

        var closure = ShiftClosureProcessor.CloseAcceptedShift(
            activeState,
            shift,
            new DateTimeOffset(2026, 03, 31, 10, 30, 00, TimeSpan.Zero),
            "Close accepted shift.");

        var localConversationPath = Path.Combine(workspaceRoot, ".zavod.local", "conversations", $"{conversationId}.jsonl");
        var sharedSnapshotPath = Path.Combine(workspaceRoot, ".zavod", "snapshots", $"{closure.Snapshot!.SnapshotId}.json");
        var sharedResumePath = Path.Combine(workspaceRoot, ".zavod", "meta", "resume-stage.json");

        AssertTrue(File.Exists(localConversationPath), "Project conversation must remain local after accepted shift closure.");
        AssertTrue(File.Exists(sharedSnapshotPath), "Accepted shift closure must still persist shared snapshot truth under .zavod.");
        AssertFalse(File.Exists(sharedResumePath), "Runtime resume snapshot must not reappear in shared .zavod/meta after the storage split.");
    }
    finally
    {
        DeleteScratchWorkspace(workspaceRoot);
    }
}

static void GitIgnoreKeepsZavodLocalOutOfSharedCommitsHonestly()
{
    var gitIgnorePath = Path.Combine(ProjectRootResolver.Resolve(), ".gitignore");
    var text = File.ReadAllText(gitIgnorePath);

    AssertContains(text, ".zavod.local/", "Git ignore must explicitly keep .zavod.local out of shared commits.");
}

static void WorkspaceScannerDetectsNestedSourceRootsHonestly()
{
    var root = CreateScratchWorkspace();
    try
    {
        Directory.CreateDirectory(Path.Combine(root, "docs"));
        Directory.CreateDirectory(Path.Combine(root, "builds"));
        Directory.CreateDirectory(Path.Combine(root, "drop"));
        var nestedRoot = Path.Combine(root, "repo-copy");
        var sourceRoot = Path.Combine(nestedRoot, "src");
        Directory.CreateDirectory(sourceRoot);
        File.WriteAllText(Path.Combine(nestedRoot, "CMakeLists.txt"), "project(Test)");
        File.WriteAllText(Path.Combine(sourceRoot, "main.cpp"), "int main() { return 0; }");

        var result = WorkspaceScanner.Scan(new WorkspaceScanRequest(root));

        AssertEqual(WorkspaceHealthStatus.Healthy, result.State.Health, "Nested project import should remain healthy.");
        AssertTrue(result.State.HasRecognizableProjectStructure, "Nested source/build markers should be recognized.");
        AssertTrue(result.State.HasSourceFiles, "Nested source files should be detected.");
        AssertTrue(result.State.Summary.SourceRoots.Contains("repo-copy"), "Nested source root should be derived from top-level project folder.");
        AssertTrue(result.State.Summary.EntryCandidates.Contains(Path.Combine("repo-copy", "src", "main.cpp")), "Nested entry candidate should be preserved.");
        AssertTrue(result.State.StructuralAnomalies.Any(a => a.Code == "SOURCE_NOT_AT_ROOT"), "Nested source import should honestly report that source is not at workspace root.");
    }
    finally
    {
        DeleteScratchWorkspace(root);
    }
}

static void WorkspaceScannerFlagsMultipleNestedSourceRootsHonestly()
{
    var root = CreateScratchWorkspace();
    try
    {
        var firstRepo = Path.Combine(root, "repo-a", "src");
        var secondRepo = Path.Combine(root, "repo-b", "src");
        Directory.CreateDirectory(firstRepo);
        Directory.CreateDirectory(secondRepo);
        File.WriteAllText(Path.Combine(root, "repo-a", "package.json"), "{ }");
        File.WriteAllText(Path.Combine(root, "repo-b", "Cargo.toml"), "[package]");
        File.WriteAllText(Path.Combine(firstRepo, "main.ts"), "export {};");
        File.WriteAllText(Path.Combine(secondRepo, "main.rs"), "fn main() {}");

        var result = WorkspaceScanner.Scan(new WorkspaceScanRequest(root));

        AssertEqual(WorkspaceImportKind.SourceProject, result.State.ImportKind, "Multiple nested source roots should still preserve source-oriented structure.");
        AssertTrue(result.State.StructuralAnomalies.Any(a => a.Code == "MULTIPLE_SOURCE_ROOTS"), "Multiple nested source roots should be reported honestly.");
        AssertTrue(result.State.Summary.SourceRoots.Contains("repo-a"), "First nested source root should be preserved.");
        AssertTrue(result.State.Summary.SourceRoots.Contains("repo-b"), "Second nested source root should be preserved.");
    }
    finally
    {
        DeleteScratchWorkspace(root);
    }
}

static void WorkspaceToolSummaryCarriesStructuralReasonsHonestly()
{
    var root = CreateScratchWorkspace();
    try
    {
        var nestedRoot = Path.Combine(root, "repo-copy");
        var sourceRoot = Path.Combine(nestedRoot, "src");
        Directory.CreateDirectory(sourceRoot);
        File.WriteAllText(Path.Combine(nestedRoot, "package.json"), "{ }");
        File.WriteAllText(Path.Combine(sourceRoot, "main.ts"), "export {};");

        var tool = new WorkspaceTool();
        var result = tool.Execute(new WorkspaceInspectRequest("REQ-WS-001", root, null));

        AssertContains(result.Summary, "import=SourceProject", "Workspace tool summary should expose import kind.");
        AssertContains(result.Summary, "roots=1", "Workspace tool summary should expose source root count.");
        AssertContains(result.Summary, "anomalies=SOURCE_NOT_AT_ROOT", "Workspace tool summary should expose nested-source anomaly code.");
    }
    finally
    {
        DeleteScratchWorkspace(root);
    }
}

static void WorkspaceScannerFlagsNestedNonSourcePayloadsBesideHostProjectHonestly()
{
    var root = CreateScratchWorkspace();
    try
    {
        Directory.CreateDirectory(Path.Combine(root, "src"));
        Directory.CreateDirectory(Path.Combine(root, "games", "sonic-1-copy"));
        File.WriteAllText(Path.Combine(root, "package.json"), "{ }");
        File.WriteAllText(Path.Combine(root, "src", "main.ts"), "export {};");
        File.WriteAllText(Path.Combine(root, "games", "sonic-1-copy", "Sonic.exe"), "MZ");
        File.WriteAllText(Path.Combine(root, "games", "sonic-1-copy", "manual.txt"), "notes");

        var result = WorkspaceScanner.Scan(new WorkspaceScanRequest(root));

        AssertEqual(WorkspaceImportKind.MixedImport, result.State.ImportKind, "Host project plus nested payload copy should remain mixed, not confused.");
        AssertTrue(result.State.Summary.SourceRoots.Contains("src"), "Host project source root should remain anchored to its real top-level source folder.");
        AssertFalse(result.State.Summary.SourceRoots.Contains("games"), "Nested payload copy must not be misclassified as a source root.");
        AssertTrue(result.State.StructuralAnomalies.Any(a => a.Code == "NESTED_NON_SOURCE_PAYLOADS"), "Nested non-source payloads should be reported honestly.");
    }
    finally
    {
        DeleteScratchWorkspace(root);
    }
}

static void WorkspaceScannerFlagsNestedGitBackedProjectsHonestly()
{
    var root = CreateScratchWorkspace();
    try
    {
        Directory.CreateDirectory(Path.Combine(root, "release"));
        Directory.CreateDirectory(Path.Combine(root, "source", "repo", ".git"));
        Directory.CreateDirectory(Path.Combine(root, "source", "repo", "src"));
        File.WriteAllText(Path.Combine(root, "release", "x64dbg.exe"), "MZ");
        File.WriteAllText(Path.Combine(root, "source", "repo", "CMakeLists.txt"), "project(Test)");
        File.WriteAllText(Path.Combine(root, "source", "repo", "src", "main.cpp"), "int main() { return 0; }");

        var result = WorkspaceScanner.Scan(new WorkspaceScanRequest(root));

        AssertTrue(result.State.StructuralAnomalies.Any(a => a.Code == "NESTED_GIT_PROJECTS"), "Scanner should report nested git-backed projects when release payload and nested repo coexist.");
        AssertTrue(result.State.StructuralAnomalies.Any(a => a.Message.Contains("source", StringComparison.OrdinalIgnoreCase)), "Nested git-backed project anomaly should include nested repo scope.");
    }
    finally
    {
        DeleteScratchWorkspace(root);
    }
}

static void WorkspaceScannerIgnoresGeneratedNoiseDirectoriesHonestly()
{
    var root = CreateScratchWorkspace();
    try
    {
        File.WriteAllText(Path.Combine(root, "notes.txt"), "docs only at root");

        Directory.CreateDirectory(Path.Combine(root, "node_modules", "left-pad"));
        File.WriteAllText(Path.Combine(root, "node_modules", "left-pad", "index.js"), "module.exports = {};");

        Directory.CreateDirectory(Path.Combine(root, ".zavod", "project"));
        File.WriteAllText(Path.Combine(root, ".zavod", "project", "project.md"), "# internal truth");

        Directory.CreateDirectory(Path.Combine(root, "coverage"));
        File.WriteAllText(Path.Combine(root, "coverage", "lcov.info"), "TN:");

        var result = WorkspaceScanner.Scan(new WorkspaceScanRequest(root));

        AssertEqual(WorkspaceImportKind.NonSourceImport, result.State.ImportKind, "Generated noise directories must not turn docs-only import into source project.");
        AssertEqual(0, result.State.Summary.SourceFileCount, "Ignored generated directories must not contribute source files.");
        AssertFalse(result.State.Summary.SourceRoots.Any(), "Ignored generated directories must not fabricate source roots.");
        AssertFalse(result.State.HasRecognizableProjectStructure, "Generated noise must not fabricate recognizable project structure.");
        AssertEqual(3, result.State.Summary.IgnoredNoiseFileCount, "Ignored generated files should be counted for honest noise reporting.");
        AssertTrue(result.State.Summary.IgnoredNoiseRoots.Contains(".zavod"), "Ignored ZAVOD-internal root should be preserved for noise reporting.");
        AssertTrue(result.State.Summary.IgnoredNoiseRoots.Contains("coverage"), "Ignored coverage root should be preserved for noise reporting.");
        AssertTrue(result.State.Summary.IgnoredNoiseRoots.Contains("node_modules"), "Ignored node_modules root should be preserved for noise reporting.");
        AssertTrue(result.State.StructuralAnomalies.Any(a => a.Code == "NOISY_WORKSPACE_HINT"), "Scanner should honestly report that valid import is surrounded by noisy payload.");
    }
    finally
    {
        DeleteScratchWorkspace(root);
    }
}

static void WorkspaceScannerKeepsAutomationFoldersOutOfPrimarySourceRootsHonestly()
{
    var root = CreateScratchWorkspace();
    try
    {
        Directory.CreateDirectory(Path.Combine(root, "src"));
        Directory.CreateDirectory(Path.Combine(root, ".github", "actions", "ship"));
        Directory.CreateDirectory(Path.Combine(root, ".github", "workflows"));
        File.WriteAllText(Path.Combine(root, "Cargo.toml"), "[package]\nname = \"demo\"\nversion = \"0.1.0\"\n");
        File.WriteAllText(Path.Combine(root, "src", "main.rs"), "fn main() {}\n");
        File.WriteAllText(Path.Combine(root, ".github", "actions", "ship", "main.js"), "console.log('action');\n");
        File.WriteAllText(Path.Combine(root, ".github", "workflows", "ci.yml"), "name: ci\n");

        var result = WorkspaceScanner.Scan(new WorkspaceScanRequest(root));
        var relativeFiles = result.RelevantFiles
            .Select(path => Path.GetRelativePath(root, path))
            .ToArray();

        AssertTrue(result.State.Summary.SourceRoots.Contains("src"), "Real source root should remain primary.");
        AssertFalse(result.State.Summary.SourceRoots.Contains(".github"), "Automation folders must not become primary source roots.");
        AssertTrue(relativeFiles.Contains(Path.Combine(".github", "actions", "ship", "main.js")), "Automation source files should remain scan evidence.");
        AssertTrue(result.State.Summary.EntryCandidates.Contains(Path.Combine(".github", "actions", "ship", "main.js")), "Automation entry-like files should remain cold candidates for later ranking/demotion.");
    }
    finally
    {
        DeleteScratchWorkspace(root);
    }
}

static void WorkspaceScannerRejectsSiblingIncludePathPrefixHonestly()
{
    var root = CreateScratchWorkspace();
    var sibling = root + "-evil";
    try
    {
        Directory.CreateDirectory(Path.Combine(root, "src"));
        File.WriteAllText(Path.Combine(root, "src", "main.cpp"), "int main() { return 0; }");
        Directory.CreateDirectory(sibling);
        File.WriteAllText(Path.Combine(sibling, "main.cpp"), "int main() { return 1; }");

        var result = WorkspaceScanner.Scan(new WorkspaceScanRequest(root, new[] { sibling }));
        var relativeFiles = result.RelevantFiles
            .Select(path => Path.GetRelativePath(root, path))
            .ToArray();

        AssertTrue(relativeFiles.Any(path => string.Equals(path, Path.Combine("src", "main.cpp"), StringComparison.OrdinalIgnoreCase)), "Scanner should fall back to workspace root when include path is outside boundary.");
        AssertFalse(result.RelevantFiles.Any(path => path.StartsWith(sibling, StringComparison.OrdinalIgnoreCase)), "Scanner must not accept sibling path prefixes as inside the workspace root.");
    }
    finally
    {
        DeleteScratchWorkspace(root);
        DeleteScratchWorkspace(sibling);
    }
}

static void WorkspaceScannerReportsNoisyWorkspaceHintHonestly()
{
    var root = CreateScratchWorkspace();
    try
    {
        Directory.CreateDirectory(Path.Combine(root, "src"));
        File.WriteAllText(Path.Combine(root, "package.json"), "{ }");
        File.WriteAllText(Path.Combine(root, "src", "main.ts"), "export {};");

        Directory.CreateDirectory(Path.Combine(root, "node_modules", "left-pad"));
        File.WriteAllText(Path.Combine(root, "node_modules", "left-pad", "index.js"), "module.exports = {};");

        Directory.CreateDirectory(Path.Combine(root, "dist"));
        File.WriteAllText(Path.Combine(root, "dist", "bundle.js"), "console.log('bundle');");

        var tool = new WorkspaceTool();
        var scan = WorkspaceScanner.Scan(new WorkspaceScanRequest(root));
        var toolResult = tool.Execute(new WorkspaceInspectRequest("REQ-WS-NOISE-001", root, null));

        AssertEqual(WorkspaceImportKind.SourceProject, scan.State.ImportKind, "Generated noise must not change host source project classification.");
        AssertTrue(scan.State.StructuralAnomalies.Any(a => a.Code == "NOISY_WORKSPACE_HINT"), "Scanner should emit a dedicated noisy workspace hint.");
        AssertContains(toolResult.Summary, "noise=2", "Workspace tool summary should expose ignored noise file count.");
        AssertContains(toolResult.Summary, "NOISY_WORKSPACE_HINT", "Workspace tool summary should surface noisy workspace anomaly code.");
    }
    finally
    {
        DeleteScratchWorkspace(root);
    }
}

static void WorkspaceScannerPreservesUserMaterialsAsContextCandidatesHonestly()
{
    var root = CreateScratchWorkspace();
    try
    {
        File.WriteAllText(Path.Combine(root, "notes.txt"), "current notes");
        File.WriteAllText(Path.Combine(root, "future-roadmap.md"), "# old plan");
        File.WriteAllText(Path.Combine(root, "integration-research.pdf"), "pdf");
        File.WriteAllText(Path.Combine(root, "mockup.png"), "png");
        File.WriteAllText(Path.Combine(root, "archive.zip"), "zip");

        var scan = WorkspaceScanner.Scan(new WorkspaceScanRequest(root));
        var tool = new WorkspaceTool();
        var toolResult = tool.Execute(new WorkspaceInspectRequest("REQ-WS-MATERIALS-001", root, null));

        AssertEqual(5, scan.MaterialCandidates.Count, "Relevant user materials should be preserved as context candidates.");
        AssertTrue(scan.MaterialCandidates.Any(m => m.RelativePath == "notes.txt" && m.Kind == WorkspaceMaterialKind.TextDocument), "Plain text notes should remain preserved text context.");
        AssertTrue(scan.MaterialCandidates.Any(m => m.RelativePath == "future-roadmap.md" && m.Kind == WorkspaceMaterialKind.TextDocument), "Roadmap-like markdown should remain preserved as plain text context until a later interpretation layer reads it.");
        AssertTrue(scan.MaterialCandidates.Any(m => m.RelativePath == "integration-research.pdf" && m.Kind == WorkspaceMaterialKind.PdfDocument), "Pdf research should remain preserved as context.");
        AssertTrue(scan.MaterialCandidates.Any(m => m.RelativePath == "mockup.png" && m.Kind == WorkspaceMaterialKind.ImageAsset), "Images should remain preserved as context candidates.");
        AssertTrue(scan.MaterialCandidates.Any(m => m.RelativePath == "archive.zip" && m.Kind == WorkspaceMaterialKind.ArchiveArtifact), "Archives should remain preserved as context candidates.");
        AssertContains(toolResult.Summary, "materials=5", "Workspace tool summary should expose preserved materials count.");
        AssertTrue(toolResult.ExtractedItems.Any(item => item.Kind == "user_material" && item.Reference.EndsWith("/notes.txt", StringComparison.OrdinalIgnoreCase)), "Workspace tool should expose preserved user materials.");
        AssertTrue(toolResult.ExtractedItems.Any(item => item.Kind == "user_material" && item.Reference.EndsWith("/future-roadmap.md", StringComparison.OrdinalIgnoreCase)), "Workspace tool should keep roadmap-like files as preserved user materials without semantic promotion.");
    }
    finally
    {
        DeleteScratchWorkspace(root);
    }
}

static void WorkspaceScannerPreservesOfficeAndMultimediaMaterialsHonestly()
{
    var root = CreateScratchWorkspace();
    try
    {
        File.WriteAllText(Path.Combine(root, "brief.docx"), "docx");
        File.WriteAllText(Path.Combine(root, "budget.xlsx"), "xlsx");
        File.WriteAllText(Path.Combine(root, "pitch.pptx"), "pptx");
        File.WriteAllText(Path.Combine(root, "dialog_concept_music.mp3"), "mp3");

        var scan = WorkspaceScanner.Scan(new WorkspaceScanRequest(root));
        var tool = new WorkspaceTool();
        var toolResult = tool.Execute(new WorkspaceInspectRequest("REQ-WS-MATERIALS-002", root, null));

        AssertEqual(WorkspaceImportKind.NonSourceImport, scan.State.ImportKind, "Office and multimedia materials without code should remain non-source import.");
        AssertEqual(4, scan.MaterialCandidates.Count, "Office and multimedia materials should be preserved as context candidates.");
        AssertTrue(scan.MaterialCandidates.Any(m => m.RelativePath == "brief.docx" && m.Kind == WorkspaceMaterialKind.OfficeDocument), "Docx should remain preserved as office document.");
        AssertTrue(scan.MaterialCandidates.Any(m => m.RelativePath == "budget.xlsx" && m.Kind == WorkspaceMaterialKind.Spreadsheet), "Spreadsheet should remain preserved as spreadsheet material.");
        AssertTrue(scan.MaterialCandidates.Any(m => m.RelativePath == "pitch.pptx" && m.Kind == WorkspaceMaterialKind.Presentation), "Presentation should remain preserved as presentation material.");
        AssertTrue(scan.MaterialCandidates.Any(m => m.RelativePath == "dialog_concept_music.mp3" && m.Kind == WorkspaceMaterialKind.Multimedia), "Audio should remain preserved as multimedia material.");
        AssertContains(toolResult.Summary, "materials=4", "Workspace tool summary should count office and multimedia materials too.");
    }
    finally
    {
        DeleteScratchWorkspace(root);
    }
}

static void WorkspaceScannerPreservesLocalizedHistoryLikeMaterialsAsPlainContextHonestly()
{
    var root = CreateScratchWorkspace();
    try
    {
        File.WriteAllText(Path.Combine(root, "Снапшот архитектуры.txt"), "old snapshot");
        File.WriteAllText(Path.Combine(root, "дорожная карта.pdf"), "old roadmap");
        File.WriteAllText(Path.Combine(root, "канон проекта.md"), "legacy canon");
        File.WriteAllText(Path.Combine(root, "обычные заметки.txt"), "notes");

        var scan = WorkspaceScanner.Scan(new WorkspaceScanRequest(root));
        var tool = new WorkspaceTool();
        var toolResult = tool.Execute(new WorkspaceInspectRequest("REQ-WS-MATERIALS-LOCALIZED-001", root, null));

        AssertTrue(scan.MaterialCandidates.Any(m => m.RelativePath == "Снапшот архитектуры.txt" && m.Kind == WorkspaceMaterialKind.TextDocument), "Localized snapshot-like files should remain preserved as plain text context until a later interpretation layer reads them.");
        AssertTrue(scan.MaterialCandidates.Any(m => m.RelativePath == "дорожная карта.pdf" && m.Kind == WorkspaceMaterialKind.PdfDocument), "Localized roadmap-like pdf should remain preserved as pdf context.");
        AssertTrue(scan.MaterialCandidates.Any(m => m.RelativePath == "канон проекта.md" && m.Kind == WorkspaceMaterialKind.TextDocument), "Localized canon-like markdown should remain preserved as plain text context.");
        AssertTrue(scan.MaterialCandidates.Any(m => m.RelativePath == "обычные заметки.txt" && m.Kind == WorkspaceMaterialKind.TextDocument), "Ordinary localized notes must stay plain preserved text context.");
        AssertEqual(4, toolResult.ExtractedItems.Count(item => item.Kind == "user_material"), "Workspace tool should expose localized context files as plain preserved user materials without semantic promotion.");
    }
    finally
    {
        DeleteScratchWorkspace(root);
    }
}

static void WorkspaceMaterialShortlistPrefersTextPreviewCandidatesHonestly()
{
    var root = CreateScratchWorkspace();
    try
    {
        File.WriteAllText(Path.Combine(root, "notes.txt"), "notes");
        File.WriteAllText(Path.Combine(root, "research.pdf"), "pdf");
        File.WriteAllText(Path.Combine(root, "brief.docx"), "docx");
        File.WriteAllText(Path.Combine(root, "mockup.png"), "png");
        File.WriteAllText(Path.Combine(root, "archive.zip"), "zip");
        File.WriteAllText(Path.Combine(root, "demo.mp3"), "mp3");

        var scan = WorkspaceScanner.Scan(new WorkspaceScanRequest(root));
        var shortlist = WorkspaceMaterialShortlistBuilder.Build(scan, maxCandidates: 3);
        var tool = new WorkspaceTool();
        var toolResult = tool.Execute(new WorkspaceInspectRequest("REQ-WS-MATERIALS-SHORTLIST-001", root, null));

        AssertEqual(3, shortlist.Count, "Shortlist should remain bounded to the requested text-first candidate count.");
        AssertEqual("notes.txt", shortlist[0].RelativePath, "Plain text should be preferred as the first preview candidate.");
        AssertEqual("research.pdf", shortlist[1].RelativePath, "Pdf should be preferred ahead of non-text media.");
        AssertEqual("brief.docx", shortlist[2].RelativePath, "Office text-bearing material should remain eligible for preview shortlist.");
        AssertTrue(shortlist.All(candidate => candidate.Kind is WorkspaceMaterialKind.TextDocument or WorkspaceMaterialKind.PdfDocument or WorkspaceMaterialKind.OfficeDocument or WorkspaceMaterialKind.Spreadsheet or WorkspaceMaterialKind.Presentation), "Shortlist must stay text-bearing only for the future import LLM layer.");
        AssertContains(toolResult.Summary, "previewCandidates=3", "Workspace tool summary should expose bounded preview-candidate count.");
        AssertTrue(toolResult.ExtractedItems.Any(item => item.Kind == "material_preview_candidate" && item.Reference.EndsWith("/notes.txt", StringComparison.OrdinalIgnoreCase)), "Workspace tool should expose preview shortlist items separately from the full preserved materials list.");
    }
    finally
    {
        DeleteScratchWorkspace(root);
    }
}

static void WorkspaceMaterialShortlistPrefersShallowerPreviewCandidatesHonestly()
{
    var root = CreateScratchWorkspace();
    try
    {
        Directory.CreateDirectory(Path.Combine(root, "docs", "nested"));
        File.WriteAllText(Path.Combine(root, "project_notes.txt"), "root");
        File.WriteAllText(Path.Combine(root, "docs", "guide.txt"), "mid");
        File.WriteAllText(Path.Combine(root, "docs", "nested", "deep_notes.txt"), "deep");

        var scan = WorkspaceScanner.Scan(new WorkspaceScanRequest(root));
        var shortlist = WorkspaceMaterialShortlistBuilder.Build(scan, maxCandidates: 3);

        AssertEqual(3, shortlist.Count, "Shortlist should preserve deterministic ordering across all text candidates.");
        AssertEqual("project_notes.txt", shortlist[0].RelativePath, "Shallower text file should be preferred first.");
        AssertEqual(Path.Combine("docs", "guide.txt"), shortlist[1].RelativePath, "Mid-depth text file should appear before deeper nested text.");
        AssertEqual(Path.Combine("docs", "nested", "deep_notes.txt"), shortlist[2].RelativePath, "Deep nested text should remain eligible but later in the shortlist.");
        AssertTrue(shortlist.All(candidate => candidate.SelectionReason == "text-first-preview"), "Text shortlist candidates should carry deterministic selection reason.");
    }
    finally
    {
        DeleteScratchWorkspace(root);
    }
}

static void WorkspaceImportMaterialPreviewPacketKeepsBoundedTextContextHonestly()
{
    var root = CreateScratchWorkspace();
    try
    {
        var longText = string.Join(' ', Enumerable.Repeat("alpha", 400));
        File.WriteAllText(Path.Combine(root, "project_notes.txt"), longText);
        File.WriteAllText(Path.Combine(root, "todo.md"), "first line\nsecond line");

        var scan = WorkspaceScanner.Scan(new WorkspaceScanRequest(root));
        var packet = WorkspaceImportMaterialPreviewPacketBuilder.Build(scan, maxMaterials: 4, maxCharsPerMaterial: 64);

        AssertEqual(WorkspaceImportKind.NonSourceImport, packet.ImportKind, "Preview packet should preserve import kind from scanner state.");
        AssertEqual(2, packet.Materials.Count, "Text preview packet should include bounded text materials only.");
        AssertEqual("project_notes.txt", packet.Materials[0].RelativePath, "Packet should preserve deterministic shortlist ordering.");
        AssertTrue(packet.Materials[0].WasTruncated, "Long text preview should be truncated to the configured bound.");
        AssertEqual(64, packet.Materials[0].PreviewText.Length, "Preview text must stay bounded by the configured char cap.");
        AssertEqual("todo.md", packet.Materials[1].RelativePath, "Markdown notes should remain eligible text preview materials.");
        AssertEqual("first line second line", packet.Materials[1].PreviewText, "Preview packet should normalize whitespace for cheap LLM input.");
    }
    finally
    {
        DeleteScratchWorkspace(root);
    }
}

static void WorkspaceMaterialTextEqualizerNormalizesBoundedTextHonestly()
{
    var root = CreateScratchWorkspace();
    try
    {
        File.WriteAllText(Path.Combine(root, "notes.txt"), "first line\r\n\r\n second\tline   third");
        var candidate = new WorkspaceMaterialPreviewCandidate(
            "notes.txt",
            WorkspaceMaterialKind.TextDocument,
            "text-first-preview");

        var extract = WorkspaceMaterialTextEqualizer.Build(root, candidate, maxCharsPerMaterial: 12);

        AssertEqual(WorkspaceMaterialTextExtractStatus.Extracted, extract.Status, "Plain text candidate should extract successfully.");
        AssertEqual("first line s", extract.PreviewText, "Equalizer should normalize whitespace before applying preview bound.");
        AssertTrue(extract.WasTruncated, "Bounded equalized text should report truncation honestly.");
        AssertEqual("extracted", extract.StatusReason, "Successful extraction should carry explicit status reason.");
    }
    finally
    {
        DeleteScratchWorkspace(root);
    }
}

static void WorkspaceMaterialTextEqualizerSkipsUnsupportedKindsHonestly()
{
    var root = CreateScratchWorkspace();
    try
    {
        File.WriteAllText(Path.Combine(root, "research.pdf"), "pdf");
        var candidate = new WorkspaceMaterialPreviewCandidate(
            "research.pdf",
            WorkspaceMaterialKind.PdfDocument,
            "pdf-preview");

        var extract = WorkspaceMaterialTextEqualizer.Build(root, candidate, maxCharsPerMaterial: 64);

        AssertEqual(WorkspaceMaterialTextExtractStatus.UnsupportedKind, extract.Status, "Current equalizer should stay honest about unsupported preview kinds.");
        AssertEqual(string.Empty, extract.PreviewText, "Unsupported preview kinds must not silently fabricate preview text.");
        AssertEqual("unsupported-kind", extract.StatusReason, "Unsupported preview kinds should expose an explicit status reason.");
    }
    finally
    {
        DeleteScratchWorkspace(root);
    }
}

static void WorkspaceImportMaterialPreviewPacketSkipsNonTextShortlistItemsHonestly()
{
    var root = CreateScratchWorkspace();
    try
    {
        File.WriteAllText(Path.Combine(root, "notes.txt"), "notes");
        File.WriteAllText(Path.Combine(root, "research.pdf"), "pdf");
        File.WriteAllText(Path.Combine(root, "mockup.png"), "png");
        File.WriteAllText(Path.Combine(root, "archive.zip"), "zip");

        var scan = WorkspaceScanner.Scan(new WorkspaceScanRequest(root));
        var packet = WorkspaceImportMaterialPreviewPacketBuilder.Build(scan, maxMaterials: 4, maxCharsPerMaterial: 128);

        AssertEqual(1, packet.Materials.Count, "Current preview packet should stay text-only until richer extractors exist.");
        AssertEqual("notes.txt", packet.Materials[0].RelativePath, "Plain text file should remain available for the future import LLM role.");
        AssertTrue(packet.Materials.All(material => material.Kind == WorkspaceMaterialKind.TextDocument), "Preview packet must not silently claim pdf/image/archive understanding before those extractors exist.");
    }
    finally
    {
        DeleteScratchWorkspace(root);
    }
}

static void WorkspaceImportMaterialInterpretationResultKeepsContextOnlyContractHonestly()
{
    var root = CreateScratchWorkspace();
    try
    {
        File.WriteAllText(Path.Combine(root, "notes.txt"), "project notes");
        File.WriteAllText(Path.Combine(root, "todo.md"), "next steps");

        var scan = WorkspaceScanner.Scan(new WorkspaceScanRequest(root));
        var packet = WorkspaceImportMaterialPreviewPacketBuilder.Build(scan, maxMaterials: 4, maxCharsPerMaterial: 128);
        var result = WorkspaceImportMaterialInterpretationResultBuilder.BuildEmpty(packet);

        AssertEqual(packet.WorkspaceRoot, result.WorkspaceRoot, "Interpretation result should preserve workspace root from the preview packet.");
        AssertEqual(packet.ImportKind, result.ImportKind, "Interpretation result should preserve import kind from the preview packet.");
        AssertEqual(2, result.Materials.Count, "Interpretation result should preserve one entry per preview packet material.");
        AssertEqual(0, result.ProjectDetails.Count, "Empty interpretation result must not fabricate project details before the import LLM runs.");
        AssertTrue(result.Materials.All(item => item.ContextOnly), "Interpretation result must mark every material as context-only.");
        AssertTrue(result.Materials.All(item => item.PossibleUsefulness == WorkspaceMaterialContextUsefulness.Unknown), "Empty interpretation result must not pretend usefulness before the import LLM runs.");
        AssertTrue(result.Materials.All(item => string.IsNullOrEmpty(item.Summary)), "Empty interpretation result must not fabricate summaries before the import LLM runs.");
        AssertContains(result.SummaryLine, "truth=context_only", "Interpretation summary must carry the non-authoritative contract explicitly.");
    }
    finally
    {
        DeleteScratchWorkspace(root);
    }
}

static void WorkspaceImportMaterialPromptResponseParserStaysNarrowHonestly()
{
    const string response = """
SUMMARY: Some imported materials may help clarify project context.
DETAIL: The workspace includes architecture documentation and test evidence.
STAGE: Build evidence suggests the project is in active implementation with separate main and test variants.
CURRENT_SIGNALS: Build cache and docs align with active implementation.
PLANNED_SIGNALS: Todo-like notes may describe future work.
POSSIBLY_STALE: Some draft notes could lag behind the build reality.
CONFLICT: Draft memo wording is weaker than build/config evidence.
LAYER: Core | Handles project logic | Mentioned in architecture docs.
ENTRY_POINT: cmd\tool\main.go | cli | Scanner sees a command entrypoint.
DIAGRAM_NODE: core | Core | layer
DIAGRAM_NODE: cli | CLI | entry
DIAGRAM_EDGE: cli | core | invokes | call
DIAGRAM_GROUP: app | Application | core, cli
MATERIAL: notes.txt | High | Main notes about the current project.
MATERIAL_STATE: notes.txt | Current | The notes align with current implementation-focused wording.
ignored line that should not parse
MATERIAL: todo.md | maybe | Follow-up ideas.
MATERIAL: broken entry without separators
""";

    var parsed = WorkspaceImportMaterialPromptResponseParser.Parse(response);

    AssertEqual("Some imported materials may help clarify project context.", parsed.Summary, "Parser should preserve explicit summary line.");
    AssertEqual(1, parsed.Details.Count, "Parser should preserve explicit DETAIL lines.");
    AssertEqual("The workspace includes architecture documentation and test evidence.", parsed.Details[0], "Parser should keep concrete project detail text.");
    AssertEqual(1, parsed.StageSignals.Count, "Parser should preserve explicit STAGE lines.");
    AssertEqual(1, parsed.CurrentSignals.Count, "Parser should preserve current-state lines.");
    AssertEqual(1, parsed.PlannedSignals.Count, "Parser should preserve planned-state lines.");
    AssertEqual(1, parsed.PossiblyStaleSignals.Count, "Parser should preserve possible stale lines.");
    AssertEqual(1, parsed.Conflicts.Count, "Parser should preserve conflict lines.");
    AssertEqual(1, parsed.Layers.Count, "Parser should preserve layer lines.");
    AssertEqual(1, parsed.EntryPoints.Count, "Parser should preserve entry point lines.");
    AssertEqual(2, parsed.DiagramSpec.Nodes.Count, "Parser should preserve diagram nodes.");
    AssertEqual(1, parsed.DiagramSpec.Edges.Count, "Parser should preserve diagram edges.");
    AssertEqual(1, parsed.DiagramSpec.Groups.Count, "Parser should preserve diagram groups.");
    AssertContains(parsed.StageSignals[0], "active implementation", "Parser should keep stage/status interpretation text.");
    AssertEqual(2, parsed.Materials.Count, "Parser should accept only narrow MATERIAL lines with the expected shape.");
    AssertEqual("notes.txt", parsed.Materials[0].RelativePath, "Parser should preserve material path.");
    AssertEqual(WorkspaceMaterialContextUsefulness.High, parsed.Materials[0].PossibleUsefulness, "Parser should preserve known usefulness values.");
    AssertEqual(WorkspaceMaterialTemporalStatus.Current, parsed.Materials[0].TemporalStatus, "Parser should preserve explicit material temporal status.");
    AssertContains(parsed.Materials[0].StatusNote, "align", "Parser should preserve material status notes.");
    AssertEqual(WorkspaceMaterialContextUsefulness.Unknown, parsed.Materials[1].PossibleUsefulness, "Unknown usefulness values should degrade honestly to Unknown.");
}

static void WorkspaceImportMaterialPromptResponseParserPreservesFallbackSummaryHonestly()
{
    const string response = """
The imported materials include architecture notes, testing logs, and project rules. They look useful as bounded project context, but the response is prose instead of the strict machine format.
""";

    var parsed = WorkspaceImportMaterialPromptResponseParser.Parse(response);

    AssertContains(parsed.Summary, "architecture notes", "Parser should preserve a bounded fallback summary when the model ignores the strict format.");
    AssertEqual(0, parsed.Materials.Count, "Fallback summary parsing must not fabricate material lines.");
}

static void WorkspaceImportMaterialPromptResponseParserAcceptsThreePartDiagramEdgesHonestly()
{
    const string response = """
SUMMARY: Project context is visible.
DIAGRAM_NODE: ui | UI | layer
DIAGRAM_NODE: core | Core | layer
DIAGRAM_EDGE: ui | core | reflects state
""";

    var parsed = WorkspaceImportMaterialPromptResponseParser.Parse(response);

    AssertEqual(2, parsed.DiagramSpec.Nodes.Count, "Parser should preserve diagram nodes when edge contract is short.");
    AssertEqual(1, parsed.DiagramSpec.Edges.Count, "Parser should accept a three-part diagram edge honestly.");
    AssertEqual("observed", parsed.DiagramSpec.Edges[0].Kind, "Short diagram edge should degrade honestly to observed kind.");
}

static void WorkspaceImportMaterialInterpretationMapsResponseHonestly()
{
    var root = CreateScratchWorkspace();
    try
    {
        File.WriteAllText(Path.Combine(root, "notes.txt"), "project notes");
        File.WriteAllText(Path.Combine(root, "todo.md"), "next steps");

        var scan = WorkspaceScanner.Scan(new WorkspaceScanRequest(root));
        var packet = WorkspaceImportMaterialPreviewPacketBuilder.Build(scan, maxMaterials: 4, maxCharsPerMaterial: 64);
        var response = WorkspaceImportMaterialPromptResponseParser.Parse("""
SUMMARY: Imported text materials may provide useful context.
DETAIL: The project exposes both architecture notes and task-oriented text guidance.
STAGE: The packet suggests active implementation, but some notes may be future-facing.
CURRENT_SIGNALS: Build/config evidence looks current.
PLANNED_SIGNALS: Todo wording points at follow-up work.
LAYER: Core | Handles main project logic | Mentioned in current notes.
ENTRY_POINT: notes.txt | context | Used as a current context anchor.
DIAGRAM_NODE: core | Core | layer
MATERIAL: notes.txt | High | Likely current project notes.
MATERIAL_STATE: notes.txt | Current | Notes align with current project reality.
MATERIAL_STATE: todo.md | Planned | The file looks like follow-up work rather than current truth.
MATERIAL: outside.txt | Low | Should be ignored because it is not in the packet.
""");
        var result = WorkspaceImportMaterialInterpretationResultBuilder.BuildFromResponse(packet, response);

        AssertEqual(2, result.Materials.Count, "Interpretation result should stay bound to packet materials only.");
        AssertEqual(1, result.ProjectDetails.Count, "Interpretation result should preserve parsed project details.");
        AssertEqual(1, result.ProjectStageSignals.Count, "Interpretation result should preserve parsed stage/status signals.");
        AssertEqual(1, result.CurrentSignals.Count, "Interpretation result should preserve parsed current signals.");
        AssertEqual(1, result.PlannedSignals.Count, "Interpretation result should preserve parsed planned signals.");
        AssertEqual(1, result.Layers.Count, "Interpretation result should preserve parsed layers.");
        AssertEqual(1, result.EntryPoints.Count, "Interpretation result should preserve parsed entry points.");
        AssertEqual(1, result.DiagramSpec.Nodes.Count, "Interpretation result should preserve parsed diagram nodes.");
        AssertContains(result.ProjectDetails[0], "architecture notes", "Interpretation result should keep concrete project detail text.");
        AssertContains(result.ProjectStageSignals[0], "active implementation", "Interpretation result should keep stage/status interpretation text.");
        AssertEqual("Likely current project notes.", result.Materials[0].Summary, "Known packet material should receive mapped summary.");
        AssertEqual(WorkspaceMaterialContextUsefulness.High, result.Materials[0].PossibleUsefulness, "Known packet material should receive mapped usefulness.");
        AssertEqual(WorkspaceMaterialTemporalStatus.Current, result.Materials[0].TemporalStatus, "Known packet material should receive mapped temporal status.");
        AssertContains(result.Materials[0].StatusNote, "current project reality", "Known packet material should receive mapped temporal note.");
        AssertEqual(string.Empty, result.Materials[1].Summary, "Unmentioned packet material should stay empty instead of fabricated.");
        AssertEqual(WorkspaceMaterialContextUsefulness.Unknown, result.Materials[1].PossibleUsefulness, "Unmentioned packet material should stay Unknown.");
        AssertEqual(WorkspaceMaterialTemporalStatus.Planned, result.Materials[1].TemporalStatus, "Temporal-only packet material should still receive mapped temporal status.");
        AssertContains(result.Materials[1].StatusNote, "follow-up work", "Temporal-only packet material should preserve status note.");
        AssertTrue(result.Materials.All(item => item.ContextOnly), "Mapped interpretation must remain context-only.");
        AssertContains(result.SummaryLine, "truth=context_only", "Mapped interpretation summary must preserve context-only contract.");
    }
    finally
    {
        DeleteScratchWorkspace(root);
    }
}

static void WorkspaceImportMaterialInterpretationDoesNotInventLayersFromWeakColdPackHonestly()
{
    var root = CreateScratchWorkspace();
    try
    {
        Directory.CreateDirectory(Path.Combine(root, "src"));
        File.WriteAllText(Path.Combine(root, "src", "main.cpp"), "int main() { return 0; }");
        File.WriteAllText(Path.Combine(root, "README.md"), "Beautiful browser-like experience with a nice interface.");

        var scan = WorkspaceScanner.Scan(new WorkspaceScanRequest(root));
        var packet = new WorkspaceMaterialRuntimeFront().BuildPreviewPacket(scan, maxMaterials: 4, maxCharsPerMaterial: 128);
        var response = new WorkspaceImportMaterialPromptResponse(
            string.Empty,
            Array.Empty<string>(),
            Array.Empty<string>(),
            Array.Empty<string>(),
            Array.Empty<string>(),
            Array.Empty<string>(),
            Array.Empty<string>(),
            Array.Empty<WorkspaceImportMaterialLayerInterpretation>(),
            Array.Empty<WorkspaceImportMaterialModuleInterpretation>(),
            Array.Empty<WorkspaceImportMaterialEntryPointInterpretation>(),
            new ArchitectureDiagramSpec(string.Empty, Array.Empty<ArchitectureDiagramNode>(), Array.Empty<ArchitectureDiagramEdge>(), Array.Empty<ArchitectureDiagramGroup>(), Array.Empty<string>(), new ArchitectureDiagramRenderHints("left-to-right", Array.Empty<string>(), true)),
            Array.Empty<WorkspaceImportMaterialPromptResponseItem>());

        var result = WorkspaceImportMaterialInterpretationResultBuilder.BuildFromResponse(packet, response);

        AssertFalse(result.Layers.Any(layer => string.Equals(layer.Name, "UI", StringComparison.OrdinalIgnoreCase)), "Weak cold pack should not invent UI layer without importer evidence.");
        AssertFalse(result.Layers.Any(layer => string.Equals(layer.Name, "Service", StringComparison.OrdinalIgnoreCase)), "Weak cold pack should not invent Service layer without importer evidence.");
        AssertFalse(result.Layers.Any(layer => string.Equals(layer.Name, "Mod Platform", StringComparison.OrdinalIgnoreCase)), "Weak cold pack should not invent Mod Platform layer without importer evidence.");
    }
    finally
    {
        DeleteScratchWorkspace(root);
    }
}

static void WorkspaceImportMaterialDiagramFallbackStaysCoarseWhenEvidenceIsWeakHonestly()
{
    var root = CreateScratchWorkspace();
    try
    {
        Directory.CreateDirectory(Path.Combine(root, "src"));
        File.WriteAllText(Path.Combine(root, "src", "main.cpp"), "int main() { return 0; }");

        var scan = WorkspaceScanner.Scan(new WorkspaceScanRequest(root));
        var packet = new WorkspaceMaterialRuntimeFront().BuildPreviewPacket(scan, maxMaterials: 4, maxCharsPerMaterial: 96);
        var response = new WorkspaceImportMaterialPromptResponse(
            string.Empty,
            Array.Empty<string>(),
            Array.Empty<string>(),
            Array.Empty<string>(),
            Array.Empty<string>(),
            Array.Empty<string>(),
            Array.Empty<string>(),
            Array.Empty<WorkspaceImportMaterialLayerInterpretation>(),
            Array.Empty<WorkspaceImportMaterialModuleInterpretation>(),
            Array.Empty<WorkspaceImportMaterialEntryPointInterpretation>(),
            new ArchitectureDiagramSpec(string.Empty, Array.Empty<ArchitectureDiagramNode>(), Array.Empty<ArchitectureDiagramEdge>(), Array.Empty<ArchitectureDiagramGroup>(), Array.Empty<string>(), new ArchitectureDiagramRenderHints("left-to-right", Array.Empty<string>(), true)),
            Array.Empty<WorkspaceImportMaterialPromptResponseItem>());

        var result = WorkspaceImportMaterialInterpretationResultBuilder.BuildFromResponse(packet, response);

        AssertFalse(result.DiagramSpec.Edges.Any(edge => string.Equals(edge.Label, "observed flow", StringComparison.OrdinalIgnoreCase)), "Weak fallback diagram should not invent coarse flow chains.");
        AssertTrue(result.DiagramSpec.Edges.Count <= 1, "Weak fallback diagram should stay bounded instead of fabricating layered chains.");
    }
    finally
    {
        DeleteScratchWorkspace(root);
    }
}

static void WorkspaceImportMaterialInterpretationPrefersBootstrapEntryPointsOverNestedMainsHonestly()
{
    var root = CreateScratchWorkspace();
    try
    {
        Directory.CreateDirectory(Path.Combine(root, "src", "dbg"));
        Directory.CreateDirectory(Path.Combine(root, "src", "gui"));
        Directory.CreateDirectory(Path.Combine(root, "src", "cross", "hex_viewer"));
        Directory.CreateDirectory(Path.Combine(root, "docs"));

        File.WriteAllText(Path.Combine(root, "src", "dbg", "main.cpp"), "int main() { return 0; }");
        File.WriteAllText(Path.Combine(root, "src", "gui", "main.cpp"), "int main() { return 0; }");
        File.WriteAllText(Path.Combine(root, "src", "cross", "hex_viewer", "main.cpp"), "int main() { return 0; }");
        File.WriteAllText(Path.Combine(root, "docs", "main.cpp"), "int main() { return 0; }");

        var scan = WorkspaceScanner.Scan(new WorkspaceScanRequest(root));
        var packet = new WorkspaceMaterialRuntimeFront().BuildPreviewPacket(scan, maxMaterials: 4, maxCharsPerMaterial: 96);
        var response = new WorkspaceImportMaterialPromptResponse(
            string.Empty,
            Array.Empty<string>(),
            Array.Empty<string>(),
            Array.Empty<string>(),
            Array.Empty<string>(),
            Array.Empty<string>(),
            Array.Empty<string>(),
            Array.Empty<WorkspaceImportMaterialLayerInterpretation>(),
            Array.Empty<WorkspaceImportMaterialModuleInterpretation>(),
            Array.Empty<WorkspaceImportMaterialEntryPointInterpretation>(),
            new ArchitectureDiagramSpec(string.Empty, Array.Empty<ArchitectureDiagramNode>(), Array.Empty<ArchitectureDiagramEdge>(), Array.Empty<ArchitectureDiagramGroup>(), Array.Empty<string>(), new ArchitectureDiagramRenderHints("left-to-right", Array.Empty<string>(), true)),
            Array.Empty<WorkspaceImportMaterialPromptResponseItem>());

        var result = WorkspaceImportMaterialInterpretationResultBuilder.BuildFromResponse(packet, response);

        AssertTrue(result.EntryPoints.Count > 0, "Fallback interpretation should preserve at least one cold entry point.");
        AssertTrue(
            string.Equals(result.EntryPoints[0].RelativePath, Path.Combine("src", "dbg", "main.cpp"), StringComparison.OrdinalIgnoreCase) ||
            string.Equals(result.EntryPoints[0].RelativePath, Path.Combine("src", "gui", "main.cpp"), StringComparison.OrdinalIgnoreCase),
            "Fallback interpretation should keep one of the shallow primary mains ahead of nested helper mains.");
        AssertFalse(string.Equals(result.EntryPoints[0].RelativePath, "src\\cross\\hex_viewer\\main.cpp", StringComparison.OrdinalIgnoreCase), "Fallback interpretation should not prefer cross/helper main over primary bootstrap.");
    }
    finally
    {
        DeleteScratchWorkspace(root);
    }
}

static void WorkspaceImportMaterialInterpretationDemotesNeutralSecondaryMainsHonestly()
{
    var root = CreateScratchWorkspace();
    try
    {
        Directory.CreateDirectory(Path.Combine(root, "src"));
        Directory.CreateDirectory(Path.Combine(root, "src", "helpers", "bridge"));
        Directory.CreateDirectory(Path.Combine(root, "src", "support", "debugger_tool"));
        Directory.CreateDirectory(Path.Combine(root, "examples", "demo_app"));

        File.WriteAllText(Path.Combine(root, "src", "main.cpp"), "int main() { return 0; }");
        File.WriteAllText(Path.Combine(root, "src", "helpers", "bridge", "main.cpp"), "int main() { return 0; }");
        File.WriteAllText(Path.Combine(root, "src", "support", "debugger_tool", "main.cpp"), "int main() { return 0; }");
        File.WriteAllText(Path.Combine(root, "examples", "demo_app", "main.cpp"), "int main() { return 0; }");

        var scan = WorkspaceScanner.Scan(new WorkspaceScanRequest(root));
        var packet = new WorkspaceMaterialRuntimeFront().BuildPreviewPacket(scan, maxMaterials: 4, maxCharsPerMaterial: 96);
        var response = new WorkspaceImportMaterialPromptResponse(
            string.Empty,
            Array.Empty<string>(),
            Array.Empty<string>(),
            Array.Empty<string>(),
            Array.Empty<string>(),
            Array.Empty<string>(),
            Array.Empty<string>(),
            Array.Empty<WorkspaceImportMaterialLayerInterpretation>(),
            Array.Empty<WorkspaceImportMaterialModuleInterpretation>(),
            Array.Empty<WorkspaceImportMaterialEntryPointInterpretation>(),
            new ArchitectureDiagramSpec(string.Empty, Array.Empty<ArchitectureDiagramNode>(), Array.Empty<ArchitectureDiagramEdge>(), Array.Empty<ArchitectureDiagramGroup>(), Array.Empty<string>(), new ArchitectureDiagramRenderHints("left-to-right", Array.Empty<string>(), true)),
            Array.Empty<WorkspaceImportMaterialPromptResponseItem>());

        var result = WorkspaceImportMaterialInterpretationResultBuilder.BuildFromResponse(packet, response);

        AssertTrue(result.EntryPoints.Count > 0, "Fallback interpretation should preserve at least one neutral primary entry point.");
        AssertEqual(Path.Combine("src", "main.cpp"), result.EntryPoints[0].RelativePath, "Shallow primary main should outrank neutral helper and support mains.");
        AssertFalse(result.EntryPoints.Any(entry => string.Equals(entry.RelativePath, Path.Combine("examples", "demo_app", "main.cpp"), StringComparison.OrdinalIgnoreCase)), "Demo subtree mains should be dropped when not strongly confirmed.");
        AssertFalse(string.Equals(result.EntryPoints[0].RelativePath, Path.Combine("src", "helpers", "bridge", "main.cpp"), StringComparison.OrdinalIgnoreCase), "Helper subtree main must not outrank the primary entry.");
        AssertFalse(string.Equals(result.EntryPoints[0].RelativePath, Path.Combine("src", "support", "debugger_tool", "main.cpp"), StringComparison.OrdinalIgnoreCase), "Support/debug subtree main must not outrank the primary entry.");
    }
    finally
    {
        DeleteScratchWorkspace(root);
    }
}

static void WorkspaceImportMaterialInterpretationDemotesWorkflowAndToolMainsHonestly()
{
    var root = CreateScratchWorkspace();
    try
    {
        Directory.CreateDirectory(Path.Combine(root, "src"));
        Directory.CreateDirectory(Path.Combine(root, ".github", "actions", "ship"));
        Directory.CreateDirectory(Path.Combine(root, ".storybook"));
        Directory.CreateDirectory(Path.Combine(root, "xtask", "src"));
        Directory.CreateDirectory(Path.Combine(root, "tools", "packager"));

        File.WriteAllText(Path.Combine(root, "src", "main.ts"), "export function main() {}");
        File.WriteAllText(Path.Combine(root, ".github", "actions", "ship", "main.js"), "console.log('action');");
        File.WriteAllText(Path.Combine(root, ".storybook", "main.ts"), "export default {};");
        File.WriteAllText(Path.Combine(root, "xtask", "src", "main.rs"), "fn main() {}");
        File.WriteAllText(Path.Combine(root, "tools", "packager", "main.go"), "package main\nfunc main() {}");

        var scan = WorkspaceScanner.Scan(new WorkspaceScanRequest(root));
        var packet = new WorkspaceMaterialRuntimeFront().BuildPreviewPacket(scan, maxMaterials: 4, maxCharsPerMaterial: 96);
        var result = WorkspaceImportMaterialInterpretationResultBuilder.BuildFromResponse(
            packet,
            new WorkspaceImportMaterialPromptResponse(
                string.Empty,
                Array.Empty<string>(),
                Array.Empty<string>(),
                Array.Empty<string>(),
                Array.Empty<string>(),
                Array.Empty<string>(),
                Array.Empty<string>(),
                Array.Empty<WorkspaceImportMaterialLayerInterpretation>(),
                Array.Empty<WorkspaceImportMaterialModuleInterpretation>(),
                Array.Empty<WorkspaceImportMaterialEntryPointInterpretation>(),
                new ArchitectureDiagramSpec(string.Empty, Array.Empty<ArchitectureDiagramNode>(), Array.Empty<ArchitectureDiagramEdge>(), Array.Empty<ArchitectureDiagramGroup>(), Array.Empty<string>(), new ArchitectureDiagramRenderHints("left-to-right", Array.Empty<string>(), true)),
                Array.Empty<WorkspaceImportMaterialPromptResponseItem>()));

        AssertTrue(result.EntryPoints.Count > 0, "Fallback interpretation should keep at least one entry candidate.");
        AssertEqual(Path.Combine("src", "main.ts"), result.EntryPoints[0].RelativePath, "Shallow app entry should outrank workflow and tool mains.");
        AssertFalse(string.Equals(result.EntryPoints[0].RelativePath, Path.Combine(".github", "actions", "ship", "main.js"), StringComparison.OrdinalIgnoreCase), "Workflow action main must not become the primary entry.");
        AssertFalse(string.Equals(result.EntryPoints[0].RelativePath, Path.Combine(".storybook", "main.ts"), StringComparison.OrdinalIgnoreCase), "Storybook main must not become the primary entry.");
        AssertFalse(string.Equals(result.EntryPoints[0].RelativePath, Path.Combine("xtask", "src", "main.rs"), StringComparison.OrdinalIgnoreCase), "xtask main must not become the primary entry.");
    }
    finally
    {
        DeleteScratchWorkspace(root);
    }
}

static void WorkspaceImportMaterialSummaryIsAdapterOwnedNotScannerOwnedHonestly()
{
    var root = CreateScratchWorkspace();
    try
    {
        Directory.CreateDirectory(Path.Combine(root, "src"));
        File.WriteAllText(Path.Combine(root, "src", "main.cpp"), "int main() { return 0; }");

        var scan = WorkspaceScanner.Scan(new WorkspaceScanRequest(root));
        var packet = new WorkspaceMaterialRuntimeFront().BuildPreviewPacket(scan, maxMaterials: 4, maxCharsPerMaterial: 96);
        var pack = packet.EvidencePack ?? throw new InvalidOperationException("Evidence pack should be present.");
        AssertTrue(string.IsNullOrWhiteSpace(pack.TreeSummary), "Scanner-owned tree summary should stay empty during cold-boundary transition.");

        var response = new WorkspaceImportMaterialPromptResponse(
            string.Empty,
            Array.Empty<string>(),
            Array.Empty<string>(),
            Array.Empty<string>(),
            Array.Empty<string>(),
            Array.Empty<string>(),
            Array.Empty<string>(),
            Array.Empty<WorkspaceImportMaterialLayerInterpretation>(),
            Array.Empty<WorkspaceImportMaterialModuleInterpretation>(),
            Array.Empty<WorkspaceImportMaterialEntryPointInterpretation>(),
            new ArchitectureDiagramSpec(string.Empty, Array.Empty<ArchitectureDiagramNode>(), Array.Empty<ArchitectureDiagramEdge>(), Array.Empty<ArchitectureDiagramGroup>(), Array.Empty<string>(), new ArchitectureDiagramRenderHints("left-to-right", Array.Empty<string>(), true)),
            Array.Empty<WorkspaceImportMaterialPromptResponseItem>());

        var result = WorkspaceImportMaterialInterpretationResultBuilder.BuildFromResponse(packet, response);

        AssertContains(result.SummaryLine, "observations=", "Fallback summary should be synthesized by importer adapter from cold evidence.");
        AssertContains(result.SummaryLine, "patterns=", "Fallback summary should mention cold pattern counts instead of scanner-authored narrative.");
    }
    finally
    {
        DeleteScratchWorkspace(root);
    }
}

static void WorkspaceImportMaterialPromptRequestKeepsBoundedContextHonestly()
{
    var root = CreateScratchWorkspace();
    try
    {
        File.WriteAllText(Path.Combine(root, "notes.txt"), "project notes for preview");
        File.WriteAllText(Path.Combine(root, "todo.md"), "next steps");
        File.WriteAllText(Path.Combine(root, "CMakeLists.txt"), "cmake_minimum_required(VERSION 3.30)\nproject(Test LANGUAGES CXX)\nset(CMAKE_CXX_STANDARD 20)");
        File.WriteAllText(Path.Combine(root, "CMakePresets.json"), "{ \"version\": 6, \"configurePresets\": [{ \"name\": \"ninja-debug\", \"generator\": \"Ninja\" }] }");
        File.WriteAllText(Path.Combine(root, "CMakeCache.txt"), "DSL_BUILD_TESTS:BOOL=OFF\nQt6_DIR:PATH=C:/Qt/6.10.0/msvc2022_64");

        var scan = WorkspaceScanner.Scan(new WorkspaceScanRequest(root));
        var packet = new WorkspaceMaterialRuntimeFront().BuildPreviewPacket(scan, maxMaterials: 4, maxCharsPerMaterial: 64);
        var request = WorkspaceImportMaterialPromptRequestBuilder.Build(packet);

        AssertContains(request.SystemPrompt, "Import Materials Interpreter", "Prompt request should use the stable import system prompt.");
        AssertContains(request.SystemPrompt, "do not create or mutate project truth", "Prompt request should preserve non-truth boundary in the system prompt.");
        AssertContains(request.SystemPrompt, "evidence over words", "Prompt request should keep the cheap evidence-first stance explicit.");
        AssertContains(request.SystemPrompt, "SUMMARY:", "System prompt should require the explicit machine-readable summary contract.");
        AssertContains(request.SystemPrompt, "DETAIL:", "System prompt should require the explicit project detail contract.");
        AssertContains(request.SystemPrompt, "STAGE:", "System prompt should require the explicit stage/status contract.");
        AssertContains(request.SystemPrompt, "MATERIAL_STATE:", "System prompt should require the explicit material temporal status contract.");
        AssertContains(request.UserPrompt, "[IMPORT PROJECT]", "User prompt should carry explicit import section.");
        AssertContains(request.UserPrompt, "[EVIDENCE PACK SNAPSHOT]", "User prompt should carry evidence-pack snapshot section.");
        AssertContains(request.UserPrompt, "[TECHNICAL EVIDENCE]", "User prompt should carry bounded build/config evidence.");
        AssertContains(request.UserPrompt, "[MATERIAL PREVIEW INPUTS]", "User prompt should carry bounded material previews.");
        AssertContains(request.UserPrompt, "[RESPONSE FORMAT]", "User prompt should carry the explicit response format contract.");
        AssertContains(request.UserPrompt, "DETAIL: <concrete project detail>", "User prompt should include the exact structured project detail line format.");
        AssertContains(request.UserPrompt, "STAGE: <short evidence-based signal about current stage, active work, likely plan, or possible staleness>", "User prompt should include the exact structured stage/status line format.");
        AssertContains(request.UserPrompt, "CURRENT_SIGNALS: <current implementation or build reality>", "User prompt should include the explicit current-signals line format.");
        AssertContains(request.UserPrompt, "LAYER: <name> | <responsibility> | <evidence note>", "User prompt should include the explicit layer line format.");
        AssertContains(request.UserPrompt, "ENTRY_POINT: <relative path> | <role> | <note>", "User prompt should include the explicit entry-point line format.");
        AssertContains(request.UserPrompt, "DIAGRAM_NODE: <id> | <label> | <kind>", "User prompt should include the explicit diagram node line format.");
        AssertContains(request.UserPrompt, "MATERIAL: <relative path> | <Unknown|Low|Medium|High> | <short evidence-based summary>", "User prompt should include the exact structured material line format.");
        AssertContains(request.UserPrompt, "MATERIAL_STATE: <relative path> | <Unknown|Current|Planned|Historical|PossiblyStale|Conflicting> | <short evidence-based status note>", "User prompt should include the exact structured material status line format.");
        AssertContains(request.UserPrompt, "path: CMakeLists.txt", "User prompt should expose technical evidence paths.");
        AssertContains(request.UserPrompt, "DSL_BUILD_TESTS:BOOL=OFF", "User prompt should expose bounded custom config flags when they are present.");
        AssertContains(request.UserPrompt, "Qt6_DIR:PATH=C:/Qt/6.10.0/msvc2022_64", "User prompt should expose bounded framework/version hints when they are present.");
        AssertContains(request.UserPrompt, "path: notes.txt", "User prompt should include material path.");
        AssertContains(request.UserPrompt, "selection_reason: text-first-preview", "User prompt should preserve deterministic shortlist reason.");
        AssertContains(request.UserPrompt, "preview:", "User prompt should include extracted preview text.");
    }
    finally
    {
        DeleteScratchWorkspace(root);
    }
}

static void WorkspaceImportMaterialInterpretationInfersStrongContentAnchorsHonestly()
{
    var root = CreateScratchWorkspace();
    try
    {
        var documentName = "notes.txt";
        File.WriteAllText(
            Path.Combine(root, documentName),
            "Проектная конституция. Архитектурные инварианты. UI -> Core -> Runtime. Core владеет truth платформы.");

        var scan = WorkspaceScanner.Scan(new WorkspaceScanRequest(root));
        var packet = WorkspaceImportMaterialPreviewPacketBuilder.Build(scan, maxMaterials: 2, maxCharsPerMaterial: 256);
        var emptyResponse = WorkspaceImportMaterialPromptResponseParser.Parse("SUMMARY: Контекст пока не интерпретирован.");
        var result = WorkspaceImportMaterialInterpretationResultBuilder.BuildFromResponse(packet, emptyResponse);
        var material = result.Materials.Single(item => string.Equals(item.RelativePath, documentName, StringComparison.OrdinalIgnoreCase));

        AssertEqual(WorkspaceMaterialContextUsefulness.High, material.PossibleUsefulness, "Content-rich architecture document should not fall to Unknown only because its file name is generic.");
        AssertContains(material.Summary, "Context material", "Fallback summary should stay neutral while reflecting content-derived project signals.");
        AssertFalse(material.Summary.Contains("инвариант", StringComparison.OrdinalIgnoreCase), "Fallback summary must not claim architecture invariants from material markers.");
    }
    finally
    {
        DeleteScratchWorkspace(root);
    }
}

static void WorkspaceImportMaterialPromptRequestIsDeterministicHonestly()
{
    var root = CreateScratchWorkspace();
    try
    {
        File.WriteAllText(Path.Combine(root, "notes.txt"), "project notes");

        var scan = WorkspaceScanner.Scan(new WorkspaceScanRequest(root));
        var packet = WorkspaceImportMaterialPreviewPacketBuilder.Build(scan, maxMaterials: 4, maxCharsPerMaterial: 64);
        var first = WorkspaceImportMaterialPromptRequestBuilder.Build(packet);
        var second = WorkspaceImportMaterialPromptRequestBuilder.Build(packet);

        AssertEqual(first.SystemPrompt, second.SystemPrompt, "Import material system prompt must be deterministic.");
        AssertEqual(first.UserPrompt, second.UserPrompt, "Import material user prompt must be deterministic.");
    }
    finally
    {
        DeleteScratchWorkspace(root);
    }
}

static void WorkspaceImportMaterialPromptRequestFollowsUserLanguageHonestly()
{
    var previousCulture = CultureInfo.CurrentCulture;
    var previousUiCulture = CultureInfo.CurrentUICulture;
    var root = CreateScratchWorkspace();
    try
    {
        CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("ru-RU");
        CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("ru-RU");

        File.WriteAllText(Path.Combine(root, "notes.txt"), "project notes for preview");

        var scan = WorkspaceScanner.Scan(new WorkspaceScanRequest(root));
        var packet = WorkspaceImportMaterialPreviewPacketBuilder.Build(scan, maxMaterials: 2, maxCharsPerMaterial: 64);
        var request = WorkspaceImportMaterialPromptRequestBuilder.Build(packet);

        AssertContains(request.UserPrompt, "[DOCUMENTATION LANGUAGE]", "User prompt should expose the documentation language section.");
        AssertContains(request.UserPrompt, "language_tag: ru-RU", "User prompt should carry the active user documentation language tag.");
        AssertContains(request.UserPrompt, "language_native: русский", "User prompt should carry the active native documentation language.");
        AssertContains(request.SystemPrompt, "keep human-facing interpretation in the user's documentation language", "System prompt should preserve the user-language contract.");
        AssertContains(request.UserPrompt, "Write all human-facing content after the prefixes in the user's documentation language", "User prompt should require human-facing output in the user's language.");
    }
    finally
    {
        CultureInfo.CurrentCulture = previousCulture;
        CultureInfo.CurrentUICulture = previousUiCulture;
        DeleteScratchWorkspace(root);
    }
}

static void WorkspaceImportMaterialInterpretationResultIsDeterministicHonestly()
{
    var root = CreateScratchWorkspace();
    try
    {
        File.WriteAllText(Path.Combine(root, "notes.txt"), "project notes");

        var scan = WorkspaceScanner.Scan(new WorkspaceScanRequest(root));
        var packet = WorkspaceImportMaterialPreviewPacketBuilder.Build(scan, maxMaterials: 4, maxCharsPerMaterial: 128);
        var first = WorkspaceImportMaterialInterpretationResultBuilder.BuildEmpty(packet);
        var second = WorkspaceImportMaterialInterpretationResultBuilder.BuildEmpty(packet);

        AssertEqual(first.WorkspaceRoot, second.WorkspaceRoot, "Interpretation result must preserve deterministic workspace root.");
        AssertEqual(first.ImportKind, second.ImportKind, "Interpretation result must preserve deterministic import kind.");
        AssertTrue(first.SourceRoots.SequenceEqual(second.SourceRoots), "Interpretation result must preserve deterministic source root ordering.");
        AssertTrue(first.Materials.SequenceEqual(second.Materials), "Interpretation result must preserve deterministic material ordering and values.");
        AssertEqual(first.SummaryLine, second.SummaryLine, "Interpretation result summary must remain deterministic.");
    }
    finally
    {
        DeleteScratchWorkspace(root);
    }
}

static void WorkspaceMaterialRuntimeFrontPreparesMixedMaterialsHonestly()
{
    var root = CreateScratchWorkspace();
    try
    {
        File.WriteAllText(Path.Combine(root, "notes.txt"), "first line\nsecond line");
        File.WriteAllText(Path.Combine(root, "CMakeLists.txt"), "cmake_minimum_required(VERSION 3.30)\nproject(Test LANGUAGES CXX)\nset(CMAKE_CXX_STANDARD 20)");
        File.WriteAllText(Path.Combine(root, "CMakePresets.json"), "{ \"version\": 6, \"configurePresets\": [{ \"name\": \"ninja-debug\", \"generator\": \"Ninja\" }] }");
        File.WriteAllText(Path.Combine(root, "CMakeCache.txt"), "DSL_BUILD_TESTS:BOOL=OFF\nQt6_DIR:PATH=C:/Qt/6.10.0/msvc2022_64");
        File.WriteAllText(Path.Combine(root, "spec.pdf"), "placeholder");
        File.WriteAllText(Path.Combine(root, "bundle.zip"), "placeholder");
        File.WriteAllText(Path.Combine(root, "shot.png"), "placeholder");

        var scan = WorkspaceScanner.Scan(new WorkspaceScanRequest(root));
        var runner = new FakeExternalProcessRunner(request => request.Purpose switch
        {
            var purpose when purpose.StartsWith("pdf_extract:pdftotext", StringComparison.Ordinal) => new ExternalProcessResult(0, "pdf extracted context", string.Empty, false),
            "archive_list" => new ExternalProcessResult(0, "2026-04-08 12:00:00 ....A 32 src/app.cs", string.Empty, false),
            "image_inspect" => new ExternalProcessResult(0, "format=PNG; size=640x480; mode=RGBA", string.Empty, false),
            _ => new ExternalProcessResult(1, string.Empty, $"Unexpected purpose: {request.Purpose}", false)
        });
        var runtimeFront = new WorkspaceMaterialRuntimeFront(
            new TextMaterialRuntimeService(),
            new PdfExtractionRuntimeService(runner),
            new ArchiveInspectionRuntimeService(runner),
            new ImageInspectionRuntimeService(runner));

        var packet = runtimeFront.BuildPreviewPacket(scan, maxMaterials: 6, maxCharsPerMaterial: 64);

        AssertTrue(packet.EvidencePack is not null, "Runtime front should attach an evidence pack for import interpretation.");
        AssertEqual(4, packet.Materials.Count, "Runtime front should preserve mixed import-facing materials instead of dropping them.");
        AssertTrue(packet.TechnicalEvidence.Count >= 3, "Runtime front should preserve bounded technical evidence for build/config understanding.");
        AssertTrue(packet.TechnicalEvidence.Any(item => item.RelativePath == "CMakePresets.json"), "Technical evidence should include CMake presets when present.");
        AssertTrue(packet.TechnicalEvidence.Any(item => item.RelativePath == "CMakeLists.txt"), "Technical evidence should include root CMake script when present.");
        AssertTrue(packet.TechnicalEvidence.Any(item => item.RelativePath == "CMakeCache.txt"), "Technical evidence should include build cache when present.");
        AssertContains(string.Join(" ", packet.EvidencePack!.TechnicalPassport.BuildSystems), "cmake", "Evidence pack should preserve observed build systems.");
        AssertTrue(packet.EvidencePack.Signals.Count > 0, "Evidence pack should preserve coarse evidence-backed signals.");
        AssertEqual("notes.txt", packet.Materials[0].RelativePath, "Text material should remain first in deterministic runtime ordering.");
        AssertEqual("spec.pdf", packet.Materials[1].RelativePath, "PDF material should remain visible to import runtime.");
        AssertEqual("bundle.zip", packet.Materials[2].RelativePath, "Archive material should remain visible to import runtime.");
        AssertEqual("shot.png", packet.Materials[3].RelativePath, "Image material should remain visible to import runtime.");
        AssertEqual("Prepared", packet.Materials[1].PreparationStatus, "PDF runtime material should preserve structured preparation status.");
        AssertEqual("pdftotext", packet.Materials[1].BackendId, "PDF runtime material should preserve backend id.");
        AssertContains(packet.Materials[2].PreparationSummary ?? string.Empty, "archive listing preview", "Archive preparation summary should stay explicit.");
        AssertEqual("windows-image", packet.Materials[3].BackendId, "Image runtime material should preserve backend id.");
        AssertContains(packet.Materials[3].PreviewText, "format=PNG", "Image preview should carry bounded metadata summary.");
    }
    finally
    {
        DeleteScratchWorkspace(root);
    }
}

static void WorkspaceMaterialRuntimeFrontSkipsSensitiveFileContentHonestly()
{
    var root = CreateScratchWorkspace();
    try
    {
        File.WriteAllText(Path.Combine(root, ".env"), "API_TOKEN=real-env-secret");
        File.WriteAllText(Path.Combine(root, "secrets.txt"), "PASSWORD=real-text-secret");

        var scan = WorkspaceScanner.Scan(new WorkspaceScanRequest(root));
        var packet = new WorkspaceMaterialRuntimeFront().BuildPreviewPacket(scan, maxMaterials: 2, maxCharsPerMaterial: 128);

        var sensitiveMaterial = packet.Materials.Single(material => string.Equals(material.RelativePath, "secrets.txt", StringComparison.OrdinalIgnoreCase));
        AssertEqual("SensitiveSkipped", sensitiveMaterial.PreparationStatus, "Sensitive text material should expose explicit skipped status.");
        AssertEqual(string.Empty, sensitiveMaterial.PreviewText, "Sensitive text material content must not enter preview text.");
        AssertTrue(packet.TechnicalEvidence.Any(item =>
            string.Equals(item.RelativePath, ".env", StringComparison.OrdinalIgnoreCase) &&
            string.Equals(item.Category, "sensitive-file", StringComparison.OrdinalIgnoreCase)),
            "Sensitive config files should stay visible as redacted technical evidence.");
        AssertTrue(packet.EvidencePack!.RawObservations.Any(observation =>
            observation.Kind == "sensitive_file_detected" &&
            string.Equals(observation.EvidencePath, ".env", StringComparison.OrdinalIgnoreCase)),
            "Evidence pack should record sensitive file presence without content.");
        AssertTrue(packet.EvidencePack.Signals.Any(signal =>
            signal.Category == "safety" &&
            signal.Code == "sensitive_file_present"),
            "Evidence pack should expose a safety signal for sensitive-looking files.");

        var exposedText = string.Join(
            "\n",
            packet.Materials.Select(material => material.PreviewText)
                .Concat(packet.TechnicalEvidence.Select(item => item.PreviewText))
                .Concat(packet.EvidencePack.EvidenceSnippets.Select(snippet => snippet.PreviewText)));
        AssertFalse(exposedText.Contains("real-env-secret", StringComparison.Ordinal), "Sensitive .env content must not leak into preview or snippets.");
        AssertFalse(exposedText.Contains("real-text-secret", StringComparison.Ordinal), "Sensitive text content must not leak into preview or snippets.");
    }
    finally
    {
        DeleteScratchWorkspace(root);
    }
}

static void WorkspaceMaterialRuntimeFrontKeepsMixedCoverageUnderTextPressureHonestly()
{
    var root = CreateScratchWorkspace();
    try
    {
        for (var index = 0; index < 12; index++)
        {
            File.WriteAllText(Path.Combine(root, $"note-{index:D2}.txt"), $"text note {index}");
        }

        File.WriteAllText(Path.Combine(root, "spec.pdf"), "placeholder");
        File.WriteAllText(Path.Combine(root, "bundle.zip"), "placeholder");
        using (var bitmap = new System.Drawing.Bitmap(2, 2))
        {
            bitmap.Save(Path.Combine(root, "shot.png"), System.Drawing.Imaging.ImageFormat.Png);
        }

        var scan = WorkspaceScanner.Scan(new WorkspaceScanRequest(root));
        var runner = new FakeExternalProcessRunner(request => request.Purpose switch
        {
            var purpose when purpose.StartsWith("pdf_extract:pdftotext", StringComparison.Ordinal) => new ExternalProcessResult(0, "pdf extracted context", string.Empty, false),
            "archive_list" => new ExternalProcessResult(0, "2026-04-08 12:00:00 ....A 32 src/app.cs", string.Empty, false),
            _ => new ExternalProcessResult(1, string.Empty, $"Unexpected purpose: {request.Purpose}", false)
        });
        var runtimeFront = new WorkspaceMaterialRuntimeFront(
            new TextMaterialRuntimeService(),
            new PdfExtractionRuntimeService(runner),
            new ArchiveInspectionRuntimeService(runner),
            new ImageInspectionRuntimeService());

        var packet = runtimeFront.BuildPreviewPacket(scan, maxMaterials: 12, maxCharsPerMaterial: 64);

        AssertEqual(12, packet.Materials.Count, "Runtime front should respect the expanded bounded packet size under text-heavy import.");
        AssertTrue(packet.Materials.Any(material => material.Kind == WorkspaceMaterialKind.TextDocument), "Runtime front should keep at least one text material.");
        AssertTrue(packet.Materials.Any(material => material.Kind == WorkspaceMaterialKind.PdfDocument), "Runtime front should keep PDF coverage under text pressure.");
        AssertTrue(packet.Materials.Any(material => material.Kind == WorkspaceMaterialKind.ArchiveArtifact), "Runtime front should keep archive coverage under text pressure.");
        AssertTrue(packet.Materials.Any(material => material.Kind == WorkspaceMaterialKind.ImageAsset), "Runtime front should keep image coverage under text pressure.");
    }
    finally
    {
        DeleteScratchWorkspace(root);
    }
}

static void WorkspaceMaterialRuntimeFrontExpandsTechnicalEvidenceBeyondCmakeHonestly()
{
    var root = CreateScratchWorkspace();
    try
    {
        Directory.CreateDirectory(Path.Combine(root, "cmd", "app"));
        File.WriteAllText(Path.Combine(root, "README.md"), "Infra orchestration project.");
        File.WriteAllText(Path.Combine(root, "go.mod"), "module example.com/infra\n\ngo 1.24");
        File.WriteAllText(Path.Combine(root, "Makefile"), "build:\n\tgo build ./...");
        File.WriteAllText(Path.Combine(root, "Dockerfile"), "FROM golang:1.24");
        File.WriteAllText(Path.Combine(root, "cmd", "app", "main.go"), "package main\nfunc main() {}");

        var scan = WorkspaceScanner.Scan(new WorkspaceScanRequest(root));
        var runtimeFront = new WorkspaceMaterialRuntimeFront(
            new TextMaterialRuntimeService(),
            new PdfExtractionRuntimeService(new FakeExternalProcessRunner(_ => new ExternalProcessResult(1, string.Empty, "Unexpected", false))),
            new ArchiveInspectionRuntimeService(new FakeExternalProcessRunner(_ => new ExternalProcessResult(1, string.Empty, "Unexpected", false))),
            new ImageInspectionRuntimeService());

        var packet = runtimeFront.BuildPreviewPacket(scan, maxMaterials: 4, maxCharsPerMaterial: 128);
        var technicalPaths = packet.TechnicalEvidence.Select(item => item.RelativePath).ToArray();

        AssertTrue(technicalPaths.Contains("go.mod"), "Runtime front should include go.mod as technical evidence for non-CMake repos.");
        AssertTrue(technicalPaths.Contains("Makefile"), "Runtime front should include Makefile as technical evidence for non-CMake repos.");
        AssertTrue(technicalPaths.Contains("Dockerfile"), "Runtime front should include Dockerfile as technical evidence for containerized or infra-heavy repos.");
    }
    finally
    {
        DeleteScratchWorkspace(root);
    }
}

static void WorkspaceEvidencePackKeepsRuntimeSurfacesBoundedHonestly()
{
    var root = CreateScratchWorkspace();
    try
    {
        Directory.CreateDirectory(Path.Combine(root, "cmd", "tool"));
        Directory.CreateDirectory(Path.Combine(root, "cloud", "testserver", "cmd", "server"));

        File.WriteAllText(Path.Combine(root, "README.md"), "Go-based infrastructure orchestration CLI with cloud test server.");
        File.WriteAllText(Path.Combine(root, "go.mod"), "module example.com/infra\n\ngo 1.24");
        File.WriteAllText(Path.Combine(root, "Makefile"), "build:\n\tgo build ./...");
        File.WriteAllText(Path.Combine(root, "cmd", "tool", "main.go"), "package main\nfunc main() {}");
        File.WriteAllText(Path.Combine(root, "cloud", "testserver", "cmd", "server", "main.go"), "package main\nfunc main() {}");

        var scan = WorkspaceScanner.Scan(new WorkspaceScanRequest(root));
        var runtimeFront = new WorkspaceMaterialRuntimeFront(
            new TextMaterialRuntimeService(),
            new PdfExtractionRuntimeService(new FakeExternalProcessRunner(_ => new ExternalProcessResult(1, string.Empty, "Unexpected", false))),
            new ArchiveInspectionRuntimeService(new FakeExternalProcessRunner(_ => new ExternalProcessResult(1, string.Empty, "Unexpected", false))),
            new ImageInspectionRuntimeService());

        var packet = runtimeFront.BuildPreviewPacket(scan, maxMaterials: 4, maxCharsPerMaterial: 128);
        var pack = packet.EvidencePack ?? throw new InvalidOperationException("Evidence pack should be present.");
        var surfaces = pack.TechnicalPassport.RuntimeSurfaces;

        AssertTrue(surfaces.Contains("cli"), "Evidence pack should preserve CLI surface when cmd entrypoints are visible.");
        AssertTrue(surfaces.Contains("service"), "Evidence pack should preserve service surface when server-style entrypoints are visible.");
        AssertFalse(surfaces.Contains("desktop"), "Evidence pack should not invent desktop surface without UI framework evidence.");
        AssertFalse(surfaces.Contains("web"), "Evidence pack should not invent web surface without actual web evidence.");
    }
    finally
    {
        DeleteScratchWorkspace(root);
    }
}

static void WorkspaceEvidencePackEmitsColdObservationsPatternsAndScoresHonestly()
{
    var root = CreateScratchWorkspace();
    try
    {
        Directory.CreateDirectory(Path.Combine(root, "src"));
        Directory.CreateDirectory(Path.Combine(root, "assets"));
        File.WriteAllText(Path.Combine(root, "src", "main.cpp"), "int main() { return 0; }");
        File.WriteAllText(Path.Combine(root, "CMakeLists.txt"), "project(Test)");
        File.WriteAllText(Path.Combine(root, "README.md"), "Qt widgets and debugger support are described here.");

        var scan = WorkspaceScanner.Scan(new WorkspaceScanRequest(root));
        var runtimeFront = new WorkspaceMaterialRuntimeFront(
            new TextMaterialRuntimeService(),
            new PdfExtractionRuntimeService(new FakeExternalProcessRunner(_ => new ExternalProcessResult(1, string.Empty, "Unexpected", false))),
            new ArchiveInspectionRuntimeService(new FakeExternalProcessRunner(_ => new ExternalProcessResult(1, string.Empty, "Unexpected", false))),
            new ImageInspectionRuntimeService());

        var packet = runtimeFront.BuildPreviewPacket(scan, maxMaterials: 4, maxCharsPerMaterial: 128);
        var pack = packet.EvidencePack ?? throw new InvalidOperationException("Evidence pack should be present.");

        AssertTrue(pack.RawObservations.Any(static observation => observation.Kind == "file_found" && observation.Value == "CMakeLists.txt"), "Evidence pack should expose raw file observations without narrative interpretation.");
        AssertTrue(pack.RawObservations.Any(static observation => observation.Kind == "source_root_detected"), "Evidence pack should expose scanner root observations explicitly.");
        AssertTrue(pack.DerivedPatterns.Any(static pattern => pattern.Code == "build_manifest_present"), "Evidence pack should expose build-manifest presence as a derived pattern.");
        AssertTrue(pack.SignalScores.Any(static score => score.Signal == "build.cmake" && score.Score > 0.0), "Evidence pack should expose scored signals separately from the raw signal list.");
        AssertFalse(
            pack.ConfidenceAnnotations.Any(static annotation => annotation.TargetKind == "signal" && annotation.Confidence == WorkspaceEvidenceConfidenceLevel.Confirmed),
            "Score-derived signal confidence must not become Confirmed without direct evidence.");
    }
    finally
    {
        DeleteScratchWorkspace(root);
    }
}

static void WorkspaceEvidencePackDoesNotInferBrowserRuntimeFromNarrativeReadmeAloneHonestly()
{
    var root = CreateScratchWorkspace();
    try
    {
        Directory.CreateDirectory(Path.Combine(root, "src"));
        File.WriteAllText(Path.Combine(root, "src", "main.cpp"), "int main() { return 0; }");
        File.WriteAllText(Path.Combine(root, "README.md"), "This project is a browser-based visual experience with a beautiful interface.");

        var scan = WorkspaceScanner.Scan(new WorkspaceScanRequest(root));
        var runtimeFront = new WorkspaceMaterialRuntimeFront(
            new TextMaterialRuntimeService(),
            new PdfExtractionRuntimeService(new FakeExternalProcessRunner(_ => new ExternalProcessResult(1, string.Empty, "Unexpected", false))),
            new ArchiveInspectionRuntimeService(new FakeExternalProcessRunner(_ => new ExternalProcessResult(1, string.Empty, "Unexpected", false))),
            new ImageInspectionRuntimeService());

        var packet = runtimeFront.BuildPreviewPacket(scan, maxMaterials: 4, maxCharsPerMaterial: 160);
        var pack = packet.EvidencePack ?? throw new InvalidOperationException("Evidence pack should be present.");

        AssertFalse(pack.TechnicalPassport.RuntimeSurfaces.Contains("web", StringComparer.OrdinalIgnoreCase), "Narrative README alone should not create browser runtime surface without supporting technical evidence.");
        AssertFalse(pack.DerivedPatterns.Any(static pattern => pattern.Code == "browser_surface_pattern"), "Narrative README alone should not create browser surface pattern without supporting technical evidence.");
    }
    finally
    {
        DeleteScratchWorkspace(root);
    }
}

static void WorkspaceEvidencePackTreeSummaryIsNoLongerScannerAuthoredTruthHonestly()
{
    var root = CreateScratchWorkspace();
    try
    {
        Directory.CreateDirectory(Path.Combine(root, "src"));
        File.WriteAllText(Path.Combine(root, "src", "main.cpp"), "int main() { return 0; }");
        File.WriteAllText(Path.Combine(root, "README.md"), "Architecture and runtime explanation in prose only.");

        var scan = WorkspaceScanner.Scan(new WorkspaceScanRequest(root));
        var pack = WorkspaceEvidencePackBuilder.Build(
            scan,
            Array.Empty<WorkspaceTechnicalPreviewInput>(),
            new[]
            {
                new WorkspaceMaterialPreviewInput(
                    "README.md",
                    WorkspaceMaterialKind.TextDocument,
                    "text-first-preview",
                    "Architecture and runtime explanation in prose only.",
                    false,
                    "Extracted",
                    "text",
                    "plain text")
            });

        AssertEqual(string.Empty, pack.TreeSummary, "Cold scanner transition should leave legacy tree summary empty instead of authoring project narrative.");
    }
    finally
    {
        DeleteScratchWorkspace(root);
    }
}

static void WorkspaceEvidencePackDerivesCoarseModulesHonestly()
{
    var root = CreateScratchWorkspace();
    try
    {
        Directory.CreateDirectory(Path.Combine(root, "src", "core"));
        Directory.CreateDirectory(Path.Combine(root, "src", "runtime"));
        Directory.CreateDirectory(Path.Combine(root, "src", "ui"));

        File.WriteAllText(Path.Combine(root, "src", "core", "registry.cpp"), "Core registry owns truth and scanning.");
        File.WriteAllText(Path.Combine(root, "src", "core", "scanner.cpp"), "Scanner discovers projects and registry entries.");
        File.WriteAllText(Path.Combine(root, "src", "runtime", "runner.cpp"), "Runtime runner executes launch processes.");
        File.WriteAllText(Path.Combine(root, "src", "ui", "view.cpp"), "UI layer reflects state without business logic.");

        var scan = WorkspaceScanner.Scan(new WorkspaceScanRequest(root));
        var runtimeFront = new WorkspaceMaterialRuntimeFront(
            new TextMaterialRuntimeService(),
            new PdfExtractionRuntimeService(new FakeExternalProcessRunner(_ => new ExternalProcessResult(1, string.Empty, "Unexpected", false))),
            new ArchiveInspectionRuntimeService(new FakeExternalProcessRunner(_ => new ExternalProcessResult(1, string.Empty, "Unexpected", false))),
            new ImageInspectionRuntimeService());

        var packet = runtimeFront.BuildPreviewPacket(scan, maxMaterials: 4, maxCharsPerMaterial: 128);
        var pack = packet.EvidencePack ?? throw new InvalidOperationException("Evidence pack should be present.");
        var modules = pack.ModuleCandidates;

        AssertTrue(modules.Any(module => module.Name.Contains("Core", StringComparison.OrdinalIgnoreCase)), "Evidence pack should preserve structural module buckets from source roots.");
        AssertTrue(modules.Any(module => module.Name.Contains("Runtime", StringComparison.OrdinalIgnoreCase)), "Evidence pack should preserve structural runtime bucket from source roots.");
        AssertTrue(modules.Any(module => module.Name.Contains("Ui", StringComparison.OrdinalIgnoreCase)), "Evidence pack should preserve structural ui bucket name from path clustering.");
        AssertFalse(modules.Any(module => string.Equals(module.Name, "UI Presentation", StringComparison.OrdinalIgnoreCase)), "Cold scanner should not synthesize semantic presentation module names from content markers.");
    }
    finally
    {
        DeleteScratchWorkspace(root);
    }
}

static void WorkspaceEvidencePackDerivesCoarseDependencyEdgesHonestly()
{
    var root = CreateScratchWorkspace();
    try
    {
        Directory.CreateDirectory(Path.Combine(root, "src", "core"));
        Directory.CreateDirectory(Path.Combine(root, "src", "runtime"));
        Directory.CreateDirectory(Path.Combine(root, "src", "ui"));

        File.WriteAllText(Path.Combine(root, "src", "ui", "view.cpp"), "ui");
        File.WriteAllText(Path.Combine(root, "src", "core", "registry.cpp"), "core");
        File.WriteAllText(Path.Combine(root, "src", "runtime", "runner.cpp"), "Runtime executes process launch.");
        File.WriteAllText(Path.Combine(root, "src", "main.cpp"), "int main() { return 0; }");

        var scan = WorkspaceScanner.Scan(new WorkspaceScanRequest(root));
        var runtimeFront = new WorkspaceMaterialRuntimeFront(
            new TextMaterialRuntimeService(),
            new PdfExtractionRuntimeService(new FakeExternalProcessRunner(_ => new ExternalProcessResult(1, string.Empty, "Unexpected", false))),
            new ArchiveInspectionRuntimeService(new FakeExternalProcessRunner(_ => new ExternalProcessResult(1, string.Empty, "Unexpected", false))),
            new ImageInspectionRuntimeService());

        var packet = runtimeFront.BuildPreviewPacket(scan, maxMaterials: 4, maxCharsPerMaterial: 128);
        var pack = packet.EvidencePack ?? throw new InvalidOperationException("Evidence pack should be present.");
        var edges = pack.DependencyEdges;

        AssertTrue(edges.Any(edge => edge.From.EndsWith("src\\main.cpp", StringComparison.OrdinalIgnoreCase)), "Evidence pack should preserve structural entry edges from detected bootstrap files.");
        AssertTrue(edges.Any(edge => string.Equals(edge.Label, "subsystem", StringComparison.OrdinalIgnoreCase) || string.Equals(edge.Label, "entry-surface", StringComparison.OrdinalIgnoreCase) || string.Equals(edge.Label, "main", StringComparison.OrdinalIgnoreCase)), "Evidence pack should preserve only mechanical edge labels from cold candidates.");
        AssertFalse(edges.Any(edge => string.Equals(edge.From, "UI", StringComparison.OrdinalIgnoreCase) && string.Equals(edge.To, "Core", StringComparison.OrdinalIgnoreCase)), "Cold scanner should not synthesize semantic UI -> Core chains.");
        AssertFalse(edges.Any(edge => string.Equals(edge.From, "Core", StringComparison.OrdinalIgnoreCase) && string.Equals(edge.To, "Runtime", StringComparison.OrdinalIgnoreCase)), "Cold scanner should not synthesize semantic Core -> Runtime chains.");
    }
    finally
    {
        DeleteScratchWorkspace(root);
    }
}

static void WorkspaceEvidencePackAvoidsBroadReverseProjectInferenceHonestly()
{
    var root = CreateScratchWorkspace();
    try
    {
        Directory.CreateDirectory(Path.Combine(root, "src", "dbg"));
        Directory.CreateDirectory(Path.Combine(root, "src", "analysis"));

        File.WriteAllText(Path.Combine(root, "README.md"), "Debugger and disassembly tooling with memory inspection and breakpoint support.");
        File.WriteAllText(Path.Combine(root, "src", "dbg", "engine.cpp"), "Debugger engine attaches to process and manages breakpoints.");
        File.WriteAllText(Path.Combine(root, "src", "analysis", "disasm.cpp"), "Disassembly pipeline decodes instructions and opcodes.");

        var scan = WorkspaceScanner.Scan(new WorkspaceScanRequest(root));
        var runtimeFront = new WorkspaceMaterialRuntimeFront(
            new TextMaterialRuntimeService(),
            new PdfExtractionRuntimeService(new FakeExternalProcessRunner(_ => new ExternalProcessResult(1, string.Empty, "Unexpected", false))),
            new ArchiveInspectionRuntimeService(new FakeExternalProcessRunner(_ => new ExternalProcessResult(1, string.Empty, "Unexpected", false))),
            new ImageInspectionRuntimeService());

        var packet = runtimeFront.BuildPreviewPacket(scan, maxMaterials: 4, maxCharsPerMaterial: 256);
        var pack = packet.EvidencePack ?? throw new InvalidOperationException("Evidence pack should be present.");
        var layerNames = pack.ObservedLayers.Select(static layer => layer.Name).ToArray();
        var moduleNames = pack.ModuleCandidates.Select(static module => module.Name).ToArray();
        var runtimeSurfaces = pack.TechnicalPassport.RuntimeSurfaces;
        var originSignals = pack.Signals.Where(static signal => signal.Category == "origin").Select(static signal => signal.Code).ToArray();

        AssertFalse(layerNames.Contains("UI", StringComparer.OrdinalIgnoreCase), "Reverse/debugger repo should not invent UI layer from generic wording alone.");
        AssertFalse(layerNames.Contains("Service", StringComparer.OrdinalIgnoreCase), "Reverse/debugger repo should not invent service layer without service evidence.");
        AssertFalse(layerNames.Contains("Mod Platform", StringComparer.OrdinalIgnoreCase), "Reverse/debugger repo should not invent mod platform without explicit modding evidence.");
        AssertFalse(moduleNames.Contains("UI Presentation", StringComparer.OrdinalIgnoreCase), "Reverse/debugger repo should not invent presentation module from generic content.");
        AssertFalse(moduleNames.Contains("Mod Integration", StringComparer.OrdinalIgnoreCase), "Reverse/debugger repo should not invent mod integration from plugin-like low-level wording.");
        AssertFalse(runtimeSurfaces.Contains("service", StringComparer.OrdinalIgnoreCase), "Reverse/debugger repo should not expose service surface without service markers.");
        AssertTrue(runtimeSurfaces.Contains("analysis", StringComparer.OrdinalIgnoreCase), "Reverse/debugger repo should still preserve analysis surface.");
        AssertTrue(originSignals.Contains("reverse", StringComparer.OrdinalIgnoreCase), "Reverse/debugger repo should preserve reverse origin signal.");
    }
    finally
    {
        DeleteScratchWorkspace(root);
    }
}

static void WorkspaceEvidencePackRequiresSupportingSignalsForCoarseDependencyEdgesHonestly()
{
    var root = CreateScratchWorkspace();
    try
    {
        Directory.CreateDirectory(Path.Combine(root, "src", "core"));
        Directory.CreateDirectory(Path.Combine(root, "src", "runtime"));
        Directory.CreateDirectory(Path.Combine(root, "src", "mods"));

        File.WriteAllText(Path.Combine(root, "src", "core", "registry.cpp"), "Core registry stores project state.");
        File.WriteAllText(Path.Combine(root, "src", "runtime", "host.cpp"), "Runtime component exists.");
        File.WriteAllText(Path.Combine(root, "src", "mods", "plugins.cpp"), "Plugin host exists, but no explicit modding language is described.");

        var scan = WorkspaceScanner.Scan(new WorkspaceScanRequest(root));
        var pack = WorkspaceEvidencePackBuilder.Build(
            scan,
            Array.Empty<WorkspaceTechnicalPreviewInput>(),
            new[]
            {
                new WorkspaceMaterialPreviewInput(
                    "notes.txt",
                    WorkspaceMaterialKind.TextDocument,
                    "text-first-preview",
                    "Core registry and runtime notes only.",
                    false,
                    "Extracted",
                    "text",
                    "plain text")
            });

        var edges = pack.DependencyEdges;

        AssertFalse(edges.Any(edge => string.Equals(edge.From, "Core", StringComparison.OrdinalIgnoreCase) && string.Equals(edge.To, "Runtime", StringComparison.OrdinalIgnoreCase)), "Core -> Runtime edge should require supporting runtime/process evidence, not only visible names.");
        AssertFalse(edges.Any(edge => string.Equals(edge.From, "Mod Platform", StringComparison.OrdinalIgnoreCase) && string.Equals(edge.To, "Runtime", StringComparison.OrdinalIgnoreCase)), "Mod Platform -> Runtime edge should require explicit modding origin evidence, not only plugin-like naming.");
    }
    finally
    {
        DeleteScratchWorkspace(root);
    }
}

static void WorkspaceEvidencePackExposesColdCandidatesAndHotspotsHonestly()
{
    var root = CreateScratchWorkspace();
    try
    {
        Directory.CreateDirectory(Path.Combine(root, "src", "core"));
        Directory.CreateDirectory(Path.Combine(root, "src", "ui"));
        Directory.CreateDirectory(Path.Combine(root, "tests"));

        File.WriteAllText(Path.Combine(root, "src", "main.cpp"), "int main() { return 0; }");
        File.WriteAllText(Path.Combine(root, "src", "core", "registry.cpp"), "Core registry stores state.");
        File.WriteAllText(Path.Combine(root, "src", "ui", "mainwindow.cpp"), "MainWindow reflects runtime state.");
        File.WriteAllText(Path.Combine(root, "tests", "registry_test.cpp"), "test");
        File.WriteAllText(Path.Combine(root, "README.md"), "Short project note.");

        var scan = WorkspaceScanner.Scan(new WorkspaceScanRequest(root));
        var runtimeFront = new WorkspaceMaterialRuntimeFront(
            new TextMaterialRuntimeService(),
            new PdfExtractionRuntimeService(new FakeExternalProcessRunner(_ => new ExternalProcessResult(1, string.Empty, "Unexpected", false))),
            new ArchiveInspectionRuntimeService(new FakeExternalProcessRunner(_ => new ExternalProcessResult(1, string.Empty, "Unexpected", false))),
            new ImageInspectionRuntimeService());

        var packet = runtimeFront.BuildPreviewPacket(scan, maxMaterials: 4, maxCharsPerMaterial: 160);
        var pack = packet.EvidencePack ?? throw new InvalidOperationException("Evidence pack should be present.");

        AssertTrue(pack.Candidates.EntryPoints.Count >= 1, "Cold candidates should preserve entry point candidates as a first-class scanner output.");
        AssertTrue(pack.Candidates.ModuleCandidates.Count >= 1, "Cold candidates should preserve module candidates as a first-class scanner output.");
        AssertTrue(pack.Candidates.FileRoles.Any(static role => role.Role == "ui"), "Cold candidates should preserve heuristic file roles without narrative interpretation.");
        AssertTrue(pack.Candidates.FileRoles.Any(static role => role.Role == "test"), "Cold candidates should preserve test-like file roles.");
        AssertTrue(pack.Hotspots is not null, "Cold scanner output should expose hotspots collection even when bounded.");
    }
    finally
    {
        DeleteScratchWorkspace(root);
    }
}

static void WorkspaceEvidencePackExposesRootReadmeIdentityAsEvidenceHonestly()
{
    var root = CreateScratchWorkspace();
    try
    {
        Directory.CreateDirectory(Path.Combine(root, "src"));
        File.WriteAllText(Path.Combine(root, "src", "main.go"), "package main\nfunc main() {}");
        File.WriteAllText(Path.Combine(root, "go.mod"), "module example.com/svix");
        File.WriteAllText(Path.Combine(root, "README.md"), "# Svix - Webhooks as a service\n\nNarrative body.");

        var scan = WorkspaceScanner.Scan(new WorkspaceScanRequest(root));
        var pack = WorkspaceEvidencePackBuilder.Build(scan, Array.Empty<WorkspaceTechnicalPreviewInput>(), Array.Empty<WorkspaceMaterialPreviewInput>());

        var identitySignal = pack.Signals.FirstOrDefault(signal =>
            string.Equals(signal.Category, "identity", StringComparison.OrdinalIgnoreCase) &&
            string.Equals(signal.Code, "root_readme_title", StringComparison.OrdinalIgnoreCase));

        AssertTrue(identitySignal is not null, "Root README identity should be exposed as a cold evidence signal.");
        AssertContains(identitySignal!.Reason, "Svix - Webhooks as a service", "Root README title should be preserved as evidence, not lost behind generic scanner output.");
        AssertEqual("README.md", identitySignal.EvidencePath, "Root README identity evidence should point at the observed file.");
    }
    finally
    {
        DeleteScratchWorkspace(root);
    }
}

static void WorkspaceEvidencePackMapsProjectUnitsHonestly()
{
    var root = CreateScratchWorkspace();
    try
    {
        Directory.CreateDirectory(Path.Combine(root, ".github", "actions", "ship"));
        Directory.CreateDirectory(Path.Combine(root, "bin", "spiced", "src"));
        Directory.CreateDirectory(Path.Combine(root, "crates", "runtime", "src"));
        Directory.CreateDirectory(Path.Combine(root, "tools", "helper", "src"));
        File.WriteAllText(Path.Combine(root, "Cargo.toml"), "[workspace]\nmembers = [\"bin/spiced\", \"crates/runtime\", \"tools/helper\"]\ndefault-members = [\"bin/spiced\"]");
        File.WriteAllText(Path.Combine(root, "bin", "spiced", "Cargo.toml"), "[package]\nname = \"spiced\"");
        File.WriteAllText(Path.Combine(root, "bin", "spiced", "src", "main.rs"), "fn main() {}");
        File.WriteAllText(Path.Combine(root, "crates", "runtime", "Cargo.toml"), "[package]\nname = \"runtime\"");
        File.WriteAllText(Path.Combine(root, "crates", "runtime", "src", "lib.rs"), "pub fn start() {}");
        File.WriteAllText(Path.Combine(root, "tools", "helper", "Cargo.toml"), "[package]\nname = \"helper\"");
        File.WriteAllText(Path.Combine(root, "tools", "helper", "src", "main.rs"), "fn main() {}");
        File.WriteAllText(Path.Combine(root, ".github", "actions", "ship", "package.json"), "{ \"name\": \"ship-action\" }");

        var scan = WorkspaceScanner.Scan(new WorkspaceScanRequest(root));
        var pack = WorkspaceEvidencePackBuilder.Build(scan, Array.Empty<WorkspaceTechnicalPreviewInput>(), Array.Empty<WorkspaceMaterialPreviewInput>());

        var units = pack.Candidates.ProjectUnits;
        AssertTrue(units.Count >= 3, "Evidence pack should preserve workspace unit candidates as first-class scanner output.");
        var spicedUnit = units.FirstOrDefault(unit => string.Equals(unit.RootPath, Path.Combine("bin", "spiced"), StringComparison.OrdinalIgnoreCase));
        AssertTrue(spicedUnit is not null, "Cargo default member should become a project unit candidate.");
        AssertEqual("rust-cargo", spicedUnit!.Kind, "Cargo unit should carry a deterministic manifest kind signal.");
        AssertEqual(WorkspaceEvidenceConfidenceLevel.Confirmed, spicedUnit.Confidence, "Unit with manifest and entrypoint overlap should be confirmed.");
        AssertTrue(spicedUnit.Manifests.Contains(Path.Combine("bin", "spiced", "Cargo.toml"), StringComparer.OrdinalIgnoreCase), "Project unit should list its manifest evidence.");
        AssertTrue(spicedUnit.EntryPoints.Contains(Path.Combine("bin", "spiced", "src", "main.rs"), StringComparer.OrdinalIgnoreCase), "Project unit should list entrypoint overlap.");
        AssertTrue(spicedUnit.Evidence.Contains("cargo_default_member", StringComparer.OrdinalIgnoreCase), "Project unit should expose Cargo default-member evidence.");
        AssertTrue(spicedUnit.Evidence.Contains("unit_zone:application", StringComparer.OrdinalIgnoreCase), "Manifest-backed bin unit should carry reusable application-zone evidence.");
        AssertTrue(spicedUnit.EvidenceMarker is not null, "Project unit should carry a structured scanner evidence marker.");
        AssertEqual("project_unit_candidate", spicedUnit.EvidenceMarker!.EvidenceKind, "Project unit marker should expose evidence kind.");
        AssertEqual(WorkspaceEvidenceConfidenceLevel.Confirmed, spicedUnit.EvidenceMarker.Confidence, "Confirmed project unit marker should come from manifest plus entrypoint overlap.");
        AssertFalse(spicedUnit.EvidenceMarker.IsPartial, "Complete scan project unit marker should not be partial.");
        var runtimeUnit = units.FirstOrDefault(unit => string.Equals(unit.RootPath, Path.Combine("crates", "runtime"), StringComparison.OrdinalIgnoreCase));
        AssertTrue(runtimeUnit is not null, "Library crate should remain visible as a project unit.");
        AssertTrue(runtimeUnit!.Evidence.Contains("unit_zone:library", StringComparer.OrdinalIgnoreCase), "Crates/packages units should carry reusable library-zone evidence.");
        var helperUnit = units.FirstOrDefault(unit => string.Equals(unit.RootPath, Path.Combine("tools", "helper"), StringComparison.OrdinalIgnoreCase));
        AssertTrue(helperUnit is not null, "Tool manifest should remain visible as a project unit.");
        AssertTrue(helperUnit!.Evidence.Contains("unit_zone:tooling", StringComparer.OrdinalIgnoreCase), "Tool units should carry reusable tooling-zone evidence instead of repo-specific demotion.");
        AssertFalse(units.Any(unit => unit.RootPath.StartsWith(".github", StringComparison.OrdinalIgnoreCase)), "Workflow/action manifests should not become project units.");
        AssertTrue(units.First().RootPath == "." || string.Equals(units.First().RootPath, Path.Combine("bin", "spiced"), StringComparison.OrdinalIgnoreCase), "Workspace unit ordering should keep root/default member ahead of helper tools.");
        var unitList = units.ToList();
        AssertTrue(
            unitList.FindIndex(unit => string.Equals(unit.RootPath, Path.Combine("crates", "runtime"), StringComparison.OrdinalIgnoreCase)) <
            unitList.FindIndex(unit => string.Equals(unit.RootPath, Path.Combine("tools", "helper"), StringComparison.OrdinalIgnoreCase)),
            "Library/package units should rank ahead of helper tool units when both are present.");

        var basePacket = WorkspaceImportMaterialPreviewPacketBuilder.Build(scan, maxMaterials: 4, maxCharsPerMaterial: 128);
        var packet = basePacket with { EvidencePack = pack };
        var prompt = WorkspaceImportMaterialPromptRequestBuilder.Build(packet).UserPrompt;
        AssertContains(prompt, $"project_units: {Path.Combine("bin", "spiced")} (rust-cargo, zone=application", "Importer prompt should expose unit zone evidence for weaker model grounding.");
        AssertContains(prompt, "evidence=cargo_default_member", "Importer prompt should expose default-member evidence beside project units.");
        AssertContains(prompt, $"{Path.Combine("tools", "helper")} (rust-cargo, zone=tooling", "Importer prompt should expose tooling-zone evidence instead of hiding support units.");
    }
    finally
    {
        DeleteScratchWorkspace(root);
    }
}

static void WorkspaceEvidencePackMapsRunProfilesHonestly()
{
    var root = CreateScratchWorkspace();
    try
    {
        Directory.CreateDirectory(Path.Combine(root, "app"));
        Directory.CreateDirectory(Path.Combine(root, "bin", "server", "src"));
        File.WriteAllText(Path.Combine(root, "package.json"), """
            {
              "scripts": {
                "dev": "vite",
                "build": "vite build",
                "test": "vitest",
                "lint": "eslint .",
                "format": "prettier ."
              }
            }
            """);
        File.WriteAllText(Path.Combine(root, "Cargo.toml"), "[workspace]\nmembers = [\"bin/server\"]\ndefault-members = [\"bin/server\"]");
        File.WriteAllText(Path.Combine(root, "bin", "server", "Cargo.toml"), "[package]\nname = \"server\"");
        File.WriteAllText(Path.Combine(root, "bin", "server", "src", "main.rs"), "fn main() {}");

        var scan = WorkspaceScanner.Scan(new WorkspaceScanRequest(root));
        var pack = WorkspaceEvidencePackBuilder.Build(scan, Array.Empty<WorkspaceTechnicalPreviewInput>(), Array.Empty<WorkspaceMaterialPreviewInput>());
        var profiles = pack.Candidates.RunProfiles;

        AssertTrue(profiles.Any(profile => profile.Kind == "build" && profile.Command == "npm run build"), "Run profile index should preserve package.json build script.");
        AssertTrue(profiles.Any(profile => profile.Kind == "test" && profile.Command == "npm run test"), "Run profile index should preserve package.json test script.");
        AssertTrue(profiles.Any(profile => profile.Kind == "run" && profile.Command == "npm run dev"), "Run profile index should preserve package.json dev/start scripts as run profiles.");
        AssertTrue(profiles.Any(profile => profile.Kind == "check" && profile.Command == "npm run lint"), "Run profile index should preserve lint/check style scripts.");
        AssertFalse(profiles.Any(profile => profile.Command.Contains("format", StringComparison.OrdinalIgnoreCase)), "Run profile index should not include arbitrary package scripts as execution recommendations.");

        var cargoRun = profiles.FirstOrDefault(profile =>
            profile.Kind == "run" &&
            profile.Command.Contains(Path.Combine("bin", "server", "Cargo.toml"), StringComparison.OrdinalIgnoreCase));
        AssertTrue(cargoRun is not null, "Cargo unit with entrypoint overlap should produce a run profile.");
        AssertEqual(WorkspaceEvidenceConfidenceLevel.Confirmed, cargoRun!.Confidence, "Cargo run profile with entrypoint overlap should be confirmed.");
        AssertTrue(cargoRun.Evidence.Contains("cargo_default_member", StringComparer.OrdinalIgnoreCase), "Cargo run profile should inherit default-member evidence.");
        AssertTrue(cargoRun.EvidenceMarker is not null, "Run profile should carry a structured scanner evidence marker.");
        AssertEqual("run_profile_candidate", cargoRun.EvidenceMarker!.EvidenceKind, "Run profile marker should expose evidence kind.");
        AssertEqual(WorkspaceEvidenceConfidenceLevel.Confirmed, cargoRun.EvidenceMarker.Confidence, "Confirmed run profile marker should come from manifest plus entrypoint overlap.");
    }
    finally
    {
        DeleteScratchWorkspace(root);
    }
}

static void WorkspaceEvidencePackDetectsPackageJsonEntryAndRunProfilesHonestly()
{
    var root = CreateScratchWorkspace();
    try
    {
        Directory.CreateDirectory(Path.Combine(root, "src"));
        File.WriteAllText(Path.Combine(root, "package.json"), """
            {
              "name": "demo",
              "scripts": {
                "dev": "vite",
                "start": "node src/cli.js",
                "build": "vite build",
                "test": "vitest"
              },
              "main": "./src/index.js",
              "module": "./src/index.ts",
              "exports": {
                ".": "./src/index.js"
              },
              "bin": {
                "demo": "./src/cli.js"
              }
            }
            """);
        File.WriteAllText(Path.Combine(root, "src", "cli.js"), "console.log('cli');");
        File.WriteAllText(Path.Combine(root, "src", "index.js"), "export const value = 1;");
        File.WriteAllText(Path.Combine(root, "src", "index.ts"), "export const value = 1;");

        var scan = WorkspaceScanner.Scan(new WorkspaceScanRequest(root));
        var pack = WorkspaceEvidencePackBuilder.Build(scan, Array.Empty<WorkspaceTechnicalPreviewInput>(), Array.Empty<WorkspaceMaterialPreviewInput>());

        AssertTrue(pack.Candidates.RunProfiles.Any(profile => profile.Kind == "run" && profile.Command == "npm run dev" && profile.Confidence == WorkspaceEvidenceConfidenceLevel.Confirmed), "package.json dev script should become a confirmed run profile.");
        AssertTrue(pack.Candidates.RunProfiles.Any(profile => profile.Kind == "run" && profile.Command == "npm run start" && profile.Confidence == WorkspaceEvidenceConfidenceLevel.Confirmed), "package.json start script should become a confirmed run profile.");
        AssertTrue(pack.Candidates.RunProfiles.Any(profile => profile.Kind == "build" && profile.Command == "npm run build" && profile.Confidence == WorkspaceEvidenceConfidenceLevel.Confirmed), "package.json build script should become a confirmed run profile.");
        AssertTrue(pack.Candidates.RunProfiles.Any(profile => profile.Kind == "test" && profile.Command == "npm run test" && profile.Confidence == WorkspaceEvidenceConfidenceLevel.Confirmed), "package.json test script should become a confirmed run profile.");

        var cliEntry = pack.Candidates.EntryPoints.FirstOrDefault(entry => string.Equals(entry.RelativePath, Path.Combine("src", "cli.js"), StringComparison.OrdinalIgnoreCase));
        AssertTrue(cliEntry is not null, "package.json bin should expose an executable entrypoint candidate.");
        AssertEqual(WorkspaceEvidenceConfidenceLevel.Confirmed, cliEntry!.EvidenceMarker!.Confidence, "package.json bin is direct executable manifest evidence and may be confirmed.");
        AssertTrue(cliEntry.Evidence.Contains("package_bin", StringComparer.OrdinalIgnoreCase), "package.json bin evidence should be preserved on entrypoint candidate.");

        var indexEntry = pack.Candidates.EntryPoints.FirstOrDefault(entry => string.Equals(entry.RelativePath, Path.Combine("src", "index.js"), StringComparison.OrdinalIgnoreCase));
        AssertTrue(indexEntry is not null, "package.json main/exports should expose a package surface candidate.");
        AssertEqual(WorkspaceEvidenceConfidenceLevel.Likely, indexEntry!.EvidenceMarker!.Confidence, "package.json main/exports are package-surface hints, not confirmed runtime main entries.");
        AssertTrue(indexEntry.Evidence.Contains("package_main_hint", StringComparer.OrdinalIgnoreCase), "package.json main evidence should remain visible as a hint.");

        var manifestIndexEvidence = pack.Candidates.ProjectUnits
            .SelectMany(unit => unit.Evidence)
            .ToArray();
        AssertTrue(manifestIndexEvidence.Contains("package_script:dev", StringComparer.OrdinalIgnoreCase), "Manifest evidence should expose selected package scripts for manifests.index.json.");
        AssertTrue(manifestIndexEvidence.Contains("package_bin_hint", StringComparer.OrdinalIgnoreCase), "Manifest evidence should expose package bin hints for manifests.index.json.");
    }
    finally
    {
        DeleteScratchWorkspace(root);
    }
}

static void WorkspaceEvidencePackDetectsPythonPyprojectEntriesHonestly()
{
    var root = CreateScratchWorkspace();
    try
    {
        Directory.CreateDirectory(Path.Combine(root, "src", "sqlfluff", "cli"));
        File.WriteAllText(Path.Combine(root, "pyproject.toml"), """
            [project]
            name = "sqlfluff"

            [project.scripts]
            sqlfluff = "sqlfluff.cli.commands:cli"

            [tool.pytest.ini_options]
            testpaths = ["test"]
            """);
        File.WriteAllText(Path.Combine(root, "src", "sqlfluff", "__init__.py"), "");
        File.WriteAllText(Path.Combine(root, "src", "sqlfluff", "cli", "__init__.py"), "");
        File.WriteAllText(Path.Combine(root, "src", "sqlfluff", "cli", "commands.py"), "def cli():\n    pass\n");

        var scan = WorkspaceScanner.Scan(new WorkspaceScanRequest(root));
        var pack = WorkspaceEvidencePackBuilder.Build(scan, Array.Empty<WorkspaceTechnicalPreviewInput>(), Array.Empty<WorkspaceMaterialPreviewInput>());

        var cliEntry = pack.Candidates.EntryPoints.FirstOrDefault(entry => string.Equals(entry.RelativePath, Path.Combine("src", "sqlfluff", "cli", "commands.py"), StringComparison.OrdinalIgnoreCase));
        AssertTrue(cliEntry is not null, "pyproject project.scripts should expose the console target as an entrypoint candidate.");
        AssertEqual(WorkspaceEvidenceConfidenceLevel.Confirmed, cliEntry!.EvidenceMarker!.Confidence, "project.scripts is direct executable manifest evidence and may be confirmed.");
        AssertTrue(cliEntry.Evidence.Contains("python_console_script:sqlfluff", StringComparer.OrdinalIgnoreCase), "Python console-script evidence should be preserved on entrypoint candidate.");

        AssertTrue(pack.Candidates.RunProfiles.Any(profile => profile.Kind == "run" && profile.Command == "sqlfluff" && profile.Confidence == WorkspaceEvidenceConfidenceLevel.Confirmed), "Python console script should become a confirmed run profile.");
        AssertTrue(pack.Candidates.RunProfiles.Any(profile => profile.Kind == "test" && profile.Command == "pytest" && profile.Confidence == WorkspaceEvidenceConfidenceLevel.Confirmed), "pyproject pytest config should become a confirmed test run profile.");

        var packageModule = pack.Candidates.ModuleCandidates.FirstOrDefault(module => string.Equals(module.Name, "Sqlfluff", StringComparison.OrdinalIgnoreCase));
        AssertTrue(packageModule is not null, "Python source package root should remain visible as a likely module candidate.");
        AssertEqual(WorkspaceEvidenceConfidenceLevel.Likely, packageModule!.EvidenceMarker!.Confidence, "Python package root is module evidence, not a confirmed main entry.");
        AssertFalse(pack.Candidates.EntryPoints.Any(entry => string.Equals(entry.RelativePath, Path.Combine("src", "sqlfluff", "__init__.py"), StringComparison.OrdinalIgnoreCase) && entry.EvidenceMarker?.Confidence == WorkspaceEvidenceConfidenceLevel.Confirmed), "Python package root must not become a confirmed main entry.");
    }
    finally
    {
        DeleteScratchWorkspace(root);
    }
}

static void WorkspaceEvidencePackDoesNotPromoteReadmeNarrativeToEntrypointHonestly()
{
    var root = CreateScratchWorkspace();
    try
    {
        Directory.CreateDirectory(Path.Combine(root, "src"));
        File.WriteAllText(Path.Combine(root, "package.json"), "{ \"name\": \"demo\" }");
        File.WriteAllText(Path.Combine(root, "src", "app.ts"), "export function run() {}");
        File.WriteAllText(Path.Combine(root, "README.md"), "# Demo\n\nRun src/app.ts as the application entry.");

        var scan = WorkspaceScanner.Scan(new WorkspaceScanRequest(root));
        var pack = WorkspaceEvidencePackBuilder.Build(scan, Array.Empty<WorkspaceTechnicalPreviewInput>(), Array.Empty<WorkspaceMaterialPreviewInput>());

        AssertFalse(pack.Candidates.EntryPoints.Any(entry => entry.EvidenceMarker?.Confidence == WorkspaceEvidenceConfidenceLevel.Confirmed), "README narrative alone must not promote an entrypoint to confirmed.");
        AssertFalse(pack.Candidates.EntryPoints.Any(entry => string.Equals(entry.RelativePath, Path.Combine("src", "app.ts"), StringComparison.OrdinalIgnoreCase)), "README-mentioned files should not become scanner entrypoints without manifest or conventional entry evidence.");
    }
    finally
    {
        DeleteScratchWorkspace(root);
    }
}

static void WorkspaceEvidencePackClassifiesSourcePlusReleaseTopologyHonestly()
{
    var root = CreateScratchWorkspace();
    try
    {
        Directory.CreateDirectory(Path.Combine(root, "src"));
        Directory.CreateDirectory(Path.Combine(root, "release"));
        File.WriteAllText(Path.Combine(root, "Makefile"), "all:\n\tcc src/main.c -o release/demo");
        File.WriteAllText(Path.Combine(root, "src", "main.c"), "int main() { return 0; }");
        File.WriteAllText(Path.Combine(root, "release", "demo.exe"), "MZ");

        var scan = WorkspaceScanner.Scan(new WorkspaceScanRequest(root));
        var pack = WorkspaceEvidencePackBuilder.Build(scan, Array.Empty<WorkspaceTechnicalPreviewInput>(), Array.Empty<WorkspaceMaterialPreviewInput>());

        AssertEqual("Mixed", pack.Topology.Kind, "Source plus release payload should remain a mixed topology, not a single clean app claim.");
        AssertTrue(pack.Topology.LikelyActiveSourceRoots.Any(), "Source plus release topology should still expose a likely active source root.");
        AssertTrue(pack.Topology.ReleaseOutputZones.Contains("release"), "Release folder with binary payload should be exposed as release/output zone.");
        AssertContains(pack.Topology.SafeImportMode, "mixed-source-release", "Safe import mode should keep source and release output separated.");
        AssertTrue(pack.Topology.UncertaintyReasons.Any(reason => reason.Contains("SOURCE_AND_RELEASE_OUTPUT", StringComparison.OrdinalIgnoreCase)), "Topology should explain source/release coexistence as uncertainty.");
    }
    finally
    {
        DeleteScratchWorkspace(root);
    }
}

static void WorkspaceEvidencePackDoesNotSplitRootManifestFromConventionalSourceRootHonestly()
{
    var root = CreateScratchWorkspace();
    try
    {
        Directory.CreateDirectory(Path.Combine(root, "src"));
        File.WriteAllText(Path.Combine(root, "package.json"), "{ \"name\": \"demo\", \"scripts\": { \"build\": \"vite build\" } }");
        File.WriteAllText(Path.Combine(root, "src", "main.ts"), "export function run() {}");

        var scan = WorkspaceScanner.Scan(new WorkspaceScanRequest(root));
        var pack = WorkspaceEvidencePackBuilder.Build(scan, Array.Empty<WorkspaceTechnicalPreviewInput>(), Array.Empty<WorkspaceMaterialPreviewInput>());

        AssertEqual("SingleProject", pack.Topology.Kind, "Root manifest plus conventional nested source should remain one project shape, not competing roots.");
        AssertFalse(pack.Topology.LikelyActiveSourceRoots.Contains("."), "Manifest-only root should not compete with nested source as a separate active source root.");
        AssertTrue(pack.Topology.LikelyActiveSourceRoots.Contains("src"), "Nested source root should remain visible as likely active source.");
    }
    finally
    {
        DeleteScratchWorkspace(root);
    }
}

static void WorkspaceEvidencePackClassifiesMaterialOnlyTopologyHonestly()
{
    var root = CreateScratchWorkspace();
    try
    {
        Directory.CreateDirectory(Path.Combine(root, "notes"));
        File.WriteAllText(Path.Combine(root, "notes", "README.md"), "random notes");

        var scan = WorkspaceScanner.Scan(new WorkspaceScanRequest(root));
        var pack = WorkspaceEvidencePackBuilder.Build(scan, Array.Empty<WorkspaceTechnicalPreviewInput>(), Array.Empty<WorkspaceMaterialPreviewInput>());

        AssertEqual("MaterialOnly", pack.Topology.Kind, "Documentation-only folders should not be presented as a standard source project topology.");
        AssertContains(pack.Topology.SafeImportMode, "material-only-review", "Material-only topology should keep documents as context materials.");
        AssertEqual(0, pack.Topology.LikelyActiveSourceRoots.Count, "Material-only topology should not invent active source roots.");
    }
    finally
    {
        DeleteScratchWorkspace(root);
    }
}

static void WorkspaceEvidencePackKeepsIgnoredDistVisibleAsReleaseOutputHonestly()
{
    var root = CreateScratchWorkspace();
    try
    {
        Directory.CreateDirectory(Path.Combine(root, "src"));
        Directory.CreateDirectory(Path.Combine(root, "dist"));
        File.WriteAllText(Path.Combine(root, "package.json"), "{ \"name\": \"demo\", \"scripts\": { \"build\": \"vite build\" } }");
        File.WriteAllText(Path.Combine(root, "src", "main.ts"), "export function run() {}");
        File.WriteAllText(Path.Combine(root, "dist", "bundle.js"), "console.log('built');");

        var scan = WorkspaceScanner.Scan(new WorkspaceScanRequest(root));
        var pack = WorkspaceEvidencePackBuilder.Build(scan, Array.Empty<WorkspaceTechnicalPreviewInput>(), Array.Empty<WorkspaceMaterialPreviewInput>());

        AssertEqual("Mixed", pack.Topology.Kind, "Source plus ignored dist output should remain mixed source/release evidence.");
        AssertTrue(pack.Topology.ReleaseOutputZones.Contains("dist"), "Ignored dist output should still be exposed as release output, not hidden as generic noise.");
        AssertContains(pack.Topology.SafeImportMode, "mixed-source-release", "Safe import mode should keep ignored release output separated from source.");
    }
    finally
    {
        DeleteScratchWorkspace(root);
    }
}

static void WorkspaceEvidencePackClassifiesLowLevelSourceAsLegacyTopologyHonestly()
{
    var root = CreateScratchWorkspace();
    try
    {
        File.WriteAllText(Path.Combine(root, "Makefile"), "all:\n\trgbasm -o main.o main.asm");
        File.WriteAllText(Path.Combine(root, "main.asm"), "SECTION \"start\", ROM0\nnop");

        var scan = WorkspaceScanner.Scan(new WorkspaceScanRequest(root));
        var pack = WorkspaceEvidencePackBuilder.Build(scan, Array.Empty<WorkspaceTechnicalPreviewInput>(), Array.Empty<WorkspaceMaterialPreviewInput>());

        AssertEqual("Legacy", pack.Topology.Kind, "Low-level assembly source without direct reverse evidence should be treated as legacy topology, not a normal app.");
        AssertContains(pack.Topology.SafeImportMode, "legacy-low-level-source-review", "Legacy topology should avoid normal application assumptions.");
    }
    finally
    {
        DeleteScratchWorkspace(root);
    }
}

static void WorkspaceEvidencePackClassifiesDecompilationTopologyHonestly()
{
    var root = CreateScratchWorkspace();
    try
    {
        Directory.CreateDirectory(Path.Combine(root, "disasm"));
        Directory.CreateDirectory(Path.Combine(root, "src"));
        File.WriteAllText(Path.Combine(root, "Makefile"), "all:\n\tcc src/main.c");
        File.WriteAllText(Path.Combine(root, "disasm", "boot.s"), "nop");
        File.WriteAllText(Path.Combine(root, "src", "main.c"), "int main() { return 0; }");
        File.WriteAllText(Path.Combine(root, "README.md"), "Game decompilation with disassembly, emulator, opcode, and memory labels.");

        var scan = WorkspaceScanner.Scan(new WorkspaceScanRequest(root));
        var pack = WorkspaceEvidencePackBuilder.Build(
            scan,
            Array.Empty<WorkspaceTechnicalPreviewInput>(),
            new[]
            {
                new WorkspaceMaterialPreviewInput(
                    "README.md",
                    WorkspaceMaterialKind.TextDocument,
                    "root readme",
                    "Game decompilation with disassembly, emulator, opcode, and memory labels.",
                    WasTruncated: false)
            });

        AssertEqual("Decompilation", pack.Topology.Kind, "Reverse/decompilation evidence should select decompilation topology instead of normal single project.");
        AssertContains(pack.Topology.SafeImportMode, "decompilation-safe-import", "Safe import mode should avoid normal application assumptions for decompilation layouts.");
        AssertTrue(pack.Topology.ObservedZones.Any(zone => string.Equals(zone.Root, "disasm", StringComparison.OrdinalIgnoreCase) && zone.Role == "active-source"), "Disassembly source zone should remain visible as active source evidence.");
    }
    finally
    {
        DeleteScratchWorkspace(root);
    }
}

static void WorkspaceEvidencePackDoesNotOverclaimReverseTopologyFromReferenceWordingHonestly()
{
    var root = CreateScratchWorkspace();
    try
    {
        Directory.CreateDirectory(Path.Combine(root, "src"));
        Directory.CreateDirectory(Path.Combine(root, "docs", "reference"));
        File.WriteAllText(Path.Combine(root, "pyproject.toml"), "[project]\nname = \"plain\"");
        File.WriteAllText(Path.Combine(root, "src", "plain.py"), "def lint(): pass");
        File.WriteAllText(Path.Combine(root, "docs", "reference", "release-notes.md"), "Reverse chronological release notes and reference docs.");

        var scan = WorkspaceScanner.Scan(new WorkspaceScanRequest(root));
        var pack = WorkspaceEvidencePackBuilder.Build(
            scan,
            Array.Empty<WorkspaceTechnicalPreviewInput>(),
            new[]
            {
                new WorkspaceMaterialPreviewInput(
                    Path.Combine("docs", "reference", "release-notes.md"),
                    WorkspaceMaterialKind.TextDocument,
                    "reference docs",
                    "Reverse chronological release notes and reference docs.",
                    WasTruncated: false)
            });

        AssertFalse(string.Equals(pack.Topology.Kind, "Decompilation", StringComparison.OrdinalIgnoreCase), "Reference/release wording alone must not classify the topology as decompilation.");
        AssertFalse(pack.Signals.Any(signal => signal.Category == "origin" && signal.Code == "reverse"), "Reverse origin signal requires direct low-level/reversing evidence, not generic wording.");
    }
    finally
    {
        DeleteScratchWorkspace(root);
    }
}

static void WorkspaceEvidencePackClassifiesUnrelatedRootsAsContainerTopologyHonestly()
{
    var root = CreateScratchWorkspace();
    try
    {
        Directory.CreateDirectory(Path.Combine(root, "alpha", "src"));
        Directory.CreateDirectory(Path.Combine(root, "beta", "src"));
        Directory.CreateDirectory(Path.Combine(root, "gamma", "src", "gamma"));
        File.WriteAllText(Path.Combine(root, "alpha", "package.json"), "{ \"name\": \"alpha\", \"scripts\": { \"build\": \"vite build\" } }");
        File.WriteAllText(Path.Combine(root, "alpha", "src", "main.ts"), "export {};");
        File.WriteAllText(Path.Combine(root, "beta", "Cargo.toml"), "[package]\nname = \"beta\"");
        File.WriteAllText(Path.Combine(root, "beta", "src", "main.rs"), "fn main() {}");
        File.WriteAllText(Path.Combine(root, "gamma", "pyproject.toml"), "[project]\nname = \"gamma\"");
        File.WriteAllText(Path.Combine(root, "gamma", "src", "gamma", "__init__.py"), "");

        var scan = WorkspaceScanner.Scan(new WorkspaceScanRequest(root));
        var pack = WorkspaceEvidencePackBuilder.Build(scan, Array.Empty<WorkspaceTechnicalPreviewInput>(), Array.Empty<WorkspaceMaterialPreviewInput>());

        AssertEqual("Container", pack.Topology.Kind, "Three unrelated manifest roots should be treated as container topology, not forced into one project.");
        AssertContains(pack.Topology.SafeImportMode, "container-review", "Container topology should require user-selected active root before primary-project claims.");
        AssertTrue(pack.Topology.LikelyActiveSourceRoots.Count >= 3, "Container topology should keep multiple active roots visible.");
        AssertTrue(pack.Topology.UncertaintyReasons.Any(reason => reason.Contains("MULTIPLE_ACTIVE_SOURCE_ROOTS", StringComparison.OrdinalIgnoreCase)), "Container topology should explain competing active roots.");
    }
    finally
    {
        DeleteScratchWorkspace(root);
    }
}

static void WorkspaceEvidencePackKeepsContainerBoundaryStrongerThanNestedDecompEvidenceHonestly()
{
    var root = CreateScratchWorkspace();
    try
    {
        Directory.CreateDirectory(Path.Combine(root, "alpha", "disasm"));
        Directory.CreateDirectory(Path.Combine(root, "beta", "src"));
        Directory.CreateDirectory(Path.Combine(root, "gamma", "src"));
        File.WriteAllText(Path.Combine(root, "alpha", "Makefile"), "all:\n\tcc disasm/boot.s");
        File.WriteAllText(Path.Combine(root, "alpha", "package.json"), "{ \"name\": \"alpha-tooling\", \"scripts\": { \"build\": \"make\" } }");
        File.WriteAllText(Path.Combine(root, "alpha", "disasm", "boot.s"), "nop");
        File.WriteAllText(Path.Combine(root, "alpha", "README.md"), "Game decompilation with disassembly, emulator, opcode, and memory labels.");
        File.WriteAllText(Path.Combine(root, "beta", "package.json"), "{ \"name\": \"beta\", \"scripts\": { \"build\": \"vite build\" } }");
        File.WriteAllText(Path.Combine(root, "beta", "src", "main.ts"), "export {};");
        File.WriteAllText(Path.Combine(root, "gamma", "Cargo.toml"), "[package]\nname = \"gamma\"");
        File.WriteAllText(Path.Combine(root, "gamma", "src", "main.rs"), "fn main() {}");

        var scan = WorkspaceScanner.Scan(new WorkspaceScanRequest(root));
        var pack = WorkspaceEvidencePackBuilder.Build(
            scan,
            Array.Empty<WorkspaceTechnicalPreviewInput>(),
            new[]
            {
                new WorkspaceMaterialPreviewInput(
                    Path.Combine("alpha", "README.md"),
                    WorkspaceMaterialKind.TextDocument,
                    "nested decomp readme",
                    "Game decompilation with disassembly, emulator, opcode, and memory labels.",
                    WasTruncated: false)
            });

        AssertEqual("Container", pack.Topology.Kind, "A folder with multiple unrelated project roots should stay container even when one nested root has decompilation evidence.");
        AssertContains(pack.Topology.SafeImportMode, "container-review", "Container boundary should require root selection before nested topology claims.");
    }
    finally
    {
        DeleteScratchWorkspace(root);
    }
}

static void WorkspaceEvidencePackAppliesScannerConfigUnitOverridesHonestly()
{
    var root = CreateScratchWorkspace();
    try
    {
        Directory.CreateDirectory(Path.Combine(root, ".zavod", "scanner"));
        Directory.CreateDirectory(Path.Combine(root, "apps", "desktop", "src"));
        Directory.CreateDirectory(Path.Combine(root, "tools", "helper", "src"));
        Directory.CreateDirectory(Path.Combine(root, "vendor", "sdk"));

        File.WriteAllText(
            Path.Combine(root, ".zavod", "scanner", "config.json"),
            """
            {
              "primaryUnits": ["tools/helper"],
              "ignoreZones": ["apps/desktop"],
              "vendorZones": ["vendor/sdk"],
              "generatedPatterns": ["*.g.cs"]
            }
            """);
        File.WriteAllText(Path.Combine(root, "Cargo.toml"), "[workspace]\nmembers = [\"apps/desktop\", \"tools/helper\", \"vendor/sdk\"]");
        File.WriteAllText(Path.Combine(root, "apps", "desktop", "Cargo.toml"), "[package]\nname = \"desktop\"");
        File.WriteAllText(Path.Combine(root, "apps", "desktop", "src", "main.rs"), "fn main() {}");
        File.WriteAllText(Path.Combine(root, "tools", "helper", "Cargo.toml"), "[package]\nname = \"helper\"");
        File.WriteAllText(Path.Combine(root, "tools", "helper", "src", "main.rs"), "fn main() {}");
        File.WriteAllText(Path.Combine(root, "tools", "helper", "src", "generated.g.cs"), "public sealed class Generated {}");
        File.WriteAllText(Path.Combine(root, "vendor", "sdk", "Cargo.toml"), "[package]\nname = \"sdk\"");

        var scan = WorkspaceScanner.Scan(new WorkspaceScanRequest(root));
        var pack = WorkspaceEvidencePackBuilder.Build(scan, Array.Empty<WorkspaceTechnicalPreviewInput>(), Array.Empty<WorkspaceMaterialPreviewInput>());
        var units = pack.Candidates.ProjectUnits;

        var helperUnit = units.FirstOrDefault(unit => string.Equals(unit.RootPath, Path.Combine("tools", "helper"), StringComparison.OrdinalIgnoreCase));
        AssertTrue(helperUnit is not null, "Configured primary unit should remain visible even when it lives under a usually demoted helper path.");
        AssertTrue(helperUnit!.Evidence.Contains("config_primary_unit", StringComparer.OrdinalIgnoreCase), "Configured primary unit should carry explicit config evidence.");
        AssertTrue(helperUnit.Evidence.Contains("config:.zavod\\scanner\\config.json", StringComparer.OrdinalIgnoreCase), "Config evidence should point to the scanner config file.");
        AssertFalse(units.Any(unit => string.Equals(unit.RootPath, Path.Combine("apps", "desktop"), StringComparison.OrdinalIgnoreCase)), "Configured ignore zone should suppress that unit candidate.");
        AssertFalse(units.Any(unit => string.Equals(unit.RootPath, Path.Combine("vendor", "sdk"), StringComparison.OrdinalIgnoreCase)), "Unsupported/vendor roots should not become project units even when present in workspace manifests.");
        AssertEqual(Path.Combine("tools", "helper"), units[0].RootPath, "Configured primary unit should outrank helper-path demotion deterministically.");
        var generatedFile = pack.FileIndex.FirstOrDefault(file => string.Equals(file.RelativePath, Path.Combine("tools", "helper", "src", "generated.g.cs"), StringComparison.OrdinalIgnoreCase));
        AssertTrue(generatedFile is not null, "Configured generated pattern should keep the file visible in inventory.");
        AssertEqual("generated", generatedFile!.Zone, "Configured generated pattern should mark file inventory zone without deleting evidence.");
        AssertContains(generatedFile.Evidence, "config_generated_pattern", "Generated file inventory item should carry config evidence.");
    }
    finally
    {
        DeleteScratchWorkspace(root);
    }
}

static void WorkspaceEvidencePackRequiresRepeatedStructuralSupportForRuntimeAndPlatformsHonestly()
{
    var root = CreateScratchWorkspace();
    try
    {
        Directory.CreateDirectory(Path.Combine(root, "src"));
        File.WriteAllText(Path.Combine(root, "src", "main.cpp"), "int main() { return 0; }");
        File.WriteAllText(
            Path.Combine(root, "README.md"),
            "Cross-platform browser-like story with Linux deployment, language server plans, web dashboard ideas, and remote service ambitions.");

        var scan = WorkspaceScanner.Scan(new WorkspaceScanRequest(root));
        var pack = WorkspaceEvidencePackBuilder.Build(
            scan,
            Array.Empty<WorkspaceTechnicalPreviewInput>(),
            new[]
            {
                new WorkspaceMaterialPreviewInput(
                    "README.md",
                    WorkspaceMaterialKind.TextDocument,
                    "text-first-preview",
                    "Cross-platform browser-like story with Linux deployment, language server plans, web dashboard ideas, and remote service ambitions.",
                    false,
                    "Extracted",
                    "text",
                    "plain text")
            });

        AssertFalse(pack.TechnicalPassport.RuntimeSurfaces.Contains("web", StringComparer.OrdinalIgnoreCase), "Narrative web wording alone should not create web runtime surface.");
        AssertFalse(pack.TechnicalPassport.RuntimeSurfaces.Contains("service", StringComparer.OrdinalIgnoreCase), "Narrative service wording alone should not create service runtime surface.");
        AssertFalse(pack.TechnicalPassport.TargetPlatforms.Contains("Linux", StringComparer.OrdinalIgnoreCase), "Narrative Linux wording alone should not create Linux target platform.");
        AssertFalse(pack.TechnicalPassport.TargetPlatforms.Contains("Web", StringComparer.OrdinalIgnoreCase), "Web target platform should require structural web evidence, not prose.");
    }
    finally
    {
        DeleteScratchWorkspace(root);
    }
}

static void WorkspaceEvidencePackKeepsBehaviorAndOriginBoundedOnNoisyMixedRepoHonestly()
{
    var root = CreateScratchWorkspace();
    try
    {
        Directory.CreateDirectory(Path.Combine(root, "src", "core"));
        Directory.CreateDirectory(Path.Combine(root, "docs"));
        File.WriteAllText(Path.Combine(root, "src", "core", "main.cpp"), "int main() { return 0; }");
        File.WriteAllText(
            Path.Combine(root, "docs", "notes.md"),
            "Plugin ideas, automation thoughts, network concepts, parser sketches, memory notes, and service dreams are captured here for later.");

        var scan = WorkspaceScanner.Scan(new WorkspaceScanRequest(root));
        var pack = WorkspaceEvidencePackBuilder.Build(
            scan,
            Array.Empty<WorkspaceTechnicalPreviewInput>(),
            new[]
            {
                new WorkspaceMaterialPreviewInput(
                    "docs\\notes.md",
                    WorkspaceMaterialKind.TextDocument,
                    "text-first-preview",
                    "Plugin ideas, automation thoughts, network concepts, parser sketches, memory notes, and service dreams are captured here for later.",
                    false,
                    "Extracted",
                    "text",
                    "plain text")
            });

        var behaviorSignals = pack.Signals
            .Where(static signal => string.Equals(signal.Category, "behavior", StringComparison.OrdinalIgnoreCase))
            .Select(static signal => signal.Code)
            .ToArray();
        var originSignals = pack.Signals
            .Where(static signal => string.Equals(signal.Category, "origin", StringComparison.OrdinalIgnoreCase))
            .Select(static signal => signal.Code)
            .ToArray();

        AssertFalse(behaviorSignals.Contains("network", StringComparer.OrdinalIgnoreCase), "Single noisy note should not inflate network behavior.");
        AssertFalse(behaviorSignals.Contains("automation", StringComparer.OrdinalIgnoreCase), "Single noisy note should not inflate automation behavior.");
        AssertFalse(behaviorSignals.Contains("low_level_memory", StringComparer.OrdinalIgnoreCase), "Single noisy note should not inflate low-level memory behavior.");
        AssertFalse(originSignals.Contains("modding", StringComparer.OrdinalIgnoreCase), "Generic plugin wording should not inflate modding origin.");
        AssertFalse(originSignals.Contains("reverse", StringComparer.OrdinalIgnoreCase), "Noisy prose should not inflate reverse origin without repeated structural support.");
    }
    finally
    {
        DeleteScratchWorkspace(root);
    }
}

static void WorkspaceEvidencePackKeepsServiceRuntimeBoundedForTestServerRootsHonestly()
{
    var root = CreateScratchWorkspace();
    try
    {
        Directory.CreateDirectory(Path.Combine(root, "cloud", "testserver", "cmd", "testserver"));
        Directory.CreateDirectory(Path.Combine(root, "api"));
        File.WriteAllText(Path.Combine(root, "go.mod"), "module example.com/infra");
        File.WriteAllText(Path.Combine(root, "cloud", "testserver", "cmd", "testserver", "main.go"), "package main\nfunc main() {}");
        File.WriteAllText(Path.Combine(root, "api", "types.go"), "package api\ntype Config struct {}");
        File.WriteAllText(Path.Combine(root, "README.md"), "Cloud orchestration notes and remote service ideas.");

        var scan = WorkspaceScanner.Scan(new WorkspaceScanRequest(root));
        var pack = WorkspaceEvidencePackBuilder.Build(
            scan,
            Array.Empty<WorkspaceTechnicalPreviewInput>(),
            new[]
            {
                new WorkspaceMaterialPreviewInput(
                    "README.md",
                    WorkspaceMaterialKind.TextDocument,
                    "text-first-preview",
                    "Cloud orchestration notes and remote service ideas.",
                    false,
                    "Extracted",
                    "text",
                    "plain text")
            });

        AssertFalse(pack.TechnicalPassport.RuntimeSurfaces.Contains("service", StringComparer.OrdinalIgnoreCase), "Testserver and api roots alone should not inflate service runtime.");
        AssertTrue(pack.TechnicalPassport.RuntimeSurfaces.Contains("cli", StringComparer.OrdinalIgnoreCase), "Go command entry should still preserve CLI/runtime reality.");
    }
    finally
    {
        DeleteScratchWorkspace(root);
    }
}

static void WorkspaceEvidencePackKeepsLegacyScannerSemanticsDeprecatedHonestly()
{
    var root = CreateScratchWorkspace();
    try
    {
        Directory.CreateDirectory(Path.Combine(root, "src", "ui"));
        Directory.CreateDirectory(Path.Combine(root, "src", "core"));
        File.WriteAllText(Path.Combine(root, "src", "ui", "mainwindow.cpp"), "MainWindow");
        File.WriteAllText(Path.Combine(root, "src", "core", "registry.cpp"), "registry");

        var scan = WorkspaceScanner.Scan(new WorkspaceScanRequest(root));
        var pack = WorkspaceEvidencePackBuilder.Build(scan, Array.Empty<WorkspaceTechnicalPreviewInput>(), Array.Empty<WorkspaceMaterialPreviewInput>());

        AssertTrue(pack.Edges.SequenceEqual(pack.DependencyEdges), "Legacy dependency edge mirror should stay equal to authoritative cold edges during transition.");
        AssertFalse(pack.ObservedLayers.Any(static layer => string.Equals(layer.Name, "UI", StringComparison.OrdinalIgnoreCase) || string.Equals(layer.Name, "Core", StringComparison.OrdinalIgnoreCase)), "Legacy observed layers should stay structural and must not expose semantic UI/Core layers as authoritative truth.");
    }
    finally
    {
        DeleteScratchWorkspace(root);
    }
}

static void WorkspaceEvidencePackRequiresNonDocOverlapForTechnicalDocBoostsHonestly()
{
    var root = CreateScratchWorkspace();
    try
    {
        Directory.CreateDirectory(Path.Combine(root, "src"));
        File.WriteAllText(Path.Combine(root, "src", "main.cpp"), "int main() { return 0; }");

        var scan = WorkspaceScanner.Scan(new WorkspaceScanRequest(root));
        var pack = WorkspaceEvidencePackBuilder.Build(
            scan,
            Array.Empty<WorkspaceTechnicalPreviewInput>(),
            new[]
            {
                new WorkspaceMaterialPreviewInput(
                    "docs\\design.md",
                    WorkspaceMaterialKind.TextDocument,
                    "text-first-preview",
                    "Debugger opcode memory disassembly architecture notes without any matching non-doc evidence.",
                    false,
                    "Extracted",
                    "text",
                    "plain text")
            });

        AssertFalse(pack.DerivedPatterns.Any(static pattern => pattern.Code == "analysis_tooling_pattern"), "Technical-sounding docs alone should not create analysis tooling pattern without non-doc overlap.");
    }
    finally
    {
        DeleteScratchWorkspace(root);
    }
}

static void WorkspaceImportMaterialPromptRequestDemotesTechnicalPassportHonestly()
{
    var root = CreateScratchWorkspace();
    try
    {
        Directory.CreateDirectory(Path.Combine(root, "src"));
        File.WriteAllText(Path.Combine(root, "src", "main.cpp"), "int main() { return 0; }");
        File.WriteAllText(Path.Combine(root, "CMakeLists.txt"), "project(Test)");

        var scan = WorkspaceScanner.Scan(new WorkspaceScanRequest(root));
        var runtimeFront = new WorkspaceMaterialRuntimeFront(
            new TextMaterialRuntimeService(),
            new PdfExtractionRuntimeService(new FakeExternalProcessRunner(_ => new ExternalProcessResult(1, string.Empty, "Unexpected", false))),
            new ArchiveInspectionRuntimeService(new FakeExternalProcessRunner(_ => new ExternalProcessResult(1, string.Empty, "Unexpected", false))),
            new ImageInspectionRuntimeService());

        var packet = runtimeFront.BuildPreviewPacket(scan, maxMaterials: 4, maxCharsPerMaterial: 128);
        var request = WorkspaceImportMaterialPromptRequestBuilder.Build(packet);

        AssertContains(request.UserPrompt, "technical_passport_transitional_ux:", "Prompt should demote technical passport to transitional UX summary.");
        AssertContains(request.UserPrompt, "deprecated comparison payloads", "Prompt should explicitly demote legacy scanner payloads.");
    }
    finally
    {
        DeleteScratchWorkspace(root);
    }
}

static void WorkspaceEvidencePackExtractsCodeEdgesAndSignaturesHonestly()
{
    var root = CreateScratchWorkspace();
    try
    {
        Directory.CreateDirectory(Path.Combine(root, "src"));
        File.WriteAllText(Path.Combine(root, "src", "main.cpp"), "#include \"engine.h\"\nint main() { return 0; }");
        File.WriteAllText(Path.Combine(root, "src", "engine.h"), "class Engine {};");

        var scan = WorkspaceScanner.Scan(new WorkspaceScanRequest(root));
        var pack = WorkspaceEvidencePackBuilder.Build(scan, Array.Empty<WorkspaceTechnicalPreviewInput>(), Array.Empty<WorkspaceMaterialPreviewInput>());

        var includeEdge = pack.CodeEdges.FirstOrDefault(edge => edge.FromPath.EndsWith("src\\main.cpp", StringComparison.OrdinalIgnoreCase) && edge.ToPath.EndsWith("src\\engine.h", StringComparison.OrdinalIgnoreCase));
        AssertTrue(includeEdge is not null, "Scanner should extract bounded file-to-file include edges.");
        AssertTrue(includeEdge!.EvidenceMarker is not null, "Code edge should carry a structured scanner evidence marker.");
        AssertEqual("code_edge_candidate", includeEdge.EvidenceMarker!.EvidenceKind, "Code edge marker should expose evidence kind.");
        AssertTrue(includeEdge.EvidenceMarker.IsBounded, "Shallow code edge extraction should mark evidence as bounded.");
        AssertEqual(WorkspaceEvidenceConfidenceLevel.Confirmed, includeEdge.EvidenceMarker.Confidence, "Resolved include edge can be confirmed by direct file evidence.");
        var prompt = WorkspaceImportMaterialPromptRequestBuilder.Build(new WorkspaceImportMaterialPreviewPacket(
            scan.State.WorkspaceRoot,
            scan.State.ImportKind,
            scan.State.Summary.SourceRoots,
            Array.Empty<WorkspaceTechnicalPreviewInput>(),
            Array.Empty<WorkspaceMaterialPreviewInput>(),
            pack)).UserPrompt;
        AssertContains(prompt, "marker=code_edge_candidate/Confirmed/partial=False/bounded=True", "Importer prompt should expose bounded code-edge marker discipline.");
        AssertTrue(pack.SignatureHints.Any(hint => hint.Kind == "bootstrap" && hint.RelativePath.EndsWith("src\\main.cpp", StringComparison.OrdinalIgnoreCase)), "Scanner should extract cheap bootstrap signature hints.");
        AssertTrue(pack.SignatureHints.Any(hint => hint.Kind == "type" && hint.RelativePath.EndsWith("src\\engine.h", StringComparison.OrdinalIgnoreCase)), "Scanner should extract cheap type signature hints.");
    }
    finally
    {
        DeleteScratchWorkspace(root);
    }
}

static void WorkspaceEvidencePackAnnotatesEdgeResolutionHonestly()
{
    var root = CreateScratchWorkspace();
    try
    {
        Directory.CreateDirectory(Path.Combine(root, "src", "core"));
        Directory.CreateDirectory(Path.Combine(root, "src", "ui"));
        File.WriteAllText(Path.Combine(root, "src", "main.cpp"), "#include \"engine.h\"\n#include \"missing.h\"\nint main() { return 0; }");
        File.WriteAllText(Path.Combine(root, "src", "core", "engine.h"), "class Engine {};");
        File.WriteAllText(Path.Combine(root, "src", "ui", "engine.h"), "class EngineView {};");

        var scan = WorkspaceScanner.Scan(new WorkspaceScanRequest(root));
        var pack = WorkspaceEvidencePackBuilder.Build(scan, Array.Empty<WorkspaceTechnicalPreviewInput>(), Array.Empty<WorkspaceMaterialPreviewInput>());

        var ambiguousEdge = pack.CodeEdges.FirstOrDefault(edge =>
                edge.FromPath.EndsWith("src\\main.cpp", StringComparison.OrdinalIgnoreCase) &&
                edge.ToPath.EndsWith("engine.h", StringComparison.OrdinalIgnoreCase) &&
                edge.Resolution == WorkspaceEvidenceEdgeResolution.Ambiguous);
        AssertTrue(
            ambiguousEdge is not null,
            "Multiple local include candidates should be marked ambiguous instead of silently presented as confirmed resolution.");
        AssertEqual(WorkspaceEvidenceConfidenceLevel.Likely, ambiguousEdge!.EvidenceMarker!.Confidence, "Ambiguous edge must not be marked confirmed.");
        AssertTrue(
            pack.CodeEdges.Any(edge =>
                edge.FromPath.EndsWith("src\\main.cpp", StringComparison.OrdinalIgnoreCase) &&
                string.Equals(edge.ToPath, "missing.h", StringComparison.OrdinalIgnoreCase) &&
                edge.Resolution == WorkspaceEvidenceEdgeResolution.Unresolved),
            "Unresolved local include evidence should remain visible with unresolved resolution.");
        AssertTrue(
            pack.Edges.Any(edge =>
                edge.From.EndsWith("src\\main.cpp", StringComparison.OrdinalIgnoreCase) &&
                edge.Resolution == WorkspaceEvidenceEdgeResolution.Ambiguous),
            "Authoritative dependency edges should preserve code edge resolution.");
    }
    finally
    {
        DeleteScratchWorkspace(root);
    }
}

static void WorkspaceEvidencePackImprovesRustAndGoCodeEdgesHonestly()
{
    var root = CreateScratchWorkspace();
    try
    {
        Directory.CreateDirectory(Path.Combine(root, "src", "pkg"));
        Directory.CreateDirectory(Path.Combine(root, "rustsrc"));
        File.WriteAllText(Path.Combine(root, "src", "main.go"), "package main\nimport \"example/pkg\"\nfunc main() {}");
        File.WriteAllText(Path.Combine(root, "src", "pkg", "main.go"), "package pkg\nfunc Run() {}");
        File.WriteAllText(Path.Combine(root, "rustsrc", "main.rs"), "use crate::engine::state;\nmod engine;\nfn main() {}");
        File.WriteAllText(Path.Combine(root, "rustsrc", "engine.rs"), "pub mod state;");
        File.WriteAllText(Path.Combine(root, "rustsrc", "state.rs"), "pub struct State;");

        var scan = WorkspaceScanner.Scan(new WorkspaceScanRequest(root));
        var pack = WorkspaceEvidencePackBuilder.Build(scan, Array.Empty<WorkspaceTechnicalPreviewInput>(), Array.Empty<WorkspaceMaterialPreviewInput>());

        AssertTrue(pack.CodeEdges.Any(edge => edge.FromPath.EndsWith("src\\main.go", StringComparison.OrdinalIgnoreCase) && edge.ToPath.EndsWith("src\\pkg\\main.go", StringComparison.OrdinalIgnoreCase)), "Cheap go import resolution should connect main.go to local package files.");
        AssertTrue(pack.CodeEdges.Any(edge => edge.FromPath.EndsWith("rustsrc\\main.rs", StringComparison.OrdinalIgnoreCase) && edge.ToPath.EndsWith("rustsrc\\engine.rs", StringComparison.OrdinalIgnoreCase)), "Cheap Rust mod resolution should connect main.rs to sibling module files.");
    }
    finally
    {
        DeleteScratchWorkspace(root);
    }
}

static void WorkspaceTaskScopeResolverMapsBoundedScopeHonestly()
{
    var root = CreateScratchWorkspace();
    try
    {
        Directory.CreateDirectory(Path.Combine(root, "Workspace"));
        Directory.CreateDirectory(Path.Combine(root, "Execution"));
        Directory.CreateDirectory(Path.Combine(root, "src", "ui"));
        Directory.CreateDirectory(Path.Combine(root, "src", "tests"));

        File.WriteAllText(Path.Combine(root, "Workspace", "PreviewDocumentService.cs"), "using zavod.Execution;\nclass PreviewDocumentService {}");
        File.WriteAllText(Path.Combine(root, "Workspace", "CanonicalPromotionService.cs"), "class CanonicalPromotionService {}");
        File.WriteAllText(Path.Combine(root, "Execution", "DecisionJournalWriter.cs"), "class DecisionJournalWriter {}");
        File.WriteAllText(Path.Combine(root, "src", "ui", "Theme.css"), ".button { color: red; }");
        File.WriteAllText(Path.Combine(root, "src", "tests", "UnrelatedTests.cs"), "class UnrelatedTests {}");

        var scan = WorkspaceScanner.Scan(new WorkspaceScanRequest(root));
        var pack = WorkspaceEvidencePackBuilder.Build(scan, Array.Empty<WorkspaceTechnicalPreviewInput>(), Array.Empty<WorkspaceMaterialPreviewInput>());
        var scope = WorkspaceTaskScopeResolver.Resolve(pack, "fix promote reject preview docs journal flow", maxPrimaryFiles: 3, maxRelatedFiles: 4);

        AssertTrue(scope.PrimaryFiles.Any(file => file.RelativePath.EndsWith("Workspace\\PreviewDocumentService.cs", StringComparison.OrdinalIgnoreCase)), "Scope resolver should select preview document files from task terms.");
        AssertTrue(scope.PrimaryFiles.Any(file => file.RelativePath.EndsWith("Workspace\\CanonicalPromotionService.cs", StringComparison.OrdinalIgnoreCase)), "Scope resolver should select promotion/canonical files from task terms.");
        AssertTrue(scope.PrimaryFiles.Concat(scope.RelatedFiles).Any(file => file.RelativePath.EndsWith("Execution\\DecisionJournalWriter.cs", StringComparison.OrdinalIgnoreCase)), "Scope resolver should keep journal-related files visible.");
        AssertTrue(scope.SoftExcludedFiles.Any(file => file.RelativePath.EndsWith("src\\ui\\Theme.css", StringComparison.OrdinalIgnoreCase) || file.RelativePath.EndsWith("src\\tests\\UnrelatedTests.cs", StringComparison.OrdinalIgnoreCase)), "Scope resolver should report soft exclusions without turning them into hard bans.");
        AssertTrue(scope.Evidence.Any(item => item.Contains("path_term:preview", StringComparison.OrdinalIgnoreCase)), "Scope resolver should explain why primary files were selected.");
    }
    finally
    {
        DeleteScratchWorkspace(root);
    }
}

static void WorkspaceEvidencePredicateRegistryMapsScannerSurfacesHonestly()
{
    var root = CreateScratchWorkspace();
    try
    {
        Directory.CreateDirectory(Path.Combine(root, "src", "core"));
        Directory.CreateDirectory(Path.Combine(root, "src", "ui"));
        File.WriteAllText(Path.Combine(root, "src", "main.cpp"), "#include \"core/engine.h\"\nint main() { return 0; }");
        File.WriteAllText(Path.Combine(root, "src", "core", "engine.h"), "class Engine {};");
        File.WriteAllText(Path.Combine(root, "src", "ui", "Theme.css"), ".button { color: red; }");
        File.WriteAllText(Path.Combine(root, "package.json"), "{ \"scripts\": { \"build\": \"vite build\" }, \"dependencies\": { \"vite\": \"^5.0.0\" } }");
        File.WriteAllText(Path.Combine(root, "README.md"), "# Predicate Demo\n\nTechnical notes.");

        var scan = WorkspaceScanner.Scan(new WorkspaceScanRequest(root));
        var pack = WorkspaceEvidencePackBuilder.Build(
            scan,
            Array.Empty<WorkspaceTechnicalPreviewInput>(),
            new[]
            {
                new WorkspaceMaterialPreviewInput(
                    "README.md",
                    WorkspaceMaterialKind.TextDocument,
                    "text-first-preview",
                    "# Predicate Demo\n\nTechnical notes.",
                    false,
                    "Extracted",
                    "text",
                    "plain text")
            });
        var rebuiltPack = WorkspaceEvidencePackBuilder.Build(
            scan,
            Array.Empty<WorkspaceTechnicalPreviewInput>(),
            new[]
            {
                new WorkspaceMaterialPreviewInput(
                    "README.md",
                    WorkspaceMaterialKind.TextDocument,
                    "text-first-preview",
                    "# Predicate Demo\n\nTechnical notes.",
                    false,
                    "Extracted",
                    "text",
                    "plain text")
            });
        var scope = WorkspaceTaskScopeResolver.Resolve(pack, "fix core engine build", maxPrimaryFiles: 4, maxRelatedFiles: 4);

        AssertTrue(pack.PredicateRegistry.Any(static predicate => predicate.Id == WorkspaceEvidencePredicateRegistry.DeclaresCodeEdge), "Predicate registry should be part of evidence pack output.");
        AssertEqual(
            pack.PredicateRegistry.Count,
            pack.PredicateRegistry.Select(static predicate => predicate.Id).Distinct(StringComparer.OrdinalIgnoreCase).Count(),
            "Predicate ids must be unique.");
        AssertTrue(
            pack.ScanRun.ExtractorVersions.ContainsKey("predicate_registry"),
            "ScanRun provenance should name predicate registry version.");
        AssertEqual(
            pack.RawObservations.Count,
            pack.RawObservations.Select(static observation => observation.Id).Distinct(StringComparer.OrdinalIgnoreCase).Count(),
            "Raw observation stable ids should be unique inside a pack.");
        AssertTrue(
            pack.RawObservations.All(static observation => observation.Id.StartsWith("EV-", StringComparison.OrdinalIgnoreCase) &&
                                                           !string.IsNullOrWhiteSpace(observation.DisplayId) &&
                                                           !string.IsNullOrWhiteSpace(observation.Predicate) &&
                                                           !string.IsNullOrWhiteSpace(observation.Source) &&
                                                           !string.IsNullOrWhiteSpace(observation.ExtractorVersion)),
            "Raw observations should carry stable id, display id, predicate, source, and extractor version.");
        AssertTrue(
            pack.RawObservations
                .OrderBy(static observation => observation.Kind, StringComparer.OrdinalIgnoreCase)
                .ThenBy(static observation => observation.Value, StringComparer.OrdinalIgnoreCase)
                .ThenBy(static observation => observation.EvidencePath, StringComparer.OrdinalIgnoreCase)
                .Select(static observation => observation.Id)
                .SequenceEqual(
                    rebuiltPack.RawObservations
                        .OrderBy(static observation => observation.Kind, StringComparer.OrdinalIgnoreCase)
                        .ThenBy(static observation => observation.Value, StringComparer.OrdinalIgnoreCase)
                        .ThenBy(static observation => observation.EvidencePath, StringComparer.OrdinalIgnoreCase)
                        .Select(static observation => observation.Id),
                    StringComparer.OrdinalIgnoreCase),
            "Raw observation stable ids should survive identical rebuilds.");

        foreach (var observation in pack.RawObservations)
        {
            var predicate = WorkspaceEvidencePredicateRegistry.PredicateForObservationKind(observation.Kind);
            AssertTrue(WorkspaceEvidencePredicateRegistry.IsRegistered(predicate), $"Observation kind '{observation.Kind}' should map to a registered predicate.");
            AssertEqual(predicate, observation.Predicate, $"Observation '{observation.Id}' should carry its registered predicate.");
        }

        foreach (var signal in pack.Signals)
        {
            var predicate = WorkspaceEvidencePredicateRegistry.PredicateForSignal(signal.Category, signal.Code);
            AssertTrue(WorkspaceEvidencePredicateRegistry.IsRegistered(predicate), $"Signal '{signal.Category}:{signal.Code}' should map to a registered predicate.");
        }

        foreach (var edge in pack.CodeEdges)
        {
            var predicate = WorkspaceEvidencePredicateRegistry.PredicateForEdge(edge.Kind, edge.Resolution);
            AssertTrue(WorkspaceEvidencePredicateRegistry.IsRegistered(predicate), $"Code edge kind '{edge.Kind}' should map to a registered predicate.");
        }

        foreach (var edge in pack.Edges)
        {
            var predicate = WorkspaceEvidencePredicateRegistry.PredicateForEdge(edge.Label, edge.Resolution);
            AssertTrue(WorkspaceEvidencePredicateRegistry.IsRegistered(predicate), $"Dependency edge label '{edge.Label}' should map to a registered predicate.");
        }

        foreach (var file in scope.PrimaryFiles.Concat(scope.RelatedFiles).Concat(scope.SoftExcludedFiles))
        {
            foreach (var evidence in file.Evidence)
            {
                AssertTrue(
                    WorkspaceEvidencePredicateRegistry.TryResolveScopeEvidence(evidence, out var predicate) &&
                    WorkspaceEvidencePredicateRegistry.IsRegistered(predicate),
                    $"Scope evidence '{evidence}' should map to a registered predicate.");
            }
        }
    }
    finally
    {
        DeleteScratchWorkspace(root);
    }
}

static void ScannerV2PlanForbidsSmokeRepoSpecializationHonestly()
{
    var planPath = Path.Combine(
        Directory.GetCurrentDirectory(),
        "docs",
        "_legacy",
        "plans",
        "scanner-v2-evidence-cartographer-v1.md");
    var plan = File.ReadAllText(planPath, Encoding.UTF8);

    AssertContains(plan, "Real Repository Smoke Discipline", "Scanner v2 plan should name the anti-specialization guardrail.");
    AssertContains(plan, "Real repositories may be used as smoke targets, but not as behavioral", "Real repository scans should stay smoke checks, not scanner contracts.");
    AssertContains(plan, "behavior must be specified through synthetic, general-case fixtures", "Scanner behavior should be contracted through reusable synthetic fixtures.");
    AssertContains(plan, "real repo names, product names, and path quirks must not become scanner", "Scanner plan should forbid path/name specialization.");
    AssertContains(plan, "repository-specific heuristics for current smoke targets", "Scanner non-goals should reject smoke-target specialization explicitly.");
}

static void WorkspaceScannerFingerprintIsProvenanceNotContentHashHonestly()
{
    var root = CreateScratchWorkspace();
    try
    {
        Directory.CreateDirectory(Path.Combine(root, "src"));
        File.WriteAllText(Path.Combine(root, "src", "Program.cs"), "public sealed class Before {}");
        File.WriteAllText(Path.Combine(root, "zavod.csproj"), "<Project Sdk=\"Microsoft.NET.Sdk\" />");

        var firstScan = WorkspaceScanner.Scan(new WorkspaceScanRequest(root));
        var firstPack = WorkspaceEvidencePackBuilder.Build(firstScan, Array.Empty<WorkspaceTechnicalPreviewInput>(), Array.Empty<WorkspaceMaterialPreviewInput>());
        var firstPrompt = WorkspaceImportMaterialPromptRequestBuilder.Build(new WorkspaceImportMaterialPreviewPacket(
            firstScan.State.WorkspaceRoot,
            firstScan.State.ImportKind,
            firstScan.State.Summary.SourceRoots,
            Array.Empty<WorkspaceTechnicalPreviewInput>(),
            Array.Empty<WorkspaceMaterialPreviewInput>(),
            firstPack)).UserPrompt;

        File.WriteAllText(Path.Combine(root, "src", "Program.cs"), "public sealed class After {}");

        var secondScan = WorkspaceScanner.Scan(new WorkspaceScanRequest(root));
        var secondPack = WorkspaceEvidencePackBuilder.Build(secondScan, Array.Empty<WorkspaceTechnicalPreviewInput>(), Array.Empty<WorkspaceMaterialPreviewInput>());

        AssertEqual(firstPack.ScanRun.RepoRootHash, secondPack.ScanRun.RepoRootHash, "Scanner fingerprint should track structural scan identity, not file-content integrity.");
        AssertContains(firstPrompt, "scan_fingerprint:", "Importer prompt should use scanner-facing scan_fingerprint wording.");
        AssertContains(firstPrompt, "scan_fingerprint_scope: structural scan identity, not content-integrity hash", "Importer prompt should state fingerprint scope honestly.");
        AssertFalse(firstPrompt.Contains("repo_root_hash:", StringComparison.Ordinal), "Importer prompt should not present the compatibility field as a repo root content hash.");
    }
    finally
    {
        DeleteScratchWorkspace(root);
    }
}

static void WorkspaceScannerReportsPerformanceBudgetsHonestly()
{
    var root = CreateScratchWorkspace();
    try
    {
        Directory.CreateDirectory(Path.Combine(root, "src"));
        Directory.CreateDirectory(Path.Combine(root, "docs"));
        File.WriteAllText(Path.Combine(root, "src", "Alpha.cs"), "public sealed class Alpha {}");
        File.WriteAllText(Path.Combine(root, "src", "Beta.cs"), "public sealed class Beta {}");
        File.WriteAllText(Path.Combine(root, "docs", "large.md"), new string('x', 96));

        var budget = new WorkspaceScanBudget(
            MaxVisitedFiles: 100,
            MaxRelevantFiles: 1,
            MaxRelevantFileBytes: 32);
        var scan = WorkspaceScanner.Scan(new WorkspaceScanRequest(root, Budget: budget));
        var report = scan.BudgetReport ?? throw new InvalidOperationException("Scan should always carry a budget report.");

        AssertTrue(report.IsPartial, "Budget-limited scan should mark itself partial.");
        AssertEqual(WorkspaceHealthStatus.ScanPending, scan.State.Health, "Budget-limited scan should not report fully healthy workspace health.");
        AssertEqual(1, report.IncludedRelevantFileCount, "Relevant file budget should cap included relevant files.");
        AssertEqual(1, report.SkippedLargeFileCount, "Large relevant file should be counted as skipped by size.");
        AssertTrue(report.SkippedRelevantFileCount >= 1, "Relevant file cap should count skipped relevant files.");
        AssertTrue(report.Skips.Any(static skip => skip.Reason == "max_relevant_file_bytes" && skip.RelativePath.EndsWith("large.md", StringComparison.OrdinalIgnoreCase)), "Budget report should preserve sampled large-file skip evidence.");
        AssertTrue(scan.State.StructuralAnomalies.Any(static anomaly => anomaly.Code == "SCAN_BUDGET_PARTIAL"), "Partial scans should surface as structural scan uncertainty.");

        var pack = WorkspaceEvidencePackBuilder.Build(scan, Array.Empty<WorkspaceTechnicalPreviewInput>(), Array.Empty<WorkspaceMaterialPreviewInput>());
        AssertTrue(pack.ScanBudget?.IsPartial == true, "Evidence pack should carry scan budget report.");
        AssertTrue(pack.RawObservations.Any(static observation => observation.Kind == "scan_budget_degraded" && observation.Value == "partial_scan"), "Evidence pack should expose partial scan observation.");
        AssertTrue(pack.RawObservations.Any(static observation => observation.Kind == "scan_budget_skip_detected" && observation.Value == "max_relevant_file_bytes"), "Evidence pack should expose budget skip observations.");
        AssertEqual(
            WorkspaceEvidencePredicateRegistry.ReportsScanBudget,
            WorkspaceEvidencePredicateRegistry.PredicateForObservationKind("scan_budget_skip_detected"),
            "Budget observations should map to a registered budget predicate.");

        var prompt = WorkspaceImportMaterialPromptRequestBuilder.Build(new WorkspaceImportMaterialPreviewPacket(
            scan.State.WorkspaceRoot,
            scan.State.ImportKind,
            scan.State.Summary.SourceRoots,
            Array.Empty<WorkspaceTechnicalPreviewInput>(),
            Array.Empty<WorkspaceMaterialPreviewInput>(),
            pack)).UserPrompt;
        AssertContains(prompt, "scan_budget:", "Importer prompt should make scan budget state visible.");
        AssertContains(prompt, "partial=True", "Importer prompt should preserve partial scan status.");
        AssertContains(prompt, "health: ScanPending", "Importer prompt should not surface partial scans as plain Healthy.");
    }
    finally
    {
        DeleteScratchWorkspace(root);
    }
}

static void WorkspaceEvidencePackRanksCargoDefaultMemberEntriesHonestly()
{
    var root = CreateScratchWorkspace();
    try
    {
        Directory.CreateDirectory(Path.Combine(root, "bin", "spiced", "src"));
        Directory.CreateDirectory(Path.Combine(root, "tools", "cayenne-flightsql", "src"));
        Directory.CreateDirectory(Path.Combine(root, ".github", "actions", "ship"));

        File.WriteAllText(
            Path.Combine(root, "Cargo.toml"),
            "[workspace]\ndefault-members = [\"bin/spiced\"]\nmembers = [\"bin/spiced\", \"tools/cayenne-flightsql\"]\n");
        File.WriteAllText(Path.Combine(root, "bin", "spiced", "Cargo.toml"), "[package]\nname = \"spiced\"\nversion = \"0.1.0\"\n");
        File.WriteAllText(Path.Combine(root, "bin", "spiced", "src", "main.rs"), "fn main() {}");
        File.WriteAllText(Path.Combine(root, "tools", "cayenne-flightsql", "Cargo.toml"), "[package]\nname = \"cayenne-flightsql\"\nversion = \"0.1.0\"\n");
        File.WriteAllText(Path.Combine(root, "tools", "cayenne-flightsql", "src", "main.rs"), "fn main() {}");
        File.WriteAllText(Path.Combine(root, ".github", "actions", "ship", "main.js"), "console.log('action');");

        var scan = WorkspaceScanner.Scan(new WorkspaceScanRequest(root));
        var pack = WorkspaceEvidencePackBuilder.Build(scan, Array.Empty<WorkspaceTechnicalPreviewInput>(), Array.Empty<WorkspaceMaterialPreviewInput>());

        var spicedPath = Path.Combine("bin", "spiced", "src", "main.rs");
        var toolPath = Path.Combine("tools", "cayenne-flightsql", "src", "main.rs");

        AssertTrue(scan.State.Summary.EntryCandidates.Contains(spicedPath), "Source-bearing bin workspace member should not be lost as generated noise.");
        AssertFalse(scan.State.Summary.IgnoredNoiseRoots.Contains("bin"), "Manifest-backed top-level bin workspace member should not be reported as ignored noise.");
        AssertTrue(pack.Candidates.EntryPoints.Count > 0, "Evidence pack should preserve entry candidates.");
        AssertEqual(spicedPath, pack.Candidates.EntryPoints[0].RelativePath, "Cargo default-member entry should outrank tool/helper mains.");
        AssertTrue(pack.Candidates.EntryPoints[0].Evidence.Contains("cargo_default_member"), "Entry point evidence should expose Cargo default-member support.");
        AssertTrue(
            pack.Candidates.EntryPoints[0].Score > pack.Candidates.EntryPoints.First(entry => string.Equals(entry.RelativePath, toolPath, StringComparison.OrdinalIgnoreCase)).Score,
            "Default-member score should beat non-default tool entry score deterministically.");

        var basePacket = WorkspaceImportMaterialPreviewPacketBuilder.Build(scan, maxMaterials: 4, maxCharsPerMaterial: 128);
        var packet = basePacket with { EvidencePack = pack };
        var prompt = WorkspaceImportMaterialPromptRequestBuilder.Build(packet).UserPrompt;
        AssertContains(prompt, $"{spicedPath} (entry, score=", "Importer prompt should expose ranked entrypoint paths.");
        AssertContains(prompt, "marker=entrypoint_candidate/Confirmed/partial=False/bounded=False", "Importer prompt should expose entrypoint evidence marker discipline.");
        AssertContains(prompt, "evidence=cargo_default_member", "Importer prompt should expose entrypoint default-member evidence.");
        AssertContains(prompt, "conventional_entry_location", "Importer prompt should expose conventional entrypoint evidence.");
        AssertContains(prompt, $"{toolPath} (entry, score=", "Importer prompt should preserve secondary tool entrypoint candidates.");
        AssertContains(prompt, "secondary_or_workflow_location", "Importer prompt should expose secondary/support entrypoint evidence.");
    }
    finally
    {
        DeleteScratchWorkspace(root);
    }
}

static void WorkspaceImportMaterialInterpretationPreservesScannerTopEntryHonestly()
{
    var root = CreateScratchWorkspace();
    try
    {
        Directory.CreateDirectory(Path.Combine(root, "bin", "spiced", "src"));
        Directory.CreateDirectory(Path.Combine(root, "bin", "spice", "src"));

        File.WriteAllText(
            Path.Combine(root, "Cargo.toml"),
            "[workspace]\ndefault-members = [\"bin/spiced\"]\nmembers = [\"bin/spiced\", \"bin/spice\"]\n");
        File.WriteAllText(Path.Combine(root, "bin", "spiced", "Cargo.toml"), "[package]\nname = \"spiced\"\nversion = \"0.1.0\"\n");
        File.WriteAllText(Path.Combine(root, "bin", "spiced", "src", "main.rs"), "fn main() {}");
        File.WriteAllText(Path.Combine(root, "bin", "spice", "Cargo.toml"), "[package]\nname = \"spice\"\nversion = \"0.1.0\"\n");
        File.WriteAllText(Path.Combine(root, "bin", "spice", "src", "main.rs"), "fn main() {}");

        var scan = WorkspaceScanner.Scan(new WorkspaceScanRequest(root));
        var pack = WorkspaceEvidencePackBuilder.Build(scan, Array.Empty<WorkspaceTechnicalPreviewInput>(), Array.Empty<WorkspaceMaterialPreviewInput>());
        var packet = new WorkspaceImportMaterialPreviewPacket(
            scan.State.WorkspaceRoot,
            scan.State.ImportKind,
            scan.State.Summary.SourceRoots,
            Array.Empty<WorkspaceTechnicalPreviewInput>(),
            Array.Empty<WorkspaceMaterialPreviewInput>(),
            pack);
        var response = new WorkspaceImportMaterialPromptResponse(
            "SUMMARY unsupported",
            Array.Empty<string>(),
            Array.Empty<string>(),
            Array.Empty<string>(),
            Array.Empty<string>(),
            Array.Empty<string>(),
            Array.Empty<string>(),
            Array.Empty<WorkspaceImportMaterialLayerInterpretation>(),
            Array.Empty<WorkspaceImportMaterialModuleInterpretation>(),
            new[] { new WorkspaceImportMaterialEntryPointInterpretation(Path.Combine("bin", "spice", "src", "main.rs"), "entry", "Model-selected secondary entry.", WorkspaceEvidenceConfidenceLevel.Likely) },
            new ArchitectureDiagramSpec("Project Architecture", Array.Empty<ArchitectureDiagramNode>(), Array.Empty<ArchitectureDiagramEdge>(), Array.Empty<ArchitectureDiagramGroup>(), Array.Empty<string>(), new ArchitectureDiagramRenderHints("left-to-right", Array.Empty<string>(), true)),
            Array.Empty<WorkspaceImportMaterialPromptResponseItem>());

        var interpretation = WorkspaceImportMaterialInterpretationResultBuilder.BuildFromResponse(packet, response);
        var spicedPath = Path.Combine("bin", "spiced", "src", "main.rs");
        var spicePath = Path.Combine("bin", "spice", "src", "main.rs");

        AssertEqual(spicedPath, interpretation.EntryPoints[0].RelativePath, "Importer must not prefer a Likely secondary entry over the scanner Confirmed top entry.");
        AssertEqual(WorkspaceEvidenceConfidenceLevel.Confirmed, interpretation.EntryPoints[0].Confidence, "Scanner Confirmed entry confidence must survive importer normalization.");
        AssertTrue(interpretation.SummaryLine.Contains("scannerEntryCandidatesTotal=", StringComparison.Ordinal), "Summary must expose scanner candidate total separately.");
        AssertTrue(interpretation.SummaryLine.Contains("displayedEntryCandidates=", StringComparison.Ordinal), "Summary must expose displayed entry subset separately.");
        AssertTrue(interpretation.SummaryLine.Contains($"selectedMainEntry={spicedPath}", StringComparison.Ordinal), "Summary must name the selected main entry explicitly.");
        AssertFalse(interpretation.SummaryLine.Contains("entry candidates: 1", StringComparison.OrdinalIgnoreCase), "Summary must not describe selected subset as total entry candidates.");

        var run = new WorkspaceImportMaterialInterpreterRunResult(
            packet,
            WorkspaceImportMaterialPromptRequestBuilder.Build(packet),
            new OpenRouterExecutionRequest("workspace.import.interpreter", "system", "user"),
            new OpenRouterExecutionResponse(true, "SUMMARY: test", "openrouter/test", 200, null, "ok"),
            interpretation,
            null,
            "runtime summary");
        var artifacts = new ProjectDocumentRuntimeService().WritePreviewDocs(run, root);
        var previewProjectText = File.ReadAllText(artifacts.PreviewProjectPath);

        AssertContains(previewProjectText, $"Main Entry: `{spicedPath}` [Confirmed]", "Preview project must show scanner top entry as main entry.");
        AssertFalse(previewProjectText.Contains($"Main Entry: `{spicePath}` [Likely]", StringComparison.OrdinalIgnoreCase), "Preview project must not promote the Likely secondary entry to main entry.");
        AssertFalse(File.Exists(Path.Combine(root, ".zavod", "project", "project.md")), "Preview writer must not promote canonical project truth during import.");
    }
    finally
    {
        DeleteScratchWorkspace(root);
    }
}

static void WorkspaceImportMaterialInterpretationPreservesScannerModuleConfidenceHonestly()
{
    var root = CreateScratchWorkspace();
    try
    {
        Directory.CreateDirectory(Path.Combine(root, "crates", "cayenne", "src"));
        File.WriteAllText(Path.Combine(root, "Cargo.toml"), "[workspace]\nmembers = [\"crates/cayenne\"]\n");
        File.WriteAllText(Path.Combine(root, "crates", "cayenne", "Cargo.toml"), "[package]\nname = \"cayenne\"\nversion = \"0.1.0\"\n");
        File.WriteAllText(Path.Combine(root, "crates", "cayenne", "src", "lib.rs"), "pub fn table() {}");
        File.WriteAllText(Path.Combine(root, "README.md"), "Workspace overview.");

        var scan = WorkspaceScanner.Scan(new WorkspaceScanRequest(root));
        var basePack = WorkspaceEvidencePackBuilder.Build(scan, Array.Empty<WorkspaceTechnicalPreviewInput>(), Array.Empty<WorkspaceMaterialPreviewInput>());
        var scannerModule = new WorkspaceEvidenceModule(
            "Cayenne",
            "subsystem-cluster",
            "crates",
            "Observed Cayenne module from scanner candidate map.",
            new WorkspaceEvidenceMarker(
                "module_candidate",
                Path.Combine("crates", "cayenne"),
                "Observed Cayenne module from scanner candidate map.",
                WorkspaceEvidenceConfidenceLevel.Likely,
                IsPartial: false,
                IsBounded: false));
        var pack = basePack with
        {
            Candidates = basePack.Candidates with
            {
                ModuleCandidates = new[] { scannerModule }
            }
        };
        var basePacket = WorkspaceImportMaterialPreviewPacketBuilder.Build(scan, maxMaterials: 4, maxCharsPerMaterial: 128);
        var packet = basePacket with { EvidencePack = pack };
        var response = new WorkspaceImportMaterialPromptResponse(
            "SUMMARY unsupported",
            Array.Empty<string>(),
            Array.Empty<string>(),
            Array.Empty<string>(),
            Array.Empty<string>(),
            Array.Empty<string>(),
            Array.Empty<string>(),
            Array.Empty<WorkspaceImportMaterialLayerInterpretation>(),
            new[] { new WorkspaceImportMaterialModuleInterpretation("Cayenne", "subsystem-cluster", "Model saw Cayenne.", WorkspaceEvidenceConfidenceLevel.Unknown) },
            Array.Empty<WorkspaceImportMaterialEntryPointInterpretation>(),
            new ArchitectureDiagramSpec("Project Architecture", Array.Empty<ArchitectureDiagramNode>(), Array.Empty<ArchitectureDiagramEdge>(), Array.Empty<ArchitectureDiagramGroup>(), Array.Empty<string>(), new ArchitectureDiagramRenderHints("left-to-right", Array.Empty<string>(), true)),
            Array.Empty<WorkspaceImportMaterialPromptResponseItem>());

        var interpretation = WorkspaceImportMaterialInterpretationResultBuilder.BuildFromResponse(packet, response);
        var module = interpretation.Modules.Single(item => string.Equals(item.Name, "Cayenne", StringComparison.OrdinalIgnoreCase));

        AssertEqual(scannerModule.EvidenceMarker!.Confidence, module.Confidence, "Importer module confidence must come from scanner module evidence.");

        var run = new WorkspaceImportMaterialInterpreterRunResult(
            packet,
            WorkspaceImportMaterialPromptRequestBuilder.Build(packet),
            new OpenRouterExecutionRequest("workspace.import.interpreter", "system", "user"),
            new OpenRouterExecutionResponse(true, "SUMMARY: test", "openrouter/test", 200, null, "ok"),
            interpretation,
            null,
            "runtime summary");
        var direction = DirectionSignalInterpreter.Interpret(run);
        var cayenneDirection = direction.Candidates.Single(candidate => candidate.Text.Contains("Cayenne", StringComparison.OrdinalIgnoreCase));

        AssertEqual(module.Confidence, cayenneDirection.Confidence, "Direction projection must not upgrade or downgrade module confidence.");
        AssertContains(cayenneDirection.Evidence, $"module `Cayenne` [{module.Confidence}]", "Direction evidence text must show the same module confidence.");
    }
    finally
    {
        DeleteScratchWorkspace(root);
    }
}

static void WorkspaceImportPreviewLabelsPackageSurfaceWithoutMainEntryClaimHonestly()
{
    var root = CreateScratchWorkspace();
    try
    {
        Directory.CreateDirectory(Path.Combine(root, "packages", "starlight"));
        File.WriteAllText(Path.Combine(root, "package.json"), "{ \"name\": \"root\" }");
        File.WriteAllText(Path.Combine(root, "packages", "starlight", "package.json"), """
            {
              "name": "@demo/starlight",
              "exports": {
                ".": "./index.ts"
              }
            }
            """);
        File.WriteAllText(Path.Combine(root, "packages", "starlight", "index.ts"), "export const starlight = true;");

        var scan = WorkspaceScanner.Scan(new WorkspaceScanRequest(root));
        var basePacket = WorkspaceImportMaterialPreviewPacketBuilder.Build(scan, maxMaterials: 4, maxCharsPerMaterial: 128);
        var pack = WorkspaceEvidencePackBuilder.Build(scan, basePacket.TechnicalEvidence, basePacket.Materials);
        var packet = basePacket with { EvidencePack = pack };
        var interpretation = WorkspaceImportMaterialInterpretationResultBuilder.BuildFromResponse(
            packet,
            new WorkspaceImportMaterialPromptResponse(
                "SUMMARY unsupported",
                Array.Empty<string>(),
                Array.Empty<string>(),
                Array.Empty<string>(),
                Array.Empty<string>(),
                Array.Empty<string>(),
                Array.Empty<string>(),
                Array.Empty<WorkspaceImportMaterialLayerInterpretation>(),
                Array.Empty<WorkspaceImportMaterialModuleInterpretation>(),
                Array.Empty<WorkspaceImportMaterialEntryPointInterpretation>(),
                new ArchitectureDiagramSpec("Project Architecture", Array.Empty<ArchitectureDiagramNode>(), Array.Empty<ArchitectureDiagramEdge>(), Array.Empty<ArchitectureDiagramGroup>(), Array.Empty<string>(), new ArchitectureDiagramRenderHints("left-to-right", Array.Empty<string>(), true)),
                Array.Empty<WorkspaceImportMaterialPromptResponseItem>()));
        var run = new WorkspaceImportMaterialInterpreterRunResult(
            packet,
            WorkspaceImportMaterialPromptRequestBuilder.Build(packet),
            new OpenRouterExecutionRequest("workspace.import.interpreter", "system", "user"),
            new OpenRouterExecutionResponse(true, "SUMMARY: test", "openrouter/test", 200, null, "ok"),
            interpretation,
            null,
            "runtime summary");
        var artifacts = new ProjectDocumentRuntimeService().WritePreviewDocs(run, root);
        var previewProjectText = File.ReadAllText(artifacts.PreviewProjectPath);

        AssertContains(previewProjectText, "Confirmed main entry: Unknown", "Package exports without executable evidence should keep confirmed main unknown.");
        AssertContains(previewProjectText, $"Selected package surface: `{Path.Combine("packages", "starlight", "index.ts")}` [Likely]", "Package surface should be labelled as package surface, not main entry.");
        AssertFalse(previewProjectText.Contains($"Main Entry: `{Path.Combine("packages", "starlight", "index.ts")}`", StringComparison.OrdinalIgnoreCase), "Likely package surface must not be displayed as Main Entry.");
    }
    finally
    {
        DeleteScratchWorkspace(root);
    }
}

static void WorkspaceImportPreviewSuppressesUnsupportedExtraEntriesBesideConfirmedMainHonestly()
{
    var root = CreateScratchWorkspace();
    try
    {
        Directory.CreateDirectory(Path.Combine(root, "src", "sqlfluff", "cli"));
        File.WriteAllText(Path.Combine(root, "pyproject.toml"), """
            [project]
            name = "sqlfluff"

            [project.scripts]
            sqlfluff = "sqlfluff.cli.commands:cli"
            """);
        File.WriteAllText(Path.Combine(root, "src", "sqlfluff", "__init__.py"), "");
        File.WriteAllText(Path.Combine(root, "src", "sqlfluff", "cli", "__init__.py"), "");
        File.WriteAllText(Path.Combine(root, "src", "sqlfluff", "cli", "commands.py"), "def cli():\n    pass\n");

        var scan = WorkspaceScanner.Scan(new WorkspaceScanRequest(root));
        var basePacket = WorkspaceImportMaterialPreviewPacketBuilder.Build(scan, maxMaterials: 4, maxCharsPerMaterial: 128);
        var pack = WorkspaceEvidencePackBuilder.Build(scan, basePacket.TechnicalEvidence, basePacket.Materials);
        var packet = basePacket with { EvidencePack = pack };
        var response = new WorkspaceImportMaterialPromptResponse(
            "SUMMARY unsupported",
            Array.Empty<string>(),
            Array.Empty<string>(),
            Array.Empty<string>(),
            Array.Empty<string>(),
            Array.Empty<string>(),
            Array.Empty<string>(),
            Array.Empty<WorkspaceImportMaterialLayerInterpretation>(),
            Array.Empty<WorkspaceImportMaterialModuleInterpretation>(),
            new[] { new WorkspaceImportMaterialEntryPointInterpretation("sqlfluffcli.py", "cli", "Model guessed a helper CLI.", WorkspaceEvidenceConfidenceLevel.Unknown) },
            new ArchitectureDiagramSpec("Project Architecture", Array.Empty<ArchitectureDiagramNode>(), Array.Empty<ArchitectureDiagramEdge>(), Array.Empty<ArchitectureDiagramGroup>(), Array.Empty<string>(), new ArchitectureDiagramRenderHints("left-to-right", Array.Empty<string>(), true)),
            Array.Empty<WorkspaceImportMaterialPromptResponseItem>());
        var interpretation = WorkspaceImportMaterialInterpretationResultBuilder.BuildFromResponse(packet, response);
        var confirmedPath = Path.Combine("src", "sqlfluff", "cli", "commands.py");

        AssertEqual(1, interpretation.EntryPoints.Count, "Unsupported importer extra entry should not compete beside scanner confirmed main.");
        AssertEqual(confirmedPath, interpretation.EntryPoints[0].RelativePath, "Scanner confirmed console script should remain selected main entry.");
        AssertEqual(WorkspaceEvidenceConfidenceLevel.Confirmed, interpretation.EntryPoints[0].Confidence, "Confirmed console entry confidence should be preserved.");
        AssertFalse(interpretation.EntryPoints.Any(entry => string.Equals(entry.RelativePath, "sqlfluffcli.py", StringComparison.OrdinalIgnoreCase)), "Importer guessed unknown entry should be suppressed from entrypoint projections.");

        var run = new WorkspaceImportMaterialInterpreterRunResult(
            packet,
            WorkspaceImportMaterialPromptRequestBuilder.Build(packet),
            new OpenRouterExecutionRequest("workspace.import.interpreter", "system", "user"),
            new OpenRouterExecutionResponse(true, "SUMMARY: test", "openrouter/test", 200, null, "ok"),
            interpretation,
            null,
            "runtime summary");
        var artifacts = new ProjectDocumentRuntimeService().WritePreviewDocs(run, root);
        var previewProjectText = File.ReadAllText(artifacts.PreviewProjectPath);

        AssertContains(previewProjectText, $"Main Entry: `{confirmedPath}` [Confirmed]", "Preview should keep confirmed console entry as Main Entry.");
        AssertFalse(previewProjectText.Contains("sqlfluffcli.py", StringComparison.OrdinalIgnoreCase), "Unsupported unknown extra entry should not appear as a competing preview entry.");
    }
    finally
    {
        DeleteScratchWorkspace(root);
    }
}

static void WorkspaceEvidencePackExtractsDependencySurfaceHonestly()
{
    var root = CreateScratchWorkspace();
    try
    {
        File.WriteAllText(Path.Combine(root, "package.json"), "{\n  \"name\": \"demo\",\n  \"license\": \"MIT\",\n  \"dependencies\": {\n    \"react\": \"^18.0.0\"\n  },\n  \"devDependencies\": {\n    \"vite\": \"^5.0.0\"\n  }\n}");
        File.WriteAllText(Path.Combine(root, "Cargo.toml"), "[package]\nname = \"demo\"\nversion = \"0.1.0\"\n[dependencies]\nserde = \"1.0\"\n");
        File.WriteAllText(Path.Combine(root, "go.mod"), "module example.com/app\nrequire github.com/hashicorp/hcl/v2 v2.0.0\n");

        var scan = WorkspaceScanner.Scan(new WorkspaceScanRequest(root));
        var pack = WorkspaceEvidencePackBuilder.Build(scan, Array.Empty<WorkspaceTechnicalPreviewInput>(), Array.Empty<WorkspaceMaterialPreviewInput>());

        AssertTrue(pack.DependencySurface.Any(item => string.Equals(item.Name, "react", StringComparison.OrdinalIgnoreCase)), "Scanner should extract package.json dependency surface honestly.");
        AssertTrue(pack.DependencySurface.Any(item => string.Equals(item.Name, "serde", StringComparison.OrdinalIgnoreCase)), "Scanner should extract Cargo dependency surface honestly.");
        AssertTrue(pack.DependencySurface.Any(item => string.Equals(item.Name, "github.com/hashicorp/hcl/v2", StringComparison.OrdinalIgnoreCase)), "Scanner should extract go.mod dependency surface honestly.");
        AssertFalse(pack.DependencySurface.Any(item => string.Equals(item.Name, "license", StringComparison.OrdinalIgnoreCase)), "Scanner should not treat package.json metadata keys as dependencies.");
        AssertFalse(pack.DependencySurface.Any(item => string.Equals(item.Name, "name", StringComparison.OrdinalIgnoreCase)), "Scanner should not treat manifest metadata as dependencies.");
        AssertFalse(pack.DependencySurface.Any(item => item.Name.Contains("node_modules", StringComparison.OrdinalIgnoreCase)), "Scanner should not invent resolved dependency graph nodes.");
    }
    finally
    {
        DeleteScratchWorkspace(root);
    }
}

static void WorkspaceEvidencePackKeepsBinaryHintsBoundedHonestly()
{
    var root = CreateScratchWorkspace();
    try
    {
        Directory.CreateDirectory(Path.Combine(root, "bin"));
        File.WriteAllText(Path.Combine(root, "rom_code1.asm"), "; source file that must stay source-only");
        File.WriteAllBytes(
            Path.Combine(root, "bin", "sample.dll"),
            Encoding.ASCII.GetBytes("MZ....CreateFileA....ReadProcessMemory....VirtualAlloc...."));

        var scan = WorkspaceScanner.Scan(new WorkspaceScanRequest(root));
        var pack = WorkspaceEvidencePackBuilder.Build(scan, Array.Empty<WorkspaceTechnicalPreviewInput>(), Array.Empty<WorkspaceMaterialPreviewInput>());

        AssertTrue(pack.SymbolHints.Any(hint => hint.RelativePath.EndsWith("bin\\sample.dll", StringComparison.OrdinalIgnoreCase)), "Scanner should expose bounded binary symbol hints for weird binary-heavy repos.");
        AssertFalse(pack.SymbolHints.Any(hint => hint.RelativePath.EndsWith("rom_code1.asm", StringComparison.OrdinalIgnoreCase)), "Binary awareness must not misclassify source files by substring-matching their extensions.");
        AssertTrue(pack.EvidenceSnippets.Any(snippet => snippet.Category == "binary_symbol_hint"), "Binary hints should surface through bounded snippets.");
        AssertFalse(pack.ObservedLayers.Any(static layer => string.Equals(layer.Name, "Service", StringComparison.OrdinalIgnoreCase)), "Binary hints alone should not inflate semantic layers.");
    }
    finally
    {
        DeleteScratchWorkspace(root);
    }
}

static void WorkspaceEvidencePackKeepsTechnicalPassportOptionsBoundedHonestly()
{
    var root = CreateScratchWorkspace();
    try
    {
        File.WriteAllText(Path.Combine(root, "CMakeLists.txt"), "cmake_minimum_required(VERSION 3.15)\nproject(demo LANGUAGES CXX)\n");

        var scan = WorkspaceScanner.Scan(new WorkspaceScanRequest(root));
        var pack = WorkspaceEvidencePackBuilder.Build(
            scan,
            new[]
            {
                new WorkspaceTechnicalPreviewInput(
                    Path.Combine("src", "CMakeLists.txt"),
                    "cmake",
                    "cmake_minimum_required(VERSION 3.15) option(X64DBG_BUILD_IN_TREE \"\" ON) set(CMDLINE_INIT_RUNTIME_DIR \"${X64DBG_TEST_OUTPUT_DIR}/cmdline_init\") function(x64dbg_set_output_dir target dir) set_target_properties(${target} PROPERTIES RUNTIME_OUTPUT_DIRECTORY ${dir})",
                    WasTruncated: false)
            },
            Array.Empty<WorkspaceMaterialPreviewInput>());

        AssertTrue(pack.TechnicalPassport.NotableOptions.All(static option => option.Length <= 96), "Technical passport should keep notable options bounded instead of dumping long generated snippets.");
        AssertFalse(pack.TechnicalPassport.NotableOptions.Any(static option => option.Contains("function(", StringComparison.OrdinalIgnoreCase)), "Technical passport should not keep long generated function/set snippets as notable options.");
    }
    finally
    {
        DeleteScratchWorkspace(root);
    }
}

static void WorkspaceImportMaterialInterpretationBuildsFallbackConfidenceSlicesHonestly()
{
    var root = CreateScratchWorkspace();
    try
    {
        Directory.CreateDirectory(Path.Combine(root, "src"));
        File.WriteAllText(Path.Combine(root, "package.json"), "{\n  \"dependencies\": {\n    \"react\": \"^18.0.0\"\n  }\n}");
        File.WriteAllText(Path.Combine(root, "src", "main.tsx"), "import React from 'react';\nexport function main() { return null; }");

        var scan = WorkspaceScanner.Scan(new WorkspaceScanRequest(root));
        var packet = new WorkspaceMaterialRuntimeFront().BuildPreviewPacket(scan, maxMaterials: 4, maxCharsPerMaterial: 128);
        var response = new WorkspaceImportMaterialPromptResponse(
            string.Empty,
            Array.Empty<string>(),
            Array.Empty<string>(),
            Array.Empty<string>(),
            Array.Empty<string>(),
            Array.Empty<string>(),
            Array.Empty<string>(),
            Array.Empty<WorkspaceImportMaterialLayerInterpretation>(),
            Array.Empty<WorkspaceImportMaterialModuleInterpretation>(),
            Array.Empty<WorkspaceImportMaterialEntryPointInterpretation>(),
            new ArchitectureDiagramSpec(string.Empty, Array.Empty<ArchitectureDiagramNode>(), Array.Empty<ArchitectureDiagramEdge>(), Array.Empty<ArchitectureDiagramGroup>(), Array.Empty<string>(), new ArchitectureDiagramRenderHints("left-to-right", Array.Empty<string>(), true)),
            Array.Empty<WorkspaceImportMaterialPromptResponseItem>());

        var result = WorkspaceImportMaterialInterpretationResultBuilder.BuildFromResponse(packet, response);

        AssertTrue(result.ConfirmedSignals.Count + result.LikelySignals.Count + result.UnknownSignals.Count > 0, "Importer adapter should synthesize fallback confidence slices from cold evidence.");
    }
    finally
    {
        DeleteScratchWorkspace(root);
    }
}

static void WorkspaceImportMaterialInterpretationSuppressesGenericModulesFromWeakColdEvidenceHonestly()
{
    var root = CreateScratchWorkspace();
    try
    {
        Directory.CreateDirectory(Path.Combine(root, "src"));
        File.WriteAllText(Path.Combine(root, "src", "main.go"), "package main\nfunc main() {}");

        var scan = WorkspaceScanner.Scan(new WorkspaceScanRequest(root));
        var packet = new WorkspaceMaterialRuntimeFront().BuildPreviewPacket(scan, maxMaterials: 4, maxCharsPerMaterial: 128);
        var response = new WorkspaceImportMaterialPromptResponse(
            "Workspace with generic folders and weak evidence.",
            Array.Empty<string>(),
            Array.Empty<string>(),
            Array.Empty<string>(),
            Array.Empty<string>(),
            Array.Empty<string>(),
            Array.Empty<string>(),
            new[]
            {
                new WorkspaceImportMaterialLayerInterpretation("root", "primary workspace", "weak root layer"),
                new WorkspaceImportMaterialLayerInterpretation("UI", "invented ui", "folder looked like ui")
            },
            new[]
            {
                new WorkspaceImportMaterialModuleInterpretation("Api", "module", "folder looked like api"),
                new WorkspaceImportMaterialModuleInterpretation("Tui", "module", "folder looked like tui")
            },
            new[]
            {
                new WorkspaceImportMaterialEntryPointInterpretation(Path.Combine("src", "main.go"), "entry", "Observed entry")
            },
            new ArchitectureDiagramSpec("Weak", Array.Empty<ArchitectureDiagramNode>(), Array.Empty<ArchitectureDiagramEdge>(), Array.Empty<ArchitectureDiagramGroup>(), Array.Empty<string>(), new ArchitectureDiagramRenderHints("left-to-right", Array.Empty<string>(), true)),
            Array.Empty<WorkspaceImportMaterialPromptResponseItem>());

        var result = WorkspaceImportMaterialInterpretationResultBuilder.BuildFromResponse(packet, response);

        AssertFalse(result.Modules.Any(module => string.Equals(module.Name, "Api", StringComparison.OrdinalIgnoreCase)), "Generic Api module should be suppressed when cold evidence is weak.");
        AssertFalse(result.Modules.Any(module => string.Equals(module.Name, "Tui", StringComparison.OrdinalIgnoreCase)), "Generic Tui module should be suppressed when cold evidence is weak.");
        AssertFalse(result.Layers.Any(layer => string.Equals(layer.Name, "UI", StringComparison.OrdinalIgnoreCase)), "Weak cold evidence should not preserve invented UI layer.");
    }
    finally
    {
        DeleteScratchWorkspace(root);
    }
}

static void WorkspaceImportMaterialInterpretationDegradesUnsupportedBroadSummaryHonestly()
{
    var root = CreateScratchWorkspace();
    try
    {
        Directory.CreateDirectory(Path.Combine(root, "src"));
        File.WriteAllText(Path.Combine(root, "src", "main.go"), "package main\nfunc main() {}");

        var scan = WorkspaceScanner.Scan(new WorkspaceScanRequest(root));
        var packet = new WorkspaceMaterialRuntimeFront().BuildPreviewPacket(scan, maxMaterials: 4, maxCharsPerMaterial: 128);
        var response = new WorkspaceImportMaterialPromptResponse(
            "This project is a layered API, UI, and cloud platform for cross-platform services.",
            Array.Empty<string>(),
            Array.Empty<string>(),
            Array.Empty<string>(),
            Array.Empty<string>(),
            Array.Empty<string>(),
            Array.Empty<string>(),
            Array.Empty<WorkspaceImportMaterialLayerInterpretation>(),
            Array.Empty<WorkspaceImportMaterialModuleInterpretation>(),
            new[]
            {
                new WorkspaceImportMaterialEntryPointInterpretation(Path.Combine("src", "main.go"), "entry", "Observed entry")
            },
            new ArchitectureDiagramSpec("Weak", Array.Empty<ArchitectureDiagramNode>(), Array.Empty<ArchitectureDiagramEdge>(), Array.Empty<ArchitectureDiagramGroup>(), Array.Empty<string>(), new ArchitectureDiagramRenderHints("left-to-right", Array.Empty<string>(), true)),
            Array.Empty<WorkspaceImportMaterialPromptResponseItem>());

        var result = WorkspaceImportMaterialInterpretationResultBuilder.BuildFromResponse(packet, response);

        AssertFalse(result.SummaryLine.Contains("layered API", StringComparison.OrdinalIgnoreCase), "Weak cold evidence should not preserve broad API/platform summary claims.");
        AssertFalse(result.SummaryLine.Contains("UI", StringComparison.OrdinalIgnoreCase), "Weak cold evidence should not preserve UI claims in summary.");
        AssertContains(result.SummaryLine, "truth=context_only.", "Normalized summary should still preserve explicit context-only truth label.");
    }
    finally
    {
        DeleteScratchWorkspace(root);
    }
}

static void WorkspaceImportMaterialInterpretationFiltersUnsupportedNarrativeDetailsHonestly()
{
    var root = CreateScratchWorkspace();
    try
    {
        Directory.CreateDirectory(Path.Combine(root, "src"));
        File.WriteAllText(Path.Combine(root, "src", "main.go"), "package main\nfunc main() {}");

        var scan = WorkspaceScanner.Scan(new WorkspaceScanRequest(root));
        var packet = new WorkspaceMaterialRuntimeFront().BuildPreviewPacket(scan, maxMaterials: 4, maxCharsPerMaterial: 128);
        var response = new WorkspaceImportMaterialPromptResponse(
            "Workspace.",
            new[]
            {
                "The project contains a rich UI and TUI platform.",
                "It also exposes an API and service surface.",
                "A single main.go entry candidate is visible."
            },
            Array.Empty<string>(),
            Array.Empty<string>(),
            Array.Empty<string>(),
            Array.Empty<string>(),
            Array.Empty<string>(),
            Array.Empty<WorkspaceImportMaterialLayerInterpretation>(),
            Array.Empty<WorkspaceImportMaterialModuleInterpretation>(),
            new[]
            {
                new WorkspaceImportMaterialEntryPointInterpretation(Path.Combine("src", "main.go"), "entry", "Observed entry")
            },
            new ArchitectureDiagramSpec("Weak", Array.Empty<ArchitectureDiagramNode>(), Array.Empty<ArchitectureDiagramEdge>(), Array.Empty<ArchitectureDiagramGroup>(), Array.Empty<string>(), new ArchitectureDiagramRenderHints("left-to-right", Array.Empty<string>(), true)),
            Array.Empty<WorkspaceImportMaterialPromptResponseItem>());

        var result = WorkspaceImportMaterialInterpretationResultBuilder.BuildFromResponse(packet, response);

        AssertFalse(result.ProjectDetails.Any(detail => detail.Contains("UI", StringComparison.OrdinalIgnoreCase)), "Weak cold evidence should filter unsupported UI detail claims.");
        AssertFalse(result.ProjectDetails.Any(detail => detail.Contains("TUI", StringComparison.OrdinalIgnoreCase)), "Weak cold evidence should filter unsupported TUI detail claims.");
        AssertFalse(result.ProjectDetails.Any(detail => detail.Contains("API", StringComparison.OrdinalIgnoreCase)), "Weak cold evidence should filter unsupported API detail claims.");
        AssertTrue(result.ProjectDetails.Any(detail => detail.Contains("main.go", StringComparison.OrdinalIgnoreCase)), "Grounded entry detail should survive narrative filtering.");
    }
    finally
    {
        DeleteScratchWorkspace(root);
    }
}

static void WorkspaceImportMaterialInterpretationDowngradesUnsupportedConfidenceClaimsHonestly()
{
    var root = CreateScratchWorkspace();
    try
    {
        Directory.CreateDirectory(Path.Combine(root, "src"));
        File.WriteAllText(Path.Combine(root, "src", "main.go"), "package main\nfunc main() {}");

        var scan = WorkspaceScanner.Scan(new WorkspaceScanRequest(root));
        var packet = new WorkspaceMaterialRuntimeFront().BuildPreviewPacket(scan, maxMaterials: 4, maxCharsPerMaterial: 128);
        var response = new WorkspaceImportMaterialPromptResponse(
            "Workspace.",
            Array.Empty<string>(),
            new[]
            {
                "Confirmed UI platform with service API surface."
            },
            new[]
            {
                "Likely web dashboard around the same service."
            },
            new[]
            {
                "Unknown primary product surface."
            },
            Array.Empty<string>(),
            Array.Empty<string>(),
            Array.Empty<string>(),
            Array.Empty<string>(),
            Array.Empty<string>(),
            Array.Empty<WorkspaceImportMaterialLayerInterpretation>(),
            Array.Empty<WorkspaceImportMaterialModuleInterpretation>(),
            new[]
            {
                new WorkspaceImportMaterialEntryPointInterpretation(Path.Combine("src", "main.go"), "entry", "Observed entry")
            },
            new ArchitectureDiagramSpec("Weak", Array.Empty<ArchitectureDiagramNode>(), Array.Empty<ArchitectureDiagramEdge>(), Array.Empty<ArchitectureDiagramGroup>(), Array.Empty<string>(), new ArchitectureDiagramRenderHints("left-to-right", Array.Empty<string>(), true)),
            Array.Empty<WorkspaceImportMaterialPromptResponseItem>());

        var result = WorkspaceImportMaterialInterpretationResultBuilder.BuildFromResponse(packet, response);

        AssertEqual(0, result.ConfirmedSignals.Count, "Unsupported broad claims should not remain in Confirmed.");
        AssertEqual(0, result.LikelySignals.Count, "Unsupported broad claims should not remain in Likely.");
        AssertTrue(result.UnknownSignals.Any(signal => signal.Contains("UI platform", StringComparison.OrdinalIgnoreCase)), "Unsupported confirmed claim should be downgraded into Unknown.");
        AssertTrue(result.UnknownSignals.Any(signal => signal.Contains("web dashboard", StringComparison.OrdinalIgnoreCase)), "Unsupported likely claim should be downgraded into Unknown.");
        AssertTrue(result.UnknownSignals.Any(signal => signal.Contains("primary product surface", StringComparison.OrdinalIgnoreCase)), "Existing Unknown lines should be preserved.");
    }
    finally
    {
        DeleteScratchWorkspace(root);
    }
}

static void WorkspaceImportMaterialInterpretationSuppressesUnsupportedCustomLayersHonestly()
{
    var root = CreateScratchWorkspace();
    try
    {
        Directory.CreateDirectory(Path.Combine(root, "cmd"));
        File.WriteAllText(Path.Combine(root, "go.mod"), "module example.com/app\n\ngo 1.24\n");
        File.WriteAllText(Path.Combine(root, "cmd", "main.go"), "package main\nfunc main() {}");

        var scan = WorkspaceScanner.Scan(new WorkspaceScanRequest(root));
        var packet = new WorkspaceMaterialRuntimeFront().BuildPreviewPacket(scan, maxMaterials: 4, maxCharsPerMaterial: 128);
        var response = new WorkspaceImportMaterialPromptResponse(
            "CLI workspace.",
            Array.Empty<string>(),
            Array.Empty<string>(),
            Array.Empty<string>(),
            Array.Empty<string>(),
            Array.Empty<string>(),
            Array.Empty<string>(),
            new[]
            {
                new WorkspaceImportMaterialLayerInterpretation("Language Server", "server", "folder-derived"),
                new WorkspaceImportMaterialLayerInterpretation("HCL Parsing", "parser", "folder-derived"),
                new WorkspaceImportMaterialLayerInterpretation("Source", "source files", "cold-supported")
            },
            Array.Empty<WorkspaceImportMaterialModuleInterpretation>(),
            new[]
            {
                new WorkspaceImportMaterialEntryPointInterpretation(Path.Combine("cmd", "main.go"), "entry", "Observed entry")
            },
            new ArchitectureDiagramSpec("Weak", Array.Empty<ArchitectureDiagramNode>(), Array.Empty<ArchitectureDiagramEdge>(), Array.Empty<ArchitectureDiagramGroup>(), Array.Empty<string>(), new ArchitectureDiagramRenderHints("left-to-right", Array.Empty<string>(), true)),
            Array.Empty<WorkspaceImportMaterialPromptResponseItem>());

        var result = WorkspaceImportMaterialInterpretationResultBuilder.BuildFromResponse(packet, response);

        AssertFalse(result.Layers.Any(layer => string.Equals(layer.Name, "Language Server", StringComparison.OrdinalIgnoreCase)), "Unsupported custom layers should be suppressed when cold evidence is weak.");
        AssertFalse(result.Layers.Any(layer => string.Equals(layer.Name, "HCL Parsing", StringComparison.OrdinalIgnoreCase)), "Folder-derived custom layers should not become human truth without stronger evidence.");
        AssertTrue(result.Layers.Any(layer => string.Equals(layer.Name, "Source", StringComparison.OrdinalIgnoreCase)), "Recognized coarse layers may remain when bounded and generic.");
    }
    finally
    {
        DeleteScratchWorkspace(root);
    }
}

static void WorkspaceImportMaterialInterpretationSanitizesUnsupportedDiagramClaimsHonestly()
{
    var root = CreateScratchWorkspace();
    try
    {
        Directory.CreateDirectory(Path.Combine(root, "src"));
        File.WriteAllText(Path.Combine(root, "src", "main.go"), "package main\nfunc main() {}");

        var scan = WorkspaceScanner.Scan(new WorkspaceScanRequest(root));
        var packet = new WorkspaceMaterialRuntimeFront().BuildPreviewPacket(scan, maxMaterials: 4, maxCharsPerMaterial: 128);
        var response = new WorkspaceImportMaterialPromptResponse(
            "Workspace.",
            Array.Empty<string>(),
            Array.Empty<string>(),
            Array.Empty<string>(),
            Array.Empty<string>(),
            Array.Empty<string>(),
            Array.Empty<string>(),
            new[]
            {
                new WorkspaceImportMaterialLayerInterpretation("root", "primary workspace", "cold root"),
            },
            Array.Empty<WorkspaceImportMaterialModuleInterpretation>(),
            new[]
            {
                new WorkspaceImportMaterialEntryPointInterpretation(Path.Combine("src", "main.go"), "entry", "Observed entry")
            },
            new ArchitectureDiagramSpec(
                "Weak",
                new[]
                {
                    new ArchitectureDiagramNode("web", "Web", "layer", "layers", WorkspaceEvidenceConfidenceLevel.Confirmed),
                    new ArchitectureDiagramNode("service", "Service", "layer", "layers", WorkspaceEvidenceConfidenceLevel.Confirmed),
                    new ArchitectureDiagramNode("entry", Path.Combine("src", "main.go"), "entry", "entries", WorkspaceEvidenceConfidenceLevel.Likely)
                },
                new[]
                {
                    new ArchitectureDiagramEdge("entry", "service", "serves", "entry", WorkspaceEvidenceConfidenceLevel.Likely),
                    new ArchitectureDiagramEdge("service", "web", "renders", "observed", WorkspaceEvidenceConfidenceLevel.Likely)
                },
                new[]
                {
                    new ArchitectureDiagramGroup("layers", "Observed Layers", new[] { "web", "service" }, WorkspaceEvidenceConfidenceLevel.Confirmed)
                },
                new[] { "Weak diagram." },
                new ArchitectureDiagramRenderHints("left-to-right", Array.Empty<string>(), true)),
            Array.Empty<WorkspaceImportMaterialPromptResponseItem>());

        var result = WorkspaceImportMaterialInterpretationResultBuilder.BuildFromResponse(packet, response);

        AssertFalse(result.DiagramSpec.Nodes.Any(node => string.Equals(node.Label, "Web", StringComparison.OrdinalIgnoreCase)), "Unsupported Web node should be removed from diagram spec.");
        AssertFalse(result.DiagramSpec.Nodes.Any(node => string.Equals(node.Label, "Service", StringComparison.OrdinalIgnoreCase)), "Unsupported Service node should be removed from diagram spec.");
        AssertFalse(result.DiagramSpec.Edges.Any(edge => string.Equals(edge.To, "service", StringComparison.OrdinalIgnoreCase) || string.Equals(edge.From, "service", StringComparison.OrdinalIgnoreCase)), "Edges referencing removed broad nodes should be dropped.");
        AssertFalse(result.DiagramSpec.Groups.Any(group => group.Members.Contains("web", StringComparer.OrdinalIgnoreCase) || group.Members.Contains("service", StringComparer.OrdinalIgnoreCase)), "Groups should not retain removed broad nodes.");
    }
    finally
    {
        DeleteScratchWorkspace(root);
    }
}

static void WorkspaceImportMaterialInterpretationKeepsDiagramTitleAndNotesCoarseOnWeakEvidenceHonestly()
{
    var root = CreateScratchWorkspace();
    try
    {
        Directory.CreateDirectory(Path.Combine(root, "src"));
        File.WriteAllText(Path.Combine(root, "src", "main.go"), "package main\nfunc main() {}");

        var scan = WorkspaceScanner.Scan(new WorkspaceScanRequest(root));
        var packet = new WorkspaceMaterialRuntimeFront().BuildPreviewPacket(scan, maxMaterials: 4, maxCharsPerMaterial: 128);
        var response = new WorkspaceImportMaterialPromptResponse(
            "Workspace.",
            Array.Empty<string>(),
            Array.Empty<string>(),
            Array.Empty<string>(),
            Array.Empty<string>(),
            Array.Empty<string>(),
            Array.Empty<string>(),
            new[]
            {
                new WorkspaceImportMaterialLayerInterpretation("root", "primary workspace", "cold root")
            },
            Array.Empty<WorkspaceImportMaterialModuleInterpretation>(),
            new[]
            {
                new WorkspaceImportMaterialEntryPointInterpretation(Path.Combine("src", "main.go"), "entry", "Observed entry")
            },
            new ArchitectureDiagramSpec(
                "Layered service platform",
                new[]
                {
                    new ArchitectureDiagramNode("root", "root", "layer", "layers", WorkspaceEvidenceConfidenceLevel.Unknown),
                    new ArchitectureDiagramNode("entry", Path.Combine("src", "main.go"), "entry", "entries", WorkspaceEvidenceConfidenceLevel.Likely)
                },
                Array.Empty<ArchitectureDiagramEdge>(),
                Array.Empty<ArchitectureDiagramGroup>(),
                new[] { "Platform service layer coordinates UI and web runtime." },
                new ArchitectureDiagramRenderHints("left-to-right", Array.Empty<string>(), true)),
            Array.Empty<WorkspaceImportMaterialPromptResponseItem>());

        var result = WorkspaceImportMaterialInterpretationResultBuilder.BuildFromResponse(packet, response);

        AssertEqual("Project Architecture", result.DiagramSpec.Title, "Weak diagram title should degrade to coarse importer-owned wording.");
        AssertFalse(result.DiagramSpec.Notes.Any(note => note.Contains("platform", StringComparison.OrdinalIgnoreCase)), "Weak diagram notes should not retain platform inflation.");
        AssertFalse(result.DiagramSpec.Notes.Any(note => note.Contains("service", StringComparison.OrdinalIgnoreCase)), "Weak diagram notes should not retain service inflation.");
        AssertFalse(result.DiagramSpec.Notes.Any(note => note.Contains("UI", StringComparison.OrdinalIgnoreCase)), "Weak diagram notes should not retain UI inflation.");
        AssertFalse(result.DiagramSpec.Notes.Any(note => note.Contains("web", StringComparison.OrdinalIgnoreCase)), "Weak diagram notes should not retain web inflation.");
    }
    finally
    {
        DeleteScratchWorkspace(root);
    }
}

static void WorkspaceImportMaterialInterpretationDegradesUnsupportedStageClaimsHonestly()
{
    var root = CreateScratchWorkspace();
    try
    {
        Directory.CreateDirectory(Path.Combine(root, "src"));
        File.WriteAllText(Path.Combine(root, "src", "main.go"), "package main\nfunc main() {}");

        var scan = WorkspaceScanner.Scan(new WorkspaceScanRequest(root));
        var packet = new WorkspaceMaterialRuntimeFront().BuildPreviewPacket(scan, maxMaterials: 4, maxCharsPerMaterial: 128);
        var basePack = packet.EvidencePack!;
        packet = packet with
        {
            EvidencePack = basePack with
            {
                Signals = basePack.Signals.Concat(new[]
                {
                    new WorkspaceEvidenceSignal("stage", "active_workspace", "Observed active source files.")
                }).ToArray()
            }
        };

        var response = new WorkspaceImportMaterialPromptResponse(
            "Workspace.",
            Array.Empty<string>(),
            new[] { "Project is in stable platform rollout stage." },
            Array.Empty<string>(),
            Array.Empty<string>(),
            Array.Empty<string>(),
            Array.Empty<string>(),
            Array.Empty<WorkspaceImportMaterialLayerInterpretation>(),
            Array.Empty<WorkspaceImportMaterialModuleInterpretation>(),
            Array.Empty<WorkspaceImportMaterialEntryPointInterpretation>(),
            new ArchitectureDiagramSpec("Weak", Array.Empty<ArchitectureDiagramNode>(), Array.Empty<ArchitectureDiagramEdge>(), Array.Empty<ArchitectureDiagramGroup>(), Array.Empty<string>(), new ArchitectureDiagramRenderHints("left-to-right", Array.Empty<string>(), true)),
            Array.Empty<WorkspaceImportMaterialPromptResponseItem>());

        var result = WorkspaceImportMaterialInterpretationResultBuilder.BuildFromResponse(packet, response);

        AssertEqual(1, result.ProjectStageSignals.Count, "Filtered stage wording should degrade to one coarse fallback line when stage evidence exists.");
        AssertContains(result.ProjectStageSignals[0], "does not confirm a specific delivery stage", "Weak stage claims should degrade to coarse wording.");
        AssertFalse(result.ProjectStageSignals.Any(signal => signal.Contains("stable platform rollout", StringComparison.OrdinalIgnoreCase)), "Unsupported stage wording should not remain verbatim.");
    }
    finally
    {
        DeleteScratchWorkspace(root);
    }
}

static void WorkspaceImportMaterialInterpretationSuppressesUnifiedNarrativeForMultipleProjectContainerHonestly()
{
    var root = CreateScratchWorkspace();
    try
    {
        Directory.CreateDirectory(Path.Combine(root, "alpha", ".git"));
        Directory.CreateDirectory(Path.Combine(root, "alpha", "src"));
        Directory.CreateDirectory(Path.Combine(root, "beta", ".git"));
        Directory.CreateDirectory(Path.Combine(root, "beta", "cmd"));
        File.WriteAllText(Path.Combine(root, "alpha", "src", "main.ts"), "export function main() {}");
        File.WriteAllText(Path.Combine(root, "alpha", "package.json"), "{ \"name\": \"alpha\" }");
        File.WriteAllText(Path.Combine(root, "beta", "cmd", "main.go"), "package main\nfunc main() {}");
        File.WriteAllText(Path.Combine(root, "beta", "go.mod"), "module beta");

        var scan = WorkspaceScanner.Scan(new WorkspaceScanRequest(root));
        var packet = new WorkspaceMaterialRuntimeFront().BuildPreviewPacket(scan, maxMaterials: 6, maxCharsPerMaterial: 96);
        var response = new WorkspaceImportMaterialPromptResponse(
            "A unified platform architecture spans both repos with shared runtime and service layers.",
            new[]
            {
                "The project exposes one layered architecture across both roots."
            },
            new[] { "Project is in coordinated rollout stage." },
            new[] { "Shared platform runtime is current." },
            Array.Empty<string>(),
            Array.Empty<string>(),
            Array.Empty<string>(),
            new[]
            {
                new WorkspaceImportMaterialLayerInterpretation("Service", "invented service", "folder-derived")
            },
            new[]
            {
                new WorkspaceImportMaterialModuleInterpretation("Platform", "module", "folder-derived")
            },
            new[]
            {
                new WorkspaceImportMaterialEntryPointInterpretation(Path.Combine("alpha", "src", "main.ts"), "main", "Alpha entry"),
                new WorkspaceImportMaterialEntryPointInterpretation(Path.Combine("beta", "cmd", "main.go"), "main", "Beta entry")
            },
            new ArchitectureDiagramSpec(
                "Unified Project Architecture",
                new[]
                {
                    new ArchitectureDiagramNode("service", "Service", "layer")
                },
                Array.Empty<ArchitectureDiagramEdge>(),
                Array.Empty<ArchitectureDiagramGroup>(),
                new[] { "One architecture spans both repos." },
                new ArchitectureDiagramRenderHints("left-to-right", Array.Empty<string>(), true)),
            Array.Empty<WorkspaceImportMaterialPromptResponseItem>());

        var result = WorkspaceImportMaterialInterpretationResultBuilder.BuildFromResponse(packet, response);

        AssertEqual(ProjectInterpretationMode.MultipleIndependentProjects, result.InterpretationMode, "Multiple nested git-backed roots should switch interpretation into container mode.");
        AssertContains(result.SummaryLine, "несколько независимых", "Container mode should replace unified summary with coarse container wording.");
        AssertTrue(result.ProjectDetails.Any(detail => detail.Contains("Единая архитектура проекта не подтверждена", StringComparison.OrdinalIgnoreCase)), "Container mode should emit explicit coarse fallback detail.");
        AssertEqual("Project Container", result.DiagramSpec.Title, "Container mode should suppress ordinary architecture diagram title.");
        AssertTrue(result.DiagramSpec.Notes.Any(note => note.Contains("suppressed", StringComparison.OrdinalIgnoreCase)), "Container mode should suppress unified diagram narrative.");
        AssertEqual(0, result.Layers.Count, "Container mode should suppress unified layers.");
        AssertEqual(0, result.Modules.Count, "Container mode should suppress unified modules.");
        AssertEqual(0, result.EntryPoints.Count, "Multiple independent project mode should suppress unified entry projection.");
        AssertEqual(1, result.ProjectStageSignals.Count, "Container mode should degrade stage output to one coarse line.");
        AssertContains(result.ProjectStageSignals[0], "single shared delivery stage", "Container mode should not keep strong shared-stage wording.");
    }
    finally
    {
        DeleteScratchWorkspace(root);
    }
}

static void WorkspaceImportMaterialInterpretationSuppressesTerragruntUiApiDriftHonestly()
{
    var root = CreateScratchWorkspace();
    try
    {
        File.WriteAllText(Path.Combine(root, "terragrunt.hcl"), "terraform { source = \"../modules/vpc\" }");
        File.WriteAllText(Path.Combine(root, "README.md"), "Terragrunt infrastructure repository for environment orchestration.");

        var scan = WorkspaceScanner.Scan(new WorkspaceScanRequest(root));
        var packet = new WorkspaceMaterialRuntimeFront().BuildPreviewPacket(scan, maxMaterials: 4, maxCharsPerMaterial: 128);
        var response = new WorkspaceImportMaterialPromptResponse(
            "This Terragrunt workspace ships a UI, TUI, API, and service platform.",
            new[]
            {
                "UI and TUI consoles manage the infrastructure.",
                "An API service exposes environment controls."
            },
            Array.Empty<string>(),
            Array.Empty<string>(),
            Array.Empty<string>(),
            Array.Empty<string>(),
            Array.Empty<string>(),
            new[]
            {
                new WorkspaceImportMaterialLayerInterpretation("UI", "invented ui", "folder-derived"),
                new WorkspaceImportMaterialLayerInterpretation("Service", "invented service", "folder-derived")
            },
            new[]
            {
                new WorkspaceImportMaterialModuleInterpretation("Api", "module", "folder-derived"),
                new WorkspaceImportMaterialModuleInterpretation("Tui", "module", "folder-derived"),
                new WorkspaceImportMaterialModuleInterpretation("Service", "module", "folder-derived")
            },
            Array.Empty<WorkspaceImportMaterialEntryPointInterpretation>(),
            new ArchitectureDiagramSpec("Weak", Array.Empty<ArchitectureDiagramNode>(), Array.Empty<ArchitectureDiagramEdge>(), Array.Empty<ArchitectureDiagramGroup>(), Array.Empty<string>(), new ArchitectureDiagramRenderHints("left-to-right", Array.Empty<string>(), true)),
            Array.Empty<WorkspaceImportMaterialPromptResponseItem>());

        var result = WorkspaceImportMaterialInterpretationResultBuilder.BuildFromResponse(packet, response);

        AssertFalse(result.SummaryLine.Contains("UI", StringComparison.OrdinalIgnoreCase), "Terragrunt summary should not preserve fake UI claims.");
        AssertFalse(result.SummaryLine.Contains("API", StringComparison.OrdinalIgnoreCase), "Terragrunt summary should not preserve fake API claims.");
        AssertFalse(result.Layers.Any(layer => string.Equals(layer.Name, "UI", StringComparison.OrdinalIgnoreCase)), "Terragrunt should not invent UI layer.");
        AssertFalse(result.Layers.Any(layer => string.Equals(layer.Name, "Service", StringComparison.OrdinalIgnoreCase)), "Terragrunt should not invent Service layer.");
        AssertFalse(result.Modules.Any(module => string.Equals(module.Name, "Api", StringComparison.OrdinalIgnoreCase)), "Terragrunt should not invent Api module.");
        AssertFalse(result.Modules.Any(module => string.Equals(module.Name, "Tui", StringComparison.OrdinalIgnoreCase)), "Terragrunt should not invent Tui module.");
        AssertFalse(result.Modules.Any(module => string.Equals(module.Name, "Service", StringComparison.OrdinalIgnoreCase)), "Terragrunt should not invent Service module.");
    }
    finally
    {
        DeleteScratchWorkspace(root);
    }
}

static void WorkspaceImportMaterialInterpretationPreservesCssDoomWebTruthHonestly()
{
    var root = CreateScratchWorkspace();
    try
    {
        Directory.CreateDirectory(Path.Combine(root, "public"));
        Directory.CreateDirectory(Path.Combine(root, "src"));
        File.WriteAllText(Path.Combine(root, "package.json"), "{ \"dependencies\": { \"react\": \"^18.0.0\" } }");
        File.WriteAllText(Path.Combine(root, "index.html"), "<!doctype html><html><body><canvas id=\"game\"></canvas></body></html>");
        File.WriteAllText(Path.Combine(root, "public", "app.js"), "document.getElementById('game'); const canvas = document.createElement('canvas');");
        File.WriteAllText(Path.Combine(root, "public", "styles.css"), "html, body, canvas { width: 100%; }");
        File.WriteAllText(Path.Combine(root, "src", "main.js"), "document.getElementById('game'); const canvas = document.createElement('canvas');");
        File.WriteAllText(Path.Combine(root, "src", "styles.css"), "html, body, canvas { width: 100%; }");
        File.WriteAllText(Path.Combine(root, "README.md"), "cssDOOM is a browser-based DOOM recreation using CSS and JavaScript.");

        var scan = WorkspaceScanner.Scan(new WorkspaceScanRequest(root));
        var packet = new WorkspaceMaterialRuntimeFront().BuildPreviewPacket(scan, maxMaterials: 5, maxCharsPerMaterial: 128);
        var response = new WorkspaceImportMaterialPromptResponse(
            "Browser-based web recreation with a native service backend.",
            new[]
            {
                "HTML canvas gameplay is directly visible from the packet.",
                "A native service daemon drives the runtime."
            },
            Array.Empty<string>(),
            Array.Empty<string>(),
            Array.Empty<string>(),
            Array.Empty<string>(),
            Array.Empty<string>(),
            Array.Empty<WorkspaceImportMaterialLayerInterpretation>(),
            new[]
            {
                new WorkspaceImportMaterialModuleInterpretation("Web", "frontend", "Observed web-facing gameplay module."),
                new WorkspaceImportMaterialModuleInterpretation("Service", "backend", "Invented native daemon.")
            },
            Array.Empty<WorkspaceImportMaterialEntryPointInterpretation>(),
            new ArchitectureDiagramSpec("Weak", Array.Empty<ArchitectureDiagramNode>(), Array.Empty<ArchitectureDiagramEdge>(), Array.Empty<ArchitectureDiagramGroup>(), Array.Empty<string>(), new ArchitectureDiagramRenderHints("left-to-right", Array.Empty<string>(), true)),
            Array.Empty<WorkspaceImportMaterialPromptResponseItem>());

        var result = WorkspaceImportMaterialInterpretationResultBuilder.BuildFromResponse(packet, response);

        AssertTrue(result.ProjectDetails.Any(detail => detail.Contains("HTML canvas gameplay", StringComparison.OrdinalIgnoreCase)), "cssDOOM should preserve supported web truth through concrete evidence-shaped wording.");
        AssertFalse(result.ProjectDetails.Any(detail => detail.Contains("service daemon", StringComparison.OrdinalIgnoreCase)), "cssDOOM should suppress unsupported native service drift.");
        AssertFalse(result.Modules.Any(module => string.Equals(module.Name, "Service", StringComparison.OrdinalIgnoreCase)), "cssDOOM should not invent a Service module from prose.");
    }
    finally
    {
        DeleteScratchWorkspace(root);
    }
}

static void WorkspaceImportMaterialInterpretationKeepsX64DbgHelperMainsSecondaryHonestly()
{
    var root = CreateScratchWorkspace();
    try
    {
        Directory.CreateDirectory(Path.Combine(root, "src", "dbg"));
        Directory.CreateDirectory(Path.Combine(root, "src", "helpers", "remote_server"));
        File.WriteAllText(Path.Combine(root, "CMakeLists.txt"), "project(x64dbg)");
        File.WriteAllText(Path.Combine(root, "README.md"), "Debugger core and plugin system.");
        File.WriteAllText(Path.Combine(root, "src", "dbg", "main.cpp"), "int main() { return 0; }");
        File.WriteAllText(Path.Combine(root, "src", "helpers", "remote_server", "main.cpp"), "int main() { return 0; }");

        var scan = WorkspaceScanner.Scan(new WorkspaceScanRequest(root));
        var packet = new WorkspaceMaterialRuntimeFront().BuildPreviewPacket(scan, maxMaterials: 4, maxCharsPerMaterial: 128);
        var response = new WorkspaceImportMaterialPromptResponse(
            "Debugger workspace with helper service runtime.",
            new[]
            {
                "Helper remote server drives the main service surface."
            },
            Array.Empty<string>(),
            Array.Empty<string>(),
            Array.Empty<string>(),
            Array.Empty<string>(),
            Array.Empty<string>(),
            new[]
            {
                new WorkspaceImportMaterialLayerInterpretation("Service", "helper service", "helper-derived")
            },
            new[]
            {
                new WorkspaceImportMaterialModuleInterpretation("Service", "backend", "helper-derived")
            },
            new[]
            {
                new WorkspaceImportMaterialEntryPointInterpretation(Path.Combine("src", "helpers", "remote_server", "main.cpp"), "main", "Helper main"),
                new WorkspaceImportMaterialEntryPointInterpretation(Path.Combine("src", "dbg", "main.cpp"), "main", "Debugger main")
            },
            new ArchitectureDiagramSpec("Weak", Array.Empty<ArchitectureDiagramNode>(), Array.Empty<ArchitectureDiagramEdge>(), Array.Empty<ArchitectureDiagramGroup>(), Array.Empty<string>(), new ArchitectureDiagramRenderHints("left-to-right", Array.Empty<string>(), true)),
            Array.Empty<WorkspaceImportMaterialPromptResponseItem>());

        var result = WorkspaceImportMaterialInterpretationResultBuilder.BuildFromResponse(packet, response);

        AssertTrue(result.EntryPoints.Count > 0, "x64dbg interpretation should keep at least one entry point.");
        AssertContains(result.EntryPoints[0].RelativePath, Path.Combine("src", "dbg", "main.cpp"), "x64dbg should keep debugger main ahead of helper mains.");
        AssertFalse(result.Layers.Any(layer => string.Equals(layer.Name, "Service", StringComparison.OrdinalIgnoreCase)), "x64dbg should not invent a Service layer from helper mains.");
        AssertFalse(result.Modules.Any(module => string.Equals(module.Name, "Service", StringComparison.OrdinalIgnoreCase)), "x64dbg should not invent a Service module from helper mains.");
    }
    finally
    {
        DeleteScratchWorkspace(root);
    }
}

static void WorkspaceImportMaterialInterpretationSuppressesCodexLayeredInflationHonestly()
{
    var root = CreateScratchWorkspace();
    try
    {
        Directory.CreateDirectory(Path.Combine(root, "app"));
        Directory.CreateDirectory(Path.Combine(root, "tools"));
        File.WriteAllText(Path.Combine(root, "package.json"), "{ \"dependencies\": { \"react\": \"^18.0.0\" } }");
        File.WriteAllText(Path.Combine(root, "README.md"), "Agent workspace with prompts, tools, and project automation.");
        File.WriteAllText(Path.Combine(root, "app", "index.ts"), "export const app = true;");
        File.WriteAllText(Path.Combine(root, "tools", "worker.ts"), "export const worker = true;");

        var scan = WorkspaceScanner.Scan(new WorkspaceScanRequest(root));
        var packet = new WorkspaceMaterialRuntimeFront().BuildPreviewPacket(scan, maxMaterials: 4, maxCharsPerMaterial: 128);
        var response = new WorkspaceImportMaterialPromptResponse(
            "Layered platform with Core, Service, UI, and Platform boundaries.",
            new[]
            {
                "A strict layered architecture is visible across UI, Core, Service, and Platform subsystems."
            },
            Array.Empty<string>(),
            Array.Empty<string>(),
            Array.Empty<string>(),
            Array.Empty<string>(),
            Array.Empty<string>(),
            new[]
            {
                new WorkspaceImportMaterialLayerInterpretation("Core", "invented core", "folder-derived"),
                new WorkspaceImportMaterialLayerInterpretation("Service", "invented service", "folder-derived"),
                new WorkspaceImportMaterialLayerInterpretation("UI", "invented ui", "folder-derived")
            },
            new[]
            {
                new WorkspaceImportMaterialModuleInterpretation("Core", "module", "folder-derived"),
                new WorkspaceImportMaterialModuleInterpretation("Platform", "module", "folder-derived")
            },
            Array.Empty<WorkspaceImportMaterialEntryPointInterpretation>(),
            new ArchitectureDiagramSpec("Weak", Array.Empty<ArchitectureDiagramNode>(), Array.Empty<ArchitectureDiagramEdge>(), Array.Empty<ArchitectureDiagramGroup>(), Array.Empty<string>(), new ArchitectureDiagramRenderHints("left-to-right", Array.Empty<string>(), true)),
            Array.Empty<WorkspaceImportMaterialPromptResponseItem>());

        var result = WorkspaceImportMaterialInterpretationResultBuilder.BuildFromResponse(packet, response);

        AssertFalse(result.SummaryLine.Contains("layered platform", StringComparison.OrdinalIgnoreCase), "codex-like workspace should not keep inflated layered summary.");
        AssertFalse(result.ProjectDetails.Any(detail => detail.Contains("strict layered architecture", StringComparison.OrdinalIgnoreCase)), "codex-like workspace should not keep layered inflation in details.");
        AssertFalse(result.Layers.Any(layer => string.Equals(layer.Name, "Service", StringComparison.OrdinalIgnoreCase)), "codex-like workspace should not invent Service layer.");
        AssertFalse(result.Modules.Any(module => string.Equals(module.Name, "Platform", StringComparison.OrdinalIgnoreCase)), "codex-like workspace should not invent Platform module.");
    }
    finally
    {
        DeleteScratchWorkspace(root);
    }
}

static void WorkspaceImportMaterialInterpretationSuppressesRadare2WebUiDriftHonestly()
{
    var root = CreateScratchWorkspace();
    try
    {
        Directory.CreateDirectory(Path.Combine(root, "libr"));
        File.WriteAllText(Path.Combine(root, "configure.ac"), "AC_INIT([radare2],[1.0])");
        File.WriteAllText(Path.Combine(root, "libr", "main.c"), "int main(void) { return 0; }");
        File.WriteAllText(Path.Combine(root, "README.md"), "Reverse engineering framework and command-line tooling.");

        var scan = WorkspaceScanner.Scan(new WorkspaceScanRequest(root));
        var packet = new WorkspaceMaterialRuntimeFront().BuildPreviewPacket(scan, maxMaterials: 4, maxCharsPerMaterial: 128);
        var response = new WorkspaceImportMaterialPromptResponse(
            "Reverse toolkit with web UI frontend.",
            new[]
            {
                "A browser UI wraps the reverse core."
            },
            Array.Empty<string>(),
            Array.Empty<string>(),
            Array.Empty<string>(),
            Array.Empty<string>(),
            Array.Empty<string>(),
            new[]
            {
                new WorkspaceImportMaterialLayerInterpretation("UI", "invented ui", "folder-derived")
            },
            new[]
            {
                new WorkspaceImportMaterialModuleInterpretation("Web", "frontend", "folder-derived"),
                new WorkspaceImportMaterialModuleInterpretation("UI", "frontend", "folder-derived")
            },
            Array.Empty<WorkspaceImportMaterialEntryPointInterpretation>(),
            new ArchitectureDiagramSpec("Weak", Array.Empty<ArchitectureDiagramNode>(), Array.Empty<ArchitectureDiagramEdge>(), Array.Empty<ArchitectureDiagramGroup>(), Array.Empty<string>(), new ArchitectureDiagramRenderHints("left-to-right", Array.Empty<string>(), true)),
            Array.Empty<WorkspaceImportMaterialPromptResponseItem>());

        var result = WorkspaceImportMaterialInterpretationResultBuilder.BuildFromResponse(packet, response);

        AssertFalse(result.SummaryLine.Contains("web UI", StringComparison.OrdinalIgnoreCase), "Radare2-like repo should not keep gratuitous web UI summary.");
        AssertFalse(result.ProjectDetails.Any(detail => detail.Contains("browser UI", StringComparison.OrdinalIgnoreCase)), "Radare2-like repo should not keep browser UI detail without support.");
        AssertFalse(result.Layers.Any(layer => string.Equals(layer.Name, "UI", StringComparison.OrdinalIgnoreCase)), "Radare2-like repo should not invent UI layer.");
        AssertFalse(result.Modules.Any(module => string.Equals(module.Name, "Web", StringComparison.OrdinalIgnoreCase)), "Radare2-like repo should not invent Web module.");
    }
    finally
    {
        DeleteScratchWorkspace(root);
    }
}

static void WorkspaceMaterialRuntimeFrontDeprioritizesNoisyBuildLogsHonestly()
{
    var root = CreateScratchWorkspace();
    try
    {
        Directory.CreateDirectory(Path.Combine(root, "docs"));
        Directory.CreateDirectory(Path.Combine(root, "SonicLegacyLauncher-build-tests"));

        File.WriteAllText(Path.Combine(root, "README.md"), "Sonic Legacy launcher platform overview.");
        File.WriteAllText(Path.Combine(root, "docs", "ARCHITECTURE.md"), "Layered architecture with UI, Core, Mod Platform, Runtime.");
        File.WriteAllText(Path.Combine(root, "docs", "CONSTITUTION.txt"), "Project rules and guardrails.");
        File.WriteAllText(Path.Combine(root, "docs", "GUIDE.md"), "How the platform is structured.");
        File.WriteAllText(Path.Combine(root, "notes.txt"), "Current implementation notes.");
        File.WriteAllText(Path.Combine(root, "spec.pdf"), "placeholder");
        File.WriteAllText(Path.Combine(root, "bundle.zip"), "placeholder");
        using (var bitmap = new System.Drawing.Bitmap(2, 2))
        {
            bitmap.Save(Path.Combine(root, "shot.png"), System.Drawing.Imaging.ImageFormat.Png);
        }

        File.WriteAllText(Path.Combine(root, "SonicLegacyLauncher-build-tests", "CMakeCache.txt"), "SL_BUILD_TESTS:BOOL=ON");
        File.WriteAllText(Path.Combine(root, "SonicLegacyLauncher-build-tests", "managedHeroesProjection.txt"), "PASS PASS PASS QWARN FAIL projection noise");
        File.WriteAllText(Path.Combine(root, "SonicLegacyLauncher-build-tests", "phase5_test_output.txt"), "PASS PASS PASS output noise");

        var scan = WorkspaceScanner.Scan(new WorkspaceScanRequest(root));
        var runner = new FakeExternalProcessRunner(request => request.Purpose switch
        {
            var purpose when purpose.StartsWith("pdf_extract:pdftotext", StringComparison.Ordinal) => new ExternalProcessResult(0, "pdf extracted context", string.Empty, false),
            "archive_list" => new ExternalProcessResult(0, "2026-04-08 12:00:00 ....A 32 src/app.cs", string.Empty, false),
            _ => new ExternalProcessResult(1, string.Empty, $"Unexpected purpose: {request.Purpose}", false)
        });
        var runtimeFront = new WorkspaceMaterialRuntimeFront(
            new TextMaterialRuntimeService(),
            new PdfExtractionRuntimeService(runner),
            new ArchiveInspectionRuntimeService(runner),
            new ImageInspectionRuntimeService());

        var packet = runtimeFront.BuildPreviewPacket(scan, maxMaterials: 8, maxCharsPerMaterial: 64);
        var selectedPaths = packet.Materials.Select(material => material.RelativePath).ToArray();

        AssertTrue(selectedPaths.Contains("README.md"), "Runtime front should keep root readme under noise pressure.");
        AssertTrue(selectedPaths.Contains(Path.Combine("docs", "ARCHITECTURE.md")), "Runtime front should keep architecture docs under noise pressure.");
        AssertFalse(selectedPaths.Contains(Path.Combine("SonicLegacyLauncher-build-tests", "managedHeroesProjection.txt")), "Runtime front should de-prioritize noisy build-log style projection output when better text context exists.");
        AssertFalse(selectedPaths.Contains(Path.Combine("SonicLegacyLauncher-build-tests", "phase5_test_output.txt")), "Runtime front should de-prioritize noisy build-log style test output when better text context exists.");
    }
    finally
    {
        DeleteScratchWorkspace(root);
    }
}

#pragma warning disable CS8321
static void WorkspaceMaterialRuntimeFrontDeprioritizesProceduralNotesHonestly()
{
    var root = CreateScratchWorkspace();
    try
    {
        Directory.CreateDirectory(Path.Combine(root, "docs"));

        File.WriteAllText(Path.Combine(root, "docs", "ARCHITECTURE.md"), "Layered architecture with UI, Core, Mod Platform, Runtime.");
        File.WriteAllText(Path.Combine(root, "docs", "Документация проекта.txt"), "Current project documentation with stack and design principles.");
        File.WriteAllText(Path.Combine(root, "README.md"), "Platform overview and current purpose.");
        File.WriteAllText(Path.Combine(root, "Как работать с Git (для себя).txt"), "Personal git cheatsheet for daily work.");
        File.WriteAllText(Path.Combine(root, "НЕ ЗАБЫТЬ.txt"), "Reminder notes.");
        File.WriteAllText(Path.Combine(root, "Примерный план.txt"), "Draft plan for future work.");

        var scan = WorkspaceScanner.Scan(new WorkspaceScanRequest(root));
        var runtimeFront = new WorkspaceMaterialRuntimeFront(
            new TextMaterialRuntimeService(),
            new PdfExtractionRuntimeService(new FakeExternalProcessRunner(_ => new ExternalProcessResult(1, string.Empty, "Unexpected", false))),
            new ArchiveInspectionRuntimeService(new FakeExternalProcessRunner(_ => new ExternalProcessResult(1, string.Empty, "Unexpected", false))),
            new ImageInspectionRuntimeService());

        var packet = runtimeFront.BuildPreviewPacket(scan, maxMaterials: 3, maxCharsPerMaterial: 64);
        var selectedPaths = packet.Materials.Select(material => material.RelativePath).ToArray();

        AssertTrue(selectedPaths.Contains("README.md"), "Runtime front should preserve high-signal root overview text.");
        AssertTrue(selectedPaths.Contains(Path.Combine("docs", "ARCHITECTURE.md")), "Runtime front should preserve architecture docs ahead of procedural notes.");
        AssertTrue(selectedPaths.Contains(Path.Combine("docs", "Документация проекта.txt")), "Runtime front should preserve explicit project documentation ahead of procedural notes.");
        AssertFalse(selectedPaths.Contains("Как работать с Git (для себя).txt"), "Runtime front should de-prioritize personal procedural git notes when stronger project docs exist.");
        AssertFalse(selectedPaths.Contains("НЕ ЗАБЫТЬ.txt"), "Runtime front should de-prioritize reminder-like notes when stronger project docs exist.");
        AssertFalse(selectedPaths.Contains("Примерный план.txt"), "Runtime front should de-prioritize draft plan notes when stronger project docs exist.");
    }
    finally
    {
        DeleteScratchWorkspace(root);
    }
}
#pragma warning restore CS8321

static void WorkspaceMaterialRuntimeFrontDeprioritizesBulkImageAssetsHonestly()
{
    var root = CreateScratchWorkspace();
    try
    {
        Directory.CreateDirectory(Path.Combine(root, "public", "assets", "flats"));

        File.WriteAllText(Path.Combine(root, "README.md"), "cssDOOM is a browser-based DOOM recreation using CSS and JavaScript.");
        File.WriteAllText(Path.Combine(root, "LICENSE.txt"), "copyright license terms");
        File.WriteAllText(Path.Combine(root, "OFL.txt"), "font license terms");
        using (var bitmap = new System.Drawing.Bitmap(4, 4))
        {
            bitmap.Save(Path.Combine(root, "public", "preview.jpg"), System.Drawing.Imaging.ImageFormat.Jpeg);
            bitmap.Save(Path.Combine(root, "public", "icon.png"), System.Drawing.Imaging.ImageFormat.Png);
            bitmap.Save(Path.Combine(root, "public", "assets", "flats", "floor-a.png"), System.Drawing.Imaging.ImageFormat.Png);
            bitmap.Save(Path.Combine(root, "public", "assets", "flats", "floor-b.png"), System.Drawing.Imaging.ImageFormat.Png);
            bitmap.Save(Path.Combine(root, "public", "assets", "flats", "floor-c.png"), System.Drawing.Imaging.ImageFormat.Png);
            bitmap.Save(Path.Combine(root, "public", "assets", "flats", "floor-d.png"), System.Drawing.Imaging.ImageFormat.Png);
            bitmap.Save(Path.Combine(root, "public", "assets", "flats", "floor-e.png"), System.Drawing.Imaging.ImageFormat.Png);
        }

        var scan = WorkspaceScanner.Scan(new WorkspaceScanRequest(root));
        var runtimeFront = new WorkspaceMaterialRuntimeFront(
            new TextMaterialRuntimeService(),
            new PdfExtractionRuntimeService(new FakeExternalProcessRunner(_ => new ExternalProcessResult(1, string.Empty, "Unexpected", false))),
            new ArchiveInspectionRuntimeService(new FakeExternalProcessRunner(_ => new ExternalProcessResult(1, string.Empty, "Unexpected", false))),
            new ImageInspectionRuntimeService());

        var packet = runtimeFront.BuildPreviewPacket(scan, maxMaterials: 6, maxCharsPerMaterial: 64);
        var selectedPaths = packet.Materials.Select(material => material.RelativePath).ToArray();

        AssertTrue(selectedPaths.Contains("README.md"), "Runtime front should preserve project readme for image-heavy repos.");
        AssertTrue(selectedPaths.Contains(Path.Combine("public", "preview.jpg")), "Runtime front should keep preview-style images ahead of bulk texture assets.");
        AssertTrue(selectedPaths.Contains(Path.Combine("public", "icon.png")), "Runtime front should keep icon-style images ahead of bulk texture assets.");
        AssertFalse(selectedPaths.Contains("LICENSE.txt"), "Runtime front should de-prioritize legal boilerplate when stronger project evidence exists.");
        AssertFalse(selectedPaths.Contains("OFL.txt"), "Runtime front should de-prioritize font legal boilerplate when stronger project evidence exists.");
        AssertFalse(selectedPaths.Contains(Path.Combine("public", "assets", "flats", "floor-d.png")), "Runtime front should not let bulk flat textures dominate the bounded packet.");
        AssertFalse(selectedPaths.Contains(Path.Combine("public", "assets", "flats", "floor-e.png")), "Runtime front should soft-cap repetitive bulk image assets when better signals exist.");
    }
    finally
    {
        DeleteScratchWorkspace(root);
    }
}

static void WorkspaceMaterialRuntimeFrontDeprioritizesProceduralNotesAsciiHonestly()
{
    var root = CreateScratchWorkspace();
    try
    {
        Directory.CreateDirectory(Path.Combine(root, "docs"));

        File.WriteAllText(Path.Combine(root, "docs", "ARCHITECTURE.md"), "Layered architecture with UI, Core, Mod Platform, Runtime.");
        File.WriteAllText(Path.Combine(root, "docs", "PROJECT_DOCUMENTATION.txt"), "Current project documentation with stack and design principles.");
        File.WriteAllText(Path.Combine(root, "README.md"), "Platform overview and current purpose.");
        File.WriteAllText(Path.Combine(root, "GIT_CHEATSHEET_FOR_MYSELF.txt"), "Personal git cheatsheet for daily work.");
        File.WriteAllText(Path.Combine(root, "REMINDER_NOTES.txt"), "Reminder notes.");
        File.WriteAllText(Path.Combine(root, "DRAFT_PLAN.txt"), "Draft plan for future work.");

        var scan = WorkspaceScanner.Scan(new WorkspaceScanRequest(root));
        var runtimeFront = new WorkspaceMaterialRuntimeFront(
            new TextMaterialRuntimeService(),
            new PdfExtractionRuntimeService(new FakeExternalProcessRunner(_ => new ExternalProcessResult(1, string.Empty, "Unexpected", false))),
            new ArchiveInspectionRuntimeService(new FakeExternalProcessRunner(_ => new ExternalProcessResult(1, string.Empty, "Unexpected", false))),
            new ImageInspectionRuntimeService());

        var packet = runtimeFront.BuildPreviewPacket(scan, maxMaterials: 3, maxCharsPerMaterial: 64);
        var selectedPaths = packet.Materials.Select(material => material.RelativePath).ToArray();

        AssertTrue(selectedPaths.Contains("README.md"), "Runtime front should preserve high-signal root overview text.");
        AssertTrue(selectedPaths.Contains(Path.Combine("docs", "ARCHITECTURE.md")), "Runtime front should preserve architecture docs ahead of procedural notes.");
        AssertTrue(selectedPaths.Contains(Path.Combine("docs", "PROJECT_DOCUMENTATION.txt")), "Runtime front should preserve explicit project documentation ahead of procedural notes.");
        AssertFalse(selectedPaths.Contains("GIT_CHEATSHEET_FOR_MYSELF.txt"), "Runtime front should de-prioritize personal procedural git notes when stronger project docs exist.");
        AssertFalse(selectedPaths.Contains("REMINDER_NOTES.txt"), "Runtime front should de-prioritize reminder-like notes when stronger project docs exist.");
        AssertFalse(selectedPaths.Contains("DRAFT_PLAN.txt"), "Runtime front should de-prioritize draft plan notes when stronger project docs exist.");
    }
    finally
    {
        DeleteScratchWorkspace(root);
    }
}

static void PdfExtractionServiceFallsBackHonestly()
{
    var root = CreateScratchWorkspace();
    try
    {
        var pdfPath = Path.Combine(root, "spec.pdf");
        File.WriteAllText(pdfPath, "placeholder");
        var runner = new FakeExternalProcessRunner(request => request.Purpose switch
        {
            var purpose when purpose.StartsWith("pdf_extract:pdftotext", StringComparison.Ordinal) => new ExternalProcessResult(1, string.Empty, "syntax error", false),
            _ => new ExternalProcessResult(1, string.Empty, $"Unexpected purpose: {request.Purpose}", false)
        });
        var service = new PdfExtractionRuntimeService(runner);

        var result = service.Prepare(new MaterialRuntimeRequest("spec.pdf", pdfPath, WorkspaceMaterialKind.PdfDocument, "pdf-runtime-preview", 64));

        AssertEqual(MaterialRuntimeStatus.Failed, result.Status, "PDF runtime should surface backend failure honestly.");
        AssertEqual("pdftotext", result.BackendId, "PDF runtime should surface bundled backend id on failure.");
        AssertFalse(result.FallbackUsed, "PDF runtime should not claim fallback usage when none exists.");
        AssertEqual("PDF_EXTRACTION_FAILED", result.Diagnostic?.Code, "PDF runtime should classify backend failure honestly.");
    }
    finally
    {
        DeleteScratchWorkspace(root);
    }
}

static void PdfExtractionServicePrefersBundledPdfToTextHonestly()
{
    var root = CreateScratchWorkspace();
    try
    {
        var pdfPath = Path.Combine(root, "spec.pdf");
        File.WriteAllText(pdfPath, "placeholder");
        string? invokedFileName = null;
        var runner = new FakeExternalProcessRunner(request =>
        {
            invokedFileName = request.FileName;
            return new ExternalProcessResult(0, "primary extracted text", string.Empty, false);
        });
        var service = new PdfExtractionRuntimeService(runner);

        var result = service.Prepare(new MaterialRuntimeRequest("spec.pdf", pdfPath, WorkspaceMaterialKind.PdfDocument, "pdf-runtime-preview", 64));

        AssertEqual(MaterialRuntimeStatus.Prepared, result.Status, "PDF service should prepare bounded text when bundled pdftotext route is available.");
        AssertTrue(invokedFileName is not null && invokedFileName.EndsWith(Path.Combine("tools", "pdf-tools", "poppler-24.07.0", "Library", "bin", "pdftotext.exe"), StringComparison.OrdinalIgnoreCase), "PDF service should prefer bundled pdftotext backend from tools before PATH lookup.");
        AssertEqual("pdftotext", result.BackendId, "PDF runtime should preserve backend id while using bundled pdftotext.");
    }
    finally
    {
        DeleteScratchWorkspace(root);
    }
}

static void ArchiveInspectionServicePrefersBundled7zaHonestly()
{
    var root = CreateScratchWorkspace();
    try
    {
        var archivePath = Path.Combine(root, "bundle.zip");
        File.WriteAllText(archivePath, "placeholder");
        string? invokedFileName = null;
        var runner = new FakeExternalProcessRunner(request =>
        {
            invokedFileName = request.FileName;
            return new ExternalProcessResult(0, "2026-04-08 12:00:00 ....A 32 src/app.cs", string.Empty, false);
        });
        var service = new ArchiveInspectionRuntimeService(runner);

        var result = service.Prepare(new MaterialRuntimeRequest("bundle.zip", archivePath, WorkspaceMaterialKind.ArchiveArtifact, "archive-runtime-preview", 64));

        AssertEqual(MaterialRuntimeStatus.Prepared, result.Status, "Archive service should prepare bounded listing when bundled backend route is available.");
        AssertTrue(invokedFileName is not null && invokedFileName.EndsWith(Path.Combine("tools", "7za.exe"), StringComparison.OrdinalIgnoreCase), "Archive service should prefer bundled 7za.exe from tools before PATH lookup.");
        AssertEqual("7z", result.BackendId, "Archive runtime evidence should stay backend-stable even when using bundled 7za executable.");
    }
    finally
    {
        DeleteScratchWorkspace(root);
    }
}

static void ImageInspectionServiceUsesWindowsImageMetadataHonestly()
{
    var root = CreateScratchWorkspace();
    try
    {
        var imagePath = Path.Combine(root, "shot.png");
        using (var bitmap = new System.Drawing.Bitmap(2, 3))
        {
            bitmap.Save(imagePath, System.Drawing.Imaging.ImageFormat.Png);
        }
        var service = new ImageInspectionRuntimeService();

        var result = service.Prepare(new MaterialRuntimeRequest("shot.png", imagePath, WorkspaceMaterialKind.ImageAsset, "image-runtime-preview", 64));

        AssertEqual(MaterialRuntimeStatus.Prepared, result.Status, "Image service should prepare bounded metadata through native Windows image inspection.");
        AssertEqual("windows-image", result.BackendId, "Image runtime should preserve native backend id honestly.");
        AssertContains(result.ExtractedText, "format=PNG", "Image runtime should preserve image format in summary.");
        AssertContains(result.ExtractedText, "size=2x3", "Image runtime should preserve image dimensions in summary.");
    }
    finally
    {
        DeleteScratchWorkspace(root);
    }
}

static void ExternalProcessRunnerDrainsStdoutAndStderrHonestly()
{
    var runner = new ExternalProcessRunner();
    var result = runner.Run(new ExternalProcessRequest(
        "powershell",
        new[]
        {
            "-NoProfile",
            "-Command",
            "1..2000 | ForEach-Object { [Console]::Error.WriteLine('err') }; Write-Output 'done'"
        },
        TimeSpan.FromSeconds(10),
        "stderr-drain-test"));

    AssertFalse(result.TimedOut, "External process runner should not deadlock while child writes stderr.");
    AssertEqual(0, result.ExitCode, "External process runner should preserve child exit code.");
    AssertContains(result.StdOut, "done", "External process runner should capture stdout.");
    AssertContains(result.StdErr, "err", "External process runner should capture stderr.");
}

static void ArchitectureDiagramRuntimeRendersBoundedPngHonestly()
{
    var root = CreateScratchWorkspace();
    try
    {
        var outputPath = Path.Combine(root, "architecture_map.png");
        var runtime = new ArchitectureDiagramRuntimeService();
        var spec = new ArchitectureDiagramSpec(
            "Sample Architecture",
            new[]
            {
                new ArchitectureDiagramNode("ui", "UI Layer", "layer", "layers"),
                new ArchitectureDiagramNode("core", "Core Runtime", "layer", "layers"),
                new ArchitectureDiagramNode("entry-main", "cmd\\app\\main.go", "entry", "entries")
            },
            new[]
            {
                new ArchitectureDiagramEdge("ui", "core", "uses", "coarse"),
                new ArchitectureDiagramEdge("entry-main", "ui", "entry", "entry")
            },
            new[]
            {
                new ArchitectureDiagramGroup("layers", "Observed Layers", new[] { "ui", "core" }),
                new ArchitectureDiagramGroup("entries", "Observed Entry Points", new[] { "entry-main" })
            },
            new[] { "Evidence-backed sample architecture." },
            new ArchitectureDiagramRenderHints("left-to-right", new[] { "ui", "core" }, true));

        var diagnostic = runtime.RenderPng(spec, outputPath);

        AssertTrue(diagnostic is null, "Diagram runtime should render valid bounded spec without diagnostics.");
        AssertTrue(File.Exists(outputPath), "Diagram runtime should emit a PNG file.");
        var info = new FileInfo(outputPath);
        AssertTrue(info.Length > 1024, "Diagram runtime should emit a non-trivial readable PNG, not an empty placeholder.");
    }
    finally
    {
        DeleteScratchWorkspace(root);
    }
}

static void OpenRouterClientFailsFastOnMissingConfigHonestly()
{
    var client = new OpenRouterExecutionClient(configuration: null, httpClient: new HttpClient(), allowEnvironmentFallback: false);
    var response = client.Execute(new OpenRouterExecutionRequest("workspace.import.interpreter", "system", "user"));

    AssertFalse(response.Success, "OpenRouter client must fail fast when configuration is absent.");
    AssertTrue(response.Diagnostic is not null, "OpenRouter client must return a typed diagnostic for missing configuration.");
    AssertEqual("OPENROUTER_CONFIG_MISSING", response.Diagnostic!.Code, "OpenRouter client must classify missing configuration honestly.");
}

static void OpenRouterConfigurationDefaultsImportModelHonestly()
{
    var previousApiKey = Environment.GetEnvironmentVariable("OPENROUTER_API_KEY");
    var previousModel = Environment.GetEnvironmentVariable("OPENROUTER_MODEL");
    var previousBaseUrl = Environment.GetEnvironmentVariable("OPENROUTER_BASE_URL");
    var previousReferer = Environment.GetEnvironmentVariable("OPENROUTER_REFERER");
    var previousTitle = Environment.GetEnvironmentVariable("OPENROUTER_TITLE");
    var previousTimeout = Environment.GetEnvironmentVariable("OPENROUTER_TIMEOUT_SECONDS");

    try
    {
        Environment.SetEnvironmentVariable("OPENROUTER_API_KEY", "test-key");
        Environment.SetEnvironmentVariable("OPENROUTER_MODEL", null);
        Environment.SetEnvironmentVariable("OPENROUTER_BASE_URL", null);
        Environment.SetEnvironmentVariable("OPENROUTER_REFERER", null);
        Environment.SetEnvironmentVariable("OPENROUTER_TITLE", null);
        Environment.SetEnvironmentVariable("OPENROUTER_TIMEOUT_SECONDS", null);

        var configuration = OpenRouterConfiguration.FromEnvironment();

        AssertTrue(configuration is not null, "OpenRouter configuration should exist when api key is present.");
        AssertEqual(OpenRouterConfiguration.DefaultImportModelId, configuration!.ModelId, "OpenRouter configuration must default import model honestly.");
    }
    finally
    {
        Environment.SetEnvironmentVariable("OPENROUTER_API_KEY", previousApiKey);
        Environment.SetEnvironmentVariable("OPENROUTER_MODEL", previousModel);
        Environment.SetEnvironmentVariable("OPENROUTER_BASE_URL", previousBaseUrl);
        Environment.SetEnvironmentVariable("OPENROUTER_REFERER", previousReferer);
        Environment.SetEnvironmentVariable("OPENROUTER_TITLE", previousTitle);
        Environment.SetEnvironmentVariable("OPENROUTER_TIMEOUT_SECONDS", previousTimeout);
    }
}

static void OpenRouterConfigurationCanLoadLocalFileHonestly()
{
    var root = CreateScratchWorkspace();
    var previousCurrentDirectory = Environment.CurrentDirectory;
    var previousApiKey = Environment.GetEnvironmentVariable("OPENROUTER_API_KEY");
    var previousModel = Environment.GetEnvironmentVariable("OPENROUTER_MODEL");
    var previousBaseUrl = Environment.GetEnvironmentVariable("OPENROUTER_BASE_URL");
    var previousReferer = Environment.GetEnvironmentVariable("OPENROUTER_REFERER");
    var previousTitle = Environment.GetEnvironmentVariable("OPENROUTER_TITLE");
    var previousConfigFile = Environment.GetEnvironmentVariable("OPENROUTER_CONFIG_FILE");
    var previousTimeout = Environment.GetEnvironmentVariable("OPENROUTER_TIMEOUT_SECONDS");

    try
    {
        var configDirectory = Path.Combine(root, "app", "config");
        Directory.CreateDirectory(configDirectory);
        File.WriteAllText(
            Path.Combine(configDirectory, "openrouter.local.json"),
            """
            {
              "apiKey": "file-key",
              "modelId": "openai/gpt-4.1-nano",
              "baseUrl": "https://openrouter.ai/api/v1",
              "timeoutSeconds": 42,
              "referer": "https://zavod.test/import",
              "title": "ZAVOD Test"
            }
            """);

        Environment.CurrentDirectory = root;
        Environment.SetEnvironmentVariable("OPENROUTER_API_KEY", null);
        Environment.SetEnvironmentVariable("OPENROUTER_MODEL", null);
        Environment.SetEnvironmentVariable("OPENROUTER_BASE_URL", null);
        Environment.SetEnvironmentVariable("OPENROUTER_REFERER", null);
        Environment.SetEnvironmentVariable("OPENROUTER_TITLE", null);
        Environment.SetEnvironmentVariable("OPENROUTER_CONFIG_FILE", Path.Combine(configDirectory, "openrouter.local.json"));
        Environment.SetEnvironmentVariable("OPENROUTER_TIMEOUT_SECONDS", null);

        var configuration = OpenRouterConfiguration.FromEnvironment();

        AssertTrue(configuration is not null, "OpenRouter configuration should load from local file when api key is present there.");
        AssertEqual("file-key", configuration!.ApiKey, "OpenRouter configuration should preserve api key from local file honestly.");
        AssertEqual(OpenRouterConfiguration.DefaultImportModelId, configuration.ModelId, "OpenRouter configuration should preserve model from local file honestly.");
        AssertEqual(TimeSpan.FromSeconds(42), configuration.Timeout, "OpenRouter configuration should preserve timeout from local file honestly.");
        AssertContains(configuration.Source, "openrouter.local.json", "OpenRouter configuration should disclose local file source honestly.");
    }
    finally
    {
        Environment.CurrentDirectory = previousCurrentDirectory;
        Environment.SetEnvironmentVariable("OPENROUTER_API_KEY", previousApiKey);
        Environment.SetEnvironmentVariable("OPENROUTER_MODEL", previousModel);
        Environment.SetEnvironmentVariable("OPENROUTER_BASE_URL", previousBaseUrl);
        Environment.SetEnvironmentVariable("OPENROUTER_REFERER", previousReferer);
        Environment.SetEnvironmentVariable("OPENROUTER_TITLE", previousTitle);
        Environment.SetEnvironmentVariable("OPENROUTER_CONFIG_FILE", previousConfigFile);
        Environment.SetEnvironmentVariable("OPENROUTER_TIMEOUT_SECONDS", previousTimeout);
        DeleteScratchWorkspace(root);
    }
}

static void BraveSearchRuntimeFailsFastOnMissingConfigHonestly()
{
    var runtime = new BraveSearchRuntimeService(configuration: null, httpClient: new HttpClient(), allowEnvironmentFallback: false);
    var result = runtime.Search("zavod runtime", 3);

    AssertFalse(result.Success, "Brave runtime must fail fast when configuration is absent.");
    AssertTrue(result.Diagnostic is not null, "Brave runtime must return a typed diagnostic for missing configuration.");
    AssertEqual("BRAVE_CONFIG_MISSING", result.Diagnostic!.Code, "Brave runtime must classify missing configuration honestly.");
}

static void WebSearchToolReturnsStructuredResultsThroughBraveRuntimeHonestly()
{
    var handler = new FakeHttpMessageHandler((request, _) =>
    {
        var body = """
{
  "web": {
    "results": [
      {
        "title": "ZAVOD Runtime",
        "url": "https://example.test/runtime",
        "description": "Runtime front overview."
      },
      {
        "title": "ZAVOD Import",
        "url": "https://example.test/import",
        "description": "Import interpreter overview."
      }
    ]
  }
}
""";
        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(body)
        };
    });
    var httpClient = new HttpClient(handler);
    var runtime = new BraveSearchRuntimeService(
        new BraveSearchConfiguration("test-key", "https://api.search.brave.com/res/v1", TimeSpan.FromSeconds(5)),
        httpClient,
        allowEnvironmentFallback: false);
    var tool = new WebSearchTool(runtime);

    var result = tool.Execute(new WebSearchRequest("REQ-WEB-001", "zavod runtime", Limit: 2));

    AssertTrue(result.Success, "Web search tool should succeed through configured Brave runtime.");
    AssertEqual(2, result.ExtractedItems.Count, "Web search tool must return structured output items.");
    AssertContains(result.ExtractedItems[0].Reference, "https://example.test/runtime", "Web search result should preserve result URL.");
    AssertContains(result.Summary, "results=2", "Web search summary should preserve runtime result count.");
}

static void BraveSearchRuntimeRespectsBrokerDenialHonestly()
{
    var broker = new NetworkBrokerService(
        RuntimeAccessMode.DenyByDefault,
        RequiresHostApproval: true,
        UsesAllowlist: true,
        RecordsAuditTrail: true,
        "Scoped runtime denies network by default.");
    var runtime = new BraveSearchRuntimeService(
        new BraveSearchConfiguration("test-key", "https://api.search.brave.com/res/v1", TimeSpan.FromSeconds(5)),
        new HttpClient(),
        broker,
        allowEnvironmentFallback: false);

    var result = runtime.Search("zavod runtime", 3);

    AssertFalse(result.Success, "Brave runtime must deny search when network broker disallows external access.");
    AssertTrue(result.Diagnostic is not null, "Brave runtime must return a typed diagnostic when broker denies network.");
    AssertEqual("BRAVE_BROKER_DENIED", result.Diagnostic!.Code, "Brave runtime must classify broker denial honestly.");
    AssertContains(result.SummaryLine, "BRAVE_BROKER_DENIED", "Brave runtime summary should preserve broker denial.");
}

static void WorkspaceImportMaterialInterpreterRuntimeBuildsContextOnlyResultHonestly()
{
    var root = CreateScratchWorkspace();
    try
    {
        File.WriteAllText(Path.Combine(root, "notes.txt"), "project notes");
        File.WriteAllText(Path.Combine(root, "spec.pdf"), "placeholder");

        var scan = WorkspaceScanner.Scan(new WorkspaceScanRequest(root));
        var runner = new FakeExternalProcessRunner(request =>
        {
            if (request.Purpose.StartsWith("pdf_extract:pdftotext", StringComparison.Ordinal))
            {
                return new ExternalProcessResult(0, "specification overview", string.Empty, false);
            }

            return new ExternalProcessResult(1, string.Empty, $"Unexpected purpose: {request.Purpose}", false);
        });
        var runtimeFront = new WorkspaceMaterialRuntimeFront(
            new TextMaterialRuntimeService(),
            new PdfExtractionRuntimeService(runner),
            new ArchiveInspectionRuntimeService(runner),
            new ImageInspectionRuntimeService(runner));
        var openRouter = new FakeOpenRouterExecutionClient(_ => new OpenRouterExecutionResponse(
            true,
            """
SUMMARY: Imported materials may clarify current project context.
DETAIL: The packet shows active test outputs, architectural docs, and prepared PDF evidence.
CURRENT_SIGNALS: Prepared PDF and text notes align with active current context.
LAYER: Source | Holds imported project context | Derived from import materials.
ENTRY_POINT: notes.txt | context | Imported text anchor.
DIAGRAM_NODE: source | Source | layer
MATERIAL: notes.txt | High | Working notes for the current project.
MATERIAL: spec.pdf | Medium | Specification overview for imported constraints.
""",
            "openrouter/test",
            200,
            null,
            "ok"));
        var runtime = new WorkspaceImportMaterialInterpreterRuntime(runtimeFront, openRouter);

        var run = runtime.Interpret(scan, maxMaterials: 4, maxCharsPerMaterial: 64);

        AssertTrue(run.ExecutionResponse.Success, "Interpreter runtime should preserve successful upstream response.");
        AssertTrue(run.Interpretation.Materials.All(item => item.ContextOnly), "Interpreter runtime result must remain context-only.");
        AssertContains(run.PromptRequest.UserPrompt, "backend_id: pdftotext", "Interpreter prompt must expose prepared backend reality.");
        AssertContains(run.PromptRequest.UserPrompt, "preparation_status: Prepared", "Interpreter prompt must expose preparation status.");
        AssertContains(run.ExecutionRequest.SystemPrompt, "Import Materials Interpreter", "Interpreter runtime must use stable import prompt.");
        AssertEqual("notes.txt", run.Interpretation.Materials[0].RelativePath, "Interpreter result should remain bound to packet ordering.");
        AssertEqual(ProjectInterpretationMode.MaterialOnly, run.Interpretation.InterpretationMode, "Material-only import should align interpretation mode with scanner topology.");
        AssertTrue(run.Interpretation.ProjectDetails.Count >= 1, "Interpreter runtime should preserve bounded material-only project details.");
        AssertEqual(0, run.Interpretation.CurrentSignals.Count, "Material-only import should not preserve current source/app signals.");
        AssertEqual(0, run.Interpretation.Layers.Count, "Material-only import should not preserve source/app layers.");
        AssertEqual(0, run.Interpretation.EntryPoints.Count, "Material-only import should not preserve source/app entry points.");
        AssertEqual(0, run.Interpretation.DiagramSpec.Nodes.Count, "Material-only import should not preserve source/app diagram nodes.");
        AssertTrue(run.ArtifactBundle is not null, "Interpreter runtime should emit evidence bundle artifacts on successful import.");
        AssertTrue(File.Exists(run.ArtifactBundle!.ProjectReportPath), "Interpreter runtime should write project report artifact.");
        AssertTrue(File.Exists(run.ArtifactBundle.ArchitectureMapPath), "Interpreter runtime should render architecture PNG artifact.");
        AssertContains(run.Interpretation.ProjectDetails[0], "material-only", "Interpreter runtime should keep material-only boundary wording.");
        AssertEqual(WorkspaceMaterialContextUsefulness.High, run.Interpretation.Materials[0].PossibleUsefulness, "Interpreter result should preserve parsed usefulness.");
        AssertContains(run.SummaryLine, "truth=context_only", "Interpreter runtime summary must preserve context-only contract.");
        AssertTrue(openRouter.LastRequest is not null, "Fake OpenRouter client should observe the runtime-built request.");
    }
    finally
    {
        DeleteScratchWorkspace(root);
    }
}

static void WorkspaceImportMaterialInterpreterRuntimePreservesUpstreamFailureHonestly()
{
    var root = CreateScratchWorkspace();
    try
    {
        File.WriteAllText(Path.Combine(root, "notes.txt"), "project notes");

        var scan = WorkspaceScanner.Scan(new WorkspaceScanRequest(root));
        var runtimeFront = new WorkspaceMaterialRuntimeFront(
            new TextMaterialRuntimeService(),
            new PdfExtractionRuntimeService(new FakeExternalProcessRunner(_ => new ExternalProcessResult(1, string.Empty, "unexpected", false))),
            new ArchiveInspectionRuntimeService(new FakeExternalProcessRunner(_ => new ExternalProcessResult(1, string.Empty, "unexpected", false))),
            new ImageInspectionRuntimeService(new FakeExternalProcessRunner(_ => new ExternalProcessResult(1, string.Empty, "unexpected", false))));
        var openRouter = new FakeOpenRouterExecutionClient(_ => new OpenRouterExecutionResponse(
            false,
            string.Empty,
            "openrouter/test",
            503,
            new OpenRouterDiagnostic("OPENROUTER_UPSTREAM_FAILED", "OpenRouter returned HTTP 503."),
            "failed"));
        var runtime = new WorkspaceImportMaterialInterpreterRuntime(runtimeFront, openRouter);

        var run = runtime.Interpret(scan, maxMaterials: 4, maxCharsPerMaterial: 64);

        AssertFalse(run.ExecutionResponse.Success, "Interpreter runtime must preserve upstream failure honestly.");
        AssertTrue(run.Interpretation.Materials.All(item => item.ContextOnly), "Interpreter runtime must keep empty fallback result context-only.");
        AssertTrue(run.Interpretation.Materials.All(item => item.PossibleUsefulness == WorkspaceMaterialContextUsefulness.Unknown), "Interpreter runtime must not fabricate usefulness on upstream failure.");
        AssertContains(run.SummaryLine, "upstream_failure=OPENROUTER_UPSTREAM_FAILED", "Interpreter runtime summary must surface upstream failure.");
    }
    finally
    {
        DeleteScratchWorkspace(root);
    }
}

static void WorkspaceEvidenceArtifactRuntimeWritesLocalizedReportHonestly()
{
    var previousCulture = CultureInfo.CurrentCulture;
    var previousUiCulture = CultureInfo.CurrentUICulture;
    var root = CreateScratchWorkspace();
    try
    {
        CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("ru-RU");
        CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("ru-RU");

        File.WriteAllText(Path.Combine(root, "notes.txt"), "project notes");
        var scan = WorkspaceScanner.Scan(new WorkspaceScanRequest(root));
        var packet = WorkspaceImportMaterialPreviewPacketBuilder.Build(scan, maxMaterials: 2, maxCharsPerMaterial: 64);
        var interpretation = new WorkspaceImportMaterialInterpretationResult(
            packet.WorkspaceRoot,
            packet.ImportKind,
            packet.SourceRoots,
            new[] { "Краткое описание проекта." },
            new[] { "Техническая деталь." },
            new[] { "Проект активен." },
            Array.Empty<string>(),
            Array.Empty<string>(),
            Array.Empty<string>(),
            Array.Empty<string>(),
            new[] { new WorkspaceImportMaterialLayerInterpretation("Core", "Истина платформы", "Наблюдаемо в документации.") },
            new[] { new WorkspaceImportMaterialEntryPointInterpretation("src\\main.cpp", "Main", "Главная точка входа.") },
            new ArchitectureDiagramSpec(
                "Тестовая архитектура",
                new[] { new ArchitectureDiagramNode("core", "Core", "layer", "layers") },
                Array.Empty<ArchitectureDiagramEdge>(),
                new[] { new ArchitectureDiagramGroup("layers", "Observed Layers", new[] { "core" }) },
                new[] { "Заметка." },
                new ArchitectureDiagramRenderHints("left-to-right", new[] { "core" }, true)),
            new[]
            {
                new WorkspaceMaterialPreviewInterpretation(
                    "notes.txt",
                    WorkspaceMaterialKind.TextDocument,
                    "Описание",
                    WorkspaceMaterialContextUsefulness.High,
                    WorkspaceMaterialTemporalStatus.Unknown,
                    string.Empty,
                    true)
            },
            "Краткое описание проекта. truth=context_only.");
        var run = new WorkspaceImportMaterialInterpreterRunResult(
            packet,
            WorkspaceImportMaterialPromptRequestBuilder.Build(packet),
            new OpenRouterExecutionRequest("workspace.import.interpreter", "system", "user"),
            new OpenRouterExecutionResponse(true, "SUMMARY: тест", "openrouter/test", 200, null, "ok"),
            interpretation,
            null,
            "runtime summary");
        var service = new WorkspaceEvidenceArtifactRuntimeService(new ArchitectureDiagramRuntimeService());

        var bundle = service.WriteBundle(run);
        var reportText = File.ReadAllText(bundle.ProjectReportPath);

        AssertContains(reportText, "# Отчет о проекте", "Artifact runtime should localize the report title to the user's language.");
        AssertContains(reportText, "## Детали", "Artifact runtime should localize section headers to the user's language.");
        AssertContains(reportText, "## Точки входа", "Artifact runtime should localize entry-point heading to the user's language.");
    }
    finally
    {
        CultureInfo.CurrentCulture = previousCulture;
        CultureInfo.CurrentUICulture = previousUiCulture;
        DeleteScratchWorkspace(root);
    }
}

static void WorkspaceEvidenceArtifactRuntimeReportDoesNotReinflateFilteredModulesHonestly()
{
    var root = CreateScratchWorkspace();
    try
    {
        File.WriteAllText(Path.Combine(root, "notes.txt"), "project notes");
        var scan = WorkspaceScanner.Scan(new WorkspaceScanRequest(root));
        var packet = WorkspaceImportMaterialPreviewPacketBuilder.Build(scan, maxMaterials: 2, maxCharsPerMaterial: 64);
        var interpretation = new WorkspaceImportMaterialInterpretationResult(
            packet.WorkspaceRoot,
            packet.ImportKind,
            packet.SourceRoots,
            Array.Empty<string>(),
            Array.Empty<string>(),
            Array.Empty<string>(),
            Array.Empty<string>(),
            Array.Empty<string>(),
            Array.Empty<string>(),
            Array.Empty<string>(),
            Array.Empty<string>(),
            Array.Empty<string>(),
            new[]
            {
                new WorkspaceImportMaterialLayerInterpretation("UI", "Visible interface surface.", "Observed bounded layer.", WorkspaceEvidenceConfidenceLevel.Likely)
            },
            new[]
            {
                new WorkspaceImportMaterialModuleInterpretation("Presentation", "frontend", "Observed presentation files only.", WorkspaceEvidenceConfidenceLevel.Unknown),
                new WorkspaceImportMaterialModuleInterpretation("Registry", "core", "Observed registry files only.", WorkspaceEvidenceConfidenceLevel.Unknown)
            },
            Array.Empty<WorkspaceImportMaterialEntryPointInterpretation>(),
            new ArchitectureDiagramSpec(
                "Test",
                new[] { new ArchitectureDiagramNode("ui", "UI", "layer", "layers", WorkspaceEvidenceConfidenceLevel.Likely) },
                Array.Empty<ArchitectureDiagramEdge>(),
                new[] { new ArchitectureDiagramGroup("layers", "Observed Layers", new[] { "ui" }, WorkspaceEvidenceConfidenceLevel.Likely) },
                Array.Empty<string>(),
                new ArchitectureDiagramRenderHints("left-to-right", new[] { "ui" }, true)),
            Array.Empty<WorkspaceMaterialPreviewInterpretation>(),
            "Context-only summary. truth=context_only.");
        var run = new WorkspaceImportMaterialInterpreterRunResult(
            packet,
            WorkspaceImportMaterialPromptRequestBuilder.Build(packet),
            new OpenRouterExecutionRequest("workspace.import.interpreter", "system", "user"),
            new OpenRouterExecutionResponse(true, "SUMMARY: test", "openrouter/test", 200, null, "ok"),
            interpretation,
            null,
            "runtime summary");
        var service = new WorkspaceEvidenceArtifactRuntimeService(new ArchitectureDiagramRuntimeService());

        var bundle = service.WriteBundle(run);
        var reportText = File.ReadAllText(bundle.ProjectReportPath);

        AssertContains(reportText, "**UI** [Likely]", "Report should surface layer confidence explicitly.");
        AssertFalse(reportText.Contains("Presentation: frontend", StringComparison.OrdinalIgnoreCase), "Report should not re-inflate Presentation into UI without direct importer attachment.");
        AssertFalse(reportText.Contains("Registry: core", StringComparison.OrdinalIgnoreCase), "Report should not re-inflate Registry into a visible layer without direct importer attachment.");
    }
    finally
    {
        DeleteScratchWorkspace(root);
    }
}

static void WorkspaceEvidenceArtifactRuntimeAttachesModulesOnlyThroughStructuredColdMatchesHonestly()
{
    var root = CreateScratchWorkspace();
    try
    {
        File.WriteAllText(Path.Combine(root, "notes.txt"), "project notes");
        Directory.CreateDirectory(Path.Combine(root, "src"));
        File.WriteAllText(Path.Combine(root, "src", "main.cpp"), "int main() { return 0; }");
        var scan = WorkspaceScanner.Scan(new WorkspaceScanRequest(root));
        var packet = new WorkspaceMaterialRuntimeFront().BuildPreviewPacket(scan, maxMaterials: 2, maxCharsPerMaterial: 64);
        AssertTrue(packet.EvidencePack is not null, "Structured attachment test requires a cold evidence pack.");
        var basePack = packet.EvidencePack!;
        packet = packet with
        {
            EvidencePack = basePack with
            {
                Candidates = new WorkspaceEvidenceCandidates(
                    basePack.Candidates.EntryPoints,
                    new[]
                    {
                        new WorkspaceEvidenceModule("Presentation", "frontend", "UI", "cold exact match")
                    },
                    basePack.Candidates.FileRoles,
                    basePack.Candidates.ProjectUnits,
                    basePack.Candidates.RunProfiles)
            }
        };

        var interpretation = new WorkspaceImportMaterialInterpretationResult(
            packet.WorkspaceRoot,
            packet.ImportKind,
            packet.SourceRoots,
            Array.Empty<string>(),
            Array.Empty<string>(),
            Array.Empty<string>(),
            Array.Empty<string>(),
            Array.Empty<string>(),
            Array.Empty<string>(),
            Array.Empty<string>(),
            Array.Empty<string>(),
            Array.Empty<string>(),
            new[]
            {
                new WorkspaceImportMaterialLayerInterpretation("UI", "Visible interface surface.", "Observed bounded layer.", WorkspaceEvidenceConfidenceLevel.Likely),
                new WorkspaceImportMaterialLayerInterpretation("root", "Observed workspace root.", "Observed bounded layer.", WorkspaceEvidenceConfidenceLevel.Unknown)
            },
            new[]
            {
                new WorkspaceImportMaterialModuleInterpretation("Presentation", "frontend", "Observed presentation files only.", WorkspaceEvidenceConfidenceLevel.Likely),
                new WorkspaceImportMaterialModuleInterpretation("Source Root Mirror", "scan", "Observed from source root metadata.", WorkspaceEvidenceConfidenceLevel.Unknown)
            },
            Array.Empty<WorkspaceImportMaterialEntryPointInterpretation>(),
            new ArchitectureDiagramSpec(
                "Test",
                new[] { new ArchitectureDiagramNode("ui", "UI", "layer", "layers", WorkspaceEvidenceConfidenceLevel.Likely) },
                Array.Empty<ArchitectureDiagramEdge>(),
                new[] { new ArchitectureDiagramGroup("layers", "Observed Layers", new[] { "ui" }, WorkspaceEvidenceConfidenceLevel.Likely) },
                Array.Empty<string>(),
                new ArchitectureDiagramRenderHints("left-to-right", new[] { "ui" }, true)),
            Array.Empty<WorkspaceMaterialPreviewInterpretation>(),
            "Context-only summary. truth=context_only.");
        var run = new WorkspaceImportMaterialInterpreterRunResult(
            packet,
            WorkspaceImportMaterialPromptRequestBuilder.Build(packet),
            new OpenRouterExecutionRequest("workspace.import.interpreter", "system", "user"),
            new OpenRouterExecutionResponse(true, "SUMMARY: test", "openrouter/test", 200, null, "ok"),
            interpretation,
            null,
            "runtime summary");
        var service = new WorkspaceEvidenceArtifactRuntimeService(new ArchitectureDiagramRuntimeService());

        var bundle = service.WriteBundle(run);
        var reportText = File.ReadAllText(bundle.ProjectReportPath);

        AssertContains(reportText, "Presentation: frontend [Likely] (Observed presentation files only.)", "Exact cold module-to-layer match should remain renderable.");
        AssertFalse(reportText.Contains("  - Source Root Mirror", StringComparison.OrdinalIgnoreCase), "Source Root module must not attach to root layer by incidental text overlap.");
        AssertContains(reportText, "[Unattached] Source Root Mirror [Unknown]: scan (Observed from source root metadata.)", "Unmatched modules should remain visible as unattached instead of being guessed into a layer.");
    }
    finally
    {
        DeleteScratchWorkspace(root);
    }
}

static void WorkspaceScannerMarksEmptyImportMissingHonestly()
{
    var root = CreateScratchWorkspace();
    try
    {
        var result = WorkspaceScanner.Scan(new WorkspaceScanRequest(root));

        AssertEqual(WorkspaceHealthStatus.Missing, result.State.Health, "Empty workspace scan should not surface as healthy.");
        AssertTrue(result.State.StructuralAnomalies.Any(static anomaly => anomaly.Code == "NO_RELEVANT_FILES"), "Empty workspace scan should preserve no-relevant-files anomaly.");
    }
    finally
    {
        DeleteScratchWorkspace(root);
    }
}

static void WorkspaceScannerSeparatesPrimarySourceRootsFromBuildRootsHonestly()
{
    var root = CreateScratchWorkspace();
    try
    {
        Directory.CreateDirectory(Path.Combine(root, "repo", "src"));
        Directory.CreateDirectory(Path.Combine(root, "repo-build-main", "CMakeFiles", "ShowIncludes"));
        File.WriteAllText(Path.Combine(root, "repo", "CMakeLists.txt"), "cmake_minimum_required(VERSION 3.20)");
        File.WriteAllText(Path.Combine(root, "repo", "src", "main.cpp"), "int main() { return 0; }");
        File.WriteAllText(Path.Combine(root, "repo-build-main", "CMakeCache.txt"), "BUILD=1");
        File.WriteAllText(Path.Combine(root, "repo-build-main", "CMakeFiles", "ShowIncludes", "main.c"), "int main(void) { return 0; }");

        var scan = WorkspaceScanner.Scan(new WorkspaceScanRequest(root));
        var tool = new WorkspaceTool();
        var toolResult = tool.Execute(new WorkspaceInspectRequest("REQ-WS-BUILDROOTS-001", root, null));

        AssertTrue(scan.State.Summary.SourceRoots.Contains("repo"), "Primary source root should remain anchored to the real project folder.");
        AssertFalse(scan.State.Summary.SourceRoots.Contains("repo-build-main"), "Build-derived root must not be promoted into primary source roots.");
        AssertTrue(scan.State.Summary.BuildRoots.Contains("repo-build-main"), "Build-derived root should still be preserved separately.");
        AssertContains(toolResult.Summary, "buildRoots=1", "Workspace tool summary should expose separated build-root count.");
        AssertTrue(toolResult.ExtractedItems.Any(item => item.Kind == "build_root" && item.Reference.EndsWith("/repo-build-main", StringComparison.OrdinalIgnoreCase)), "Workspace tool should expose build-derived roots separately.");
    }
    finally
    {
        DeleteScratchWorkspace(root);
    }
}

static void WorkspaceEvidenceArtifactRuntimeWritesLocalizedReportUtf8Honestly()
{
    var previousCulture = CultureInfo.CurrentCulture;
    var previousUiCulture = CultureInfo.CurrentUICulture;
    var root = CreateScratchWorkspace();
    try
    {
        CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("ru-RU");
        CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("ru-RU");

        File.WriteAllText(Path.Combine(root, "notes.txt"), "project notes");
        var scan = WorkspaceScanner.Scan(new WorkspaceScanRequest(root));
        var packet = WorkspaceImportMaterialPreviewPacketBuilder.Build(scan, maxMaterials: 2, maxCharsPerMaterial: 64);
        var interpretation = new WorkspaceImportMaterialInterpretationResult(
            packet.WorkspaceRoot,
            packet.ImportKind,
            packet.SourceRoots,
            new[] { "Краткое описание проекта." },
            new[] { "Техническая деталь." },
            new[] { "Проект активен." },
            Array.Empty<string>(),
            Array.Empty<string>(),
            Array.Empty<string>(),
            Array.Empty<string>(),
            new[] { new WorkspaceImportMaterialLayerInterpretation("Core", "Основа платформы", "Наблюдаемо в документации.") },
            new[] { new WorkspaceImportMaterialEntryPointInterpretation("src\\main.cpp", "Main", "Главная точка входа.") },
            new ArchitectureDiagramSpec(
                "Тестовая архитектура",
                new[] { new ArchitectureDiagramNode("core", "Core", "layer", "layers") },
                Array.Empty<ArchitectureDiagramEdge>(),
                new[] { new ArchitectureDiagramGroup("layers", "Observed Layers", new[] { "core" }) },
                new[] { "Заметка." },
                new ArchitectureDiagramRenderHints("left-to-right", new[] { "core" }, true)),
            new[]
            {
                new WorkspaceMaterialPreviewInterpretation(
                    "notes.txt",
                    WorkspaceMaterialKind.TextDocument,
                    "Описание",
                    WorkspaceMaterialContextUsefulness.High,
                    WorkspaceMaterialTemporalStatus.Unknown,
                    string.Empty,
                    true)
            },
            "Краткое описание проекта. truth=context_only.");
        var run = new WorkspaceImportMaterialInterpreterRunResult(
            packet,
            WorkspaceImportMaterialPromptRequestBuilder.Build(packet),
            new OpenRouterExecutionRequest("workspace.import.interpreter", "system", "user"),
            new OpenRouterExecutionResponse(true, "SUMMARY: тест", "openrouter/test", 200, null, "ok"),
            interpretation,
            null,
            "runtime summary");
        var service = new WorkspaceEvidenceArtifactRuntimeService(new ArchitectureDiagramRuntimeService());

        var bundle = service.WriteBundle(run);
        var reportText = File.ReadAllText(bundle.ProjectReportPath);

        AssertContains(reportText, "# Отчет о проекте", "Artifact runtime should localize the report title to the user's language.");
        AssertContains(reportText, "## Детали", "Artifact runtime should localize section headers to the user's language.");
        AssertContains(reportText, "## Точки входа", "Artifact runtime should localize entry-point heading to the user's language.");
    }
    finally
    {
        CultureInfo.CurrentCulture = previousCulture;
        CultureInfo.CurrentUICulture = previousUiCulture;
        DeleteScratchWorkspace(root);
    }
}

static void WorkspaceEvidenceArtifactRuntimeKeepsBundleAtWorkspaceRootWhenDotSourceRootExistsHonestly()
{
    var root = CreateScratchWorkspace();
    try
    {
        Directory.CreateDirectory(Path.Combine(root, "src"));
        File.WriteAllText(Path.Combine(root, "package.json"), "{ \"name\": \"demo\" }");
        File.WriteAllText(Path.Combine(root, "index.js"), "console.log('root entry');");
        File.WriteAllText(Path.Combine(root, "src", "renderer.js"), "export const renderer = true;");

        var scan = WorkspaceScanner.Scan(new WorkspaceScanRequest(root));
        var packet = WorkspaceImportMaterialPreviewPacketBuilder.Build(scan, maxMaterials: 2, maxCharsPerMaterial: 64);
        var interpretation = new WorkspaceImportMaterialInterpretationResult(
            packet.WorkspaceRoot,
            packet.ImportKind,
            packet.SourceRoots,
            Array.Empty<string>(),
            new[] { "Корневой manifest и source root src принадлежат одному проекту." },
            Array.Empty<string>(),
            Array.Empty<string>(),
            Array.Empty<string>(),
            Array.Empty<string>(),
            Array.Empty<string>(),
            Array.Empty<WorkspaceImportMaterialLayerInterpretation>(),
            Array.Empty<WorkspaceImportMaterialEntryPointInterpretation>(),
            new ArchitectureDiagramSpec(
                "Test",
                new[] { new ArchitectureDiagramNode("root", "Root", "root") },
                Array.Empty<ArchitectureDiagramEdge>(),
                Array.Empty<ArchitectureDiagramGroup>(),
                Array.Empty<string>(),
                new ArchitectureDiagramRenderHints("left-to-right", Array.Empty<string>(), true)),
            Array.Empty<WorkspaceMaterialPreviewInterpretation>(),
            "Корневой manifest должен удерживать bundle в корне workspace. truth=context_only.");
        var run = new WorkspaceImportMaterialInterpreterRunResult(
            packet,
            WorkspaceImportMaterialPromptRequestBuilder.Build(packet),
            new OpenRouterExecutionRequest("workspace.import.interpreter", "system", "user"),
            new OpenRouterExecutionResponse(true, "SUMMARY: test", "openrouter/test", 200, null, "ok"),
            interpretation,
            null,
            "runtime summary");
        var service = new WorkspaceEvidenceArtifactRuntimeService(new ArchitectureDiagramRuntimeService());

        var bundle = service.WriteBundle(run);

        AssertEqual(Path.Combine(root, ".zavod", "import_evidence_bundle"), bundle.OutputDirectory, "Artifact runtime should keep .zavod at workspace root when '.' is already an observed source root.");
    }
    finally
    {
        DeleteScratchWorkspace(root);
    }
}

static void WorkspaceEvidenceArtifactRuntimePrefersGitBackedNestedProjectRootHonestly()
{
    var root = CreateScratchWorkspace();
    try
    {
        Directory.CreateDirectory(Path.Combine(root, "release"));
        Directory.CreateDirectory(Path.Combine(root, "source", "x64dbg", ".git"));
        Directory.CreateDirectory(Path.Combine(root, "source", "x64dbg", "src"));
        File.WriteAllText(Path.Combine(root, "release", "x64dbg.exe"), "MZ");
        File.WriteAllText(Path.Combine(root, "source", "x64dbg", "CMakeLists.txt"), "project(x64dbg)");
        File.WriteAllText(Path.Combine(root, "source", "x64dbg", "README.md"), "Debugger core and plugin system.");
        File.WriteAllText(Path.Combine(root, "source", "x64dbg", "src", "main.cpp"), "int main() { return 0; }");

        var scan = WorkspaceScanner.Scan(new WorkspaceScanRequest(root));
        var packet = WorkspaceImportMaterialPreviewPacketBuilder.Build(scan, maxMaterials: 3, maxCharsPerMaterial: 96);
        var interpretation = new WorkspaceImportMaterialInterpretationResult(
            packet.WorkspaceRoot,
            packet.ImportKind,
            packet.SourceRoots,
            Array.Empty<string>(),
            new[] { "Debugger core lives under the nested git-backed project root." },
            Array.Empty<string>(),
            Array.Empty<string>(),
            Array.Empty<string>(),
            Array.Empty<string>(),
            Array.Empty<string>(),
            Array.Empty<WorkspaceImportMaterialLayerInterpretation>(),
            new[] { new WorkspaceImportMaterialEntryPointInterpretation(Path.Combine("source", "x64dbg", "src", "main.cpp"), "main", "Nested git-backed entry point.") },
            new ArchitectureDiagramSpec(
                "Test",
                new[] { new ArchitectureDiagramNode("root", "Root", "root") },
                Array.Empty<ArchitectureDiagramEdge>(),
                Array.Empty<ArchitectureDiagramGroup>(),
                Array.Empty<string>(),
                new ArchitectureDiagramRenderHints("left-to-right", Array.Empty<string>(), true)),
            Array.Empty<WorkspaceMaterialPreviewInterpretation>(),
            "Nested git-backed project root should own the bundle. truth=context_only.");
        var run = new WorkspaceImportMaterialInterpreterRunResult(
            packet,
            WorkspaceImportMaterialPromptRequestBuilder.Build(packet),
            new OpenRouterExecutionRequest("workspace.import.interpreter", "system", "user"),
            new OpenRouterExecutionResponse(true, "SUMMARY: test", "openrouter/test", 200, null, "ok"),
            interpretation,
            null,
            "runtime summary");
        var service = new WorkspaceEvidenceArtifactRuntimeService(new ArchitectureDiagramRuntimeService());

        var bundle = service.WriteBundle(run);

        AssertEqual(
            Path.Combine(root, "source", "x64dbg", ".zavod", "import_evidence_bundle"),
            bundle.OutputDirectory,
            "Artifact runtime should place .zavod next to the git-backed nested project root when that root carries the observed source scope.");
    }
    finally
    {
        DeleteScratchWorkspace(root);
    }
}

static void WorkspaceEvidenceArtifactRuntimeKeepsBundleAtWorkspaceRootForMultipleProjectContainerHonestly()
{
    var root = CreateScratchWorkspace();
    try
    {
        Directory.CreateDirectory(Path.Combine(root, "alpha", ".git"));
        Directory.CreateDirectory(Path.Combine(root, "alpha", "src"));
        Directory.CreateDirectory(Path.Combine(root, "beta", ".git"));
        Directory.CreateDirectory(Path.Combine(root, "beta", "cmd"));
        File.WriteAllText(Path.Combine(root, "alpha", "src", "main.ts"), "export function main() {}");
        File.WriteAllText(Path.Combine(root, "alpha", "package.json"), "{ \"name\": \"alpha\" }");
        File.WriteAllText(Path.Combine(root, "beta", "cmd", "main.go"), "package main\nfunc main() {}");
        File.WriteAllText(Path.Combine(root, "beta", "go.mod"), "module beta");

        var scan = WorkspaceScanner.Scan(new WorkspaceScanRequest(root));
        var packet = new WorkspaceMaterialRuntimeFront().BuildPreviewPacket(scan, maxMaterials: 4, maxCharsPerMaterial: 96);
        var interpretation = WorkspaceImportMaterialInterpretationResultBuilder.BuildFromResponse(
            packet,
            new WorkspaceImportMaterialPromptResponse(
                string.Empty,
                Array.Empty<string>(),
                Array.Empty<string>(),
                Array.Empty<string>(),
                Array.Empty<string>(),
                Array.Empty<string>(),
                Array.Empty<string>(),
                Array.Empty<WorkspaceImportMaterialLayerInterpretation>(),
                Array.Empty<WorkspaceImportMaterialModuleInterpretation>(),
                Array.Empty<WorkspaceImportMaterialEntryPointInterpretation>(),
                new ArchitectureDiagramSpec(string.Empty, Array.Empty<ArchitectureDiagramNode>(), Array.Empty<ArchitectureDiagramEdge>(), Array.Empty<ArchitectureDiagramGroup>(), Array.Empty<string>(), new ArchitectureDiagramRenderHints("left-to-right", Array.Empty<string>(), true)),
                Array.Empty<WorkspaceImportMaterialPromptResponseItem>()));
        var run = new WorkspaceImportMaterialInterpreterRunResult(
            packet,
            WorkspaceImportMaterialPromptRequestBuilder.Build(packet),
            new OpenRouterExecutionRequest("workspace.import.interpreter", "system", "user"),
            new OpenRouterExecutionResponse(true, "SUMMARY: test", "openrouter/test", 200, null, "ok"),
            interpretation,
            null,
            "runtime summary");
        var service = new WorkspaceEvidenceArtifactRuntimeService(new ArchitectureDiagramRuntimeService());

        var bundle = service.WriteBundle(run);

        AssertEqual(ProjectInterpretationMode.MultipleIndependentProjects, interpretation.InterpretationMode, "Container case should be recognized before artifact placement.");
        AssertEqual(Path.Combine(root, ".zavod", "import_evidence_bundle"), bundle.OutputDirectory, "Container mode should keep bundle at the scanned case root instead of hiding it under one nested repo.");
    }
    finally
    {
        DeleteScratchWorkspace(root);
    }
}

static void WorkspaceEvidenceArtifactRuntimeSuppressesUnifiedReportProjectionForMultipleProjectContainerHonestly()
{
    var root = CreateScratchWorkspace();
    try
    {
        Directory.CreateDirectory(Path.Combine(root, "alpha", ".git"));
        Directory.CreateDirectory(Path.Combine(root, "alpha", "src"));
        Directory.CreateDirectory(Path.Combine(root, "beta", ".git"));
        Directory.CreateDirectory(Path.Combine(root, "beta", "cmd"));
        File.WriteAllText(Path.Combine(root, "alpha", "src", "main.ts"), "export function main() {}");
        File.WriteAllText(Path.Combine(root, "alpha", "package.json"), "{ \"name\": \"alpha\" }");
        File.WriteAllText(Path.Combine(root, "beta", "cmd", "main.go"), "package main\nfunc main() {}");
        File.WriteAllText(Path.Combine(root, "beta", "go.mod"), "module beta");

        var scan = WorkspaceScanner.Scan(new WorkspaceScanRequest(root));
        var packet = new WorkspaceMaterialRuntimeFront().BuildPreviewPacket(scan, maxMaterials: 6, maxCharsPerMaterial: 96);
        var interpretation = WorkspaceImportMaterialInterpretationResultBuilder.BuildFromResponse(
            packet,
            new WorkspaceImportMaterialPromptResponse(
                "A unified platform spans both repos.",
                new[] { "Shared runtime and service layers appear across the whole folder." },
                new[] { "Project is in shared release rollout stage." },
                Array.Empty<string>(),
                Array.Empty<string>(),
                Array.Empty<string>(),
                Array.Empty<string>(),
                new[] { new WorkspaceImportMaterialLayerInterpretation("Service", "invented", "derived") },
                new[] { new WorkspaceImportMaterialModuleInterpretation("Platform", "module", "derived") },
                new[]
                {
                    new WorkspaceImportMaterialEntryPointInterpretation(Path.Combine("alpha", "src", "main.ts"), "main", "Alpha entry"),
                    new WorkspaceImportMaterialEntryPointInterpretation(Path.Combine("beta", "cmd", "main.go"), "main", "Beta entry")
                },
                new ArchitectureDiagramSpec("Unified Architecture", new[] { new ArchitectureDiagramNode("service", "Service", "layer") }, Array.Empty<ArchitectureDiagramEdge>(), Array.Empty<ArchitectureDiagramGroup>(), new[] { "One architecture spans both repos." }, new ArchitectureDiagramRenderHints("left-to-right", Array.Empty<string>(), true)),
                Array.Empty<WorkspaceImportMaterialPromptResponseItem>()));
        var run = new WorkspaceImportMaterialInterpreterRunResult(
            packet,
            WorkspaceImportMaterialPromptRequestBuilder.Build(packet),
            new OpenRouterExecutionRequest("workspace.import.interpreter", "system", "user"),
            new OpenRouterExecutionResponse(true, "SUMMARY: test", "openrouter/test", 200, null, "ok"),
            interpretation,
            null,
            "runtime summary");
        var service = new WorkspaceEvidenceArtifactRuntimeService(new ArchitectureDiagramRuntimeService());

        var bundle = service.WriteBundle(run);
        var reportText = File.ReadAllText(bundle.ProjectReportPath);

        AssertContains(reportText, "несколько независимых", "Report should preserve coarse container summary.");
        AssertFalse(reportText.Contains("Technical Passport", StringComparison.OrdinalIgnoreCase), "Container report should suppress technical passport inflation.");
        AssertFalse(reportText.Contains("A unified platform spans both repos", StringComparison.OrdinalIgnoreCase), "Container report should not preserve the raw unified narrative from importer prose.");
        AssertFalse(reportText.Contains("shared runtime and service layers", StringComparison.OrdinalIgnoreCase), "Container report should not keep unified layered/runtime claims from weak mixed evidence.");
        AssertTrue(
            reportText.Contains("Unified layer/module projection is suppressed", StringComparison.OrdinalIgnoreCase) ||
            reportText.Contains("Единая проекция слоёв и модулей подавлена", StringComparison.OrdinalIgnoreCase),
            "Container report should stay projection-only.");
        AssertTrue(
            reportText.Contains("Unified entry-point projection is suppressed", StringComparison.OrdinalIgnoreCase) ||
            reportText.Contains("Единая проекция entry points подавлена", StringComparison.OrdinalIgnoreCase),
            "Container report should not project a single shared entry surface.");
    }
    finally
    {
        DeleteScratchWorkspace(root);
    }
}

static void WorkspaceEvidenceArtifactRuntimeWritesPreviewHtmlForSingleProjectHonestly()
{
    var root = CreateScratchWorkspace();
    try
    {
        Directory.CreateDirectory(Path.Combine(root, "src"));
        File.WriteAllText(Path.Combine(root, "src", "main.go"), "package main\nfunc main() {}");
        File.WriteAllText(Path.Combine(root, "README.md"), "Single project preview case.");

        var scan = WorkspaceScanner.Scan(new WorkspaceScanRequest(root));
        var packet = new WorkspaceMaterialRuntimeFront().BuildPreviewPacket(scan, maxMaterials: 4, maxCharsPerMaterial: 96);
        var interpretation = WorkspaceImportMaterialInterpretationResultBuilder.BuildFromResponse(
            packet,
            new WorkspaceImportMaterialPromptResponse(
                "Single project summary.",
                new[] { "Project detail line." },
                Array.Empty<string>(),
                Array.Empty<string>(),
                Array.Empty<string>(),
                Array.Empty<string>(),
                Array.Empty<string>(),
                Array.Empty<WorkspaceImportMaterialLayerInterpretation>(),
                Array.Empty<WorkspaceImportMaterialModuleInterpretation>(),
                new[] { new WorkspaceImportMaterialEntryPointInterpretation(Path.Combine("src", "main.go"), "main", "Observed entry") },
                new ArchitectureDiagramSpec("Project Architecture", Array.Empty<ArchitectureDiagramNode>(), Array.Empty<ArchitectureDiagramEdge>(), Array.Empty<ArchitectureDiagramGroup>(), Array.Empty<string>(), new ArchitectureDiagramRenderHints("left-to-right", Array.Empty<string>(), true)),
                Array.Empty<WorkspaceImportMaterialPromptResponseItem>()));
        var run = new WorkspaceImportMaterialInterpreterRunResult(
            packet,
            WorkspaceImportMaterialPromptRequestBuilder.Build(packet),
            new OpenRouterExecutionRequest("workspace.import.interpreter", "system", "user"),
            new OpenRouterExecutionResponse(true, "SUMMARY: test", "openrouter/test", 200, null, "ok"),
            interpretation,
            null,
            "runtime summary");
        var service = new WorkspaceEvidenceArtifactRuntimeService(new ArchitectureDiagramRuntimeService());

        var bundle = service.WriteBundle(run);
        var previewText = File.ReadAllText(bundle.PreviewPath);

        AssertTrue(File.Exists(bundle.PreviewPath), "Preview html should be written to .zavod root.");
        AssertTrue(File.Exists(Path.Combine(bundle.OutputDirectory, "preview.html")), "Preview html should also be written beside project_report.md in bundle directory.");
        AssertContains(previewText, "Stage: Preview Docs", "Preview should expose the active preview-doc stage.");
        AssertContains(previewText, "Source: preview docs (preview_project.md)", "Preview should show the selected document source explicitly.");
        AssertContains(previewText, "project_report.md", "Preview should link to the markdown report artifact.");
        AssertContains(previewText, "Ambiguous", "Preview header should expose importer-owned interpretation mode.");
        AssertContains(previewText, "Project (Preview)", "Preview should render the richer preview project doc.");
        AssertContains(previewText, "Companion Document", "Preview should render the companion capsule section when available.");
        AssertContains(previewText, "Structure / Map", "Preview should render the richer structure block.");
    }
    finally
    {
        DeleteScratchWorkspace(root);
    }
}

static void WorkspaceEvidenceArtifactRuntimeWritesPreviewHtmlWarningForMultipleProjectContainerHonestly()
{
    var root = CreateScratchWorkspace();
    try
    {
        Directory.CreateDirectory(Path.Combine(root, "alpha", ".git"));
        Directory.CreateDirectory(Path.Combine(root, "alpha", "src"));
        Directory.CreateDirectory(Path.Combine(root, "beta", ".git"));
        Directory.CreateDirectory(Path.Combine(root, "beta", "cmd"));
        File.WriteAllText(Path.Combine(root, "alpha", "src", "main.ts"), "export function main() {}");
        File.WriteAllText(Path.Combine(root, "alpha", "package.json"), "{ \"name\": \"alpha\" }");
        File.WriteAllText(Path.Combine(root, "beta", "cmd", "main.go"), "package main\nfunc main() {}");
        File.WriteAllText(Path.Combine(root, "beta", "go.mod"), "module beta");

        var scan = WorkspaceScanner.Scan(new WorkspaceScanRequest(root));
        var packet = new WorkspaceMaterialRuntimeFront().BuildPreviewPacket(scan, maxMaterials: 6, maxCharsPerMaterial: 96);
        var interpretation = WorkspaceImportMaterialInterpretationResultBuilder.BuildFromResponse(
            packet,
            new WorkspaceImportMaterialPromptResponse(
                "A unified product architecture spans both roots.",
                new[] { "Shared service layers appear across the folder." },
                Array.Empty<string>(),
                Array.Empty<string>(),
                Array.Empty<string>(),
                Array.Empty<string>(),
                Array.Empty<string>(),
                Array.Empty<WorkspaceImportMaterialLayerInterpretation>(),
                Array.Empty<WorkspaceImportMaterialModuleInterpretation>(),
                Array.Empty<WorkspaceImportMaterialEntryPointInterpretation>(),
                new ArchitectureDiagramSpec("Unified Architecture", Array.Empty<ArchitectureDiagramNode>(), Array.Empty<ArchitectureDiagramEdge>(), Array.Empty<ArchitectureDiagramGroup>(), Array.Empty<string>(), new ArchitectureDiagramRenderHints("left-to-right", Array.Empty<string>(), true)),
                Array.Empty<WorkspaceImportMaterialPromptResponseItem>()));
        var run = new WorkspaceImportMaterialInterpreterRunResult(
            packet,
            WorkspaceImportMaterialPromptRequestBuilder.Build(packet),
            new OpenRouterExecutionRequest("workspace.import.interpreter", "system", "user"),
            new OpenRouterExecutionResponse(true, "SUMMARY: test", "openrouter/test", 200, null, "ok"),
            interpretation,
            null,
            "runtime summary");
        var service = new WorkspaceEvidenceArtifactRuntimeService(new ArchitectureDiagramRuntimeService());

        var bundle = service.WriteBundle(run);
        var previewText = File.ReadAllText(bundle.PreviewPath);

        AssertContains(previewText, "MultipleIndependentProjects", "Preview should expose multiple-project container mode.");
        AssertContains(previewText, "Stage: Preview Docs", "Container preview should still disclose the active document stage.");
        AssertContains(previewText, "Unified architecture is not assumed", "Preview should show an explicit container warning.");
        AssertFalse(previewText.Contains("Shared service layers appear across the folder.", StringComparison.OrdinalIgnoreCase), "Preview must not re-inflate suppressed container narrative.");
    }
    finally
    {
        DeleteScratchWorkspace(root);
    }
}

static void WorkspaceEvidenceArtifactRuntimeWritesPreviewHtmlWarningForAmbiguousContainerHonestly()
{
    var root = CreateScratchWorkspace();
    try
    {
        Directory.CreateDirectory(Path.Combine(root, "src"));
        Directory.CreateDirectory(Path.Combine(root, "vendor-tool", "src"));
        File.WriteAllText(Path.Combine(root, "src", "main.cpp"), "int main() { return 0; }");
        File.WriteAllText(Path.Combine(root, "CMakeLists.txt"), "project(host)");
        File.WriteAllText(Path.Combine(root, "vendor-tool", "src", "main.cpp"), "int main() { return 0; }");

        var scan = WorkspaceScanner.Scan(new WorkspaceScanRequest(root));
        var packet = new WorkspaceMaterialRuntimeFront().BuildPreviewPacket(scan, maxMaterials: 5, maxCharsPerMaterial: 96);
        var interpretation = WorkspaceImportMaterialInterpretationResultBuilder.BuildFromResponse(
            packet,
            new WorkspaceImportMaterialPromptResponse(
                "One architecture spans the workspace and auxiliary source subtree.",
                Array.Empty<string>(),
                Array.Empty<string>(),
                Array.Empty<string>(),
                Array.Empty<string>(),
                Array.Empty<string>(),
                Array.Empty<string>(),
                Array.Empty<WorkspaceImportMaterialLayerInterpretation>(),
                Array.Empty<WorkspaceImportMaterialModuleInterpretation>(),
                new[]
                {
                    new WorkspaceImportMaterialEntryPointInterpretation(Path.Combine("src", "main.cpp"), "main", "Host entry"),
                    new WorkspaceImportMaterialEntryPointInterpretation(Path.Combine("vendor-tool", "src", "main.cpp"), "main", "Nested tool entry")
                },
                new ArchitectureDiagramSpec("Unified Workspace Architecture", Array.Empty<ArchitectureDiagramNode>(), Array.Empty<ArchitectureDiagramEdge>(), Array.Empty<ArchitectureDiagramGroup>(), Array.Empty<string>(), new ArchitectureDiagramRenderHints("left-to-right", Array.Empty<string>(), true)),
                Array.Empty<WorkspaceImportMaterialPromptResponseItem>()));
        var run = new WorkspaceImportMaterialInterpreterRunResult(
            packet,
            WorkspaceImportMaterialPromptRequestBuilder.Build(packet),
            new OpenRouterExecutionRequest("workspace.import.interpreter", "system", "user"),
            new OpenRouterExecutionResponse(true, "SUMMARY: test", "openrouter/test", 200, null, "ok"),
            interpretation,
            null,
            "runtime summary");
        var service = new WorkspaceEvidenceArtifactRuntimeService(new ArchitectureDiagramRuntimeService());

        var bundle = service.WriteBundle(run);
        var previewText = File.ReadAllText(bundle.PreviewPath);

        AssertContains(previewText, "Ambiguous", "Preview should expose ambiguous review mode.");
        AssertContains(previewText, "Stage: Preview Docs", "Ambiguous container preview should still disclose preview-doc stage.");
        AssertContains(previewText, "Unified architecture is not assumed", "Ambiguous container preview should still show a warning.");
    }
    finally
    {
        DeleteScratchWorkspace(root);
    }
}

static void WorkspaceEvidenceArtifactRuntimeWritesPreviewHtmlFromCanonicalDocsHonestly()
{
    var root = CreateScratchWorkspace();
    try
    {
        Directory.CreateDirectory(Path.Combine(root, "src"));
        File.WriteAllText(Path.Combine(root, "src", "main.go"), "package main\nfunc main() {}");
        File.WriteAllText(Path.Combine(root, "README.md"), "Canonical html preview case.");

        var scan = WorkspaceScanner.Scan(new WorkspaceScanRequest(root));
        var packet = new WorkspaceMaterialRuntimeFront().BuildPreviewPacket(scan, maxMaterials: 4, maxCharsPerMaterial: 96);
        var interpretation = WorkspaceImportMaterialInterpretationResultBuilder.BuildFromResponse(
            packet,
            new WorkspaceImportMaterialPromptResponse(
                "Single project summary.",
                new[] { "Project detail line." },
                Array.Empty<string>(),
                Array.Empty<string>(),
                Array.Empty<string>(),
                Array.Empty<string>(),
                Array.Empty<string>(),
                Array.Empty<WorkspaceImportMaterialLayerInterpretation>(),
                Array.Empty<WorkspaceImportMaterialModuleInterpretation>(),
                new[] { new WorkspaceImportMaterialEntryPointInterpretation(Path.Combine("src", "main.go"), "main", "Observed entry") },
                new ArchitectureDiagramSpec("Project Architecture", new[] { new ArchitectureDiagramNode("runtime", "Runtime", "layer") }, Array.Empty<ArchitectureDiagramEdge>(), Array.Empty<ArchitectureDiagramGroup>(), new[] { "Observed main runtime path." }, new ArchitectureDiagramRenderHints("left-to-right", Array.Empty<string>(), true)),
                Array.Empty<WorkspaceImportMaterialPromptResponseItem>()));
        var run = new WorkspaceImportMaterialInterpreterRunResult(
            packet,
            WorkspaceImportMaterialPromptRequestBuilder.Build(packet),
            new OpenRouterExecutionRequest("workspace.import.interpreter", "system", "user"),
            new OpenRouterExecutionResponse(true, "SUMMARY: test", "openrouter/test", 200, null, "ok"),
            interpretation,
            null,
            "runtime summary");
        var service = new WorkspaceEvidenceArtifactRuntimeService(new ArchitectureDiagramRuntimeService());
        _ = service.WriteBundle(run);

        var documentRuntime = new ProjectDocumentRuntimeService();
        _ = documentRuntime.ConfirmPreviewDocs(root);

        var canonicalBundle = service.WriteBundle(run);
        var previewText = File.ReadAllText(canonicalBundle.PreviewPath);

        AssertContains(previewText, "Stage: Canonical", "Preview should switch to canonical stage when canonical docs exist.");
        AssertContains(previewText, "Source: canonical docs (project.md)", "Preview should read canonical main doc when it exists.");
        AssertContains(previewText, "Confirmed canonical project base materialized from", "Canonical preview should render canonical project markdown content.");
        AssertContains(previewText, "Source: canonical docs (capsule.md)", "Preview should read canonical capsule companion when it exists.");
    }
    finally
    {
        DeleteScratchWorkspace(root);
    }
}

static void WorkspaceEvidenceArtifactRuntimeWritesPreviewDocsHonestly()
{
    var root = CreateScratchWorkspace();
    try
    {
        Directory.CreateDirectory(Path.Combine(root, "src"));
        File.WriteAllText(Path.Combine(root, "src", "main.go"), "package main\nfunc main() {}");
        File.WriteAllText(Path.Combine(root, "README.md"), "Preview docs test case.");

        var scan = WorkspaceScanner.Scan(new WorkspaceScanRequest(root));
        var packet = new WorkspaceMaterialRuntimeFront().BuildPreviewPacket(scan, maxMaterials: 4, maxCharsPerMaterial: 96);
        var interpretation = WorkspaceImportMaterialInterpretationResultBuilder.BuildFromResponse(
            packet,
            new WorkspaceImportMaterialPromptResponse(
                "Single project summary.",
                new[] { "Project detail line." },
                Array.Empty<string>(),
                Array.Empty<string>(),
                Array.Empty<string>(),
                Array.Empty<string>(),
                Array.Empty<string>(),
                Array.Empty<WorkspaceImportMaterialLayerInterpretation>(),
                Array.Empty<WorkspaceImportMaterialModuleInterpretation>(),
                new[] { new WorkspaceImportMaterialEntryPointInterpretation(Path.Combine("src", "main.go"), "main", "Observed entry") },
                new ArchitectureDiagramSpec("Project Architecture", Array.Empty<ArchitectureDiagramNode>(), Array.Empty<ArchitectureDiagramEdge>(), Array.Empty<ArchitectureDiagramGroup>(), Array.Empty<string>(), new ArchitectureDiagramRenderHints("left-to-right", Array.Empty<string>(), true)),
                Array.Empty<WorkspaceImportMaterialPromptResponseItem>()));
        var run = new WorkspaceImportMaterialInterpreterRunResult(
            packet,
            WorkspaceImportMaterialPromptRequestBuilder.Build(packet),
            new OpenRouterExecutionRequest("workspace.import.interpreter", "system", "user"),
            new OpenRouterExecutionResponse(true, "SUMMARY: test", "openrouter/test", 200, null, "ok"),
            interpretation,
            null,
            "runtime summary");
        var service = new WorkspaceEvidenceArtifactRuntimeService(new ArchitectureDiagramRuntimeService());

        var bundle = service.WriteBundle(run);
        var previewProjectText = File.ReadAllText(bundle.PreviewProjectDocumentPath);
        var previewCapsuleText = File.ReadAllText(bundle.PreviewCapsuleDocumentPath);
        var previewDirectionPath = Path.Combine(Path.GetDirectoryName(bundle.PreviewProjectDocumentPath)!, "preview_direction.md");
        var previewDirectionText = File.ReadAllText(previewDirectionPath);
        var previewRoadmapPath = Path.Combine(Path.GetDirectoryName(bundle.PreviewProjectDocumentPath)!, "preview_roadmap.md");
        var previewRoadmapText = File.ReadAllText(previewRoadmapPath);
        var previewCanonPath = Path.Combine(Path.GetDirectoryName(bundle.PreviewProjectDocumentPath)!, "preview_canon.md");
        var previewCanonText = File.ReadAllText(previewCanonPath);

        AssertTrue(File.Exists(bundle.PreviewProjectDocumentPath), "Preview project document should be materialized beside shared import artifacts.");
        AssertTrue(File.Exists(bundle.PreviewCapsuleDocumentPath), "Preview capsule document should be materialized beside shared import artifacts.");
        AssertTrue(File.Exists(previewDirectionPath), "Preview direction document should be materialized once S3 direction writer exists.");
        AssertTrue(File.Exists(previewRoadmapPath), "Preview roadmap document should be materialized once S4 roadmap writer exists.");
        AssertTrue(File.Exists(previewCanonPath), "Preview canon document should be materialized once S2 observed canon writer exists.");
        AssertContains(previewProjectText, "# Project (Preview)", "Preview project should expose candidate-document heading.");
        AssertContains(previewProjectText, "## Identity", "Preview project should expose identity section.");
        AssertContains(previewProjectText, "Project Id: `", "Preview project should expose deterministic project id.");
        AssertContains(previewProjectText, "Project Name: `", "Preview project should expose deterministic project name.");
        AssertContains(previewProjectText, "## Scope and container mode", "Preview project should expose scope/container section.");
        AssertContains(previewProjectText, "## What this project appears to be", "Preview project should expose bounded human description.");
        AssertContains(previewProjectText, "## Observed structure", "Preview project should expose observed structure section.");
        AssertContains(previewProjectText, "## Runtime / stack signals", "Preview project should expose runtime signal section.");
        AssertContains(previewProjectText, "## What is confirmed / likely / unknown", "Preview project should expose explicit confidence split.");
        AssertContains(previewProjectText, "## Materials worth reading", "Preview project should expose materials section.");
        AssertContains(previewProjectText, "## Open uncertainty", "Preview project should expose uncertainty section.");
        AssertContains(previewProjectText, "## Canonical readiness", "Preview project should expose readiness section.");
        AssertTrue(previewProjectText.Split("- Confidence: `").Length - 1 >= 9, "Preview project should carry explicit confidence markers on every section.");
        AssertContains(previewProjectText, "Single project summary", "Preview project should preserve importer-owned summary.");
        AssertContains(previewProjectText, "not canonical truth yet", "Preview project should keep non-truth disclaimer.");
        AssertContains(previewCapsuleText, "# Capsule (Preview)", "Preview capsule should expose derived candidate heading.");
        AssertCapsuleV2Shape(previewCapsuleText, "preview");
        AssertContains(previewCapsuleText, "Reader obligation: source_stage preview is below canonical truth.", "Preview capsule must read below canonical truth.");
        AssertContains(previewCapsuleText, "- Source: `preview_project.md` [preview]", "Preview capsule should trace project section to preview project.");
        AssertContains(previewCapsuleText, "- Source: `preview_direction.md` [preview]", "Preview capsule should trace direction section to preview direction.");
        AssertContains(previewCapsuleText, "- Source: runtime overlay [runtime]", "Preview capsule should mark current focus as runtime overlay.");
        AssertContains(previewCapsuleText, "- Status: canonical 0/5, preview 5/5, absent 0/5", "Preview capsule should expose 5/5 preview status.");
        AssertContains(previewDirectionText, "# Direction (Preview)", "Preview direction should expose candidate-document heading.");
        AssertContains(previewDirectionText, "## Likely / candidate direction signals", "Preview direction should expose candidate signals when README exists.");
        AssertContains(previewDirectionText, "README", "Preview direction should trace to README evidence without copying README body.");
        AssertContains(previewRoadmapText, "# Roadmap (Preview)", "Preview roadmap should expose candidate-document heading.");
        AssertContains(previewRoadmapText, "## Unknown / not-yet-established", "Preview roadmap should expose Unknown section when git history is unavailable.");
        AssertContains(previewCanonText, "# Canon (Preview)", "Preview canon should expose candidate-document heading.");
        AssertContains(previewCanonText, "## Observed technical signals", "Preview canon should expose observed technical signals section.");
        AssertContains(previewCanonText, "## Contributor-authored rules", "Preview canon should expose empty contributor-owned section.");
        AssertContains(previewCanonText, "## Unknown / not-yet-established", "Preview canon should expose explicit gap section.");
    }
    finally
    {
        DeleteScratchWorkspace(root);
    }
}

static void ProjectDocumentRuntimeWritesBoundedContainerProjectPreviewHonestly()
{
    var root = CreateScratchWorkspace();
    try
    {
        Directory.CreateDirectory(Path.Combine(root, "alpha", ".git"));
        Directory.CreateDirectory(Path.Combine(root, "alpha", "src"));
        Directory.CreateDirectory(Path.Combine(root, "beta", ".git"));
        Directory.CreateDirectory(Path.Combine(root, "beta", "cmd"));
        File.WriteAllText(Path.Combine(root, "alpha", "src", "main.ts"), "export function main() {}");
        File.WriteAllText(Path.Combine(root, "alpha", "package.json"), "{ \"name\": \"alpha\" }");
        File.WriteAllText(Path.Combine(root, "beta", "cmd", "main.go"), "package main\nfunc main() {}");
        File.WriteAllText(Path.Combine(root, "beta", "go.mod"), "module beta");

        var scan = WorkspaceScanner.Scan(new WorkspaceScanRequest(root));
        var packet = new WorkspaceMaterialRuntimeFront().BuildPreviewPacket(scan, maxMaterials: 6, maxCharsPerMaterial: 96);
        var response = new WorkspaceImportMaterialPromptResponse(
            "A unified platform architecture spans both repos with shared runtime and service layers.",
            new[] { "The project exposes one layered architecture across both roots." },
            new[] { "Project is in coordinated rollout stage." },
            new[] { "Shared platform runtime is current." },
            Array.Empty<string>(),
            Array.Empty<string>(),
            Array.Empty<string>(),
            new[] { new WorkspaceImportMaterialLayerInterpretation("Service", "invented service", "folder-derived") },
            new[] { new WorkspaceImportMaterialModuleInterpretation("Platform", "module", "folder-derived") },
            new[]
            {
                new WorkspaceImportMaterialEntryPointInterpretation(Path.Combine("alpha", "src", "main.ts"), "main", "Alpha entry"),
                new WorkspaceImportMaterialEntryPointInterpretation(Path.Combine("beta", "cmd", "main.go"), "main", "Beta entry")
            },
            new ArchitectureDiagramSpec(
                "Unified Project Architecture",
                new[] { new ArchitectureDiagramNode("service", "Service", "layer") },
                Array.Empty<ArchitectureDiagramEdge>(),
                Array.Empty<ArchitectureDiagramGroup>(),
                new[] { "One architecture spans both repos." },
                new ArchitectureDiagramRenderHints("left-to-right", Array.Empty<string>(), true)),
            Array.Empty<WorkspaceImportMaterialPromptResponseItem>());
        var interpretation = WorkspaceImportMaterialInterpretationResultBuilder.BuildFromResponse(packet, response);
        var run = new WorkspaceImportMaterialInterpreterRunResult(
            packet,
            WorkspaceImportMaterialPromptRequestBuilder.Build(packet),
            new OpenRouterExecutionRequest("workspace.import.interpreter", "system", "user"),
            new OpenRouterExecutionResponse(true, "SUMMARY: test", "openrouter/test", 200, null, "ok"),
            interpretation,
            null,
            "runtime summary");
        var documentRuntime = new ProjectDocumentRuntimeService();

        var artifacts = documentRuntime.WritePreviewDocs(run, root);
        var previewProjectText = File.ReadAllText(artifacts.PreviewProjectPath);

        AssertContains(previewProjectText, "Interpretation Mode: `MultipleIndependentProjects`", "Container project preview should expose interpretation mode.");
        AssertContains(previewProjectText, "Scanner Topology: `Container`", "Container project preview should preserve scanner topology.");
        AssertContains(previewProjectText, "Safe Import Mode: `container-review", "Container project preview should preserve safe import mode.");
        AssertContains(previewProjectText, "Unified architecture across the whole folder is not confirmed.", "Container project preview must avoid unified architecture claims.");
        AssertContains(previewProjectText, "Unified module map is suppressed for this container.", "Container project preview should suppress unified module projection.");
        AssertContains(previewProjectText, "evidence remains too coarse for a strong unified truth claim", "Container project preview should keep readiness unknown/coarse.");
        AssertFalse(previewProjectText.Contains("A unified platform architecture spans both repos", StringComparison.OrdinalIgnoreCase), "Container project preview must not preserve inflated summary wording.");
        AssertFalse(previewProjectText.Contains("shared runtime and service layers", StringComparison.OrdinalIgnoreCase), "Container project preview must not preserve inflated shared-runtime wording.");
    }
    finally
    {
        DeleteScratchWorkspace(root);
    }
}

static void ProjectDocumentRuntimePreservesNonstandardTopologyInProjectPreviewHonestly()
{
    var root = CreateScratchWorkspace();
    try
    {
        File.WriteAllText(Path.Combine(root, "Makefile"), "all:\n\trgbasm -o main.o main.asm");
        File.WriteAllText(Path.Combine(root, "main.asm"), "SECTION \"start\", ROM0\nnop");

        var scan = WorkspaceScanner.Scan(new WorkspaceScanRequest(root));
        var packet = new WorkspaceMaterialRuntimeFront().BuildPreviewPacket(scan, maxMaterials: 4, maxCharsPerMaterial: 96);
        var interpretation = WorkspaceImportMaterialInterpretationResultBuilder.BuildFromResponse(
            packet,
            new WorkspaceImportMaterialPromptResponse(
                "Normal application summary should stay preview-only.",
                Array.Empty<string>(),
                Array.Empty<string>(),
                Array.Empty<string>(),
                Array.Empty<string>(),
                Array.Empty<string>(),
                Array.Empty<string>(),
                Array.Empty<WorkspaceImportMaterialLayerInterpretation>(),
                Array.Empty<WorkspaceImportMaterialModuleInterpretation>(),
                new[] { new WorkspaceImportMaterialEntryPointInterpretation("main.asm", "main", "assembly entry candidate") },
                new ArchitectureDiagramSpec("Project Architecture", Array.Empty<ArchitectureDiagramNode>(), Array.Empty<ArchitectureDiagramEdge>(), Array.Empty<ArchitectureDiagramGroup>(), Array.Empty<string>(), new ArchitectureDiagramRenderHints("left-to-right", Array.Empty<string>(), true)),
                Array.Empty<WorkspaceImportMaterialPromptResponseItem>()));
        var run = new WorkspaceImportMaterialInterpreterRunResult(
            packet,
            WorkspaceImportMaterialPromptRequestBuilder.Build(packet),
            new OpenRouterExecutionRequest("workspace.import.interpreter", "system", "user"),
            new OpenRouterExecutionResponse(true, "SUMMARY: test", "openrouter/test", 200, null, "ok"),
            interpretation,
            null,
            "runtime summary");

        var documentRuntime = new ProjectDocumentRuntimeService();
        var artifacts = documentRuntime.WritePreviewDocs(run, root);
        var previewProjectText = File.ReadAllText(artifacts.PreviewProjectPath);

        AssertContains(previewProjectText, "Scanner Topology: `Legacy`", "Preview project should preserve nonstandard scanner topology.");
        AssertContains(previewProjectText, "Safe Import Mode: `legacy-low-level-source-review", "Preview project should preserve safe import mode.");
        AssertContains(previewProjectText, "normal single-application assumptions are not confirmed", "Nonstandard topology should block normal app assumptions.");
        AssertContains(previewProjectText, "Candidate entry surface: `main.asm`", "Likely nonstandard entry should not be labeled as Main Entry.");
        AssertFalse(previewProjectText.Contains("- Current preview looks bounded enough", StringComparison.OrdinalIgnoreCase), "Nonstandard topology should not look promotion-ready by default.");
    }
    finally
    {
        DeleteScratchWorkspace(root);
    }
}

static void ProjectDocumentRuntimePromotesNonstandardTopologyPreviewHonestly()
{
    var root = CreateScratchWorkspace();
    try
    {
        File.WriteAllText(Path.Combine(root, "Makefile"), "all:\n\trgbasm -o main.o main.asm");
        File.WriteAllText(Path.Combine(root, "main.asm"), "SECTION \"start\", ROM0\nnop");

        var scan = WorkspaceScanner.Scan(new WorkspaceScanRequest(root));
        var packet = new WorkspaceMaterialRuntimeFront().BuildPreviewPacket(scan, maxMaterials: 4, maxCharsPerMaterial: 96);
        var interpretation = WorkspaceImportMaterialInterpretationResultBuilder.BuildFromResponse(
            packet,
            new WorkspaceImportMaterialPromptResponse(
                "Normal application summary should stay bounded.",
                Array.Empty<string>(),
                Array.Empty<string>(),
                Array.Empty<string>(),
                Array.Empty<string>(),
                Array.Empty<string>(),
                Array.Empty<string>(),
                Array.Empty<WorkspaceImportMaterialLayerInterpretation>(),
                Array.Empty<WorkspaceImportMaterialModuleInterpretation>(),
                new[] { new WorkspaceImportMaterialEntryPointInterpretation("main.asm", "main", "assembly entry candidate") },
                new ArchitectureDiagramSpec("Project Architecture", Array.Empty<ArchitectureDiagramNode>(), Array.Empty<ArchitectureDiagramEdge>(), Array.Empty<ArchitectureDiagramGroup>(), Array.Empty<string>(), new ArchitectureDiagramRenderHints("left-to-right", Array.Empty<string>(), true)),
                Array.Empty<WorkspaceImportMaterialPromptResponseItem>()));
        var run = new WorkspaceImportMaterialInterpreterRunResult(
            packet,
            WorkspaceImportMaterialPromptRequestBuilder.Build(packet),
            new OpenRouterExecutionRequest("workspace.import.interpreter", "system", "user"),
            new OpenRouterExecutionResponse(true, "SUMMARY: test", "openrouter/test", 200, null, "ok"),
            interpretation,
            null,
            "runtime summary");

        var documentRuntime = new ProjectDocumentRuntimeService();
        _ = documentRuntime.WritePreviewDocs(run, root);
        var promotion = documentRuntime.PromotePreviewDoc(root, ProjectDocumentKind.Project, "test-contributor");
        var canonicalProjectText = File.ReadAllText(promotion.CanonicalDocumentPath);
        var decisionText = File.ReadAllText(promotion.DecisionPath);
        var journalText = File.ReadAllText(promotion.JournalPath);

        AssertContains(canonicalProjectText, "# Project", "Promoted nonstandard project doc should use canonical heading.");
        AssertContains(canonicalProjectText, "Confirmed canonical project base materialized from `preview_project.md`.", "Promoted nonstandard project doc should expose promotion provenance.");
        AssertContains(canonicalProjectText, "Scanner Topology: `Legacy`", "Promoted nonstandard project doc should preserve scanner topology.");
        AssertContains(canonicalProjectText, "Safe Import Mode: `legacy-low-level-source-review", "Promoted nonstandard project doc should preserve safe import mode.");
        AssertContains(canonicalProjectText, "normal single-application assumptions are not confirmed", "Promoted nonstandard topology must not become a normal application claim.");
        AssertContains(canonicalProjectText, "Candidate entry surface: `main.asm`", "Promoted likely nonstandard entry should not be relabeled as Main Entry.");
        AssertFalse(File.Exists(Path.Combine(root, ".zavod", "project", "direction.md")), "Promoting project alone must not create unrelated canonical direction.md.");
        AssertFalse(File.Exists(Path.Combine(root, ".zavod", "project", "roadmap.md")), "Promoting project alone must not create unrelated canonical roadmap.md.");
        AssertFalse(File.Exists(Path.Combine(root, ".zavod", "project", "canon.md")), "Promoting project alone must not create unrelated canonical canon.md.");
        AssertFalse(File.Exists(Path.Combine(root, ".zavod", "project", "capsule.md")), "Promoting project alone must not create unrelated canonical capsule.md.");
        AssertContains(decisionText, "type: canonical_promotion", "Nonstandard topology promotion should write a canonical_promotion decision.");
        AssertContains(decisionText, "# Promote project.md", "Promotion decision should identify project.md.");
        AssertContains(decisionText, "contributor: test-contributor", "Promotion decision should preserve contributor attribution.");
        AssertContains(decisionText, promotion.PreviewSha256, "Promotion decision should record the promoted preview hash.");
        AssertContains(journalText, "\"event_type\":\"decision_recorded\"", "Promotion journal should record decision_recorded.");
        AssertContains(journalText, "\"event_type\":\"canonical_promoted\"", "Promotion journal should record canonical_promoted.");
        AssertContains(journalText, "\"kind\":\"project\"", "Promotion journal should identify the promoted project kind.");
        AssertContains(journalText, promotion.PreviewSha256, "Promotion journal should record the promoted preview hash.");
    }
    finally
    {
        DeleteScratchWorkspace(root);
    }
}

static void ProjectDocumentRuntimeKeepsProjectPreviewIdentityStableOnReimportHonestly()
{
    var root = CreateScratchWorkspace();
    try
    {
        Directory.CreateDirectory(Path.Combine(root, "src"));
        File.WriteAllText(Path.Combine(root, "src", "main.go"), "package main\nfunc main() {}");

        var scan = WorkspaceScanner.Scan(new WorkspaceScanRequest(root));
        var packet = new WorkspaceMaterialRuntimeFront().BuildPreviewPacket(scan, maxMaterials: 4, maxCharsPerMaterial: 96);
        var interpretation = WorkspaceImportMaterialInterpretationResultBuilder.BuildFromResponse(
            packet,
            new WorkspaceImportMaterialPromptResponse(
                "Single project summary.",
                new[] { "Project detail line." },
                Array.Empty<string>(),
                Array.Empty<string>(),
                Array.Empty<string>(),
                Array.Empty<string>(),
                Array.Empty<string>(),
                Array.Empty<WorkspaceImportMaterialLayerInterpretation>(),
                Array.Empty<WorkspaceImportMaterialModuleInterpretation>(),
                new[] { new WorkspaceImportMaterialEntryPointInterpretation(Path.Combine("src", "main.go"), "main", "Observed entry") },
                new ArchitectureDiagramSpec("Project Architecture", Array.Empty<ArchitectureDiagramNode>(), Array.Empty<ArchitectureDiagramEdge>(), Array.Empty<ArchitectureDiagramGroup>(), Array.Empty<string>(), new ArchitectureDiagramRenderHints("left-to-right", Array.Empty<string>(), true)),
                Array.Empty<WorkspaceImportMaterialPromptResponseItem>()));
        var firstRun = new WorkspaceImportMaterialInterpreterRunResult(
            packet,
            WorkspaceImportMaterialPromptRequestBuilder.Build(packet),
            new OpenRouterExecutionRequest("workspace.import.interpreter", "system", "user"),
            new OpenRouterExecutionResponse(true, "SUMMARY: test", "openrouter/test", 200, null, "ok"),
            interpretation,
            null,
            "runtime summary");
        var documentRuntime = new ProjectDocumentRuntimeService();

        var firstArtifacts = documentRuntime.WritePreviewDocs(firstRun, root);
        var firstIdentity = ExtractMarkdownSection(File.ReadAllText(firstArtifacts.PreviewProjectPath), "## Identity");

        Directory.CreateDirectory(Path.Combine(root, "tools"));
        File.WriteAllText(Path.Combine(root, "tools", "tool.go"), "package main\nfunc main() {}");
        var secondPacket = packet with { SourceRoots = packet.SourceRoots.Concat(new[] { "tools" }).ToArray() };
        var secondInterpretation = interpretation with { SourceRoots = interpretation.SourceRoots.Concat(new[] { "tools" }).ToArray() };
        var secondRun = firstRun with
        {
            PreviewPacket = secondPacket,
            PromptRequest = WorkspaceImportMaterialPromptRequestBuilder.Build(secondPacket),
            Interpretation = secondInterpretation
        };

        var secondArtifacts = documentRuntime.WritePreviewDocs(secondRun, root);
        var secondIdentity = ExtractMarkdownSection(File.ReadAllText(secondArtifacts.PreviewProjectPath), "## Identity");

        AssertEqual(firstIdentity, secondIdentity, "Preview project identity block must stay stable when source roots change under the same workspace root.");
    }
    finally
    {
        DeleteScratchWorkspace(root);
    }
}

static void ProjectDocumentRuntimeWritesObservedCanonPreviewHonestly()
{
    var root = CreateScratchWorkspace();
    try
    {
        Directory.CreateDirectory(Path.Combine(root, "src"));
        File.WriteAllText(Path.Combine(root, "src", "main.cs"), "using System;\nConsole.WriteLine(\"hi\");");
        File.WriteAllText(Path.Combine(root, "zavod.csproj"), "<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup><TargetFramework>net8.0</TargetFramework></PropertyGroup></Project>");

        var scan = WorkspaceScanner.Scan(new WorkspaceScanRequest(root));
        var packet = new WorkspaceMaterialRuntimeFront().BuildPreviewPacket(scan, maxMaterials: 4, maxCharsPerMaterial: 96);
        var interpretation = WorkspaceImportMaterialInterpretationResultBuilder.BuildFromResponse(
            packet,
            new WorkspaceImportMaterialPromptResponse(
                "Single project summary.",
                new[] { "Project detail line." },
                Array.Empty<string>(),
                Array.Empty<string>(),
                Array.Empty<string>(),
                Array.Empty<string>(),
                Array.Empty<string>(),
                new[] { new WorkspaceImportMaterialLayerInterpretation("Runtime", "Observed runtime files.", "Observed from source/build files.", WorkspaceEvidenceConfidenceLevel.Likely) },
                new[] { new WorkspaceImportMaterialModuleInterpretation("Runtime", "entry/runtime", "Observed from source/build files.", WorkspaceEvidenceConfidenceLevel.Likely) },
                new[] { new WorkspaceImportMaterialEntryPointInterpretation(Path.Combine("src", "main.cs"), "main", "Observed entry", WorkspaceEvidenceConfidenceLevel.Likely) },
                new ArchitectureDiagramSpec("Project Architecture", Array.Empty<ArchitectureDiagramNode>(), Array.Empty<ArchitectureDiagramEdge>(), Array.Empty<ArchitectureDiagramGroup>(), Array.Empty<string>(), new ArchitectureDiagramRenderHints("left-to-right", Array.Empty<string>(), true)),
                Array.Empty<WorkspaceImportMaterialPromptResponseItem>()));
        var run = new WorkspaceImportMaterialInterpreterRunResult(
            packet,
            WorkspaceImportMaterialPromptRequestBuilder.Build(packet),
            new OpenRouterExecutionRequest("workspace.import.interpreter", "system", "user"),
            new OpenRouterExecutionResponse(true, "SUMMARY: test", "openrouter/test", 200, null, "ok"),
            interpretation,
            null,
            "runtime summary");
        var documentRuntime = new ProjectDocumentRuntimeService();

        var artifacts = documentRuntime.WritePreviewDocs(run, root);
        var previewCanonText = File.ReadAllText(artifacts.PreviewCanonPath);

        AssertContains(previewCanonText, "# Canon (Preview)", "Preview canon should expose candidate-document heading.");
        AssertContains(previewCanonText, "This document is not canonical truth yet.", "Preview canon should keep non-truth disclaimer.");
        AssertContains(previewCanonText, "## Observed technical signals", "Preview canon must have observed technical signals section.");
        AssertContains(previewCanonText, "Evidence Boundary: Derived from TechnicalPassport", "Preview canon should name its bounded data source.");
        AssertContains(previewCanonText, "Observed Entry Points", "Preview canon should preserve observed entry-point evidence.");
        AssertContains(previewCanonText, "## Contributor-authored rules", "Preview canon must keep contributor-authored section separate.");
        AssertContains(previewCanonText, "No authored rules yet. Contributor must add review rules / execution rules / intent rules here.", "Preview canon must not fabricate authored rules.");
        AssertContains(previewCanonText, "## Unknown / not-yet-established", "Preview canon must have unknown gap section.");
        AssertContains(previewCanonText, "What is not yet canonical: review workflow.", "Preview canon should expose missing review workflow as a gap.");
        AssertContains(previewCanonText, "What is not yet canonical: execution boundaries.", "Preview canon should expose missing execution boundaries as a gap.");
        AssertFalse(previewCanonText.Contains("Rule:", StringComparison.OrdinalIgnoreCase), "Preview canon must not synthesize rule lines from observed evidence.");
        AssertFalse(previewCanonText.Contains("must never be broken", StringComparison.OrdinalIgnoreCase), "Preview canon must not invent architectural law from observed evidence.");
        AssertFalse(File.Exists(Path.Combine(root, ".zavod", "project", "canon.md")), "Preview canon writer must not silently promote canonical canon.md.");
    }
    finally
    {
        DeleteScratchWorkspace(root);
    }
}

static void ProjectDocumentRuntimeWritesCandidateDirectionPreviewHonestly()
{
    var root = CreateScratchWorkspace();
    const string readmeBody = "The project will conquer enterprise dashboards with launch-wave automation.";
    try
    {
        Directory.CreateDirectory(Path.Combine(root, "src"));
        File.WriteAllText(Path.Combine(root, "src", "main.go"), "package main\nfunc main() {}");
        File.WriteAllText(Path.Combine(root, "README.md"), readmeBody);

        var scan = WorkspaceScanner.Scan(new WorkspaceScanRequest(root));
        var packet = new WorkspaceMaterialRuntimeFront().BuildPreviewPacket(scan, maxMaterials: 4, maxCharsPerMaterial: 256);
        var interpretation = WorkspaceImportMaterialInterpretationResultBuilder.BuildFromResponse(
            packet,
            new WorkspaceImportMaterialPromptResponse(
                "Single project summary.",
                new[] { "Project detail line." },
                Array.Empty<string>(),
                Array.Empty<string>(),
                Array.Empty<string>(),
                Array.Empty<string>(),
                Array.Empty<string>(),
                Array.Empty<WorkspaceImportMaterialLayerInterpretation>(),
                new[] { new WorkspaceImportMaterialModuleInterpretation("Runtime", "entry/runtime", "Observed module.", WorkspaceEvidenceConfidenceLevel.Likely) },
                new[] { new WorkspaceImportMaterialEntryPointInterpretation(Path.Combine("src", "main.go"), "main", "Observed entry", WorkspaceEvidenceConfidenceLevel.Likely) },
                new ArchitectureDiagramSpec("Project Architecture", Array.Empty<ArchitectureDiagramNode>(), Array.Empty<ArchitectureDiagramEdge>(), Array.Empty<ArchitectureDiagramGroup>(), Array.Empty<string>(), new ArchitectureDiagramRenderHints("left-to-right", Array.Empty<string>(), true)),
                Array.Empty<WorkspaceImportMaterialPromptResponseItem>()));
        var run = new WorkspaceImportMaterialInterpreterRunResult(
            packet,
            WorkspaceImportMaterialPromptRequestBuilder.Build(packet),
            new OpenRouterExecutionRequest("workspace.import.interpreter", "system", "user"),
            new OpenRouterExecutionResponse(true, "SUMMARY: test", "openrouter/test", 200, null, "ok"),
            interpretation,
            null,
            "runtime summary");
        var documentRuntime = new ProjectDocumentRuntimeService();

        var artifacts = documentRuntime.WritePreviewDocs(run, root);
        var previewDirectionText = File.ReadAllText(artifacts.PreviewDirectionPath);

        AssertContains(previewDirectionText, "# Direction (Preview)", "Preview direction should expose candidate-document heading.");
        AssertContains(previewDirectionText, "## Confirmed direction", "Preview direction should expose confirmed split.");
        AssertContains(previewDirectionText, "No confirmed direction statement is derived automatically.", "Preview direction should not claim confirmed intent.");
        AssertContains(previewDirectionText, "## Likely / candidate direction signals", "Preview direction should expose candidate split.");
        AssertContains(previewDirectionText, "material `README.md`", "Preview direction should trace candidate signals to README path.");
        AssertContains(previewDirectionText, "entry point `src", "Preview direction should trace entry point evidence.");
        AssertContains(previewDirectionText, "Contributor may reject, rewrite, or author direction from scratch before promotion.", "Preview direction should keep contributor control explicit.");
        AssertFalse(previewDirectionText.Contains(readmeBody, StringComparison.Ordinal), "Preview direction must not copy README body verbatim.");
        AssertFalse(previewDirectionText.Contains("the project will", StringComparison.OrdinalIgnoreCase), "Preview direction must not present README aspiration as system-known intent.");
        AssertFalse(File.Exists(Path.Combine(root, ".zavod", "project", "direction.md")), "Preview direction writer must not silently promote canonical direction.md.");
    }
    finally
    {
        DeleteScratchWorkspace(root);
    }
}

static void ProjectDocumentRuntimeWritesUnknownOnlyDirectionWithoutReadmeHonestly()
{
    var root = CreateScratchWorkspace();
    try
    {
        Directory.CreateDirectory(Path.Combine(root, "src"));
        File.WriteAllText(Path.Combine(root, "src", "main.go"), "package main\nfunc main() {}");

        var scan = WorkspaceScanner.Scan(new WorkspaceScanRequest(root));
        var packet = new WorkspaceMaterialRuntimeFront().BuildPreviewPacket(scan, maxMaterials: 4, maxCharsPerMaterial: 96);
        var interpretation = WorkspaceImportMaterialInterpretationResultBuilder.BuildFromResponse(
            packet,
            new WorkspaceImportMaterialPromptResponse(
                "Single project summary.",
                new[] { "Project detail line." },
                Array.Empty<string>(),
                Array.Empty<string>(),
                Array.Empty<string>(),
                Array.Empty<string>(),
                Array.Empty<string>(),
                Array.Empty<WorkspaceImportMaterialLayerInterpretation>(),
                Array.Empty<WorkspaceImportMaterialModuleInterpretation>(),
                new[] { new WorkspaceImportMaterialEntryPointInterpretation(Path.Combine("src", "main.go"), "main", "Observed entry", WorkspaceEvidenceConfidenceLevel.Likely) },
                new ArchitectureDiagramSpec("Project Architecture", Array.Empty<ArchitectureDiagramNode>(), Array.Empty<ArchitectureDiagramEdge>(), Array.Empty<ArchitectureDiagramGroup>(), Array.Empty<string>(), new ArchitectureDiagramRenderHints("left-to-right", Array.Empty<string>(), true)),
                Array.Empty<WorkspaceImportMaterialPromptResponseItem>()));
        var run = new WorkspaceImportMaterialInterpreterRunResult(
            packet,
            WorkspaceImportMaterialPromptRequestBuilder.Build(packet),
            new OpenRouterExecutionRequest("workspace.import.interpreter", "system", "user"),
            new OpenRouterExecutionResponse(true, "SUMMARY: test", "openrouter/test", 200, null, "ok"),
            interpretation,
            null,
            "runtime summary");
        var documentRuntime = new ProjectDocumentRuntimeService();

        var artifacts = documentRuntime.WritePreviewDocs(run, root);
        var previewDirectionText = File.ReadAllText(artifacts.PreviewDirectionPath);

        AssertContains(previewDirectionText, "# Direction (Preview)", "Preview direction should still be materialized without README evidence.");
        AssertContains(previewDirectionText, "## Unknown / not-yet-established", "No-README direction preview should contain Unknown section.");
        AssertContains(previewDirectionText, "No README/overview material was imported for direction evidence.", "No-README direction preview should state the blocker.");
        AssertFalse(previewDirectionText.Contains("## Confirmed direction", StringComparison.Ordinal), "No-README direction preview must not include confirmed section.");
        AssertFalse(previewDirectionText.Contains("## Likely / candidate direction signals", StringComparison.Ordinal), "No-README direction preview must not include candidate section.");
        AssertFalse(File.Exists(Path.Combine(root, ".zavod", "project", "direction.md")), "No-README direction preview must not silently promote canonical direction.md.");
    }
    finally
    {
        DeleteScratchWorkspace(root);
    }
}

static void ProjectDocumentRuntimeWritesCandidateRoadmapPreviewHonestly()
{
    var root = CreateScratchWorkspace();
    try
    {
        Directory.CreateDirectory(Path.Combine(root, ".git"));
        Directory.CreateDirectory(Path.Combine(root, "src"));
        File.WriteAllText(Path.Combine(root, "src", "main.go"), "package main\nfunc main() {}");

        var scan = WorkspaceScanner.Scan(new WorkspaceScanRequest(root));
        var packet = new WorkspaceMaterialRuntimeFront().BuildPreviewPacket(scan, maxMaterials: 4, maxCharsPerMaterial: 96);
        var interpretation = WorkspaceImportMaterialInterpretationResultBuilder.BuildFromResponse(
            packet,
            new WorkspaceImportMaterialPromptResponse(
                "Single project summary.",
                new[] { "Project detail line." },
                Array.Empty<string>(),
                Array.Empty<string>(),
                Array.Empty<string>(),
                Array.Empty<string>(),
                Array.Empty<string>(),
                Array.Empty<WorkspaceImportMaterialLayerInterpretation>(),
                Array.Empty<WorkspaceImportMaterialModuleInterpretation>(),
                new[] { new WorkspaceImportMaterialEntryPointInterpretation(Path.Combine("src", "main.go"), "main", "Observed entry", WorkspaceEvidenceConfidenceLevel.Likely) },
                new ArchitectureDiagramSpec("Project Architecture", Array.Empty<ArchitectureDiagramNode>(), Array.Empty<ArchitectureDiagramEdge>(), Array.Empty<ArchitectureDiagramGroup>(), Array.Empty<string>(), new ArchitectureDiagramRenderHints("left-to-right", Array.Empty<string>(), true)),
                Array.Empty<WorkspaceImportMaterialPromptResponseItem>()));
        var run = new WorkspaceImportMaterialInterpreterRunResult(
            packet,
            WorkspaceImportMaterialPromptRequestBuilder.Build(packet),
            new OpenRouterExecutionRequest("workspace.import.interpreter", "system", "user"),
            new OpenRouterExecutionResponse(true, "SUMMARY: test", "openrouter/test", 200, null, "ok"),
            interpretation,
            null,
            "runtime summary");
        var gitReader = new GitRoadmapHistoryReader(new FakeExternalProcessRunner(request => request.Purpose switch
        {
            "roadmap_git_log" => new ExternalProcessResult(
                0,
                "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa\u001ffeat: import preview docs\nbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb\u001fphase-2 harden docs pipeline",
                string.Empty,
                false),
            "roadmap_git_tags" => new ExternalProcessResult(0, "v1.0.0", string.Empty, false),
            "roadmap_git_branches" => new ExternalProcessResult(0, "phase-3-candidate", string.Empty, false),
            _ => new ExternalProcessResult(1, string.Empty, $"Unexpected purpose: {request.Purpose}", false)
        }));
        var documentRuntime = new ProjectDocumentRuntimeService(gitReader);

        var artifacts = documentRuntime.WritePreviewDocs(run, root);
        var previewRoadmapText = File.ReadAllText(artifacts.PreviewRoadmapPath);

        AssertContains(previewRoadmapText, "# Roadmap (Preview)", "Preview roadmap should expose candidate-document heading.");
        AssertContains(previewRoadmapText, "## Candidate phases", "Preview roadmap should expose candidate phase section when git history exists.");
        AssertContains(previewRoadmapText, "Candidate phase from commit aaaaaaaaaaaa. Contributor must confirm or replace.", "Preview roadmap should trace candidate commit evidence.");
        AssertContains(previewRoadmapText, "Candidate phase from tag v1.0.0. Contributor must confirm or replace.", "Preview roadmap should trace tag evidence.");
        AssertContains(previewRoadmapText, "Candidate phase from branch phase-3-candidate. Contributor must confirm or replace.", "Preview roadmap should trace branch evidence.");
        AssertContains(previewRoadmapText, "Done criteria are not derivable from git history.", "Preview roadmap should keep done criteria unknown.");
        AssertFalse(previewRoadmapText.Contains("[Confirmed]", StringComparison.OrdinalIgnoreCase), "Preview roadmap must not mark phases confirmed.");
        AssertFalse(previewRoadmapText.Contains("[Likely]", StringComparison.OrdinalIgnoreCase), "Preview roadmap must not mark phases likely.");
        AssertFalse(previewRoadmapText.Contains("the project will", StringComparison.OrdinalIgnoreCase), "Preview roadmap must not claim project intent.");
        AssertFalse(previewRoadmapText.Contains("next we plan to", StringComparison.OrdinalIgnoreCase), "Preview roadmap must not claim plan intent.");
        AssertFalse(previewRoadmapText.Contains("Priority", StringComparison.OrdinalIgnoreCase), "Preview roadmap must not rank candidate phases by priority.");
        AssertFalse(File.Exists(Path.Combine(root, ".zavod", "project", "roadmap.md")), "Preview roadmap writer must not silently promote canonical roadmap.md.");
    }
    finally
    {
        DeleteScratchWorkspace(root);
    }
}

static void ProjectDocumentRuntimeWritesUnknownOnlyRoadmapWithoutGitHonestly()
{
    var root = CreateScratchWorkspace();
    try
    {
        Directory.CreateDirectory(Path.Combine(root, "src"));
        File.WriteAllText(Path.Combine(root, "src", "main.go"), "package main\nfunc main() {}");

        var scan = WorkspaceScanner.Scan(new WorkspaceScanRequest(root));
        var packet = new WorkspaceMaterialRuntimeFront().BuildPreviewPacket(scan, maxMaterials: 4, maxCharsPerMaterial: 96);
        var interpretation = WorkspaceImportMaterialInterpretationResultBuilder.BuildFromResponse(
            packet,
            new WorkspaceImportMaterialPromptResponse(
                "Single project summary.",
                new[] { "Project detail line." },
                Array.Empty<string>(),
                Array.Empty<string>(),
                Array.Empty<string>(),
                Array.Empty<string>(),
                Array.Empty<string>(),
                Array.Empty<WorkspaceImportMaterialLayerInterpretation>(),
                Array.Empty<WorkspaceImportMaterialModuleInterpretation>(),
                new[] { new WorkspaceImportMaterialEntryPointInterpretation(Path.Combine("src", "main.go"), "main", "Observed entry", WorkspaceEvidenceConfidenceLevel.Likely) },
                new ArchitectureDiagramSpec("Project Architecture", Array.Empty<ArchitectureDiagramNode>(), Array.Empty<ArchitectureDiagramEdge>(), Array.Empty<ArchitectureDiagramGroup>(), Array.Empty<string>(), new ArchitectureDiagramRenderHints("left-to-right", Array.Empty<string>(), true)),
                Array.Empty<WorkspaceImportMaterialPromptResponseItem>()));
        var run = new WorkspaceImportMaterialInterpreterRunResult(
            packet,
            WorkspaceImportMaterialPromptRequestBuilder.Build(packet),
            new OpenRouterExecutionRequest("workspace.import.interpreter", "system", "user"),
            new OpenRouterExecutionResponse(true, "SUMMARY: test", "openrouter/test", 200, null, "ok"),
            interpretation,
            null,
            "runtime summary");
        var documentRuntime = new ProjectDocumentRuntimeService(new GitRoadmapHistoryReader(new FakeExternalProcessRunner(_ => new ExternalProcessResult(1, string.Empty, "unexpected", false))));

        var artifacts = documentRuntime.WritePreviewDocs(run, root);
        var previewRoadmapText = File.ReadAllText(artifacts.PreviewRoadmapPath);

        AssertContains(previewRoadmapText, "# Roadmap (Preview)", "Preview roadmap should still be materialized without git history.");
        AssertContains(previewRoadmapText, "## Unknown / not-yet-established", "No-git roadmap preview should contain Unknown section.");
        AssertContains(previewRoadmapText, "No git history was available at the project root.", "No-git roadmap preview should state the blocker.");
        AssertFalse(previewRoadmapText.Contains("## Candidate phases", StringComparison.Ordinal), "No-git roadmap preview must not include candidate section.");
        AssertFalse(File.Exists(Path.Combine(root, ".zavod", "project", "roadmap.md")), "No-git roadmap preview must not silently promote canonical roadmap.md.");
    }
    finally
    {
        DeleteScratchWorkspace(root);
    }
}

static void ProjectDocumentSourceSelectorResolvesStagesHonestly()
{
    var root = CreateScratchWorkspace();
    try
    {
        Directory.CreateDirectory(Path.Combine(root, ".zavod", "import_evidence_bundle"));
        File.WriteAllText(Path.Combine(root, ".zavod", "import_evidence_bundle", "project_report.md"), "# Import Report");

        var documentRuntime = new ProjectDocumentRuntimeService();
        var importSelection = documentRuntime.SelectSources(root);
        AssertEqual(ProjectDocumentStage.ImportPreview, importSelection.ActiveStage, "Selector should begin at import preview when only project_report exists.");
        AssertTrue(importSelection.ProjectDocument is not null, "Import preview selection should expose a project-source descriptor.");
        AssertContains(importSelection.ProjectDocument!.Path, "project_report.md", "Import preview should point to project_report.md.");

        ProjectDocumentPathResolver.EnsurePreviewDocsRoot(root);
        File.WriteAllText(ProjectDocumentPathResolver.GetPreviewProjectPath(root), "# Project Preview");
        File.WriteAllText(ProjectDocumentPathResolver.GetPreviewCapsulePath(root), "# Preview Capsule");
        var previewSelection = documentRuntime.SelectSources(root);
        AssertEqual(ProjectDocumentStage.PreviewDocs, previewSelection.ActiveStage, "Selector should switch to preview docs when preview_project.md exists.");
        AssertContains(previewSelection.ProjectDocument!.Path, "preview_project.md", "Preview docs stage should point to preview_project.md.");
        AssertContains(previewSelection.CapsuleDocument!.Path, "preview_capsule.md", "Preview docs stage should point to preview_capsule.md.");

        Directory.CreateDirectory(Path.Combine(root, ".zavod", "project"));
        File.WriteAllText(Path.Combine(root, ".zavod", "project", "project.md"), "# Project");
        File.WriteAllText(Path.Combine(root, ".zavod", "project", "capsule.md"), "# Capsule");
        var canonicalSelection = documentRuntime.SelectSources(root);
        AssertEqual(ProjectDocumentStage.CanonicalDocs, canonicalSelection.ActiveStage, "Selector should switch to canonical docs when project.md exists.");
        AssertContains(canonicalSelection.ProjectDocument!.Path, "project.md", "Canonical stage should point to project.md.");
        AssertContains(canonicalSelection.CapsuleDocument!.Path, "capsule.md", "Canonical stage should point to capsule.md.");
    }
    finally
    {
        DeleteScratchWorkspace(root);
    }
}

static void ProjectDocumentRuntimeConfirmsPreviewDocsIntoFiveCanonicalDocsHonestly()
{
    var root = CreateScratchWorkspace();
    try
    {
        Directory.CreateDirectory(Path.Combine(root, "src"));
        File.WriteAllText(Path.Combine(root, "src", "main.go"), "package main\nfunc main() {}");
        File.WriteAllText(Path.Combine(root, "README.md"), "Confirm preview docs test case.");

        var scan = WorkspaceScanner.Scan(new WorkspaceScanRequest(root));
        var packet = new WorkspaceMaterialRuntimeFront().BuildPreviewPacket(scan, maxMaterials: 4, maxCharsPerMaterial: 96);
        var interpretation = WorkspaceImportMaterialInterpretationResultBuilder.BuildFromResponse(
            packet,
            new WorkspaceImportMaterialPromptResponse(
                "Single project summary.",
                new[] { "Project detail line." },
                Array.Empty<string>(),
                Array.Empty<string>(),
                Array.Empty<string>(),
                Array.Empty<string>(),
                Array.Empty<string>(),
                Array.Empty<WorkspaceImportMaterialLayerInterpretation>(),
                Array.Empty<WorkspaceImportMaterialModuleInterpretation>(),
                new[] { new WorkspaceImportMaterialEntryPointInterpretation(Path.Combine("src", "main.go"), "main", "Observed entry") },
                new ArchitectureDiagramSpec("Project Architecture", Array.Empty<ArchitectureDiagramNode>(), Array.Empty<ArchitectureDiagramEdge>(), Array.Empty<ArchitectureDiagramGroup>(), Array.Empty<string>(), new ArchitectureDiagramRenderHints("left-to-right", Array.Empty<string>(), true)),
                Array.Empty<WorkspaceImportMaterialPromptResponseItem>()));
        var run = new WorkspaceImportMaterialInterpreterRunResult(
            packet,
            WorkspaceImportMaterialPromptRequestBuilder.Build(packet),
            new OpenRouterExecutionRequest("workspace.import.interpreter", "system", "user"),
            new OpenRouterExecutionResponse(true, "SUMMARY: test", "openrouter/test", 200, null, "ok"),
            interpretation,
            null,
            "runtime summary");
        var artifactService = new WorkspaceEvidenceArtifactRuntimeService(new ArchitectureDiagramRuntimeService());
        _ = artifactService.WriteBundle(run);

        var documentRuntime = new ProjectDocumentRuntimeService();
        var materialized = documentRuntime.ConfirmPreviewDocs(root, "test-contributor");
        var projectText = File.ReadAllText(materialized.ProjectDocumentPath);
        var directionText = File.ReadAllText(materialized.DirectionDocumentPath);
        var roadmapText = File.ReadAllText(materialized.RoadmapDocumentPath);
        var canonText = File.ReadAllText(materialized.CanonDocumentPath);
        var capsuleText = File.ReadAllText(materialized.CapsuleDocumentPath);

        AssertTrue(File.Exists(materialized.ProjectDocumentPath), "Confirm path should write canonical project.md.");
        AssertTrue(File.Exists(materialized.DirectionDocumentPath), "Confirm path should write canonical direction.md.");
        AssertTrue(File.Exists(materialized.RoadmapDocumentPath), "Confirm path should write canonical roadmap.md.");
        AssertTrue(File.Exists(materialized.CanonDocumentPath), "Confirm path should write canonical canon.md.");
        AssertTrue(File.Exists(materialized.CapsuleDocumentPath), "Confirm path should write derived canonical capsule.md.");
        AssertEqual(5, materialized.Promotions.Count, "Confirm path should promote all 5 document kinds explicitly.");
        AssertContains(projectText, "# Project", "Canonical project doc should use canonical heading.");
        AssertContains(projectText, "Confirmed canonical project base materialized from `preview_project.md`.", "Canonical project doc should expose confirm/materialization provenance.");
        AssertContains(projectText, "Single project summary", "Canonical project doc should preserve confirmed summary.");
        AssertContains(directionText, "# Direction", "Canonical direction doc should use canonical heading.");
        AssertContains(directionText, "Confirmed canonical direction.md materialized from `preview_direction.md`.", "Canonical direction doc should expose promotion provenance.");
        AssertContains(directionText, "## Likely / candidate direction signals", "Canonical direction should preserve reviewed preview sections.");
        AssertContains(roadmapText, "# Roadmap", "Canonical roadmap doc should use canonical heading.");
        AssertContains(roadmapText, "Confirmed canonical roadmap.md materialized from `preview_roadmap.md`.", "Canonical roadmap doc should expose promotion provenance.");
        AssertContains(roadmapText, "## Unknown / not-yet-established", "Canonical roadmap should preserve reviewed preview sections.");
        AssertContains(canonText, "# Canon", "Canonical canon doc should use canonical heading.");
        AssertContains(canonText, "Confirmed canonical canon.md materialized from `preview_canon.md`.", "Canonical canon doc should expose promotion provenance.");
        AssertContains(canonText, "## Contributor-authored rules", "Canonical canon should preserve contributor-authored rules surface.");
        AssertContains(capsuleText, "# Capsule", "Canonical capsule should use canonical heading.");
        AssertCapsuleV2Shape(capsuleText, "canonical");
        AssertContains(capsuleText, "Derived capsule v2. This document is a compressed view over Layer A sources, not an independent truth layer.", "Canonical capsule must remain derived.");
        AssertContains(capsuleText, "- Source: `project.md` [canonical]", "Canonical capsule should trace project section to canonical project.");
        AssertContains(capsuleText, "- Source: `direction.md` [canonical]", "Canonical capsule should draw direction from canonical direction when promoted.");
        AssertContains(capsuleText, "- Source: `roadmap.md` [canonical]", "Canonical capsule should draw roadmap from canonical roadmap when promoted.");
        AssertContains(capsuleText, "- Source: `canon.md` [canonical]", "Canonical capsule should draw canon from canonical canon when promoted.");
        AssertContains(capsuleText, "- Source: runtime overlay [runtime]", "Canonical capsule should mark current focus as runtime overlay.");
        AssertContains(capsuleText, "- Status: canonical 5/5, preview 0/5, absent 0/5", "Canonical capsule should expose full 5/5 canonical completeness.");
        AssertContains(capsuleText, "- Project identity source: canonical", "Canonical capsule should list per-section source stages.");
        AssertContains(capsuleText, "- Current direction source: canonical", "Canonical capsule should list canonical section source stages.");
        AssertContains(capsuleText, "Single project summary", "Canonical capsule should derive summary from project content, not section metadata.");

        var decisionsRoot = Path.GetDirectoryName(materialized.Promotions[0].DecisionPath)!;
        var decisionFiles = Directory.GetFiles(decisionsRoot, "DEC-*.md");
        AssertEqual(5, decisionFiles.Length, "Each promoted canonical kind must leave a Layer C decision entry.");
        foreach (var promotion in materialized.Promotions)
        {
            var decisionText = File.ReadAllText(promotion.DecisionPath);
            AssertTrue(File.Exists(promotion.DecisionPath), "Promotion result should point to a real decision file.");
            AssertTrue(File.Exists(promotion.JournalPath), "Promotion result should point to a real journal file.");
            AssertContains(decisionText, "type: canonical_promotion", "Promotion decision must use canonical_promotion type.");
            AssertContains(decisionText, "contributor: test-contributor", "Promotion decision must carry contributor identity.");
            AssertContains(decisionText, $"related_journal: {promotion.CanonicalPromotedEventId}", "Promotion decision must cross-reference the canonical promotion journal event.");
            AssertContains(decisionText, promotion.PreviewSha256, "Promotion decision must record source preview hash.");
        }

        var journalRoot = Path.GetDirectoryName(materialized.Promotions[0].JournalPath)!;
        var journalLines = Directory.GetFiles(journalRoot, "*.jsonl")
            .SelectMany(File.ReadAllLines)
            .ToArray();
        AssertEqual(10, journalLines.Length, "Five promotions should emit decision_recorded and canonical_promoted events.");
        AssertTrue(journalLines.Any(line => line.Contains("\"event_type\":\"decision_recorded\"", StringComparison.Ordinal)), "Journal must include decision_recorded events.");
        AssertTrue(journalLines.Any(line => line.Contains("\"event_type\":\"canonical_promoted\"", StringComparison.Ordinal)), "Journal must include canonical_promoted events.");
        AssertTrue(journalLines.Any(line => line.Contains("\"kind\":\"direction\"", StringComparison.Ordinal)), "Journal must identify promoted direction kind.");
        AssertTrue(journalLines.Any(line => line.Contains("\"kind\":\"capsule\"", StringComparison.Ordinal)), "Journal must identify promoted capsule kind.");
    }
    finally
    {
        DeleteScratchWorkspace(root);
    }
}

static void ProjectDocumentRuntimePromotesCapsuleWithoutPhantomProjectCanonHonestly()
{
    var root = CreateScratchWorkspace();
    try
    {
        ProjectDocumentPathResolver.EnsurePreviewDocsRoot(root);
        File.WriteAllText(ProjectDocumentPathResolver.GetPreviewProjectPath(root), """
# Project (Preview)

## Identity

- Project Id: `capsule-only`

## What this project appears to be

- Preview-only project identity.
""");
        File.WriteAllText(ProjectDocumentPathResolver.GetPreviewDirectionPath(root), """
# Direction (Preview)

## Unknown / not-yet-established

- Direction is preview-only.
""");
        File.WriteAllText(ProjectDocumentPathResolver.GetPreviewRoadmapPath(root), """
# Roadmap (Preview)

## Unknown / not-yet-established

- Roadmap is preview-only.
""");
        File.WriteAllText(ProjectDocumentPathResolver.GetPreviewCanonPath(root), """
# Canon (Preview)

## Contributor-authored rules

- No authored rules yet.
""");
        File.WriteAllText(ProjectDocumentPathResolver.GetPreviewCapsulePath(root), "# Capsule (Preview)");

        var documentRuntime = new ProjectDocumentRuntimeService();
        var promotion = documentRuntime.PromotePreviewDoc(root, ProjectDocumentKind.Capsule, "test-contributor");
        var capsuleText = File.ReadAllText(promotion.CanonicalDocumentPath);

        AssertFalse(File.Exists(Path.Combine(root, ".zavod", "project", "project.md")), "Promoting capsule alone must not create phantom project.md.");
        AssertContains(capsuleText, "- Source: `preview_project.md` [preview]", "Capsule-only promotion must keep project source at preview stage.");
        AssertContains(capsuleText, "- Status: canonical 1/5, preview 4/5, absent 0/5", "Capsule-only promotion must count only capsule as canonical.");
        AssertContains(capsuleText, "- Project identity source: preview", "Capsule-only promotion must not report project identity as canonical.");
        AssertContains(capsuleText, "- Current direction source: preview", "Capsule-only promotion must preserve preview direction source.");
        AssertTrue(File.Exists(promotion.DecisionPath), "Capsule-only promotion should still write a promotion decision.");
        AssertTrue(File.Exists(promotion.JournalPath), "Capsule-only promotion should still write journal events.");
    }
    finally
    {
        DeleteScratchWorkspace(root);
    }
}

static void ProjectDocumentRuntimeRejectsPreviewDocWithJournalEventHonestly()
{
    var root = CreateScratchWorkspace();
    try
    {
        ProjectDocumentPathResolver.EnsurePreviewDocsRoot(root);
        var previewPath = ProjectDocumentPathResolver.GetPreviewDirectionPath(root);
        File.WriteAllText(previewPath, "# Direction Preview\n\n- Candidate only.");

        var documentRuntime = new ProjectDocumentRuntimeService();
        var rejection = documentRuntime.RejectPreviewDoc(root, ProjectDocumentKind.Direction, "test-contributor");

        AssertFalse(File.Exists(previewPath), "Rejecting a preview doc should remove the rejected preview file.");
        AssertEqual(ProjectDocumentKind.Direction, rejection.Kind, "Rejection result should preserve rejected kind.");
        AssertEqual(previewPath, rejection.PreviewDocumentPath, "Rejection result should preserve preview path.");
        AssertTrue(File.Exists(rejection.JournalPath), "Preview rejection must flush a journal event.");

        var journalText = File.ReadAllText(rejection.JournalPath);
        AssertContains(journalText, "\"event_type\":\"preview_rejected\"", "Journal must record preview_rejected event.");
        AssertContains(journalText, "\"kind\":\"direction\"", "Journal must identify rejected preview kind.");
        AssertContains(journalText, "\"contributor\":\"test-contributor\"", "Journal must carry contributor attribution.");
        AssertContains(journalText, rejection.PreviewSha256, "Journal must record rejected preview hash.");
        AssertContains(journalText, "preview_docs/preview_direction.md", "Journal must reference rejected preview path.");

        var decisionsRoot = Path.Combine(root, ".zavod", "decisions");
        AssertFalse(Directory.Exists(decisionsRoot), "Routine preview rejection should not create a Layer C decision.");
    }
    finally
    {
        DeleteScratchWorkspace(root);
    }
}

static void ProjectDocumentRuntimeRegeneratesCapsuleV2DeterministicallyHonestly()
{
    var root = CreateScratchWorkspace();
    try
    {
        var state = ProjectStateStorage.EnsureInitialized(root, "capsule-v2-test", "Capsule V2 Test");
        state = ProjectStateStorage.Save(state with { ActiveShiftId = "SHIFT-123", ActiveTaskId = "TASK-456" });
        File.WriteAllText(state.TruthPointers.ProjectDocumentPath, """
# Project

Confirmed canonical project base.

## Identity

- Project Id: `capsule-v2-test`
- Project Name: `Capsule V2 Test`

## What this project appears to be

- Single project summary.
- Project detail line.
""");
        File.WriteAllText(state.TruthPointers.DirectionDocumentPath, """
# Direction

## Confirmed direction

- Stabilize canonical docs generation.
""");
        File.WriteAllText(state.TruthPointers.RoadmapDocumentPath, """
# Roadmap

## Candidate phases

- Harden capsule regeneration.
""");
        File.WriteAllText(state.TruthPointers.CanonDocumentPath, """
# Canon

## Contributor-authored rules

- Rule: Preview is never canonical truth.
- Rule: Capsule remains derived.

## Unknown / not-yet-established

- None listed.
""");
        var documentRuntime = new ProjectDocumentRuntimeService();

        var first = documentRuntime.RegenerateCapsule(root);
        var second = documentRuntime.RegenerateCapsule(root);
        var firstText = first.Markdown;
        var secondText = second.Markdown;

        AssertEqual(firstText, secondText, "Capsule regeneration must be deterministic for identical Layer A inputs.");
        AssertContains(firstText, "# Capsule", "Regenerated capsule should use canonical heading.");
        AssertCapsuleV2Shape(firstText, "canonical");
        AssertContains(firstText, "- Status: canonical 5/5, preview 0/5, absent 0/5", "Fully canonical Layer A should produce 5/5 canonical status.");
        AssertContains(firstText, "- Active shift: `SHIFT-123`", "Capsule current focus overlay should include active shift when present.");
        AssertContains(firstText, "- Active task: `TASK-456`", "Capsule current focus overlay should include active task when present.");
        AssertContains(firstText, "- Rule: Preview is never canonical truth.", "Capsule should compress contributor-authored canon rules.");

        File.AppendAllText(state.TruthPointers.DirectionDocumentPath, "\n- Keep source stages explicit.\n");
        var changed = documentRuntime.RegenerateCapsule(root).Markdown;

        AssertFalse(string.Equals(firstText, changed, StringComparison.Ordinal), "Capsule regeneration should reflect Layer A document changes.");
        AssertContains(changed, "- Keep source stages explicit.", "Regenerated capsule should pick up changed direction content.");
    }
    finally
    {
        DeleteScratchWorkspace(root);
    }
}

static void WorkspaceEvidenceArtifactRuntimeWritesReadableUtf8JsonHonestly()
{
    var previousCulture = CultureInfo.CurrentCulture;
    var previousUiCulture = CultureInfo.CurrentUICulture;
    var root = CreateScratchWorkspace();
    try
    {
        CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("ru-RU");
        CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("ru-RU");

        File.WriteAllText(Path.Combine(root, "notes.txt"), "project notes");
        var scan = WorkspaceScanner.Scan(new WorkspaceScanRequest(root));
        var packet = WorkspaceImportMaterialPreviewPacketBuilder.Build(scan, maxMaterials: 2, maxCharsPerMaterial: 64);
        var interpretation = new WorkspaceImportMaterialInterpretationResult(
            packet.WorkspaceRoot,
            packet.ImportKind,
            packet.SourceRoots,
            Array.Empty<string>(),
            new[] { "Техническая деталь." },
            Array.Empty<string>(),
            Array.Empty<string>(),
            Array.Empty<string>(),
            Array.Empty<string>(),
            Array.Empty<string>(),
            new[] { new WorkspaceImportMaterialLayerInterpretation("Core", "Основа платформы", "Наблюдаемо в документации.") },
            Array.Empty<WorkspaceImportMaterialEntryPointInterpretation>(),
            new ArchitectureDiagramSpec(
                "Test",
                new[] { new ArchitectureDiagramNode("core", "Core", "layer") },
                Array.Empty<ArchitectureDiagramEdge>(),
                Array.Empty<ArchitectureDiagramGroup>(),
                Array.Empty<string>(),
                new ArchitectureDiagramRenderHints("left-to-right", Array.Empty<string>(), true)),
            Array.Empty<WorkspaceMaterialPreviewInterpretation>(),
            "Сводка. truth=context_only.");
        var run = new WorkspaceImportMaterialInterpreterRunResult(
            packet,
            WorkspaceImportMaterialPromptRequestBuilder.Build(packet),
            new OpenRouterExecutionRequest("workspace.import.interpreter", "system", "user"),
            new OpenRouterExecutionResponse(true, "SUMMARY: test", "openrouter/test", 200, null, "ok"),
            interpretation,
            null,
            "runtime summary");
        var service = new WorkspaceEvidenceArtifactRuntimeService(new ArchitectureDiagramRuntimeService());

        var bundle = service.WriteBundle(run);
        var layerMapText = File.ReadAllText(bundle.LayerMapPath);

        AssertContains(layerMapText, "Основа платформы", "Artifact runtime should keep JSON human-readable in UTF-8.");
        AssertFalse(layerMapText.Contains("\\u041e", StringComparison.Ordinal), "Artifact runtime should not escape Cyrillic into unreadable unicode sequences by default.");
    }
    finally
    {
        CultureInfo.CurrentCulture = previousCulture;
        CultureInfo.CurrentUICulture = previousUiCulture;
        DeleteScratchWorkspace(root);
    }
}

static void WorkspaceEvidenceArtifactRuntimeWritesColdScannerPayloadsHonestly()
{
    var root = CreateScratchWorkspace();
    try
    {
        Directory.CreateDirectory(Path.Combine(root, "src"));
        File.WriteAllText(Path.Combine(root, "src", "main.cpp"), "int main() { return 0; }");
        File.WriteAllText(Path.Combine(root, "CMakeLists.txt"), "project(Test)");
        File.WriteAllText(Path.Combine(root, "README.md"), "Debugger and widget notes.");

        var scan = WorkspaceScanner.Scan(new WorkspaceScanRequest(root));
        var runtimeFront = new WorkspaceMaterialRuntimeFront(
            new TextMaterialRuntimeService(),
            new PdfExtractionRuntimeService(new FakeExternalProcessRunner(_ => new ExternalProcessResult(1, string.Empty, "Unexpected", false))),
            new ArchiveInspectionRuntimeService(new FakeExternalProcessRunner(_ => new ExternalProcessResult(1, string.Empty, "Unexpected", false))),
            new ImageInspectionRuntimeService());
        var packet = runtimeFront.BuildPreviewPacket(scan, maxMaterials: 4, maxCharsPerMaterial: 128);
        var interpretation = WorkspaceImportMaterialInterpretationResultBuilder.BuildEmpty(packet);
        var run = new WorkspaceImportMaterialInterpreterRunResult(
            packet,
            WorkspaceImportMaterialPromptRequestBuilder.Build(packet),
            new OpenRouterExecutionRequest("workspace.import.interpreter", "system", "user"),
            new OpenRouterExecutionResponse(true, "SUMMARY: test", "openrouter/test", 200, null, "ok"),
            interpretation,
            null,
            "runtime summary");
        var service = new WorkspaceEvidenceArtifactRuntimeService(new ArchitectureDiagramRuntimeService());

        var bundle = service.WriteBundle(run);

        AssertTrue(File.Exists(bundle.ScanRunPath), "Artifact runtime should write scanrun provenance as a first-class cold scanner payload.");
        AssertTrue(File.Exists(bundle.ScanSummaryPath), "Artifact runtime should write cold scan summary as a first-class scanner review payload.");
        AssertTrue(File.Exists(bundle.FilesIndexPath), "Artifact runtime should write file inventory index as a first-class scanner review payload.");
        AssertTrue(File.Exists(bundle.ManifestsIndexPath), "Artifact runtime should write manifest index as a first-class scanner review payload.");
        AssertTrue(File.Exists(bundle.SymbolsIndexPath), "Artifact runtime should write symbol index as a first-class scanner review payload.");
        AssertTrue(File.Exists(bundle.EdgesIndexPath), "Artifact runtime should write edge index as a first-class scanner review payload.");
        AssertTrue(File.Exists(bundle.EntryPointsIndexPath), "Artifact runtime should write entrypoint index as a first-class scanner review payload.");
        AssertTrue(File.Exists(bundle.ModulesMapPath), "Artifact runtime should write module map as a first-class scanner review payload.");
        AssertTrue(File.Exists(bundle.ProjectUnitsIndexPath), "Artifact runtime should write project unit index as a first-class scanner review payload.");
        AssertTrue(File.Exists(bundle.RunProfilesIndexPath), "Artifact runtime should write run/test profile index as a first-class scanner review payload.");
        AssertTrue(File.Exists(bundle.PredicateRegistryPath), "Artifact runtime should write predicate registry as a first-class scanner review payload.");
        AssertTrue(File.Exists(bundle.ScanBudgetPath), "Artifact runtime should write scan budget as a first-class scanner review payload.");
        AssertTrue(File.Exists(bundle.UncertaintyReportPath), "Artifact runtime should write uncertainty report as a first-class scanner review payload.");
        AssertTrue(File.Exists(bundle.RawObservationsPath), "Artifact runtime should write raw observations as a first-class cold scanner payload.");
        AssertTrue(File.Exists(bundle.DerivedPatternsPath), "Artifact runtime should write derived patterns as a first-class cold scanner payload.");
        AssertTrue(File.Exists(bundle.SignalScoresPath), "Artifact runtime should write signal scores as a first-class cold scanner payload.");
        AssertTrue(File.Exists(bundle.CandidatesPath), "Artifact runtime should write candidates as a first-class cold scanner payload.");
        AssertTrue(File.Exists(bundle.HotspotsPath), "Artifact runtime should write hotspots as a first-class cold scanner payload.");
        var scanSummary = File.ReadAllText(bundle.ScanSummaryPath);
        var filesIndexText = File.ReadAllText(bundle.FilesIndexPath);
        AssertContains(File.ReadAllText(bundle.ScanRunPath), "\"ScannerVersion\": \"2.0.0-alpha\"", "ScanRun artifact should preserve scanner version provenance.");
        AssertContains(File.ReadAllText(bundle.ScanRunPath), "\"RepoRootHash\": \"sha256:", "ScanRun artifact should preserve the compatibility fingerprint field.");
        AssertContains(File.ReadAllText(bundle.PredicateRegistryPath), "declares_code_edge", "Predicate registry artifact should preserve registered predicate ids.");
        AssertContains(File.ReadAllText(bundle.ScanBudgetPath), "\"IsPartial\": false", "Scan budget artifact should preserve default complete budget status.");
        AssertContains(File.ReadAllText(bundle.UncertaintyReportPath), "\"anomalies\"", "Uncertainty report artifact should expose scanner anomalies.");
        var probeBundleProjection = JsonSerializer.Serialize(new { artifactBundle = bundle }, new JsonSerializerOptions { WriteIndented = true });
        AssertContains(probeBundleProjection, "\"ScanSummaryPath\"", "Probe-style artifactBundle JSON should expose scan summary path.");
        AssertContains(probeBundleProjection, "\"PredicateRegistryPath\"", "Probe-style artifactBundle JSON should expose predicate registry path.");
        AssertContains(probeBundleProjection, "\"ScanBudgetPath\"", "Probe-style artifactBundle JSON should expose scan budget path.");
        AssertContains(probeBundleProjection, "\"UncertaintyReportPath\"", "Probe-style artifactBundle JSON should expose uncertainty report path.");
        AssertContains(scanSummary, "# Scan Summary", "Scan summary should be a dedicated scanner review document.");
        AssertContains(scanSummary, "Cold scanner projection", "Scan summary should state it is evidence-only.");
        AssertContains(scanSummary, "scan_fingerprint:", "Scan summary should use scanner-facing scan_fingerprint wording.");
        AssertContains(scanSummary, "fingerprint_scope: structural scan identity, not content-integrity hash", "Scan summary should not overclaim content-integrity hashing.");
        AssertFalse(scanSummary.Contains("repo_root_hash:", StringComparison.Ordinal), "Scan summary should not present the compatibility field as a repo root content hash.");
        AssertContains(scanSummary, "budget_status: `complete`", "Scan summary should preserve complete budget status.");
        AssertContains(scanSummary, "noise_files_ignored:", "Scan summary should preserve noise count surface.");
        AssertContains(scanSummary, "`src\\main.cpp`", "Scan summary should list observed entry points.");
        AssertContains(scanSummary, "marker=entrypoint_candidate/Likely/partial=False/bounded=False", "Scan summary should expose entrypoint evidence marker discipline.");
        AssertFalse(scanSummary.Contains("What This Project Is", StringComparison.OrdinalIgnoreCase), "Scan summary must not reuse importer narrative/project-purpose headings.");
        AssertContains(filesIndexText, "\"RelativePath\": \"src\\\\main.cpp\"", "Files index should preserve observed source file path.");
        AssertContains(filesIndexText, "\"Zone\": \"source\"", "Files index should classify source files without narrative interpretation.");
        AssertContains(filesIndexText, "\"RelativePath\": \"README.md\"", "Files index should preserve observed material file path.");
        AssertContains(filesIndexText, "\"MaterialKind\": \"TextDocument\"", "Files index should preserve material kind metadata.");
        AssertContains(File.ReadAllText(bundle.ManifestsIndexPath), "\"RelativePath\": \"CMakeLists.txt\"", "Manifest index should preserve observed build manifest paths.");
        AssertContains(File.ReadAllText(bundle.SymbolsIndexPath), "\"Symbols\"", "Symbol index should keep shallow symbol observations separate from narrative.");
        AssertContains(File.ReadAllText(bundle.EdgesIndexPath), "\"DependencyEdges\"", "Edge index should keep dependency edges as a cold scanner surface.");
        AssertContains(File.ReadAllText(bundle.EntryPointsIndexPath), "\"RelativePath\": \"src\\\\main.cpp\"", "Entrypoint index should preserve cold entrypoint candidates.");
        AssertContains(File.ReadAllText(bundle.ProjectUnitsIndexPath), "\"Manifests\"", "Project unit index should preserve unit manifest evidence.");
        AssertContains(File.ReadAllText(bundle.RunProfilesIndexPath), "cmake", "Run profile index should preserve discovered run/test/build commands.");
        AssertContains(File.ReadAllText(bundle.DerivedPatternsPath), "build_manifest_present", "Derived patterns artifact should preserve cold pattern codes.");
    }
    finally
    {
        DeleteScratchWorkspace(root);
    }
}

static void WorkspaceScannerClassifiesConfigOnlyImportHonestly()
{
    var root = CreateScratchWorkspace();
    try
    {
        File.WriteAllText(Path.Combine(root, ".editorconfig"), "root = true");
        File.WriteAllText(Path.Combine(root, "settings.json"), "{ }");
        File.WriteAllText(Path.Combine(root, "local.env"), "FOO=bar");

        var result = WorkspaceScanner.Scan(new WorkspaceScanRequest(root));
        var tool = new WorkspaceTool();
        var toolResult = tool.Execute(new WorkspaceInspectRequest("REQ-WS-CONFIG-001", root, null));

        AssertEqual(WorkspaceImportKind.NonSourceImport, result.State.ImportKind, "Config-only import should remain non-source, not source project.");
        AssertEqual(WorkspaceHealthStatus.MaterialOnly, result.State.Health, "Config-only import should not surface as healthy codebase structure.");
        AssertEqual(3, result.State.Summary.ConfigFileCount, "Config-only import should be counted separately from docs and source.");
        AssertFalse(result.State.HasRecognizableProjectStructure, "Config-only import should not pretend to be a source/build project.");
        AssertContains(toolResult.Summary, "config=3", "Workspace tool summary should expose config file count.");
    }
    finally
    {
        DeleteScratchWorkspace(root);
    }
}

static void WorkspaceScannerHandlesDocumentsOnlyImportHonestly()
{
    var root = CreateScratchWorkspace();
    try
    {
        File.WriteAllText(Path.Combine(root, "notes.txt"), "hello");
        File.WriteAllText(Path.Combine(root, "overview.md"), "# Docs only");

        var result = WorkspaceScanner.Scan(new WorkspaceScanRequest(root));

        AssertEqual(WorkspaceHealthStatus.MaterialOnly, result.State.Health, "Documents-only import should surface as material-only, not healthy codebase structure.");
        AssertEqual(WorkspaceImportKind.NonSourceImport, result.State.ImportKind, "Documents-only import should classify as non-source import.");
        AssertFalse(result.State.HasRecognizableProjectStructure, "Documents-only import should not pretend it recognized a normal source project.");
        AssertFalse(result.State.HasSourceFiles, "Documents-only import should not report source files.");
        AssertTrue(result.State.StructuralAnomalies.Any(a => a.Code == "NO_SOURCE_FILES"), "Documents-only import should honestly report missing source files.");
    }
    finally
    {
        DeleteScratchWorkspace(root);
    }
}

static void WorkspaceScannerClassifiesNonSourceImportHonestly()
{
    var root = CreateScratchWorkspace();
    try
    {
        File.WriteAllText(Path.Combine(root, "manual.pdf"), "binary docs");
        File.WriteAllText(Path.Combine(root, "build.zip"), "archive");

        var result = WorkspaceScanner.Scan(new WorkspaceScanRequest(root));

        AssertEqual(WorkspaceImportKind.NonSourceImport, result.State.ImportKind, "Non-source import should be classified honestly.");
        AssertEqual(WorkspaceHealthStatus.MaterialOnly, result.State.Health, "Non-source import should not surface as healthy codebase structure.");
        AssertFalse(result.State.HasRecognizableProjectStructure, "Non-source import should not pretend to be a source project.");
    }
    finally
    {
        DeleteScratchWorkspace(root);
    }
}

static void WorkspaceScannerClassifiesMixedImportHonestly()
{
    var root = CreateScratchWorkspace();
    try
    {
        File.WriteAllText(Path.Combine(root, "Program.cs"), "class Program {}");
        File.WriteAllText(Path.Combine(root, "readme.txt"), "notes");
        File.WriteAllText(Path.Combine(root, "archive.zip"), "bundle");

        var result = WorkspaceScanner.Scan(new WorkspaceScanRequest(root));

        AssertEqual(WorkspaceImportKind.MixedImport, result.State.ImportKind, "Mixed import should be classified honestly.");
        AssertTrue(result.State.HasRecognizableProjectStructure, "Mixed import should still preserve source-project recognition.");
    }
    finally
    {
        DeleteScratchWorkspace(root);
    }
}

static void WorkspaceScannerClassifiesAssemblerSourceImportHonestly()
{
    var root = CreateScratchWorkspace();
    try
    {
        File.WriteAllText(Path.Combine(root, "boot.asm"), "org 100h");
        File.WriteAllText(Path.Combine(root, "macros.inc"), "macro noop {}");

        var result = WorkspaceScanner.Scan(new WorkspaceScanRequest(root));

        AssertEqual(WorkspaceImportKind.SourceProject, result.State.ImportKind, "Assembler source import should be classified as source-oriented.");
        AssertTrue(result.State.HasRecognizableProjectStructure, "Assembler source import should count as recognizable source structure.");
        AssertTrue(result.State.HasSourceFiles, "Assembler source import should report source files.");
    }
    finally
    {
        DeleteScratchWorkspace(root);
    }
}

static void WorkspaceScannerClassifiesAssemblerMixedImportHonestly()
{
    var root = CreateScratchWorkspace();
    try
    {
        File.WriteAllText(Path.Combine(root, "patch.s"), ".globl _start");
        File.WriteAllText(Path.Combine(root, "notes.txt"), "reverse engineering notes");
        File.WriteAllText(Path.Combine(root, "game.zip"), "binary bundle");

        var result = WorkspaceScanner.Scan(new WorkspaceScanRequest(root));

        AssertEqual(WorkspaceImportKind.MixedImport, result.State.ImportKind, "Assembler mixed import should remain mixed, not non-source.");
        AssertTrue(result.State.HasRecognizableProjectStructure, "Assembler mixed import should still preserve source recognition.");
        AssertTrue(result.State.HasSourceFiles, "Assembler mixed import should report source presence.");
    }
    finally
    {
        DeleteScratchWorkspace(root);
    }
}

static void WorkspaceScannerClassifiesBinaryImportHonestly()
{
    var root = CreateScratchWorkspace();
    try
    {
        File.WriteAllText(Path.Combine(root, "game.exe"), "MZ");
        File.WriteAllText(Path.Combine(root, "plugin.dll"), "PE");

        var result = WorkspaceScanner.Scan(new WorkspaceScanRequest(root));

        AssertEqual(WorkspaceImportKind.NonSourceImport, result.State.ImportKind, "Binary-only import should classify as non-source import.");
        AssertEqual(WorkspaceHealthStatus.MaterialOnly, result.State.Health, "Binary-only import should not surface as healthy codebase structure.");
        AssertEqual(2, result.State.Summary.BinaryFileCount, "Binary-only import should count binary evidence separately.");
        AssertFalse(result.State.HasRecognizableProjectStructure, "Binary-only import should not pretend to be a source project.");
    }
    finally
    {
        DeleteScratchWorkspace(root);
    }
}

static void WorkspaceScannerClassifiesSourcePlusBinariesAsMixedHonestly()
{
    var root = CreateScratchWorkspace();
    try
    {
        File.WriteAllText(Path.Combine(root, "Program.cs"), "class Program {}");
        File.WriteAllText(Path.Combine(root, "helper.dll"), "PE");
        File.WriteAllText(Path.Combine(root, "payload.bin"), "blob");

        var result = WorkspaceScanner.Scan(new WorkspaceScanRequest(root));

        AssertEqual(WorkspaceImportKind.MixedImport, result.State.ImportKind, "Source plus binaries should remain mixed, not pure source.");
        AssertEqual(2, result.State.Summary.BinaryFileCount, "Mixed import should preserve separate binary evidence count.");
        AssertTrue(result.State.HasRecognizableProjectStructure, "Source plus binaries should still preserve source-project recognition.");
    }
    finally
    {
        DeleteScratchWorkspace(root);
    }
}

static void WorkspaceScannerClassifiesScriptingImportHonestly()
{
    var root = CreateScratchWorkspace();
    try
    {
        File.WriteAllText(Path.Combine(root, "init.lua"), "print('hi')");
        File.WriteAllText(Path.Combine(root, "tools.ps1"), "Write-Host hi");
        File.WriteAllText(Path.Combine(root, "readme.txt"), "notes");

        var result = WorkspaceScanner.Scan(new WorkspaceScanRequest(root));

        AssertEqual(WorkspaceImportKind.MixedImport, result.State.ImportKind, "Scripting plus docs should classify as mixed import.");
        AssertTrue(result.State.HasRecognizableProjectStructure, "Scripting import should count as recognizable source structure.");
        AssertTrue(result.State.HasSourceFiles, "Scripting import should report source files.");
    }
    finally
    {
        DeleteScratchWorkspace(root);
    }
}

static void WorkspaceScannerRecognizesManifestOnlyImportHonestly()
{
    var root = CreateScratchWorkspace();
    try
    {
        File.WriteAllText(Path.Combine(root, "go.mod"), "module example.com/test");
        File.WriteAllText(Path.Combine(root, "requirements.txt"), "requests==2.0.0");

        var result = WorkspaceScanner.Scan(new WorkspaceScanRequest(root));

        AssertEqual(WorkspaceImportKind.SourceProject, result.State.ImportKind, "Manifest-only import should still count as source-oriented project structure.");
        AssertTrue(result.State.HasRecognizableProjectStructure, "Manifest-only import should preserve recognizable project structure.");
        AssertFalse(result.State.HasSourceFiles, "Manifest-only import should not lie about source files when only manifests exist.");
        AssertTrue(result.State.HasBuildFiles, "Manifest-only import should preserve build/manifest evidence.");
    }
    finally
    {
        DeleteScratchWorkspace(root);
    }
}

static void WorkspaceScannerClassifiesModernWebSourceImportHonestly()
{
    var root = CreateScratchWorkspace();
    try
    {
        File.WriteAllText(Path.Combine(root, "App.vue"), "<template><div /></template>");
        File.WriteAllText(Path.Combine(root, "shell.mjs"), "export default {};");
        File.WriteAllText(Path.Combine(root, "readme.txt"), "web notes");

        var result = WorkspaceScanner.Scan(new WorkspaceScanRequest(root));

        AssertEqual(WorkspaceImportKind.MixedImport, result.State.ImportKind, "Modern web source plus docs should classify as mixed import.");
        AssertTrue(result.State.HasRecognizableProjectStructure, "Modern web source import should count as recognizable source structure.");
        AssertTrue(result.State.HasSourceFiles, "Modern web source import should report source files.");
    }
    finally
    {
        DeleteScratchWorkspace(root);
    }
}

static void WorkspaceScannerRecognizesWebConfigOnlyImportHonestly()
{
    var root = CreateScratchWorkspace();
    try
    {
        File.WriteAllText(Path.Combine(root, "tsconfig.json"), "{ }");
        File.WriteAllText(Path.Combine(root, "vite.config.ts"), "export default {};");

        var result = WorkspaceScanner.Scan(new WorkspaceScanRequest(root));

        AssertEqual(WorkspaceImportKind.SourceProject, result.State.ImportKind, "Web config-only import should still count as source-oriented project structure.");
        AssertTrue(result.State.HasRecognizableProjectStructure, "Web config-only import should preserve recognizable project structure.");
        AssertFalse(result.State.HasSourceFiles, "Web config-only import should not claim source files when only config markers exist.");
        AssertTrue(result.State.HasBuildFiles, "Web config-only import should preserve config/build evidence.");
    }
    finally
    {
        DeleteScratchWorkspace(root);
    }
}

static void WorkspaceScannerRecognizesInfraMarkerOnlyImportHonestly()
{
    var root = CreateScratchWorkspace();
    try
    {
        File.WriteAllText(Path.Combine(root, "Dockerfile"), "FROM alpine");
        File.WriteAllText(Path.Combine(root, "docker-compose.yml"), "services: {}");

        var result = WorkspaceScanner.Scan(new WorkspaceScanRequest(root));

        AssertEqual(WorkspaceImportKind.SourceProject, result.State.ImportKind, "Infra marker-only import should still count as source-oriented project structure.");
        AssertTrue(result.State.HasRecognizableProjectStructure, "Infra marker-only import should preserve recognizable project structure.");
        AssertFalse(result.State.HasSourceFiles, "Infra marker-only import should not claim source files when only infra markers exist.");
        AssertTrue(result.State.HasBuildFiles, "Infra marker-only import should preserve infra/build evidence.");
    }
    finally
    {
        DeleteScratchWorkspace(root);
    }
}

static void WorkspaceScannerClassifiesInfraMarkersPlusDocsHonestly()
{
    var root = CreateScratchWorkspace();
    try
    {
        File.WriteAllText(Path.Combine(root, "Dockerfile"), "FROM alpine");
        File.WriteAllText(Path.Combine(root, "compose.yaml"), "services: {}");
        File.WriteAllText(Path.Combine(root, "readme.txt"), "deployment notes");

        var result = WorkspaceScanner.Scan(new WorkspaceScanRequest(root));

        AssertEqual(WorkspaceImportKind.MixedImport, result.State.ImportKind, "Infra markers plus docs should classify as mixed import.");
        AssertTrue(result.State.HasRecognizableProjectStructure, "Infra marker import should preserve recognizable project structure.");
        AssertFalse(result.State.HasSourceFiles, "Infra marker import should remain honest about missing source files.");
    }
    finally
    {
        DeleteScratchWorkspace(root);
    }
}

static void WorkspaceScannerClassifiesSqlImportHonestly()
{
    var root = CreateScratchWorkspace();
    try
    {
        File.WriteAllText(Path.Combine(root, "schema.sql"), "create table users(id int);");
        File.WriteAllText(Path.Combine(root, "notes.txt"), "migration notes");

        var result = WorkspaceScanner.Scan(new WorkspaceScanRequest(root));

        AssertEqual(WorkspaceImportKind.MixedImport, result.State.ImportKind, "SQL plus docs should classify as mixed import.");
        AssertTrue(result.State.HasRecognizableProjectStructure, "SQL import should count as recognizable source structure.");
        AssertTrue(result.State.HasSourceFiles, "SQL import should report source files.");
    }
    finally
    {
        DeleteScratchWorkspace(root);
    }
}

static void WorkspaceScannerRecognizesDataToolingConfigOnlyImportHonestly()
{
    var root = CreateScratchWorkspace();
    try
    {
        File.WriteAllText(Path.Combine(root, "schema.prisma"), "model User { id Int @id }");
        File.WriteAllText(Path.Combine(root, "dbt_project.yml"), "name: analytics");

        var result = WorkspaceScanner.Scan(new WorkspaceScanRequest(root));

        AssertEqual(WorkspaceImportKind.SourceProject, result.State.ImportKind, "Data tooling config-only import should still count as source-oriented project structure.");
        AssertTrue(result.State.HasRecognizableProjectStructure, "Data tooling config-only import should preserve recognizable project structure.");
        AssertFalse(result.State.HasSourceFiles, "Data tooling config-only import should not claim source files when only config markers exist.");
        AssertTrue(result.State.HasBuildFiles, "Data tooling config-only import should preserve config/build evidence.");
    }
    finally
    {
        DeleteScratchWorkspace(root);
    }
}

static void WorkspaceBaselineMarksNonProjectImportAsPartial()
{
    var root = CreateScratchWorkspace();
    try
    {
        File.WriteAllText(Path.Combine(root, "archive.zip"), "fake");
        File.WriteAllText(Path.Combine(root, "readme.txt"), "docs");

        var scan = WorkspaceScanner.Scan(new WorkspaceScanRequest(root));
        var baseline = WorkspaceBaselineBuilder.Build(scan);

        AssertTrue(baseline.IsPartial, "Baseline should be partial when no recognizable project structure exists.");
        AssertEqual(2, baseline.RelevantFiles.Count, "Relevant files should still be captured for partial baseline.");
    }
    finally
    {
        DeleteScratchWorkspace(root);
    }
}

static void AcceptanceGuardAllowsSafeApplyForUnchangedTouchedScope()
{
    var root = CreateScratchWorkspace();
    try
    {
        File.WriteAllText(Path.Combine(root, "Program.cs"), "class Program {}");
        File.WriteAllText(Path.Combine(root, "project.csproj"), "<Project />");

        var scan = WorkspaceScanner.Scan(new WorkspaceScanRequest(root));
        var baseline = WorkspaceBaselineBuilder.Build(scan);
        var executionBase = ExecutionBaseBuilder.Build(root, new[] { "Program.cs" }, "EXEC-001");
        var runtimeProfile = RuntimeProfile.ScopedLocalDefault;
        var runtimeSelection = new RuntimeSelectionDecision(runtimeProfile, IsAllowed: true, "Scoped local selected for safe default execution.");
        var processEvidence = new AcceptanceProcessEvidence(0, "ok", string.Empty, TimedOut: false, WasCanceled: false);
        var evidence = AcceptanceEvidenceBuilder.Build(
            scan.State,
            baseline,
            executionBase,
            runtimeSelection,
            runtimeProfile,
            "worker completed successfully",
            "Program.cs updated",
            processEvidence,
            toolExecution: null,
            "workspace unchanged");

        var decision = AcceptanceGuard.Evaluate(evidence);

        AssertEqual(AcceptanceClassification.SafeApply, decision.Classification, "Unchanged touched scope should allow safe apply.");
        AssertEqual(AcceptanceDecisionStatus.Allowed, decision.Status, "Safe apply should be allowed.");
        AssertEqual("scoped-local-default", evidence.Inputs.RuntimeProfile.ProfileId, "Acceptance evidence should preserve runtime profile id.");
        AssertEqual(RuntimeFamily.ScopedLocalWorkspace, evidence.Inputs.RuntimeProfile.Family, "Acceptance evidence should preserve runtime family.");
        AssertContains(evidence.Inputs.RuntimeSelectionReason, "safe default", "Acceptance evidence should preserve runtime selection reason.");
    }
    finally
    {
        DeleteScratchWorkspace(root);
    }
}

static void AcceptanceGuardBlocksTouchedFileConflict()
{
    var root = CreateScratchWorkspace();
    try
    {
        var codePath = Path.Combine(root, "Program.cs");
        File.WriteAllText(codePath, "class Program {}");
        File.WriteAllText(Path.Combine(root, "project.csproj"), "<Project />");

        var scan = WorkspaceScanner.Scan(new WorkspaceScanRequest(root));
        var baseline = WorkspaceBaselineBuilder.Build(scan);
        var executionBase = ExecutionBaseBuilder.Build(root, new[] { "Program.cs" }, "EXEC-002");
        File.AppendAllText(codePath, "\n// external change");

        var currentScan = WorkspaceScanner.Scan(new WorkspaceScanRequest(root));
        var runtimeProfile = RuntimeProfile.ScopedLocalDefault;
        var runtimeSelection = new RuntimeSelectionDecision(runtimeProfile, IsAllowed: true, "Scoped local selected for safe default execution.");
        var processEvidence = new AcceptanceProcessEvidence(0, "ok", string.Empty, TimedOut: false, WasCanceled: false);
        var evidence = AcceptanceEvidenceBuilder.Build(
            currentScan.State,
            baseline,
            executionBase,
            runtimeSelection,
            runtimeProfile,
            "worker completed successfully",
            "Program.cs updated",
            processEvidence,
            toolExecution: null,
            "workspace changed after execution base");

        var decision = AcceptanceGuard.Evaluate(evidence);

        AssertEqual(AcceptanceClassification.Conflict, decision.Classification, "Touched file change should produce conflict.");
        AssertEqual(AcceptanceDecisionStatus.Blocked, decision.Status, "Conflict should block acceptance.");
        AssertTrue(decision.Conflicts.Any(conflict => conflict.RelativePath == "Program.cs"), "Conflict should mention the touched file.");
        AssertEqual(RuntimeIsolationLevel.ScopedWorkspace, evidence.Inputs.RuntimeProfile.Isolation, "Acceptance evidence should preserve runtime isolation level.");
    }
    finally
    {
        DeleteScratchWorkspace(root);
    }
}

static void AcceptanceGuardRejectsSiblingPathPrefixEscapeHonestly()
{
    var root = CreateScratchWorkspace();
    var sibling = root + "-sibling";
    try
    {
        Directory.CreateDirectory(sibling);
        File.WriteAllText(Path.Combine(root, "project.csproj"), "<Project />");
        File.WriteAllText(Path.Combine(sibling, "Program.cs"), "class Program {}");
        var escapePath = Path.Combine("..", Path.GetFileName(sibling), "Program.cs");

        AssertThrows<InvalidOperationException>(
            () => ExecutionBaseBuilder.Build(root, new[] { escapePath }, "EXEC-ESCAPE"),
            "Execution base builder must reject sibling paths that only share a string prefix with the workspace root.");

        var scan = WorkspaceScanner.Scan(new WorkspaceScanRequest(root));
        var baseline = WorkspaceBaselineBuilder.Build(scan);
        var executionBase = new ExecutionBase(
            "EXEC-ESCAPE-MANUAL",
            DateTimeOffset.UtcNow,
            new TouchedScope(new[] { escapePath }),
            new[] { new ExecutionBaseFileEntry(escapePath, 0, 0) },
            "Manual escape base for guard regression.");
        var runtimeProfile = RuntimeProfile.ScopedLocalDefault;
        var runtimeSelection = new RuntimeSelectionDecision(runtimeProfile, IsAllowed: true, "Scoped local selected for safe default execution.");
        var processEvidence = new AcceptanceProcessEvidence(0, "ok", string.Empty, TimedOut: false, WasCanceled: false);
        var evidence = AcceptanceEvidenceBuilder.Build(
            scan.State,
            baseline,
            executionBase,
            runtimeSelection,
            runtimeProfile,
            "worker completed successfully",
            "Program.cs updated",
            processEvidence,
            toolExecution: null,
            "workspace unchanged");

        var decision = AcceptanceGuard.Evaluate(evidence);

        AssertEqual(AcceptanceDecisionStatus.Blocked, decision.Status, "Acceptance guard must block sibling path prefix escape.");
        AssertTrue(decision.Conflicts.Any(conflict => conflict.RelativePath == escapePath), "Acceptance guard should report escaped touched path.");
    }
    finally
    {
        DeleteScratchWorkspace(root);
        DeleteScratchWorkspace(sibling);
    }
}

static void StagingTaskIdPathSegmentAcceptsSafeIdsHonestly()
{
    var root = CreateScratchWorkspace();
    try
    {
        var manifest = StagingWriter.Stage(
            root,
            "TASK-001",
            1,
            "Safe task id regression.",
            new[]
            {
                new WorkerEdit("src/File.txt", WorkerEdit.OperationWriteFull, "content")
            });

        AssertEqual("TASK-001", manifest.TaskId, "Safe task ids should be preserved as staging path segments.");
        AssertTrue(Directory.Exists(Path.Combine(root, ".zavod.local", "staging", "TASK-001", "attempt-01")), "Safe task id should create the expected staging directory.");
        AssertTrue(File.Exists(Path.Combine(root, ".zavod.local", "staging", "TASK-001", "attempt-01", "manifest.json")), "Safe task id should preserve a load-bearing manifest.");

        var outcome = StagingApplier.Apply(root, "TASK-001");

        AssertEqual("content", File.ReadAllText(Path.Combine(root, "src", "File.txt"), Encoding.UTF8), "Safe task id should allow staged content to apply.");
        AssertEqual(1, outcome.AppliedFiles.Count, "Safe task id should allow one staged file to apply.");
    }
    finally
    {
        DeleteScratchWorkspace(root);
    }
}

static void StagingTaskIdPathSegmentRejectsUnsafeIdsHonestly()
{
    foreach (var unsafeTaskId in new[] { "..\\evil", "../evil", "TASK/001", "TASK\\001", "TASK:001", "TASK*001", "   " })
    {
        var root = CreateScratchWorkspace();
        try
        {
            AssertThrows<ArgumentException>(
                () => StagingWriter.Stage(
                    root,
                    unsafeTaskId,
                    1,
                    "Unsafe task id regression.",
                    new[]
                    {
                        new WorkerEdit("src/File.txt", WorkerEdit.OperationWriteFull, "content")
                    }),
                $"Unsafe task id '{unsafeTaskId}' should be rejected before staging writes.");
            AssertThrows<ArgumentException>(
                () => StagingApplier.Apply(root, unsafeTaskId),
                $"Unsafe task id '{unsafeTaskId}' should be rejected before apply path lookup.");
            StagingWriter.Cleanup(root, unsafeTaskId);
            var quarantined = StagingWriter.Quarantine(root, unsafeTaskId);
            AssertEqual<string?>(null, quarantined, "Unsafe task id should not produce a quarantine path.");
            AssertFalse(Directory.Exists(Path.Combine(root, ".zavod.local", "staging")), "Rejected task id must not create staging directories.");
        }
        finally
        {
            DeleteScratchWorkspace(root);
        }
    }
}

static void StagingWriterRejectsSiblingPathPrefixEscapeHonestly()
{
    var root = CreateScratchWorkspace();
    var sibling = root + "-sibling";
    try
    {
        Directory.CreateDirectory(sibling);
        var siblingRelative = Path.Combine("..", Path.GetFileName(sibling), "owned.txt");

        var manifest = StagingWriter.Stage(
            root,
            "TASK-STAGE-ESCAPE",
            1,
            "Stage escape regression.",
            new[]
            {
                new WorkerEdit(siblingRelative, WorkerEdit.OperationWriteFull, "escaped")
            });

        var result = manifest.Results.Single();
        AssertFalse(result.Applied, "Staging writer must not stage sibling path prefix escapes.");
        AssertContains(result.SkipReason ?? string.Empty, "path escapes project root", "Staging writer should explain escaped paths.");
        AssertFalse(File.Exists(Path.Combine(sibling, "owned.txt")), "Staging writer must not write into a sibling directory.");
    }
    finally
    {
        DeleteScratchWorkspace(root);
        DeleteScratchWorkspace(sibling);
    }
}

static void StagingApplierBlocksHashDriftHonestly()
{
    var root = CreateScratchWorkspace();
    try
    {
        var targetPath = Path.Combine(root, "src", "File.txt");
        Directory.CreateDirectory(Path.GetDirectoryName(targetPath)!);
        File.WriteAllText(targetPath, "original", Encoding.UTF8);

        StagingWriter.Stage(
            root,
            "TASK-HASH-DRIFT",
            1,
            "Hash drift regression.",
            new[]
            {
                new WorkerEdit("src/File.txt", WorkerEdit.OperationWriteFull, "staged")
            });
        File.WriteAllText(targetPath, "external edit", Encoding.UTF8);

        var outcome = StagingApplier.Apply(root, "TASK-HASH-DRIFT");

        AssertEqual("external edit", File.ReadAllText(targetPath, Encoding.UTF8), "Hash drift must preserve external project edits.");
        AssertEqual(0, outcome.AppliedFiles.Count, "Hash drift must block the staged file from applying.");
        AssertTrue(outcome.SkippedFiles.Any(static item => item.Contains("project file changed since staging", StringComparison.OrdinalIgnoreCase)), "Hash drift should be surfaced as a skipped file.");
        AssertTrue(outcome.HashMismatchWarnings.Any(static item => item.Contains("Apply blocked", StringComparison.OrdinalIgnoreCase)), "Hash drift warning must say apply was blocked.");
        AssertFalse(outcome.HashMismatchWarnings.Any(static item => item.Contains("Apply proceeded", StringComparison.OrdinalIgnoreCase)), "Hash drift warning must not claim apply proceeded.");
    }
    finally
    {
        DeleteScratchWorkspace(root);
    }
}

static void StagingApplierIgnoresManifestAbsoluteStagedPathHonestly()
{
    var root = CreateScratchWorkspace();
    var outside = CreateScratchWorkspace();
    try
    {
        var targetPath = Path.Combine(root, "src", "File.txt");
        Directory.CreateDirectory(Path.GetDirectoryName(targetPath)!);
        File.WriteAllText(targetPath, "original", Encoding.UTF8);
        var outsidePayload = Path.Combine(outside, "payload.txt");
        File.WriteAllText(outsidePayload, "outside payload", Encoding.UTF8);

        var manifest = StagingWriter.Stage(
            root,
            "TASK-STAGED-PATH",
            1,
            "Manifest path trust regression.",
            new[]
            {
                new WorkerEdit("src/File.txt", WorkerEdit.OperationWriteFull, "safe staged payload")
            });
        var attemptRoot = manifest.StagingRoot;
        var poisoned = manifest with
        {
            Results = manifest.Results.Select(result => result with { StagedAbsolutePath = outsidePayload }).ToArray()
        };
        File.WriteAllText(
            Path.Combine(attemptRoot, "manifest.json"),
            JsonSerializer.Serialize(poisoned, new JsonSerializerOptions { WriteIndented = true }),
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));

        var outcome = StagingApplier.Apply(root, "TASK-STAGED-PATH");

        AssertEqual("safe staged payload", File.ReadAllText(targetPath, Encoding.UTF8), "Applier must read the canonical staged path under the attempt root, not a manifest-provided absolute path.");
        AssertEqual(1, outcome.AppliedFiles.Count, "Valid canonical staged file should still apply.");
        AssertFalse(File.ReadAllText(targetPath, Encoding.UTF8).Contains("outside payload", StringComparison.Ordinal), "Manifest-provided outside payload must not be read.");
    }
    finally
    {
        DeleteScratchWorkspace(root);
        DeleteScratchWorkspace(outside);
    }
}

static void AcceptanceEvidenceCanBeAssembledFromExecutionRunResult()
{
    var root = CreateScratchWorkspace();
    try
    {
        File.WriteAllText(Path.Combine(root, "Program.cs"), "class Program {}");
        File.WriteAllText(Path.Combine(root, "project.csproj"), "<Project />");

        var scan = WorkspaceScanner.Scan(new WorkspaceScanRequest(root));
        var baseline = WorkspaceBaselineBuilder.Build(scan);
        var executionBase = ExecutionBaseBuilder.Build(root, new[] { "Program.cs" }, "EXEC-003");
        var runResult = new ExecutionRunResult(
            new ExecutionOutcome(ExecutionTarget.ActiveShiftSubsystem, ExecutionOutcomeStatus.Deferred, "Execution result produced."),
            new ExecutionRecord("SHIFT-001", "TASK-001", ExecutionTarget.ActiveShiftSubsystem, ExecutionOutcomeStatus.Deferred, "Execution result produced.", RuntimeProfile.ScopedLocalDefault),
            RuntimeProfile.ScopedLocalDefault);
        var workerResult = new WorkerExecutionResult(
            "RESULT-001",
            "TASK-001",
            WorkerExecutionStatus.Success,
            "Worker prepared Program.cs update.",
            Array.Empty<IntakeArtifact>(),
            new[] { new WorkerExecutionModification("Program.cs", "update", "Adjusted entry point.") },
            Array.Empty<ToolWarning>());
        var processEvidence = new AcceptanceProcessEvidence(0, "ok", string.Empty, TimedOut: false, WasCanceled: false);
        var runtimeSelection = new RuntimeSelectionDecision(runResult.EffectiveRuntimeProfile, IsAllowed: true, "Scoped local selected for safe default execution.");

        var evidence = AcceptanceEvidenceBuilder.BuildFromExecution(
            scan.State,
            baseline,
            executionBase,
            runtimeSelection,
            runResult,
            workerResult,
            processEvidence,
            toolExecution: null,
            "workspace unchanged");

        AssertContains(evidence.ExecutionResultSummary, "Success", "Execution-derived evidence should include worker execution status.");
        AssertContains(evidence.ExecutionResultSummary, "Worker prepared Program.cs update.", "Execution-derived evidence should include worker summary.");
        AssertContains(evidence.ChangePayloadSummary, "update:Program.cs", "Execution-derived evidence should describe modified files.");
        AssertEqual("scoped-local-default", evidence.Inputs.RuntimeProfile.ProfileId, "Execution-derived evidence should keep runtime profile.");
    }
    finally
    {
        DeleteScratchWorkspace(root);
    }
}

static void AcceptanceEvidenceFactoryAssemblesScannerAndExecutionLayersTogether()
{
    var root = CreateScratchWorkspace();
    try
    {
        File.WriteAllText(Path.Combine(root, "Program.cs"), "class Program {}");
        File.WriteAllText(Path.Combine(root, "project.csproj"), "<Project />");

        var runResult = new ExecutionRunResult(
            new ExecutionOutcome(ExecutionTarget.ActiveShiftSubsystem, ExecutionOutcomeStatus.Deferred, "Execution result produced."),
            new ExecutionRecord("SHIFT-002", "TASK-002", ExecutionTarget.ActiveShiftSubsystem, ExecutionOutcomeStatus.Deferred, "Execution result produced.", RuntimeProfile.ScopedLocalDefault),
            RuntimeProfile.ScopedLocalDefault);
        var workerResult = new WorkerExecutionResult(
            "RESULT-002",
            "TASK-002",
            WorkerExecutionStatus.Success,
            "Worker prepared Program.cs update.",
            Array.Empty<IntakeArtifact>(),
            new[] { new WorkerExecutionModification("Program.cs", "update", "Adjusted entry point.") },
            Array.Empty<ToolWarning>());
        var processEvidence = new AcceptanceProcessEvidence(0, "ok", string.Empty, TimedOut: false, WasCanceled: false);
        var runtimeSelection = new RuntimeSelectionDecision(runResult.EffectiveRuntimeProfile, IsAllowed: true, "Scoped local selected for safe default execution.");

        var evidence = AcceptanceEvidenceFactory.Create(
            root,
            new[] { "Program.cs" },
            "EXEC-004",
            runtimeSelection,
            runResult,
            workerResult,
            processEvidence,
            toolExecution: null,
            "workspace unchanged");

        AssertEqual(WorkspaceHealthStatus.Healthy, evidence.WorkspaceObservation.Health, "Factory should include live workspace observation.");
        AssertEqual("EXEC-004", evidence.ExecutionBase.ExecutionId, "Factory should build execution base.");
        AssertEqual("scoped-local-default", evidence.Inputs.RuntimeProfile.ProfileId, "Factory should preserve runtime profile.");
        AssertContains(evidence.ChangePayloadSummary, "update:Program.cs", "Factory should derive change payload summary from worker result.");
    }
    finally
    {
        DeleteScratchWorkspace(root);
    }
}

static void AcceptanceEvidenceCarriesToolExecutionEnvelopeAutomatically()
{
    var root = CreateScratchWorkspace();
    try
    {
        File.WriteAllText(Path.Combine(root, "Program.cs"), "class Program {}");
        File.WriteAllText(Path.Combine(root, "project.csproj"), "<Project />");

        var layer = UnifiedToolLayer.CreateDefault();
        var toolEnvelope = layer.InspectWorkspaceWithEnvelope(
            PromptRole.Qc,
            new WorkspaceInspectRequest("REQ-ACC-WS-001", root, new[] { "Program.cs" }));

        var runResult = new ExecutionRunResult(
            new ExecutionOutcome(ExecutionTarget.ActiveShiftSubsystem, ExecutionOutcomeStatus.Deferred, "Execution result produced."),
            new ExecutionRecord("SHIFT-003", "TASK-003", ExecutionTarget.ActiveShiftSubsystem, ExecutionOutcomeStatus.Deferred, "Execution result produced.", RuntimeProfile.ScopedLocalDefault),
            RuntimeProfile.ScopedLocalDefault);
        var workerResult = new WorkerExecutionResult(
            "RESULT-003",
            "TASK-003",
            WorkerExecutionStatus.Success,
            "Worker prepared Program.cs update.",
            Array.Empty<IntakeArtifact>(),
            new[] { new WorkerExecutionModification("Program.cs", "update", "Adjusted entry point.") },
            Array.Empty<ToolWarning>());
        var processEvidence = new AcceptanceProcessEvidence(0, "ok", string.Empty, TimedOut: false, WasCanceled: false);
        var runtimeSelection = new RuntimeSelectionDecision(runResult.EffectiveRuntimeProfile, IsAllowed: true, "Scoped local selected for safe default execution.");
        var scan = WorkspaceScanner.Scan(new WorkspaceScanRequest(root));
        var baseline = WorkspaceBaselineBuilder.Build(scan);
        var executionBase = ExecutionBaseBuilder.Build(root, new[] { "Program.cs" }, "EXEC-005");

        var evidence = AcceptanceEvidenceBuilder.BuildFromExecution(
            scan.State,
            baseline,
            executionBase,
            runtimeSelection,
            runResult,
            workerResult,
            processEvidence,
            toolEnvelope,
            "workspace unchanged");

        AssertTrue(evidence.ToolExecution is not null, "Acceptance evidence should carry tool execution envelope when provided.");
        AssertEqual("workspace.inspect", evidence.ToolExecution!.ResolvedTool.ToolName, "Acceptance evidence must preserve tool identity.");
        AssertEqual(RoleCapabilityProfile.ReadOnly, evidence.ToolExecution.ResolvedTool.Route.CapabilityProfile, "Acceptance evidence must preserve tool capability profile.");
        AssertContains(evidence.Inputs.ToolExecution!.EvidenceSummary, "tool=workspace.inspect", "Acceptance inputs should preserve tool-level evidence summary.");
        AssertContains(evidence.SummaryLine, "tool=workspace.inspect", "Acceptance summary should surface tool participation.");
    }
    finally
    {
        DeleteScratchWorkspace(root);
    }
}

static void AcceptanceEvaluationFactoryProducesSafeApplyForUnchangedWorkspace()
{
    var root = CreateScratchWorkspace();
    try
    {
        File.WriteAllText(Path.Combine(root, "Program.cs"), "class Program {}");
        File.WriteAllText(Path.Combine(root, "project.csproj"), "<Project />");

        var runResult = new ExecutionRunResult(
            new ExecutionOutcome(ExecutionTarget.ActiveShiftSubsystem, ExecutionOutcomeStatus.Deferred, "Execution result produced."),
            new ExecutionRecord("SHIFT-003", "TASK-003", ExecutionTarget.ActiveShiftSubsystem, ExecutionOutcomeStatus.Deferred, "Execution result produced.", RuntimeProfile.ScopedLocalDefault),
            RuntimeProfile.ScopedLocalDefault);
        var workerResult = new WorkerExecutionResult(
            "RESULT-003",
            "TASK-003",
            WorkerExecutionStatus.Success,
            "Worker prepared Program.cs update.",
            Array.Empty<IntakeArtifact>(),
            new[] { new WorkerExecutionModification("Program.cs", "update", "Adjusted entry point.") },
            Array.Empty<ToolWarning>());
        var processEvidence = new AcceptanceProcessEvidence(0, "ok", string.Empty, TimedOut: false, WasCanceled: false);
        var runtimeSelection = new RuntimeSelectionDecision(runResult.EffectiveRuntimeProfile, IsAllowed: true, "Scoped local selected for safe default execution.");

        var evaluation = AcceptanceEvaluationFactory.Create(
            root,
            new[] { "Program.cs" },
            "EXEC-005",
            runtimeSelection,
            runResult,
            workerResult,
            processEvidence,
            "workspace unchanged");

        AssertEqual(AcceptanceClassification.SafeApply, evaluation.Decision.Classification, "Evaluation factory should allow safe apply when touched scope is unchanged.");
        AssertEqual(AcceptanceDecisionStatus.Allowed, evaluation.Decision.Status, "Evaluation factory should allow safe apply when workspace is unchanged.");
    }
    finally
    {
        DeleteScratchWorkspace(root);
    }
}

static void AcceptanceEvaluationFactoryProducesConflictForTouchedFileDrift()
{
    var root = CreateScratchWorkspace();
    try
    {
        var codePath = Path.Combine(root, "Program.cs");
        File.WriteAllText(codePath, "class Program {}");
        File.WriteAllText(Path.Combine(root, "project.csproj"), "<Project />");

        var initialScan = WorkspaceScanner.Scan(new WorkspaceScanRequest(root));
        var baseline = WorkspaceBaselineBuilder.Build(initialScan);
        var executionBase = ExecutionBaseBuilder.Build(root, new[] { "Program.cs" }, "EXEC-006");
        File.AppendAllText(codePath, "\n// external change");
        var currentScan = WorkspaceScanner.Scan(new WorkspaceScanRequest(root));

        var runResult = new ExecutionRunResult(
            new ExecutionOutcome(ExecutionTarget.ActiveShiftSubsystem, ExecutionOutcomeStatus.Deferred, "Execution result produced."),
            new ExecutionRecord("SHIFT-004", "TASK-004", ExecutionTarget.ActiveShiftSubsystem, ExecutionOutcomeStatus.Deferred, "Execution result produced.", RuntimeProfile.ScopedLocalDefault),
            RuntimeProfile.ScopedLocalDefault);
        var workerResult = new WorkerExecutionResult(
            "RESULT-004",
            "TASK-004",
            WorkerExecutionStatus.Success,
            "Worker prepared Program.cs update.",
            Array.Empty<IntakeArtifact>(),
            new[] { new WorkerExecutionModification("Program.cs", "update", "Adjusted entry point.") },
            Array.Empty<ToolWarning>());
        var processEvidence = new AcceptanceProcessEvidence(0, "ok", string.Empty, TimedOut: false, WasCanceled: false);
        var runtimeSelection = new RuntimeSelectionDecision(runResult.EffectiveRuntimeProfile, IsAllowed: true, "Scoped local selected for safe default execution.");

        var evaluation = AcceptanceEvaluationFactory.CreateFromComponents(
            currentScan.State,
            baseline,
            executionBase,
            runtimeSelection,
            runResult,
            workerResult,
            processEvidence,
            "workspace changed after execution base");

        AssertEqual(AcceptanceClassification.Conflict, evaluation.Decision.Classification, "Evaluation path should produce conflict when touched file drift exists.");
        AssertEqual(AcceptanceDecisionStatus.Blocked, evaluation.Decision.Status, "Conflict should block acceptance.");
    }
    finally
    {
        DeleteScratchWorkspace(root);
    }
}

static void ExecutionAcceptanceAdapterEvaluatesProducedRuntimeAgainstWorkspace()
{
    var root = CreateScratchWorkspace();
    try
    {
        File.WriteAllText(Path.Combine(root, "Program.cs"), "class Program {}");
        File.WriteAllText(Path.Combine(root, "project.csproj"), "<Project />");

        var task = CreateCustomTaskState(
            "TASK-ACC-001",
            TaskStateStatus.Active,
            "Prepare acceptance bridge",
            new[] { "Program.cs" },
            PromptRole.Worker);
        var shift = CreateShiftState(task);
        var runtime = ExecutionRuntimeController.Begin(task, shift);
        var providedResult = new WorkerExecutionResult(
            "RESULT-ACC-001",
            task.TaskId,
            WorkerExecutionStatus.Success,
            "Worker prepared Program.cs update.",
            Array.Empty<IntakeArtifact>(),
            new[] { new WorkerExecutionModification("Program.cs", "update", "Adjusted entry point.") },
            Array.Empty<ToolWarning>());
        runtime = ExecutionRuntimeController.ProduceProvidedResult(runtime, providedResult);

        var processEvidence = new AcceptanceProcessEvidence(0, "ok", string.Empty, TimedOut: false, WasCanceled: false);
        var evaluation = ExecutionAcceptanceAdapter.Evaluate(
            root,
            runtime,
            processEvidence,
            "workspace unchanged");

        AssertEqual(AcceptanceClassification.SafeApply, evaluation.Decision.Classification, "Runtime adapter should allow safe apply for unchanged touched scope.");
        AssertEqual("SESSION-TASK-ACC-001", evaluation.Evidence.ExecutionBase.ExecutionId, "Runtime adapter should bind execution base to runtime session id.");
        AssertContains(evaluation.Evidence.ChangePayloadSummary, "update:Program.cs", "Runtime adapter should derive touched scope from worker modifications.");
    }
    finally
    {
        DeleteScratchWorkspace(root);
    }
}

static void ExecutionRuntimeCanObserveAcceptanceAfterQcAccept()
{
    var root = CreateScratchWorkspace();
    try
    {
        File.WriteAllText(Path.Combine(root, "Program.cs"), "class Program {}");
        File.WriteAllText(Path.Combine(root, "project.csproj"), "<Project />");

        var task = CreateCustomTaskState(
            "TASK-ACC-002",
            TaskStateStatus.Active,
            "Observe acceptance after QC",
            new[] { "Program.cs" },
            PromptRole.Worker);
        var shift = CreateShiftState(task);
        var runtime = ExecutionRuntimeController.Begin(task, shift);
        var providedResult = new WorkerExecutionResult(
            "RESULT-ACC-002",
            task.TaskId,
            WorkerExecutionStatus.Success,
            "Worker prepared Program.cs update.",
            Array.Empty<IntakeArtifact>(),
            new[] { new WorkerExecutionModification("Program.cs", "update", "Adjusted entry point.") },
            Array.Empty<ToolWarning>());

        runtime = ExecutionRuntimeController.ProduceProvidedResult(runtime, providedResult);
        runtime = ExecutionRuntimeController.RequestQcReview(runtime);
        runtime = ExecutionRuntimeController.AcceptQcReview(runtime);

        var processEvidence = new AcceptanceProcessEvidence(0, "ok", string.Empty, TimedOut: false, WasCanceled: false);
        runtime = ExecutionRuntimeController.ObserveAcceptanceAfterQc(
            runtime,
            root,
            processEvidence,
            "workspace unchanged");

        AssertTrue(runtime.AcceptanceEvaluation is not null, "Accepted runtime should expose acceptance evaluation after observation step.");
        AssertEqual(AcceptanceClassification.SafeApply, runtime.AcceptanceEvaluation!.Decision.Classification, "Acceptance observation should classify unchanged workspace as safe apply.");
        AssertEqual(AcceptanceDecisionStatus.Allowed, runtime.AcceptanceEvaluation!.Decision.Status, "Acceptance observation should remain read-only but allowed.");
        AssertEqual(QCReviewStatus.Accepted, runtime.QcStatus, "Acceptance observation must not alter accepted QC state.");
    }
    finally
    {
        DeleteScratchWorkspace(root);
    }
}

static void ExecutionRuntimeWatchdogInterruptionIsPreservedInAcceptanceEvidence()
{
    var root = CreateScratchWorkspace();
    try
    {
        File.WriteAllText(Path.Combine(root, "Program.cs"), "class Program {}");
        File.WriteAllText(Path.Combine(root, "project.csproj"), "<Project />");

        var taskId = "TASK-ACC-WD-001";
        var layer = UnifiedToolLayer.CreateDefault();
        var toolEnvelope = layer.InspectWorkspaceWithEnvelope(
            PromptRole.Qc,
            new WorkspaceInspectRequest("REQ-WS-WD-001", root, new[] { "Program.cs" }));
        var providedResult = new WorkerExecutionResult(
            "RESULT-ACC-WD-001",
            taskId,
            WorkerExecutionStatus.Success,
            "Worker prepared Program.cs update.",
            Array.Empty<IntakeArtifact>(),
            new[] { new WorkerExecutionModification("Program.cs", "update", "Adjusted entry point.") },
            Array.Empty<ToolWarning>());
        var interruption = new RuntimeInterruptionRecord(
            StopReason.TimeoutExceeded,
            new DateTimeOffset(2026, 04, 08, 12, 05, 00, TimeSpan.Zero),
            GracefulStopAttempted: true,
            HardKillRequired: true,
            "Execution watchdog interruption: reason=TimeoutExceeded, gracefulStop=True.");
        var runResult = new ExecutionRunResult(
            new ExecutionOutcome(ExecutionTarget.ActiveShiftSubsystem, ExecutionOutcomeStatus.Deferred, "Execution interrupted by host runtime (TimeoutExceeded)."),
            new ExecutionRecord(
                "SHIFT-ACC-WD-001",
                taskId,
                ExecutionTarget.ActiveShiftSubsystem,
                ExecutionOutcomeStatus.Deferred,
                "Execution interrupted by host runtime (TimeoutExceeded).",
                RuntimeProfile.ScopedLocalDefault,
                interruption),
            RuntimeProfile.ScopedLocalDefault);
        var processEvidence = new AcceptanceProcessEvidence(
            ExitCode: 124,
            StdoutSummary: "watchdog interrupted execution",
            StderrSummary: string.Empty,
            TimedOut: true,
            WasCanceled: false,
            interruption);

        var evaluation = AcceptanceEvaluationFactory.Create(
            root,
            new[] { "Program.cs" },
            "EXEC-WD-001",
            new RuntimeSelectionDecision(RuntimeProfile.ScopedLocalDefault, IsAllowed: true, "Scoped local selected for safe default execution."),
            runResult,
            providedResult,
            processEvidence,
            "workspace unchanged",
            toolExecution: toolEnvelope);

        AssertTrue(evaluation.Evidence.RuntimeInterruption is not null, "Acceptance evidence must preserve runtime interruption.");
        AssertEqual(StopReason.TimeoutExceeded, evaluation.Evidence.RuntimeInterruption!.Reason, "Acceptance evidence must preserve watchdog reason.");
        AssertTrue(evaluation.Evidence.Inputs.RuntimeInterruption is not null, "Acceptance inputs must preserve runtime interruption.");
        AssertContains(evaluation.Evidence.SummaryLine, "interruption=TimeoutExceeded", "Acceptance summary must surface interruption truth.");
        AssertTrue(evaluation.Evidence.ToolExecution is not null, "Acceptance evidence must still preserve tool envelope beside interruption.");
        AssertContains(evaluation.Evidence.ToolExecution!.EvidenceSummary, "tool=workspace.inspect", "Tool evidence must survive alongside runtime interruption.");
    }
    finally
    {
        DeleteScratchWorkspace(root);
    }
}

static WorkerExecutionResult CreateWorkerExecutionResult(string taskId, WorkerExecutionStatus status)
{
    return new WorkerExecutionResult(
        $"RESULT-{taskId}",
        taskId,
        status,
        "Worker execution completed.",
        Array.Empty<IntakeArtifact>(),
        new[]
        {
            new WorkerExecutionModification("Prompting/PromptAssembler.cs", "edit", "Adjusted deterministic rendering.")
        },
        Array.Empty<ToolWarning>(),
        status == WorkerExecutionStatus.Failed ? new ToolDiagnostic("EXECUTION_FAILED", "Execution failed.") : null);
}

static QCReviewResult CreateQcReviewResult(string resultId, QCReviewStatus status, string? rejectReason = null)
{
    return new QCReviewResult(
        $"REVIEW-{resultId}",
        resultId,
        status,
        new[] { "Review completed." },
        new[] { "DECISION://qc/review" },
        rejectReason);
}

static void AssertEqual<T>(T expected, T actual, string message)
{
    if (!EqualityComparer<T>.Default.Equals(expected, actual))
    {
        throw new InvalidOperationException($"{message} Expected: {expected}. Actual: {actual}.");
    }
}

static void AssertNotEqual<T>(T notExpected, T actual, string message)
{
    if (EqualityComparer<T>.Default.Equals(notExpected, actual))
    {
        throw new InvalidOperationException($"{message} Unexpected: {actual}.");
    }
}

static void AssertSequenceEqual<T>(IEnumerable<T> expected, IEnumerable<T> actual, string message)
{
    if (!expected.SequenceEqual(actual))
    {
        throw new InvalidOperationException($"{message} Expected: [{string.Join(", ", expected)}]. Actual: [{string.Join(", ", actual)}].");
    }
}

static void AssertContains(string text, string expected, string message)
{
    if (!text.Contains(expected, StringComparison.Ordinal))
    {
        throw new InvalidOperationException($"{message} Missing: {expected}.");
    }
}

static void AssertCapsuleV2Shape(string text, string expectedSourceStage)
{
    AssertContains(text, $"source_stage: {expectedSourceStage}", "Capsule v2 must expose source_stage marker.");
    var normalized = text.Replace("\r\n", "\n");
    var headings = normalized
        .Split('\n')
        .Where(static line => line.StartsWith("## ", StringComparison.Ordinal))
        .Select(static line => line.Trim())
        .ToArray();
    var expectedHeadings = new[]
    {
        "## Project identity",
        "## What this project is",
        "## Current direction",
        "## Current roadmap phase",
        "## Core canon rules",
        "## Current focus",
        "## Open risks / unresolved items",
        "## Canon completeness status"
    };

    AssertEqual(expectedHeadings.Length, headings.Length, "Capsule v2 must have exactly 8 sections.");
    for (var index = 0; index < expectedHeadings.Length; index++)
    {
        AssertEqual(expectedHeadings[index], headings[index], $"Capsule v2 section {index + 1} must match canon order.");
    }
}

static string ExtractMarkdownSection(string markdown, string heading)
{
    var normalized = markdown.Replace("\r\n", "\n");
    var start = normalized.IndexOf(heading, StringComparison.Ordinal);
    if (start < 0)
    {
        throw new InvalidOperationException($"Markdown section not found: {heading}.");
    }

    var next = normalized.IndexOf("\n## ", start + heading.Length, StringComparison.Ordinal);
    return (next < 0 ? normalized[start..] : normalized[start..next]).Trim();
}

static void LogIntentClassification(string text, IntentClassificationResult result)
{
    Console.WriteLine(result.ToDebugString(text));
}

static TException AssertThrows<TException>(Action action, string message, Func<TException, bool>? predicate = null) where TException : Exception
{
    try
    {
        action();
    }
    catch (TException exception)
    {
        if (predicate is not null && !predicate(exception))
        {
            throw new InvalidOperationException($"{message} Exception predicate failed for {typeof(TException).Name}.");
        }

        return exception;
    }

    throw new InvalidOperationException($"{message} Expected {typeof(TException).Name}.");
}

static void AssertTrue(bool condition, string message)
{
    if (!condition)
    {
        throw new InvalidOperationException(message);
    }
}

static void AssertFalse(bool condition, string message)
{
    if (condition)
    {
        throw new InvalidOperationException(message);
    }
}

static string CreateScratchWorkspace()
{
    var rootPath = Path.Combine(AppContext.BaseDirectory, "test-scratch", Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(rootPath);
    return rootPath;
}

static ProjectState CreateProjectState(string? activeShiftId, string? activeTaskId, string projectRoot = "C:\\zavod-test")
{
    var zavodRoot = $"{projectRoot}\\.zavod";
    var projectTruthRoot = $"{zavodRoot}\\project";
    var metaFilePath = $"{zavodRoot}\\meta\\project.json";

    return new ProjectState(
        "1.0",
        "zavod-test",
        "ZAVOD Test",
        "v1",
        "cold_start",
        activeShiftId,
        activeTaskId,
        new ProjectPaths(
            projectRoot,
            zavodRoot,
            metaFilePath,
            projectTruthRoot),
        new TruthPointers(
            $"{projectTruthRoot}\\project.md",
            $"{projectTruthRoot}\\direction.md",
            $"{projectTruthRoot}\\roadmap.md",
            $"{projectTruthRoot}\\canon.md",
            $"{projectTruthRoot}\\capsule.md"));
}

static void DeleteScratchWorkspace(string rootPath)
{
    if (Directory.Exists(rootPath))
    {
        Directory.Delete(rootPath, recursive: true);
    }
}

// ---------------------------------------------------------------------------
// Welcome Surface Selector tests (project_welcome_surface_v1.md)
// ---------------------------------------------------------------------------

static zavod.Persistence.ProjectDocumentSourceDescriptor WelcomeDocDescriptor(
    zavod.Persistence.ProjectDocumentKind kind,
    zavod.Persistence.ProjectDocumentStage stage,
    bool exists)
{
    return new zavod.Persistence.ProjectDocumentSourceDescriptor(kind, stage, $"preview/{kind}.md", exists);
}

static zavod.Persistence.ProjectDocumentSourceSelection WelcomeSelection(
    zavod.Persistence.ProjectDocumentStage activeStage,
    zavod.Persistence.ProjectDocumentStage? project = null,
    zavod.Persistence.ProjectDocumentStage? direction = null,
    zavod.Persistence.ProjectDocumentStage? roadmap = null,
    zavod.Persistence.ProjectDocumentStage? canon = null,
    zavod.Persistence.ProjectDocumentStage? capsule = null)
{
    zavod.Persistence.ProjectDocumentSourceDescriptor? Build(
        zavod.Persistence.ProjectDocumentKind kind,
        zavod.Persistence.ProjectDocumentStage? stage)
    {
        if (stage is null)
        {
            return null;
        }
        return WelcomeDocDescriptor(kind, stage.Value, true);
    }

    return new zavod.Persistence.ProjectDocumentSourceSelection(
        activeStage,
        Build(zavod.Persistence.ProjectDocumentKind.Project, project),
        Build(zavod.Persistence.ProjectDocumentKind.Direction, direction),
        Build(zavod.Persistence.ProjectDocumentKind.Roadmap, roadmap),
        Build(zavod.Persistence.ProjectDocumentKind.Canon, canon),
        Build(zavod.Persistence.ProjectDocumentKind.Capsule, capsule));
}

static void WelcomeSelectorR1OffersContinueWhenActiveShift()
{
    var selection = WelcomeSelection(
        zavod.Persistence.ProjectDocumentStage.CanonicalDocs,
        project: zavod.Persistence.ProjectDocumentStage.CanonicalDocs,
        roadmap: zavod.Persistence.ProjectDocumentStage.CanonicalDocs);
    var input = new WelcomeStateInput(selection, HasActiveShift: true, HasActiveTask: false, HasStaleSections: false, HasImportFailure: false);

    var result = WelcomeSurfaceSelector.Select(input);

    if (result.PrimaryRule != WelcomeSelectionRule.R1_ActiveShiftOrTask)
    {
        throw new Exception($"Expected R1, got {result.PrimaryRule}.");
    }
    if (!result.Actions.Contains(WelcomeAction.ContinueWorkCycle))
    {
        throw new Exception("R1 must offer ContinueWorkCycle.");
    }
    if (!result.Actions.Contains(WelcomeAction.OpenRoadmap))
    {
        throw new Exception("R1 with roadmap present must offer OpenRoadmap.");
    }
}

static void WelcomeSelectorR2OffersStartCycleOnCanonical5Of5()
{
    var selection = WelcomeSelection(
        zavod.Persistence.ProjectDocumentStage.CanonicalDocs,
        project: zavod.Persistence.ProjectDocumentStage.CanonicalDocs,
        direction: zavod.Persistence.ProjectDocumentStage.CanonicalDocs,
        roadmap: zavod.Persistence.ProjectDocumentStage.CanonicalDocs,
        canon: zavod.Persistence.ProjectDocumentStage.CanonicalDocs,
        capsule: zavod.Persistence.ProjectDocumentStage.CanonicalDocs);
    var input = new WelcomeStateInput(selection, HasActiveShift: false, HasActiveTask: false, HasStaleSections: false, HasImportFailure: false);

    var result = WelcomeSurfaceSelector.Select(input);

    if (result.PrimaryRule != WelcomeSelectionRule.R2_Canonical_5_of_5)
    {
        throw new Exception($"Expected R2, got {result.PrimaryRule}.");
    }
    if (!result.Actions.Contains(WelcomeAction.StartWorkCycle))
    {
        throw new Exception("R2 must offer StartWorkCycle.");
    }
    if (!result.Actions.Contains(WelcomeAction.OpenRoadmap))
    {
        throw new Exception("R2 must offer OpenRoadmap.");
    }
}

static void WelcomeSelectorR3OffersPromoteAndAuthorOnPartialCanonical()
{
    var selection = WelcomeSelection(
        zavod.Persistence.ProjectDocumentStage.CanonicalDocs,
        project: zavod.Persistence.ProjectDocumentStage.CanonicalDocs,
        direction: zavod.Persistence.ProjectDocumentStage.CanonicalDocs,
        roadmap: zavod.Persistence.ProjectDocumentStage.PreviewDocs);
    var input = new WelcomeStateInput(selection, HasActiveShift: false, HasActiveTask: false, HasStaleSections: false, HasImportFailure: false);

    var result = WelcomeSurfaceSelector.Select(input);

    if (result.PrimaryRule != WelcomeSelectionRule.R3_Canonical_Partial)
    {
        throw new Exception($"Expected R3, got {result.PrimaryRule}.");
    }
    if (!result.Actions.Contains(WelcomeAction.PromotePreviewToCanonical))
    {
        throw new Exception("R3 must offer PromotePreviewToCanonical.");
    }
    if (!result.Actions.Contains(WelcomeAction.AuthorCanonicalDoc))
    {
        throw new Exception("R3 must offer AuthorCanonicalDoc.");
    }
    if (result.Actions.Contains(WelcomeAction.StartWorkCycle))
    {
        throw new Exception("R3 must not offer StartWorkCycle until thin-memory mode is confirmed.");
    }
}

static void WelcomeSelectorR3RequiresThinMemoryConfirmationForStartCycle()
{
    var selection = WelcomeSelection(
        zavod.Persistence.ProjectDocumentStage.CanonicalDocs,
        project: zavod.Persistence.ProjectDocumentStage.CanonicalDocs,
        direction: zavod.Persistence.ProjectDocumentStage.CanonicalDocs,
        roadmap: zavod.Persistence.ProjectDocumentStage.PreviewDocs);

    var unconfirmed = WelcomeSurfaceSelector.Select(new WelcomeStateInput(
        selection,
        HasActiveShift: false,
        HasActiveTask: false,
        HasStaleSections: false,
        HasImportFailure: false));
    var confirmed = WelcomeSurfaceSelector.Select(new WelcomeStateInput(
        selection,
        HasActiveShift: false,
        HasActiveTask: false,
        HasStaleSections: false,
        HasImportFailure: false,
        HasThinMemoryModeConfirmed: true));

    if (unconfirmed.Actions.Contains(WelcomeAction.StartWorkCycle))
    {
        throw new Exception("R3 without thin-memory confirmation must not offer StartWorkCycle.");
    }
    if (!confirmed.Actions.Contains(WelcomeAction.StartWorkCycle))
    {
        throw new Exception("R3 with thin-memory confirmation must offer StartWorkCycle.");
    }
}

static void WelcomeSelectorR4OffersReviewAndPromoteWhenPreviewOnly()
{
    var selection = WelcomeSelection(
        zavod.Persistence.ProjectDocumentStage.PreviewDocs,
        project: zavod.Persistence.ProjectDocumentStage.PreviewDocs,
        capsule: zavod.Persistence.ProjectDocumentStage.PreviewDocs);
    var input = new WelcomeStateInput(selection, HasActiveShift: false, HasActiveTask: false, HasStaleSections: false, HasImportFailure: false);

    var result = WelcomeSurfaceSelector.Select(input);

    if (result.PrimaryRule != WelcomeSelectionRule.R4_Canonical_Zero_PreviewPresent)
    {
        throw new Exception($"Expected R4, got {result.PrimaryRule}.");
    }
    if (!result.Actions.Contains(WelcomeAction.ReviewPreviewDocs))
    {
        throw new Exception("R4 must offer ReviewPreviewDocs.");
    }
    if (!result.Actions.Contains(WelcomeAction.PromotePreviewToCanonical))
    {
        throw new Exception("R4 must offer PromotePreviewToCanonical.");
    }
    if (result.Actions.Contains(WelcomeAction.StartWorkCycle))
    {
        throw new Exception("R4 must not offer StartWorkCycle as primary action on preview-only state.");
    }
}

static void WelcomeSelectorR5OffersRetryAndAuthorOnEmptyState()
{
    var selection = WelcomeSelection(zavod.Persistence.ProjectDocumentStage.ImportPreview);
    var input = new WelcomeStateInput(selection, HasActiveShift: false, HasActiveTask: false, HasStaleSections: false, HasImportFailure: true);

    var result = WelcomeSurfaceSelector.Select(input);

    if (result.PrimaryRule != WelcomeSelectionRule.R5_Canonical_Zero_PreviewZero)
    {
        throw new Exception($"Expected R5, got {result.PrimaryRule}.");
    }
    if (!result.Actions.Contains(WelcomeAction.ImportRetry))
    {
        throw new Exception("R5 must offer ImportRetry.");
    }
    if (!result.Actions.Contains(WelcomeAction.AuthorCanonicalDoc))
    {
        throw new Exception("R5 must offer AuthorCanonicalDoc.");
    }
    if (result.Actions.Contains(WelcomeAction.StartWorkCycle))
    {
        throw new Exception("R5 must not offer StartWorkCycle on empty project state.");
    }
}

static void WelcomeSelectorR6OverlaysStaleReviewWhenStalePresent()
{
    var selection = WelcomeSelection(
        zavod.Persistence.ProjectDocumentStage.CanonicalDocs,
        project: zavod.Persistence.ProjectDocumentStage.CanonicalDocs,
        direction: zavod.Persistence.ProjectDocumentStage.CanonicalDocs,
        roadmap: zavod.Persistence.ProjectDocumentStage.CanonicalDocs,
        canon: zavod.Persistence.ProjectDocumentStage.CanonicalDocs,
        capsule: zavod.Persistence.ProjectDocumentStage.CanonicalDocs);
    var input = new WelcomeStateInput(selection, HasActiveShift: false, HasActiveTask: false, HasStaleSections: true, HasImportFailure: false);

    var result = WelcomeSurfaceSelector.Select(input);

    if (!result.StaleOverlayApplied)
    {
        throw new Exception("StaleOverlayApplied must be true when HasStaleSections is true.");
    }
    if (!result.Actions.Contains(WelcomeAction.ReviewStaleSections))
    {
        throw new Exception("R6 overlay must inject ReviewStaleSections into the action set.");
    }
}

static void WelcomeSelectorCapsOutputAt4Actions()
{
    var selection = WelcomeSelection(
        zavod.Persistence.ProjectDocumentStage.CanonicalDocs,
        project: zavod.Persistence.ProjectDocumentStage.CanonicalDocs,
        direction: zavod.Persistence.ProjectDocumentStage.CanonicalDocs,
        roadmap: zavod.Persistence.ProjectDocumentStage.CanonicalDocs,
        canon: zavod.Persistence.ProjectDocumentStage.CanonicalDocs,
        capsule: zavod.Persistence.ProjectDocumentStage.CanonicalDocs);
    // R2 produces 3 actions + stale overlay = 4, already at cap.
    var input = new WelcomeStateInput(selection, HasActiveShift: false, HasActiveTask: false, HasStaleSections: true, HasImportFailure: false);

    var result = WelcomeSurfaceSelector.Select(input);

    if (result.Actions.Count > 4)
    {
        throw new Exception($"Action count exceeded cap: {result.Actions.Count}.");
    }
}

static void WelcomeSelectorPadsBelowMinimumWithProjectAudit()
{
    // R1 with no roadmap -> only ContinueWorkCycle -> must pad to min 2 via ReviewProjectAudit.
    var selection = WelcomeSelection(zavod.Persistence.ProjectDocumentStage.ImportPreview);
    var input = new WelcomeStateInput(selection, HasActiveShift: true, HasActiveTask: false, HasStaleSections: false, HasImportFailure: false);

    var result = WelcomeSurfaceSelector.Select(input);

    if (result.Actions.Count < 2)
    {
        throw new Exception($"Action count below minimum: {result.Actions.Count}.");
    }
    if (!result.Actions.Contains(WelcomeAction.ReviewProjectAudit))
    {
        throw new Exception("Below-minimum set must be padded with ReviewProjectAudit.");
    }
}

static void WelcomeSelectorIsDeterministicForIdenticalInput()
{
    var selection = WelcomeSelection(
        zavod.Persistence.ProjectDocumentStage.PreviewDocs,
        project: zavod.Persistence.ProjectDocumentStage.PreviewDocs,
        capsule: zavod.Persistence.ProjectDocumentStage.PreviewDocs);
    var input = new WelcomeStateInput(selection, HasActiveShift: false, HasActiveTask: false, HasStaleSections: false, HasImportFailure: false);

    var a = WelcomeSurfaceSelector.Select(input);
    var b = WelcomeSurfaceSelector.Select(input);

    if (a.PrimaryRule != b.PrimaryRule)
    {
        throw new Exception("PrimaryRule must be deterministic.");
    }
    if (a.Actions.Count != b.Actions.Count)
    {
        throw new Exception("Action count must be deterministic.");
    }
    for (var i = 0; i < a.Actions.Count; i++)
    {
        if (a.Actions[i] != b.Actions[i])
        {
            throw new Exception($"Action order differs at index {i}: {a.Actions[i]} vs {b.Actions[i]}.");
        }
    }
}

// ---------------------------------------------------------------------------
// Work Packet bridge tests (project_work_packet_v1.md, B2)
//
// These tests cover both record-level Work Packet contract and the current
// unified prompt pipeline boundary for first-cycle Shift Lead packets.
// ---------------------------------------------------------------------------

static void WorkPacketInputDefaultsPreservePreB2CallShape()
{
    var capsule = CreateCapsule();
    var task = CreateTaskState(ContextIntentState.Validated, TaskStateStatus.Active, PromptRole.Worker);
    var shift = CreateShiftState(task);

    // Pre-B2 4-arg call shape must still compile and yield null/false Work Packet fields.
    var input = new PromptRequestInput(PromptRole.Worker, capsule, shift, task);

    if (input.CanonicalDocsStatus is not null)
    {
        throw new Exception("Default CanonicalDocsStatus must be null for pre-B2 call shape.");
    }
    if (input.PreviewStatus is not null)
    {
        throw new Exception("Default PreviewStatus must be null for pre-B2 call shape.");
    }
    if (input.MissingTruthWarnings is not null)
    {
        throw new Exception("Default MissingTruthWarnings must be null for pre-B2 call shape.");
    }
    if (input.IsFirstCycle)
    {
        throw new Exception("Default IsFirstCycle must be false for pre-B2 call shape.");
    }
}

static void WorkPacketInputCarriesCanonicalDocsStatusWhenProvided()
{
    var capsule = CreateCapsule();
    var task = CreateTaskState(ContextIntentState.Validated, TaskStateStatus.Active, PromptRole.Worker);
    var shift = CreateShiftState(task);
    var status = new CanonicalDocsStatus(
        DocumentCanonicalState.Canonical,
        DocumentCanonicalState.Canonical,
        DocumentCanonicalState.Preview,
        DocumentCanonicalState.Absent,
        DocumentCanonicalState.Canonical);
    var warnings = new[] { "canon.md absent, do not invent architectural invariants" };

    var input = new PromptRequestInput(
        PromptRole.Worker,
        capsule,
        shift,
        task,
        CanonicalDocsStatus: status,
        MissingTruthWarnings: warnings);

    if (input.CanonicalDocsStatus is null)
    {
        throw new Exception("CanonicalDocsStatus must be carried when provided.");
    }
    if (input.CanonicalDocsStatus.Roadmap != DocumentCanonicalState.Preview)
    {
        throw new Exception($"Roadmap state not preserved: {input.CanonicalDocsStatus.Roadmap}.");
    }
    if (input.MissingTruthWarnings is null || input.MissingTruthWarnings.Count != 1)
    {
        throw new Exception("MissingTruthWarnings must carry the provided list.");
    }
}

static void WorkPacketInputCarriesFirstCycleFlagAndPreviewStatus()
{
    var capsule = CreateCapsule();
    var task = CreateTaskState(ContextIntentState.Validated, TaskStateStatus.Active, PromptRole.Worker);
    var shift = CreateShiftState(task);
    var preview = new PreviewStatus(new[]
    {
        zavod.Persistence.ProjectDocumentKind.Project,
        zavod.Persistence.ProjectDocumentKind.Capsule
    });

    var input = new PromptRequestInput(
        PromptRole.Worker,
        capsule,
        shift,
        task,
        PreviewStatus: preview,
        IsFirstCycle: true);

    if (!input.IsFirstCycle)
    {
        throw new Exception("IsFirstCycle must be true when set.");
    }
    if (input.PreviewStatus is null || input.PreviewStatus.PreviewKinds.Count != 2)
    {
        throw new Exception("PreviewStatus must carry the provided kinds list.");
    }
}

static void WorkPacketMetadataDefaultsAreNullOrFalse()
{
    // Metadata constructor defaults must preserve pre-B2 call shape.
    var metadata = new PromptAssemblyMetadata("SHIFT-001", "TASK-001", 0, PromptTruthMode.Anchored);

    if (metadata.CanonicalDocsStatus is not null)
    {
        throw new Exception("Default metadata CanonicalDocsStatus must be null.");
    }
    if (metadata.PreviewStatus is not null)
    {
        throw new Exception("Default metadata PreviewStatus must be null.");
    }
    if (metadata.IsFirstCycle)
    {
        throw new Exception("Default metadata IsFirstCycle must be false.");
    }
}

static void PromptRequestPipelineOpensFirstCycleLeadPacketHonestly()
{
    var capsule = CreateCapsule();
    var firstCycleTask = CreateTaskState(ContextIntentState.Candidate, TaskStateStatus.Active, PromptRole.ShiftLead)
        with
        {
            TaskId = "FIRST-CYCLE",
            Description = "Inspect project memory and choose the first bounded action.",
            Scope = new[] { "project-root" },
            AcceptanceCriteria = Array.Empty<string>()
        };
    var emptyShift = new ShiftState(
        "SHIFT-001",
        "Start project work safely",
        null,
        ShiftStateStatus.Active,
        Array.Empty<StateTaskState>(),
        new[] { "Project has preview docs only" },
        Array.Empty<string>(),
        new[] { "Do not treat preview docs as canonical truth." });
    var status = new CanonicalDocsStatus(
        DocumentCanonicalState.Preview,
        DocumentCanonicalState.Preview,
        DocumentCanonicalState.Absent,
        DocumentCanonicalState.Absent,
        DocumentCanonicalState.Preview);
    var preview = new PreviewStatus(new[]
    {
        ProjectDocumentKind.Project,
        ProjectDocumentKind.Direction,
        ProjectDocumentKind.Capsule
    });

    var result = PromptRequestPipeline.Execute(new PromptRequestInput(
        PromptRole.ShiftLead,
        capsule,
        emptyShift,
        firstCycleTask,
        CanonicalDocsStatus: status,
        PreviewStatus: preview,
        MissingTruthWarnings: new[] { "roadmap.md absent: do not invent content for this kind." },
        IsFirstCycle: true));

    AssertEqual(PromptTruthMode.Anchored, result.TruthMode, "First-cycle Lead packet must still be anchored to structured project truth.");
    AssertTrue(result.Metadata.IsFirstCycle, "First-cycle metadata must remain observable.");
    AssertEqual("FIRST-CYCLE", result.Metadata.TaskId, "Synthetic first-cycle task id must be preserved as runtime metadata.");
    AssertContains(result.FinalPrompt, "CurrentStep: First work cycle", "First-cycle Lead prompt must expose first-cycle shift context.");
    AssertContains(result.FinalPrompt, "WorkPacket: first_cycle=true", "First-cycle Lead prompt must expose Work Packet first-cycle state.");
    AssertContains(result.FinalPrompt, "FirstCycleGuardrail: do not pretend the project is fully understood", "First-cycle Lead prompt must carry the honesty guardrail.");
    AssertContains(result.FinalPrompt, "WorkPacket: canonical_docs_count=0/5", "First-cycle Lead prompt must expose canonical doc count.");
    AssertContains(result.FinalPrompt, "WorkPacket: at_least_preview_count=3/5", "First-cycle Lead prompt must expose at-least-preview doc count.");
    AssertContains(result.FinalPrompt, "WorkPacketWarning: roadmap.md absent: do not invent content for this kind.", "First-cycle Lead prompt must carry missing truth warnings.");
}

static void PromptRequestPipelineKeepsFirstCycleWorkerGatedHonestly()
{
    var capsule = CreateCapsule();
    var firstCycleTask = CreateTaskState(ContextIntentState.Candidate, TaskStateStatus.Active, PromptRole.Worker)
        with
        {
            TaskId = "FIRST-CYCLE",
            Description = "Inspect project memory and choose the first bounded action.",
            Scope = new[] { "project-root" }
        };
    var emptyShift = new ShiftState(
        "SHIFT-001",
        "Start project work safely",
        null,
        ShiftStateStatus.Active,
        Array.Empty<StateTaskState>(),
        Array.Empty<string>(),
        Array.Empty<string>(),
        new[] { "Do not execute without a validated task." });

    AssertThrows<PromptRequestPipelineException>(
        () => PromptRequestPipeline.Execute(new PromptRequestInput(
            PromptRole.Worker,
            capsule,
            emptyShift,
            firstCycleTask,
            IsFirstCycle: true)),
        "First-cycle mode must not relax Worker validated-task requirements.");
}

static void LeadAgentPromptCarriesWorkPacketStatusHonestly()
{
    var status = new CanonicalDocsStatus(
        DocumentCanonicalState.Canonical,
        DocumentCanonicalState.Preview,
        DocumentCanonicalState.Absent,
        DocumentCanonicalState.Absent,
        DocumentCanonicalState.Canonical);
    var preview = new PreviewStatus(new[]
    {
        zavod.Persistence.ProjectDocumentKind.Direction
    });
    var client = new FakeOpenRouterExecutionClient(_ => new OpenRouterExecutionResponse(
        true,
        """
        {"intent_state":"refining","reply":"ok","scope_notes":"","task_brief":"","warnings":[]}
        """,
        "openrouter/test",
        200,
        null,
        "ok"));
    var runtime = new LeadAgentRuntime(clientFactory: _ => client);

    var result = runtime.Run(new LeadAgentInput(
        ProjectName: "demo",
        ProjectRoot: "C:\\demo",
        ProjectKind: "unknown",
        UserMessage: "what next?",
        PreClassifierIntentState: "Candidate",
        CurrentIntentSummary: string.Empty,
        AdvisoryNotes: Array.Empty<string>(),
        RecentTurns: Array.Empty<LeadAgentTurn>(),
        IsOrientationRequest: false,
        ProjectStackSummary: Array.Empty<string>(),
        CanonicalDocsStatus: status,
        PreviewStatus: preview,
        MissingTruthWarnings: new[] { "roadmap.md absent: do not invent content for this kind." },
        IsFirstCycle: true));

    AssertTrue(result.Success, "Fake Lead response should parse successfully.");
    var prompt = client.LastRequest?.UserPrompt ?? string.Empty;
    AssertContains(prompt, "WORK PACKET (project truth status; preview is below canonical)", "Lead prompt must include model-facing Work Packet truth status.");
    AssertContains(prompt, "- first_cycle: true", "Lead prompt must expose first-cycle status.");
    AssertContains(prompt, "first_cycle_guidance: determine whether project memory is mature enough for direct execution", "Lead prompt must include first-cycle guidance.");
    AssertContains(prompt, "first_cycle_guardrail: do not pretend the project is fully understood", "Lead prompt must include first-cycle honesty guardrail.");
    AssertContains(prompt, "project=Canonical; direction=Preview; roadmap=Absent; canon=Absent; capsule=Canonical", "Lead prompt must preserve canonical doc status per kind.");
    AssertContains(prompt, "- canonical_docs_count: 2/5", "Lead prompt must expose canonical count.");
    AssertContains(prompt, "- at_least_preview_count: 3/5", "Lead prompt must expose at-least-preview count.");
    AssertContains(prompt, "- preview_docs: Direction", "Lead prompt must list preview doc kinds.");
    AssertContains(prompt, "roadmap.md absent: do not invent content for this kind.", "Lead prompt must carry missing truth warnings.");
}

static void WorkerAgentPromptCarriesWorkPacketStatusHonestly()
{
    var status = new CanonicalDocsStatus(
        DocumentCanonicalState.Canonical,
        DocumentCanonicalState.Preview,
        DocumentCanonicalState.Absent,
        DocumentCanonicalState.Absent,
        DocumentCanonicalState.Canonical);
    var preview = new PreviewStatus(new[]
    {
        zavod.Persistence.ProjectDocumentKind.Direction
    });
    var client = new FakeOpenRouterExecutionClient(_ => new OpenRouterExecutionResponse(
        true,
        """
        {"status":"failed","summary":"No edits for test.","plan":[],"actions":[],"modifications":[],"edits":[],"blockers":[],"risks":[],"warnings":[]}
        """,
        "openrouter/test",
        200,
        null,
        "ok"));
    var runtime = new WorkerAgentRuntime(clientFactory: _ => client);

    var result = runtime.Run(new WorkerAgentInput(
        ProjectName: "demo",
        ProjectRoot: "C:\\demo",
        ProjectKind: "unknown",
        TaskId: "TASK-001",
        TaskDescription: "Check first task",
        Scope: new[] { "src" },
        AcceptanceCriteria: new[] { "Stay bounded." },
        AdvisoryNotes: Array.Empty<string>(),
        Anchors: Array.Empty<string>(),
        RevisionNotes: null,
        CanonicalDocsStatus: status,
        PreviewStatus: preview,
        MissingTruthWarnings: new[] { "canon.md absent: do not invent content for this kind." },
        IsFirstCycle: true));

    AssertTrue(result.Success, "Fake Worker response should parse successfully.");
    var prompt = client.LastRequest?.UserPrompt ?? string.Empty;
    AssertContains(prompt, "WORK PACKET (project truth status; preview is below canonical)", "Worker prompt must include model-facing Work Packet truth status.");
    AssertContains(prompt, "- first_cycle: true", "Worker prompt must expose first-cycle status.");
    AssertContains(prompt, "project=Canonical; direction=Preview; roadmap=Absent; canon=Absent; capsule=Canonical", "Worker prompt must preserve canonical doc status per kind.");
    AssertContains(prompt, "- canonical_docs_count: 2/5", "Worker prompt must expose canonical count.");
    AssertContains(prompt, "- at_least_preview_count: 3/5", "Worker prompt must expose at-least-preview count.");
    AssertContains(prompt, "- preview_docs: Direction", "Worker prompt must list preview doc kinds.");
    AssertContains(prompt, "canon.md absent: do not invent content for this kind.", "Worker prompt must carry missing truth warnings.");
}

static void QcAgentPromptCarriesWorkPacketStatusHonestly()
{
    var status = new CanonicalDocsStatus(
        DocumentCanonicalState.Canonical,
        DocumentCanonicalState.Preview,
        DocumentCanonicalState.Absent,
        DocumentCanonicalState.Absent,
        DocumentCanonicalState.Canonical);
    var preview = new PreviewStatus(new[]
    {
        zavod.Persistence.ProjectDocumentKind.Direction
    });
    var client = new FakeOpenRouterExecutionClient(_ => new OpenRouterExecutionResponse(
        true,
        """
        {"decision":"REVISE","rationale":"Needs bounded review.","issues":[],"next_action":"Revise."}
        """,
        "openrouter/test",
        200,
        null,
        "ok"));
    var runtime = new QcAgentRuntime(clientFactory: _ => client);

    var result = runtime.Run(new QcAgentInput(
        ProjectName: "demo",
        ProjectRoot: "C:\\demo",
        ProjectKind: "unknown",
        TaskId: "TASK-001",
        TaskDescription: "Check first task",
        AcceptanceCriteria: new[] { "Stay bounded." },
        WorkerStatus: "success",
        WorkerSummary: "Prepared change.",
        WorkerBlockers: Array.Empty<string>(),
        WorkerWarnings: Array.Empty<string>(),
        WorkerModifications: new[] { "edit: src/App.cs - update" },
        StagedArtifacts: new[] { "edit: src/App.cs (origin=1B -> staged=2B, sha256=abc)" },
        CanonicalDocsStatus: status,
        PreviewStatus: preview,
        MissingTruthWarnings: new[] { "roadmap.md absent: do not invent content for this kind." },
        IsFirstCycle: true));

    AssertTrue(result.Success, "Fake QC response should parse successfully.");
    var prompt = client.LastRequest?.UserPrompt ?? string.Empty;
    AssertContains(prompt, "WORK PACKET (project truth status; preview is below canonical)", "QC prompt must include model-facing Work Packet truth status.");
    AssertContains(prompt, "- first_cycle: true", "QC prompt must expose first-cycle status.");
    AssertContains(prompt, "project=Canonical; direction=Preview; roadmap=Absent; canon=Absent; capsule=Canonical", "QC prompt must preserve canonical doc status per kind.");
    AssertContains(prompt, "- canonical_docs_count: 2/5", "QC prompt must expose canonical count.");
    AssertContains(prompt, "- at_least_preview_count: 3/5", "QC prompt must expose at-least-preview count.");
    AssertContains(prompt, "- preview_docs: Direction", "QC prompt must list preview doc kinds.");
    AssertContains(prompt, "roadmap.md absent: do not invent content for this kind.", "QC prompt must carry missing truth warnings.");
}

static zavod.Persistence.ProjectDocumentSourceSelection BuildSelectionForBuilder(
    zavod.Persistence.ProjectDocumentStage? project = null,
    zavod.Persistence.ProjectDocumentStage? direction = null,
    zavod.Persistence.ProjectDocumentStage? roadmap = null,
    zavod.Persistence.ProjectDocumentStage? canon = null,
    zavod.Persistence.ProjectDocumentStage? capsule = null)
{
    zavod.Persistence.ProjectDocumentSourceDescriptor? D(
        zavod.Persistence.ProjectDocumentKind kind,
        zavod.Persistence.ProjectDocumentStage? stage)
    {
        if (stage is null)
        {
            return null;
        }
        return new zavod.Persistence.ProjectDocumentSourceDescriptor(kind, stage.Value, $"path/{kind}.md", true);
    }
    return new zavod.Persistence.ProjectDocumentSourceSelection(
        zavod.Persistence.ProjectDocumentStage.CanonicalDocs,
        D(zavod.Persistence.ProjectDocumentKind.Project, project),
        D(zavod.Persistence.ProjectDocumentKind.Direction, direction),
        D(zavod.Persistence.ProjectDocumentKind.Roadmap, roadmap),
        D(zavod.Persistence.ProjectDocumentKind.Canon, canon),
        D(zavod.Persistence.ProjectDocumentKind.Capsule, capsule));
}

static void WorkPacketBuilderMapsSelectionToCanonicalStatusHonestly()
{
    var selection = BuildSelectionForBuilder(
        project: zavod.Persistence.ProjectDocumentStage.CanonicalDocs,
        direction: zavod.Persistence.ProjectDocumentStage.PreviewDocs,
        canon: zavod.Persistence.ProjectDocumentStage.ImportPreview);
    var status = WorkPacketBuilder.BuildCanonicalDocsStatus(selection);

    if (status.Project != DocumentCanonicalState.Canonical)
    {
        throw new Exception($"Project must map to Canonical, got {status.Project}.");
    }
    if (status.Direction != DocumentCanonicalState.Preview)
    {
        throw new Exception($"Direction PreviewDocs must map to Preview, got {status.Direction}.");
    }
    if (status.Canon != DocumentCanonicalState.Preview)
    {
        throw new Exception($"Canon ImportPreview must also map to Preview, got {status.Canon}.");
    }
    if (status.Roadmap != DocumentCanonicalState.Absent)
    {
        throw new Exception($"Missing Roadmap must map to Absent, got {status.Roadmap}.");
    }
    if (status.Capsule != DocumentCanonicalState.Absent)
    {
        throw new Exception($"Missing Capsule must map to Absent, got {status.Capsule}.");
    }
}

static void WorkPacketBuilderReturnsNullPreviewStatusWhen5Of5Canonical()
{
    var selection = BuildSelectionForBuilder(
        project: zavod.Persistence.ProjectDocumentStage.CanonicalDocs,
        direction: zavod.Persistence.ProjectDocumentStage.CanonicalDocs,
        roadmap: zavod.Persistence.ProjectDocumentStage.CanonicalDocs,
        canon: zavod.Persistence.ProjectDocumentStage.CanonicalDocs,
        capsule: zavod.Persistence.ProjectDocumentStage.CanonicalDocs);
    var preview = WorkPacketBuilder.BuildPreviewStatus(selection);

    if (preview is not null)
    {
        throw new Exception("PreviewStatus must be null when all 5 docs are canonical.");
    }
}

static void WorkPacketBuilderListsPreviewKindsWhenMixed()
{
    var selection = BuildSelectionForBuilder(
        project: zavod.Persistence.ProjectDocumentStage.CanonicalDocs,
        direction: zavod.Persistence.ProjectDocumentStage.PreviewDocs,
        capsule: zavod.Persistence.ProjectDocumentStage.PreviewDocs);
    var preview = WorkPacketBuilder.BuildPreviewStatus(selection);

    if (preview is null)
    {
        throw new Exception("PreviewStatus must be present when at least one doc is preview.");
    }
    if (preview.PreviewKinds.Count != 2)
    {
        throw new Exception($"PreviewStatus must list 2 preview kinds (Direction, Capsule), got {preview.PreviewKinds.Count}.");
    }
    if (!preview.PreviewKinds.Contains(zavod.Persistence.ProjectDocumentKind.Direction))
    {
        throw new Exception("PreviewStatus must include Direction as preview kind.");
    }
    if (!preview.PreviewKinds.Contains(zavod.Persistence.ProjectDocumentKind.Capsule))
    {
        throw new Exception("PreviewStatus must include Capsule as preview kind.");
    }
    if (preview.PreviewKinds.Contains(zavod.Persistence.ProjectDocumentKind.Project))
    {
        throw new Exception("PreviewStatus must NOT include Project (it is canonical).");
    }
}

static void WorkPacketBuilderProducesHonestWarningsForAbsentAndPreview()
{
    var status = new CanonicalDocsStatus(
        DocumentCanonicalState.Canonical,  // project — no warning
        DocumentCanonicalState.Preview,    // direction — preview warning
        DocumentCanonicalState.Absent,     // roadmap — absent warning
        DocumentCanonicalState.Absent,     // canon — absent warning
        DocumentCanonicalState.Canonical); // capsule — no warning
    var warnings = WorkPacketBuilder.BuildMissingTruthWarnings(status);

    if (warnings.Count != 3)
    {
        throw new Exception($"Expected 3 warnings (1 preview + 2 absent), got {warnings.Count}.");
    }
    var joined = string.Join("\n", warnings);
    if (!joined.Contains("roadmap.md absent"))
    {
        throw new Exception("Warnings must mention roadmap.md absent.");
    }
    if (!joined.Contains("canon.md absent"))
    {
        throw new Exception("Warnings must mention canon.md absent.");
    }
    if (!joined.Contains("direction.md available as preview only"))
    {
        throw new Exception("Warnings must mention direction.md preview only.");
    }
    if (joined.Contains("project.md"))
    {
        throw new Exception("Warnings must NOT mention canonical project.md.");
    }
}

static void CanonicalDocsStatusCountsCanonicalAndAtLeastPreviewHonestly()
{
    var mixed = new CanonicalDocsStatus(
        DocumentCanonicalState.Canonical,  // project
        DocumentCanonicalState.Canonical,  // direction
        DocumentCanonicalState.Preview,    // roadmap
        DocumentCanonicalState.Absent,     // canon
        DocumentCanonicalState.Stale);     // capsule

    if (mixed.CanonicalCount != 2)
    {
        throw new Exception($"CanonicalCount must be 2, got {mixed.CanonicalCount}.");
    }
    // AtLeastPreview counts Preview + Canonical + Stale (Stale is canonical-but-falsified, still coverage).
    if (mixed.AtLeastPreviewCount != 4)
    {
        throw new Exception($"AtLeastPreviewCount must be 4 (Canonical x2 + Preview + Stale), got {mixed.AtLeastPreviewCount}.");
    }

    var empty = new CanonicalDocsStatus(
        DocumentCanonicalState.Absent,
        DocumentCanonicalState.Absent,
        DocumentCanonicalState.Absent,
        DocumentCanonicalState.Absent,
        DocumentCanonicalState.Absent);
    if (empty.CanonicalCount != 0 || empty.AtLeastPreviewCount != 0)
    {
        throw new Exception("Empty status must have zero counts.");
    }
}

sealed class FakeExternalProcessRunner(Func<ExternalProcessRequest, ExternalProcessResult> handler) : IExternalProcessRunner
{
    public ExternalProcessResult Run(ExternalProcessRequest request)
    {
        return handler(request);
    }
}

sealed class FakeOpenRouterExecutionClient(Func<OpenRouterExecutionRequest, OpenRouterExecutionResponse> handler) : IOpenRouterExecutionClient
{
    public OpenRouterExecutionRequest? LastRequest { get; private set; }

    public OpenRouterExecutionResponse Execute(OpenRouterExecutionRequest request)
    {
        LastRequest = request;
        return handler(request);
    }
}

sealed class FakeHttpMessageHandler(Func<HttpRequestMessage, CancellationToken, HttpResponseMessage> handler) : HttpMessageHandler
{
    protected override HttpResponseMessage Send(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        return handler(request, cancellationToken);
    }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        return Task.FromResult(handler(request, cancellationToken));
    }
}
