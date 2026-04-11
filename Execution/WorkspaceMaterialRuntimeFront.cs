using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using zavod.Workspace;

namespace zavod.Execution;

public sealed class WorkspaceMaterialRuntimeFront(
    TextMaterialRuntimeService? textService = null,
    PdfExtractionRuntimeService? pdfService = null,
    ArchiveInspectionRuntimeService? archiveService = null,
    ImageInspectionRuntimeService? imageService = null)
{
    public const int DefaultMaxMaterials = 12;
    public const int DefaultMaxCharsPerMaterial = 3200;
    public const int DefaultMaxTechnicalEvidence = 12;
    public const int DefaultAdaptivePreviewCharFloor = 18000;

    private readonly TextMaterialRuntimeService _textService = textService ?? new TextMaterialRuntimeService();
    private readonly PdfExtractionRuntimeService _pdfService = pdfService ?? new PdfExtractionRuntimeService();
    private readonly ArchiveInspectionRuntimeService _archiveService = archiveService ?? new ArchiveInspectionRuntimeService();
    private readonly ImageInspectionRuntimeService _imageService = imageService ?? new ImageInspectionRuntimeService();

    public WorkspaceImportMaterialPreviewPacket BuildPreviewPacket(
        WorkspaceScanResult scanResult,
        int maxMaterials = DefaultMaxMaterials,
        int maxCharsPerMaterial = DefaultMaxCharsPerMaterial)
    {
        ArgumentNullException.ThrowIfNull(scanResult);
        if (maxMaterials <= 0 || maxCharsPerMaterial <= 0)
        {
            return new WorkspaceImportMaterialPreviewPacket(
                scanResult.State.WorkspaceRoot,
                scanResult.State.ImportKind,
                scanResult.State.Summary.SourceRoots,
                Array.Empty<WorkspaceTechnicalPreviewInput>(),
                Array.Empty<WorkspaceMaterialPreviewInput>(),
                WorkspaceEvidencePackBuilder.Build(scanResult, Array.Empty<WorkspaceTechnicalPreviewInput>(), Array.Empty<WorkspaceMaterialPreviewInput>()));
        }

        var technicalEvidence = BuildTechnicalEvidence(scanResult, maxCharsPerMaterial);
        var selectedCandidates = SelectImportCandidates(scanResult, maxMaterials);
        var preparedResults = selectedCandidates
            .Select(candidate => Prepare(scanResult.State.WorkspaceRoot, candidate, maxCharsPerMaterial))
            .ToList();

        ExpandPreparedResultsIfSignalIsThin(
            scanResult,
            technicalEvidence,
            maxMaterials,
            maxCharsPerMaterial,
            preparedResults);

        var materials = preparedResults
            .Select(static result => new WorkspaceMaterialPreviewInput(
                result.DisplayPath,
                result.Kind,
                result.SelectionReason,
                result.ExtractedText,
                result.WasTruncated,
                result.Status.ToString(),
                result.BackendId,
                result.Summary))
            .ToArray();

        var packetMaterials = materials;
        var evidencePack = WorkspaceEvidencePackBuilder.Build(scanResult, technicalEvidence, packetMaterials);

        return new WorkspaceImportMaterialPreviewPacket(
            scanResult.State.WorkspaceRoot,
            scanResult.State.ImportKind,
            scanResult.State.Summary.SourceRoots,
            technicalEvidence,
            packetMaterials,
            evidencePack);
    }

    private static WorkspaceMaterialCandidate[] SelectImportCandidates(WorkspaceScanResult scanResult, int maxMaterials)
    {
        var buildRoots = scanResult.State.Summary.BuildRoots;
        var orderedCandidates = scanResult.MaterialCandidates
            .Where(candidate => IsImportFacing(candidate.Kind))
            .OrderBy(candidate => GetKindPriority(candidate.Kind))
            .ThenBy(candidate => GetNoisePenalty(candidate.RelativePath, candidate.Kind, buildRoots))
            .ThenByDescending(candidate => GetSignalBoost(candidate.RelativePath, candidate.Kind))
            .ThenBy(candidate => GetPathDepth(candidate.RelativePath))
            .ThenBy(candidate => candidate.RelativePath, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (orderedCandidates.Length <= maxMaterials)
        {
            return orderedCandidates;
        }

        var imageSoftCap = GetImageSoftCap(orderedCandidates, maxMaterials);
        var selected = new List<WorkspaceMaterialCandidate>(maxMaterials);
        foreach (var kind in GetImportFacingKindsInPriorityOrder())
        {
            var firstOfKind = orderedCandidates.FirstOrDefault(candidate => candidate.Kind == kind);
            if (firstOfKind is null)
            {
                continue;
            }

            selected.Add(firstOfKind);
            if (selected.Count == maxMaterials)
            {
                return OrderSelectedCandidates(selected, buildRoots);
            }
        }

        foreach (var candidate in GetFillCandidates(orderedCandidates, buildRoots))
        {
            if (selected.Any(existing => string.Equals(existing.RelativePath, candidate.RelativePath, StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            if (ShouldSkipInBoundedFill(candidate, selected, orderedCandidates, buildRoots))
            {
                continue;
            }

            if (candidate.Kind == WorkspaceMaterialKind.ImageAsset &&
                selected.Count(existing => existing.Kind == WorkspaceMaterialKind.ImageAsset) >= imageSoftCap)
            {
                continue;
            }

            selected.Add(candidate);
            if (selected.Count == maxMaterials)
            {
                return OrderSelectedCandidates(selected, buildRoots);
            }
        }

        foreach (var candidate in GetFillCandidates(orderedCandidates, buildRoots))
        {
            if (selected.Any(existing => string.Equals(existing.RelativePath, candidate.RelativePath, StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            if (candidate.Kind == WorkspaceMaterialKind.ImageAsset &&
                selected.Count(existing => existing.Kind == WorkspaceMaterialKind.ImageAsset) >= imageSoftCap)
            {
                continue;
            }

            if (ShouldSkipInBoundedFill(candidate, selected, orderedCandidates, buildRoots))
            {
                continue;
            }

            selected.Add(candidate);
            if (selected.Count == maxMaterials)
            {
                break;
            }
        }

        return OrderSelectedCandidates(selected, buildRoots);
    }

    private MaterialRuntimeResult Prepare(string workspaceRoot, WorkspaceMaterialCandidate candidate, int maxChars)
    {
        var request = new MaterialRuntimeRequest(candidate.RelativePath, Path.Combine(workspaceRoot, candidate.RelativePath), candidate.Kind, GetSelectionReason(candidate.Kind), maxChars);

        return candidate.Kind switch
        {
            WorkspaceMaterialKind.TextDocument => _textService.Prepare(request),
            WorkspaceMaterialKind.PdfDocument => _pdfService.Prepare(request),
            WorkspaceMaterialKind.ArchiveArtifact => _archiveService.Prepare(request),
            WorkspaceMaterialKind.ImageAsset => _imageService.Prepare(request),
            _ => new MaterialRuntimeResult(
                candidate.RelativePath,
                candidate.Kind,
                GetSelectionReason(candidate.Kind),
                MaterialRuntimeStatus.UnsupportedKind,
                "runtime-front",
                false,
                string.Empty,
                false,
                Array.Empty<string>(),
                new MaterialRuntimeDiagnostic("MATERIAL_KIND_UNSUPPORTED", "Workspace material kind is outside the import-facing runtime front."),
                $"Material evidence: path={candidate.RelativePath}, backend=runtime-front, status=UnsupportedKind, fallback=False.",
                "Workspace material kind is outside the import-facing runtime front.").Normalize()
        };
    }

    private void ExpandPreparedResultsIfSignalIsThin(
        WorkspaceScanResult scanResult,
        IReadOnlyList<WorkspaceTechnicalPreviewInput> technicalEvidence,
        int maxMaterials,
        int maxCharsPerMaterial,
        List<MaterialRuntimeResult> preparedResults)
    {
        if (preparedResults.Count >= maxMaterials)
        {
            return;
        }

        if (CountMeaningfulTextMaterials(preparedResults) > 1)
        {
            return;
        }

        if (EstimatePreparedSignalChars(preparedResults, technicalEvidence) >= DefaultAdaptivePreviewCharFloor)
        {
            return;
        }

        var buildRoots = scanResult.State.Summary.BuildRoots;
        var orderedExpansionCandidates = scanResult.MaterialCandidates
            .Where(candidate => IsImportFacing(candidate.Kind))
            .Where(candidate => !preparedResults.Any(existing => string.Equals(existing.DisplayPath, candidate.RelativePath, StringComparison.OrdinalIgnoreCase)))
            .Where(candidate => !ShouldSkipAdaptiveExpansion(candidate, buildRoots))
            .OrderBy(candidate => GetNoisePenalty(candidate.RelativePath, candidate.Kind, buildRoots))
            .ThenByDescending(candidate => GetSignalBoost(candidate.RelativePath, candidate.Kind))
            .ThenBy(candidate => GetPathDepth(candidate.RelativePath))
            .ThenBy(candidate => candidate.RelativePath, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        foreach (var candidate in orderedExpansionCandidates)
        {
            preparedResults.Add(Prepare(scanResult.State.WorkspaceRoot, candidate, maxCharsPerMaterial));
            if (preparedResults.Count >= maxMaterials)
            {
                break;
            }
        }
    }

    private static WorkspaceTechnicalPreviewInput[] BuildTechnicalEvidence(WorkspaceScanResult scanResult, int maxCharsPerMaterial)
    {
        return scanResult.RelevantFiles
            .Where(IsTechnicalEvidenceFile)
            .Select(path => BuildTechnicalEvidenceItem(scanResult.State.WorkspaceRoot, path, maxCharsPerMaterial))
            .Where(static item => item is not null)
            .Cast<WorkspaceTechnicalPreviewInput>()
            .OrderBy(item => GetTechnicalEvidencePriority(item.RelativePath))
            .ThenBy(item => GetPathDepth(item.RelativePath))
            .ThenBy(item => item.RelativePath, StringComparer.OrdinalIgnoreCase)
            .Take(DefaultMaxTechnicalEvidence)
            .ToArray();
    }

    private static bool IsImportFacing(WorkspaceMaterialKind kind)
    {
        return kind is WorkspaceMaterialKind.TextDocument
            or WorkspaceMaterialKind.PdfDocument
            or WorkspaceMaterialKind.ArchiveArtifact
            or WorkspaceMaterialKind.ImageAsset;
    }

    private static bool IsTechnicalEvidenceFile(string fullPath)
    {
        var fileName = Path.GetFileName(fullPath);
        var extension = Path.GetExtension(fullPath);

        if (string.Equals(fileName, "CMakePresets.json", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(fileName, "CMakeLists.txt", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(fileName, "CMakeCache.txt", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(fileName, "go.mod", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(fileName, "Cargo.toml", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(fileName, "pyproject.toml", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(fileName, "requirements.txt", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(fileName, "package.json", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(fileName, "tsconfig.json", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(fileName, "vite.config.ts", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(fileName, "vite.config.js", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(fileName, "webpack.config.js", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(fileName, "next.config.js", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(fileName, "nuxt.config.ts", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(fileName, "Dockerfile", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(fileName, "docker-compose.yml", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(fileName, "docker-compose.yaml", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(fileName, "compose.yml", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(fileName, "compose.yaml", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(fileName, "Makefile", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(fileName, ".sln", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(fileName, "Directory.Build.props", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(fileName, "Directory.Build.targets", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return string.Equals(extension, ".cmake", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(extension, ".csproj", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(extension, ".vcxproj", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(extension, ".props", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(extension, ".targets", StringComparison.OrdinalIgnoreCase);
    }

    private static WorkspaceTechnicalPreviewInput? BuildTechnicalEvidenceItem(string workspaceRoot, string fullPath, int maxCharsPerMaterial)
    {
        if (!File.Exists(fullPath))
        {
            return null;
        }

        try
        {
            var rawText = File.ReadAllText(fullPath);
            var normalized = TextMaterialRuntimeService.NormalizeText(rawText);
            if (normalized.Length == 0)
            {
                return null;
            }

            var wasTruncated = normalized.Length > maxCharsPerMaterial;
            var preview = wasTruncated ? normalized[..maxCharsPerMaterial] : normalized;
            return new WorkspaceTechnicalPreviewInput(
                Path.GetRelativePath(workspaceRoot, fullPath),
                GetTechnicalEvidenceCategory(fullPath),
                preview,
                wasTruncated);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    private static int GetKindPriority(WorkspaceMaterialKind kind)
    {
        return kind switch
        {
            WorkspaceMaterialKind.TextDocument => 0,
            WorkspaceMaterialKind.PdfDocument => 1,
            WorkspaceMaterialKind.ArchiveArtifact => 2,
            WorkspaceMaterialKind.ImageAsset => 3,
            _ => int.MaxValue
        };
    }

    private static WorkspaceMaterialKind[] GetImportFacingKindsInPriorityOrder()
    {
        return
        [
            WorkspaceMaterialKind.TextDocument,
            WorkspaceMaterialKind.PdfDocument,
            WorkspaceMaterialKind.ArchiveArtifact,
            WorkspaceMaterialKind.ImageAsset
        ];
    }

    private static int GetPathDepth(string relativePath)
    {
        return relativePath.Count(static ch => ch is '\\' or '/');
    }

    private static int GetNoisePenalty(string relativePath, WorkspaceMaterialKind kind, IReadOnlyList<string> buildRoots)
    {
        var normalizedPath = relativePath.Replace('/', '\\');
        var fileNameWithoutExtension = Path.GetFileNameWithoutExtension(normalizedPath);
        var penalty = 0;

        if (IsUnderBuildRoot(normalizedPath, buildRoots))
        {
            penalty += 3;
            if (kind == WorkspaceMaterialKind.TextDocument)
            {
                penalty += 2;
            }
        }

        if (kind == WorkspaceMaterialKind.TextDocument)
        {
            if (ContainsAny(fileNameWithoutExtension, "license", "copying", "copyright", "ofl", "fontlog"))
            {
                penalty += 4;
            }

            if (ContainsAny(fileNameWithoutExtension,
                "log", "trace", "dump", "stdout", "stderr", "coverage", "results", "result", "output", "projection"))
            {
                penalty += 3;
            }

            if (ContainsAny(normalizedPath,
                "test-output", "test_output", "test-results", "test_results", "\\artifacts\\", "\\coverage\\"))
            {
                penalty += 2;
            }

            if (ContainsAny(fileNameWithoutExtension,
                "todo", "note", "notes", "draft", "reminder", "cheatsheet", "cheat-sheet", "how to", "plan",
                "РїСЂРёРјРµСЂРЅС‹Р№", "С€РїР°СЂРіР°Р»РєР°", "РґР»СЏ СЃРµР±СЏ", "РЅРµ Р·Р°Р±С‹С‚СЊ"))
            {
                penalty += 2;
            }

            if (ContainsAny(normalizedPath,
                "\\notes\\", "\\personal\\", "\\drafts\\", "\\scratch\\"))
            {
                penalty += 1;
            }
        }
        else if (kind == WorkspaceMaterialKind.ImageAsset)
        {
            if (ContainsAny(normalizedPath,
                "\\assets\\flats\\", "\\assets\\textures\\", "\\assets\\sprites\\", "\\assets\\tiles\\", "\\textures\\", "\\sprites\\", "\\tiles\\"))
            {
                penalty += 3;
            }

            if (ContainsAny(fileNameWithoutExtension, "favicon"))
            {
                penalty += 1;
            }
        }

        return penalty;
    }

    private static int GetSignalBoost(string relativePath, WorkspaceMaterialKind kind)
    {
        var normalizedPath = relativePath.Replace('/', '\\');
        var fileNameWithoutExtension = Path.GetFileNameWithoutExtension(normalizedPath);
        var boost = 0;

        if (kind == WorkspaceMaterialKind.TextDocument)
        {
            if (ContainsAny(fileNameWithoutExtension,
                "readme", "architecture", "overview", "summary", "spec", "design", "constitution", "roadmap", "guide", "documentation"))
            {
                boost += 4;
            }

            if (ContainsAny(normalizedPath,
                "\\docs\\", "\\documentation\\", "\\architecture\\", "\\research\\", "\\files\\", "\\agent\\"))
            {
                boost += 2;
            }
        }

        if (kind == WorkspaceMaterialKind.PdfDocument && ContainsAny(fileNameWithoutExtension, "summary", "architecture", "overview", "report", "research"))
        {
            boost += 2;
        }

        if (kind == WorkspaceMaterialKind.ArchiveArtifact && ContainsAny(fileNameWithoutExtension, "sandbox", "source", "project", "workspace"))
        {
            boost += 1;
        }

        if (kind == WorkspaceMaterialKind.ImageAsset && ContainsAny(fileNameWithoutExtension, "preview", "screenshot", "cover", "poster", "icon", "logo"))
        {
            boost += 2;
        }

        return boost;
    }

    private static int GetImageSoftCap(IReadOnlyList<WorkspaceMaterialCandidate> orderedCandidates, int maxMaterials)
    {
        var hasNonImage = orderedCandidates.Any(candidate => candidate.Kind != WorkspaceMaterialKind.ImageAsset);
        if (!hasNonImage)
        {
            return maxMaterials;
        }

        return Math.Max(2, Math.Min(3, maxMaterials / 4));
    }

    private static int CountMeaningfulTextMaterials(IEnumerable<MaterialRuntimeResult> preparedResults)
    {
        return preparedResults.Count(result =>
            result.Kind == WorkspaceMaterialKind.TextDocument &&
            !string.IsNullOrWhiteSpace(result.ExtractedText) &&
            result.ExtractedText.Length >= 120 &&
            !ContainsAny(result.DisplayPath, "license", "copying", "copyright", "ofl", "fontlog"));
    }

    private static int EstimatePreparedSignalChars(
        IEnumerable<MaterialRuntimeResult> preparedResults,
        IReadOnlyList<WorkspaceTechnicalPreviewInput> technicalEvidence)
    {
        var materialChars = preparedResults.Sum(result => result.ExtractedText?.Length ?? 0);
        var technicalChars = technicalEvidence.Sum(item => item.PreviewText?.Length ?? 0);
        return materialChars + technicalChars;
    }

    private static bool ShouldSkipAdaptiveExpansion(WorkspaceMaterialCandidate candidate, IReadOnlyList<string> buildRoots)
    {
        var penalty = GetNoisePenalty(candidate.RelativePath, candidate.Kind, buildRoots);
        if (candidate.Kind == WorkspaceMaterialKind.TextDocument && penalty >= 4)
        {
            return true;
        }

        return false;
    }

    private static IEnumerable<WorkspaceMaterialCandidate> GetFillCandidates(IEnumerable<WorkspaceMaterialCandidate> orderedCandidates, IReadOnlyList<string> buildRoots)
    {
        return orderedCandidates
            .OrderBy(candidate => GetNoisePenalty(candidate.RelativePath, candidate.Kind, buildRoots))
            .ThenByDescending(candidate => GetSignalBoost(candidate.RelativePath, candidate.Kind))
            .ThenBy(candidate => GetKindPriority(candidate.Kind))
            .ThenBy(candidate => GetPathDepth(candidate.RelativePath))
            .ThenBy(candidate => candidate.RelativePath, StringComparer.OrdinalIgnoreCase);
    }

    private static bool ShouldSkipInBoundedFill(
        WorkspaceMaterialCandidate candidate,
        IReadOnlyList<WorkspaceMaterialCandidate> selected,
        IReadOnlyList<WorkspaceMaterialCandidate> orderedCandidates,
        IReadOnlyList<string> buildRoots)
    {
        if (candidate.Kind != WorkspaceMaterialKind.TextDocument)
        {
            return false;
        }

        var textAlreadySelected = selected.Any(existing => existing.Kind == WorkspaceMaterialKind.TextDocument);
        if (!textAlreadySelected)
        {
            return false;
        }

        var penalty = GetNoisePenalty(candidate.RelativePath, candidate.Kind, buildRoots);
        if (penalty < 4)
        {
            return false;
        }

        var hasUnusedNonText = orderedCandidates.Any(other =>
            other.Kind != WorkspaceMaterialKind.TextDocument &&
            !selected.Any(existing => string.Equals(existing.RelativePath, other.RelativePath, StringComparison.OrdinalIgnoreCase)));

        return hasUnusedNonText;
    }

    private static WorkspaceMaterialCandidate[] OrderSelectedCandidates(IEnumerable<WorkspaceMaterialCandidate> candidates, IReadOnlyList<string> buildRoots)
    {
        return candidates
            .OrderBy(candidate => GetKindPriority(candidate.Kind))
            .ThenBy(candidate => GetNoisePenalty(candidate.RelativePath, candidate.Kind, buildRoots))
            .ThenByDescending(candidate => GetSignalBoost(candidate.RelativePath, candidate.Kind))
            .ThenBy(candidate => GetPathDepth(candidate.RelativePath))
            .ThenBy(candidate => candidate.RelativePath, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static int GetTechnicalEvidencePriority(string relativePath)
    {
        var fileName = Path.GetFileName(relativePath);
        var extension = Path.GetExtension(fileName);
        if (string.Equals(fileName, "CMakePresets.json", StringComparison.OrdinalIgnoreCase))
        {
            return 0;
        }

        if (string.Equals(fileName, "CMakeLists.txt", StringComparison.OrdinalIgnoreCase))
        {
            return 1;
        }

        if (string.Equals(fileName, "CMakeCache.txt", StringComparison.OrdinalIgnoreCase))
        {
            return 2;
        }

        if (string.Equals(fileName, "go.mod", StringComparison.OrdinalIgnoreCase))
        {
            return 3;
        }

        if (string.Equals(fileName, "Cargo.toml", StringComparison.OrdinalIgnoreCase))
        {
            return 4;
        }

        if (string.Equals(fileName, "package.json", StringComparison.OrdinalIgnoreCase))
        {
            return 5;
        }

        if (string.Equals(fileName, "pyproject.toml", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(fileName, "requirements.txt", StringComparison.OrdinalIgnoreCase))
        {
            return 6;
        }

        if (string.Equals(fileName, "Dockerfile", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(fileName, "docker-compose.yml", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(fileName, "docker-compose.yaml", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(fileName, "compose.yml", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(fileName, "compose.yaml", StringComparison.OrdinalIgnoreCase))
        {
            return 7;
        }

        if (string.Equals(fileName, "Makefile", StringComparison.OrdinalIgnoreCase))
        {
            return 8;
        }

        if (string.Equals(extension, ".cmake", StringComparison.OrdinalIgnoreCase))
        {
            return 9;
        }

        if (string.Equals(fileName, "Directory.Build.props", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(fileName, "Directory.Build.targets", StringComparison.OrdinalIgnoreCase))
        {
            return 10;
        }

        if (string.Equals(extension, ".props", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(extension, ".targets", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(extension, ".csproj", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(extension, ".vcxproj", StringComparison.OrdinalIgnoreCase))
        {
            return 11;
        }

        return 99;
    }

    private static string GetTechnicalEvidenceCategory(string fullPath)
    {
        var fileName = Path.GetFileName(fullPath);
        var extension = Path.GetExtension(fullPath);

        if (string.Equals(fileName, "CMakePresets.json", StringComparison.OrdinalIgnoreCase))
        {
            return "build-presets";
        }

        if (string.Equals(fileName, "CMakeLists.txt", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(extension, ".cmake", StringComparison.OrdinalIgnoreCase))
        {
            return "cmake";
        }

        if (string.Equals(fileName, "CMakeCache.txt", StringComparison.OrdinalIgnoreCase))
        {
            return "build-cache";
        }

        if (string.Equals(fileName, "go.mod", StringComparison.OrdinalIgnoreCase))
        {
            return "go-module";
        }

        if (string.Equals(fileName, "Cargo.toml", StringComparison.OrdinalIgnoreCase))
        {
            return "cargo-manifest";
        }

        if (string.Equals(fileName, "package.json", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(fileName, "tsconfig.json", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(fileName, "vite.config.ts", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(fileName, "vite.config.js", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(fileName, "webpack.config.js", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(fileName, "next.config.js", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(fileName, "nuxt.config.ts", StringComparison.OrdinalIgnoreCase))
        {
            return "web-build";
        }

        if (string.Equals(fileName, "pyproject.toml", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(fileName, "requirements.txt", StringComparison.OrdinalIgnoreCase))
        {
            return "python-build";
        }

        if (string.Equals(fileName, "Dockerfile", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(fileName, "docker-compose.yml", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(fileName, "docker-compose.yaml", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(fileName, "compose.yml", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(fileName, "compose.yaml", StringComparison.OrdinalIgnoreCase))
        {
            return "container-build";
        }

        if (string.Equals(fileName, "Makefile", StringComparison.OrdinalIgnoreCase))
        {
            return "make-build";
        }

        if (string.Equals(fileName, "Directory.Build.props", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(fileName, "Directory.Build.targets", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(extension, ".props", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(extension, ".targets", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(extension, ".csproj", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(extension, ".vcxproj", StringComparison.OrdinalIgnoreCase))
        {
            return "msbuild";
        }

        return "technical-config";
    }

    private static string GetSelectionReason(WorkspaceMaterialKind kind)
    {
        return kind switch
        {
            WorkspaceMaterialKind.TextDocument => "text-first-preview",
            WorkspaceMaterialKind.PdfDocument => "pdf-runtime-preview",
            WorkspaceMaterialKind.ArchiveArtifact => "archive-runtime-preview",
            WorkspaceMaterialKind.ImageAsset => "image-runtime-preview",
            _ => "not-import-facing"
        };
    }

    private static bool IsUnderBuildRoot(string relativePath, IReadOnlyList<string> buildRoots)
    {
        foreach (var root in buildRoots)
        {
            if (string.IsNullOrWhiteSpace(root))
            {
                continue;
            }

            if (string.Equals(root, ".", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var normalizedRoot = root.Replace('/', '\\').TrimEnd('\\');
            if (relativePath.StartsWith($"{normalizedRoot}\\", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static bool ContainsAny(string value, params string[] patterns)
    {
        foreach (var pattern in patterns)
        {
            if (value.Contains(pattern, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }
}
