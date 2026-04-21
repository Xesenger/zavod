using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace zavod.Workspace;

public static class WorkspaceScanner
{
    private sealed record ScanInventory(
        IReadOnlyList<string> RelevantFiles,
        int IgnoredNoiseFileCount,
        IReadOnlyList<string> IgnoredNoiseRoots);

    private static readonly HashSet<string> IgnoredDirectoryNames = new(StringComparer.OrdinalIgnoreCase)
    {
        ".git",
        ".vs",
        ".idea",
        ".vscode",
        ".zavod",
        ".cache",
        ".next",
        ".nuxt",
        ".svelte-kit",
        ".turbo",
        "node_modules",
        "bin",
        "obj",
        "build",
        "dist",
        "out",
        "coverage"
    };

    private static readonly HashSet<string> SourceExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".asm",
        ".astro",
        ".c",
        ".cjs",
        ".cc",
        ".cpp",
        ".cs",
        ".go",
        ".h",
        ".hpp",
        ".inc",
        ".java",
        ".js",
        ".jsx",
        ".kt",
        ".lua",
        ".mjs",
        ".php",
        ".ps1",
        ".py",
        ".rb",
        ".rs",
        ".s",
        ".sh",
        ".sql",
        ".svelte",
        ".swift",
        ".ts",
        ".tsx",
        ".vue",
        // Markup + styles count as source for anchor-pack purposes. Projects
        // like cssDOOM are css-heavy — missing these meant Worker got a
        // js-only file tree and then honestly declared "need CSS changes" as
        // a blocker because the relevant .css files were outside the
        // grounded anchor pack it was handed.
        ".html",
        ".htm",
        ".css",
        ".scss",
        ".sass",
        ".less"
    };

    private static readonly HashSet<string> DocumentExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".md",
        ".pdf",
        ".rst",
        ".txt"
    };

    private static readonly HashSet<string> ConfigFileNames = new(StringComparer.OrdinalIgnoreCase)
    {
        ".editorconfig",
        ".env"
    };

    private static readonly HashSet<string> ConfigExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".env",
        ".ini",
        ".json",
        ".toml",
        ".yaml",
        ".yml"
    };

    private static readonly HashSet<string> AssetExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".bmp",
        ".gif",
        ".jpeg",
        ".jpg",
        ".png",
        ".rar",
        ".svg",
        ".tar",
        ".webp",
        ".zip",
        ".7z"
    };

    private static readonly HashSet<string> BinaryEvidenceExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".bin",
        ".dll",
        ".dylib",
        ".exe",
        ".so"
    };

    private static readonly HashSet<string> OfficeDocumentExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".doc",
        ".docx",
        ".odt"
    };

    private static readonly HashSet<string> SpreadsheetExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".csv",
        ".ods",
        ".xls",
        ".xlsx"
    };

    private static readonly HashSet<string> PresentationExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".odp",
        ".ppt",
        ".pptx"
    };

    private static readonly HashSet<string> MultimediaExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".avi",
        ".flac",
        ".mkv",
        ".mov",
        ".mp3",
        ".mp4",
        ".ogg",
        ".wav"
    };

    private static readonly HashSet<string> BuildFileNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "CMakeCache.txt",
        "CMakeLists.txt",
        "Cargo.toml",
        "composer.json",
        "compose.yaml",
        "compose.yml",
        "docker-compose.yaml",
        "docker-compose.yml",
        "Dockerfile",
        "alembic.ini",
        "dbt_project.yml",
        "flyway.conf",
        "Gemfile",
        "go.mod",
        "Makefile",
        "next.config.js",
        "next.config.mjs",
        "next.config.ts",
        "nuxt.config.js",
        "nuxt.config.ts",
        "package.json",
        "Pipfile",
        "pom.xml",
        "pyproject.toml",
        "requirements.txt",
        "schema.prisma",
        "tsconfig.json",
        "vite.config.js",
        "vite.config.mjs",
        "vite.config.ts",
        "webpack.config.js",
        "webpack.config.mjs",
        "webpack.config.ts",
        "build.gradle",
        "build.gradle.kts"
    };

    private static readonly HashSet<string> BuildExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".csproj",
        ".fsproj",
        ".props",
        ".sln",
        ".targets",
        ".vcxproj"
    };

    private static readonly HashSet<string> EntryFileNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "main.c",
        "main.cc",
        "main.cpp",
        "main.cs",
        "main.go",
        "main.java",
        "main.js",
        "main.py",
        "main.rs",
        "main.ts",
        "Program.cs",
        "App.xaml"
    };

    public static WorkspaceScanResult Scan(WorkspaceScanRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (string.IsNullOrWhiteSpace(request.WorkspaceRoot))
        {
            throw new ArgumentException("Workspace root is required.", nameof(request));
        }

        var workspaceRoot = Path.GetFullPath(request.WorkspaceRoot.Trim());
        var scannedAt = DateTimeOffset.UtcNow;

        if (!Directory.Exists(workspaceRoot))
        {
            return BuildTerminalResult(
                workspaceRoot,
                WorkspaceHealthStatus.Missing,
                WorkspaceDriftStatus.Unknown,
                scannedAt,
                new WorkspaceStructuralAnomaly("ROOT_MISSING", "Workspace root does not exist.", workspaceRoot));
        }

        try
        {
            var scanRoots = ResolveScanRoots(workspaceRoot, request.IncludePaths);
            var inventory = EnumerateRelevantFiles(workspaceRoot, scanRoots);
            var state = BuildState(workspaceRoot, scannedAt, inventory);
            var materialCandidates = BuildMaterialCandidates(workspaceRoot, inventory.RelevantFiles);
            return new WorkspaceScanResult(state, inventory.RelevantFiles, materialCandidates);
        }
        catch (UnauthorizedAccessException ex)
        {
            return BuildTerminalResult(
                workspaceRoot,
                WorkspaceHealthStatus.Unavailable,
                WorkspaceDriftStatus.ScanFailed,
                scannedAt,
                new WorkspaceStructuralAnomaly("ACCESS_DENIED", ex.Message, workspaceRoot));
        }
        catch (IOException ex)
        {
            return BuildTerminalResult(
                workspaceRoot,
                WorkspaceHealthStatus.Unavailable,
                WorkspaceDriftStatus.ScanFailed,
                scannedAt,
                new WorkspaceStructuralAnomaly("IO_FAILURE", ex.Message, workspaceRoot));
        }
    }

    private static WorkspaceScanResult BuildTerminalResult(
        string workspaceRoot,
        WorkspaceHealthStatus health,
        WorkspaceDriftStatus driftStatus,
        DateTimeOffset scannedAt,
        WorkspaceStructuralAnomaly anomaly)
    {
        var summary = new WorkspaceChangeSummary(
            RelevantFileCount: 0,
            SourceFileCount: 0,
            BuildFileCount: 0,
            ConfigFileCount: 0,
            DocumentFileCount: 0,
            AssetFileCount: 0,
            BinaryFileCount: 0,
            IgnoredNoiseFileCount: 0,
            IgnoredNoiseRoots: Array.Empty<string>(),
            SourceRoots: Array.Empty<string>(),
            BuildRoots: Array.Empty<string>(),
            EntryCandidates: Array.Empty<string>());

        var state = new WorkspaceState(
            workspaceRoot,
            health,
            driftStatus,
            WorkspaceImportKind.Empty,
            scannedAt,
            summary,
            new[] { anomaly },
            HasRecognizableProjectStructure: false,
            HasSourceFiles: false,
            HasBuildFiles: false);

        return new WorkspaceScanResult(state, Array.Empty<string>(), Array.Empty<WorkspaceMaterialCandidate>());
    }

    private static WorkspaceState BuildState(string workspaceRoot, DateTimeOffset scannedAt, ScanInventory inventory)
    {
        var relevantFiles = inventory.RelevantFiles;
        var sourceFiles = relevantFiles.Where(IsSourceFile).ToArray();
        var buildFiles = relevantFiles.Where(IsBuildFile).ToArray();
        var configFiles = relevantFiles.Where(IsConfigFile).ToArray();
        var documentFiles = relevantFiles.Where(IsDocumentFile).ToArray();
        var assetFiles = relevantFiles.Where(IsAssetFile).ToArray();
        var additionalMaterialFiles = relevantFiles.Where(IsAdditionalMaterialFile).ToArray();
        var binaryFiles = relevantFiles.Where(IsBinaryEvidenceFile).ToArray();
        var entryCandidates = relevantFiles
            .Where(IsEntryCandidate)
            .Select(path => Path.GetRelativePath(workspaceRoot, path))
            .OrderBy(static value => value, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var rawSourceRoots = sourceFiles
            .Select(path => GetSourceRoot(workspaceRoot, path))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(static value => value, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var observedBuildRoots = buildFiles
            .Select(path => GetTopLevelRoot(workspaceRoot, path))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(static value => value, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var buildRoots = observedBuildRoots
            .Where(IsLikelyBuildDerivedRoot)
            .ToArray();
        var sourceRoots = FilterPrimarySourceRoots(rawSourceRoots, observedBuildRoots);
        var nestedNonSourcePayloadRoots = FindNestedNonSourcePayloadRoots(workspaceRoot, relevantFiles);
        var nestedGitProjectRoots = FindNestedGitProjectRoots(workspaceRoot);

        var anomalies = new List<WorkspaceStructuralAnomaly>();
        if (relevantFiles.Count == 0)
        {
            anomalies.Add(new WorkspaceStructuralAnomaly(
                "NO_RELEVANT_FILES",
                "Workspace contains no relevant source, build, document, or asset files.",
                workspaceRoot));
        }

        if (sourceFiles.Length == 0)
        {
            anomalies.Add(new WorkspaceStructuralAnomaly(
                "NO_SOURCE_FILES",
                "No source files were detected in the scanned workspace scope.",
                workspaceRoot));
        }

        if (buildFiles.Length == 0)
        {
            anomalies.Add(new WorkspaceStructuralAnomaly(
                "NO_BUILD_MARKERS",
                "No build-system markers were detected in the scanned workspace scope.",
                workspaceRoot));
        }

        if (sourceRoots.Count > 0 && !sourceRoots.Contains(".", StringComparer.OrdinalIgnoreCase))
        {
            anomalies.Add(new WorkspaceStructuralAnomaly(
                "SOURCE_NOT_AT_ROOT",
                $"Detected source structure is nested under '{sourceRoots[0]}' instead of the workspace root.",
                workspaceRoot));
        }

        if (sourceRoots.Count > 1)
        {
            anomalies.Add(new WorkspaceStructuralAnomaly(
                "MULTIPLE_SOURCE_ROOTS",
                $"Detected multiple top-level source roots: {string.Join(", ", sourceRoots)}.",
                workspaceRoot));
        }

        var hasHostStructureAtRoot = sourceRoots.Contains(".", StringComparer.OrdinalIgnoreCase) ||
            buildFiles.Any(path => string.Equals(GetTopLevelRoot(workspaceRoot, path), ".", StringComparison.OrdinalIgnoreCase));
        if (hasHostStructureAtRoot && nestedNonSourcePayloadRoots.Length > 0)
        {
            anomalies.Add(new WorkspaceStructuralAnomaly(
                "NESTED_NON_SOURCE_PAYLOADS",
                $"Detected nested non-source payload roots alongside host project structure: {string.Join(", ", nestedNonSourcePayloadRoots)}.",
                workspaceRoot));
        }

        if (nestedGitProjectRoots.Length > 0)
        {
            anomalies.Add(new WorkspaceStructuralAnomaly(
                "NESTED_GIT_PROJECTS",
                $"Detected nested git-backed project roots inside the scanned workspace: {string.Join(", ", nestedGitProjectRoots)}.",
                workspaceRoot));
        }

        if (ShouldReportNoisyWorkspace(inventory.IgnoredNoiseFileCount, inventory.IgnoredNoiseRoots.Count))
        {
            anomalies.Add(new WorkspaceStructuralAnomaly(
                "NOISY_WORKSPACE_HINT",
                $"Workspace remains valid, but generated/noisy payload was ignored under: {string.Join(", ", inventory.IgnoredNoiseRoots)}.",
                workspaceRoot));
        }

        var hasRecognizableProjectStructure = sourceFiles.Length > 0 || buildFiles.Length > 0;
        var importKind = DetermineImportKind(
            sourceFiles.Length,
            buildFiles.Length,
            configFiles.Length,
            documentFiles.Length,
            assetFiles.Length,
            additionalMaterialFiles.Length,
            binaryFiles.Length);
        var health = DetermineHealth(relevantFiles.Count, hasRecognizableProjectStructure);
        var summary = new WorkspaceChangeSummary(
            relevantFiles.Count,
            sourceFiles.Length,
            buildFiles.Length,
            configFiles.Length,
            documentFiles.Length,
            assetFiles.Length,
            binaryFiles.Length,
            inventory.IgnoredNoiseFileCount,
            inventory.IgnoredNoiseRoots,
            sourceRoots,
            buildRoots,
            entryCandidates);

        return new WorkspaceState(
            workspaceRoot,
            health,
            WorkspaceDriftStatus.Unknown,
            importKind,
            scannedAt,
            summary,
            anomalies,
            hasRecognizableProjectStructure,
            HasSourceFiles: sourceFiles.Length > 0,
            HasBuildFiles: buildFiles.Length > 0);
    }

    private static WorkspaceHealthStatus DetermineHealth(int relevantFileCount, bool hasRecognizableProjectStructure)
    {
        if (relevantFileCount == 0)
        {
            return WorkspaceHealthStatus.Healthy;
        }

        if (!hasRecognizableProjectStructure)
        {
            return WorkspaceHealthStatus.Healthy;
        }

        return WorkspaceHealthStatus.Healthy;
    }

    private static WorkspaceImportKind DetermineImportKind(
        int sourceFileCount,
        int buildFileCount,
        int configFileCount,
        int documentFileCount,
        int assetFileCount,
        int additionalMaterialFileCount,
        int binaryFileCount)
    {
        var hasSourceStructure = sourceFileCount > 0 || buildFileCount > 0;
        var hasNonSourceMaterials = configFileCount > 0 || documentFileCount > 0 || assetFileCount > 0 || additionalMaterialFileCount > 0 || binaryFileCount > 0;

        if (!hasSourceStructure && !hasNonSourceMaterials)
        {
            return WorkspaceImportKind.Empty;
        }

        if (hasSourceStructure && hasNonSourceMaterials)
        {
            return WorkspaceImportKind.MixedImport;
        }

        if (hasSourceStructure)
        {
            return WorkspaceImportKind.SourceProject;
        }

        return WorkspaceImportKind.NonSourceImport;
    }

    private static IReadOnlyList<string> ResolveScanRoots(string workspaceRoot, IReadOnlyList<string>? includePaths)
    {
        if (includePaths is not { Count: > 0 })
        {
            return new[] { workspaceRoot };
        }

        var roots = includePaths
            .Where(static path => !string.IsNullOrWhiteSpace(path))
            .Select(path => Path.GetFullPath(Path.IsPathRooted(path) ? path : Path.Combine(workspaceRoot, path)))
            .Where(path => path.StartsWith(workspaceRoot, StringComparison.OrdinalIgnoreCase))
            .Where(Directory.Exists)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return roots.Length > 0 ? roots : new[] { workspaceRoot };
    }

    private static ScanInventory EnumerateRelevantFiles(string workspaceRoot, IReadOnlyList<string> scanRoots)
    {
        var relevantFiles = new List<string>();
        var ignoredNoiseRoots = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var ignoredNoiseFileCount = 0;

        foreach (var root in scanRoots)
        {
            foreach (var file in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories))
            {
                if (TryGetIgnoredNoiseRoot(workspaceRoot, file, out var ignoredNoiseRoot))
                {
                    if (IsBinaryEvidenceFile(file))
                    {
                        relevantFiles.Add(file);
                        continue;
                    }

                    ignoredNoiseFileCount++;
                    ignoredNoiseRoots.Add(ignoredNoiseRoot);
                    continue;
                }

                if (IsRelevantFile(file))
                {
                    relevantFiles.Add(file);
                }
            }
        }

        return new ScanInventory(
            relevantFiles,
            ignoredNoiseFileCount,
            ignoredNoiseRoots.OrderBy(static value => value, StringComparer.OrdinalIgnoreCase).ToArray());
    }

    private static bool TryGetIgnoredNoiseRoot(string workspaceRoot, string path, out string ignoredNoiseRoot)
    {
        var relativePath = Path.GetRelativePath(workspaceRoot, path);
        var segments = relativePath.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        for (var index = 0; index < segments.Length; index++)
        {
            if (IgnoredDirectoryNames.Contains(segments[index]))
            {
                ignoredNoiseRoot = string.Join(Path.DirectorySeparatorChar, segments.Take(index + 1));
                return true;
            }
        }

        ignoredNoiseRoot = string.Empty;
        return false;
    }

    private static bool IsRelevantFile(string path)
    {
        return IsSourceFile(path) || IsBuildFile(path) || IsConfigFile(path) || IsDocumentFile(path) || IsAssetFile(path) || IsAdditionalMaterialFile(path) || IsBinaryEvidenceFile(path);
    }

    private static bool IsSourceFile(string path)
    {
        return !IsBuildFile(path) && SourceExtensions.Contains(Path.GetExtension(path));
    }

    private static bool IsBuildFile(string path)
    {
        var fileName = Path.GetFileName(path);
        return BuildFileNames.Contains(fileName) || BuildExtensions.Contains(Path.GetExtension(fileName));
    }

    private static bool IsDocumentFile(string path)
    {
        return !IsBuildFile(path) && DocumentExtensions.Contains(Path.GetExtension(path));
    }

    private static bool IsConfigFile(string path)
    {
        var fileName = Path.GetFileName(path);
        return !IsBuildFile(path) &&
               !IsDocumentFile(path) &&
               (ConfigFileNames.Contains(fileName) || ConfigExtensions.Contains(Path.GetExtension(fileName)));
    }

    private static bool IsAssetFile(string path)
    {
        return AssetExtensions.Contains(Path.GetExtension(path));
    }

    private static bool IsBinaryEvidenceFile(string path)
    {
        return BinaryEvidenceExtensions.Contains(Path.GetExtension(path));
    }

    private static bool IsAdditionalMaterialFile(string path)
    {
        var extension = Path.GetExtension(path);
        return OfficeDocumentExtensions.Contains(extension) ||
               SpreadsheetExtensions.Contains(extension) ||
               PresentationExtensions.Contains(extension) ||
               MultimediaExtensions.Contains(extension);
    }

    private static IReadOnlyList<WorkspaceMaterialCandidate> BuildMaterialCandidates(string workspaceRoot, IReadOnlyList<string> relevantFiles)
    {
        return relevantFiles
            .Select(path => TryBuildMaterialCandidate(workspaceRoot, path))
            .Where(static candidate => candidate is not null)
            .Cast<WorkspaceMaterialCandidate>()
            .OrderBy(static candidate => candidate.RelativePath, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static WorkspaceMaterialCandidate? TryBuildMaterialCandidate(string workspaceRoot, string path)
    {
        var relativePath = Path.GetRelativePath(workspaceRoot, path);
        if (IsPdfFile(path))
        {
            return new WorkspaceMaterialCandidate(relativePath, WorkspaceMaterialKind.PdfDocument);
        }

        if (IsTextDocumentFile(path))
        {
            return new WorkspaceMaterialCandidate(relativePath, WorkspaceMaterialKind.TextDocument);
        }

        if (IsImageFile(path))
        {
            return new WorkspaceMaterialCandidate(relativePath, WorkspaceMaterialKind.ImageAsset);
        }

        if (IsArchiveLikeFile(path))
        {
            return new WorkspaceMaterialCandidate(relativePath, WorkspaceMaterialKind.ArchiveArtifact);
        }

        if (IsOfficeDocumentFile(path))
        {
            return new WorkspaceMaterialCandidate(relativePath, WorkspaceMaterialKind.OfficeDocument);
        }

        if (IsSpreadsheetFile(path))
        {
            return new WorkspaceMaterialCandidate(relativePath, WorkspaceMaterialKind.Spreadsheet);
        }

        if (IsPresentationFile(path))
        {
            return new WorkspaceMaterialCandidate(relativePath, WorkspaceMaterialKind.Presentation);
        }

        if (IsMultimediaFile(path))
        {
            return new WorkspaceMaterialCandidate(relativePath, WorkspaceMaterialKind.Multimedia);
        }

        return null;
    }

    private static bool IsPdfFile(string path)
    {
        return string.Equals(Path.GetExtension(path), ".pdf", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsTextDocumentFile(string path)
    {
        return !IsBuildFile(path) &&
               !IsPdfFile(path) &&
               (string.Equals(Path.GetExtension(path), ".md", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(Path.GetExtension(path), ".rst", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(Path.GetExtension(path), ".txt", StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsImageFile(string path)
    {
        return IsAssetFile(path) &&
               (string.Equals(Path.GetExtension(path), ".bmp", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(Path.GetExtension(path), ".gif", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(Path.GetExtension(path), ".jpeg", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(Path.GetExtension(path), ".jpg", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(Path.GetExtension(path), ".png", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(Path.GetExtension(path), ".svg", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(Path.GetExtension(path), ".webp", StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsArchiveLikeFile(string path)
    {
        return IsAssetFile(path) &&
               (string.Equals(Path.GetExtension(path), ".rar", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(Path.GetExtension(path), ".tar", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(Path.GetExtension(path), ".zip", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(Path.GetExtension(path), ".7z", StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsOfficeDocumentFile(string path)
    {
        return OfficeDocumentExtensions.Contains(Path.GetExtension(path));
    }

    private static bool IsSpreadsheetFile(string path)
    {
        return SpreadsheetExtensions.Contains(Path.GetExtension(path));
    }

    private static bool IsPresentationFile(string path)
    {
        return PresentationExtensions.Contains(Path.GetExtension(path));
    }

    private static bool IsMultimediaFile(string path)
    {
        return MultimediaExtensions.Contains(Path.GetExtension(path));
    }

    private static bool IsEntryCandidate(string path)
    {
        return EntryFileNames.Contains(Path.GetFileName(path));
    }

    private static string GetSourceRoot(string workspaceRoot, string sourceFilePath)
    {
        var relativeDirectory = Path.GetRelativePath(workspaceRoot, Path.GetDirectoryName(sourceFilePath) ?? workspaceRoot);
        if (string.Equals(relativeDirectory, ".", StringComparison.Ordinal))
        {
            return ".";
        }

        var separatorIndex = relativeDirectory.IndexOfAny(new[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar });
        return separatorIndex >= 0 ? relativeDirectory[..separatorIndex] : relativeDirectory;
    }

    private static IReadOnlyList<string> FilterPrimarySourceRoots(IReadOnlyList<string> rawSourceRoots, IReadOnlyList<string> buildRoots)
    {
        if (rawSourceRoots.Count == 0)
        {
            return rawSourceRoots;
        }

        var buildDerivedRoots = rawSourceRoots
            .Where(root => buildRoots.Contains(root, StringComparer.OrdinalIgnoreCase))
            .Where(IsLikelyBuildDerivedRoot)
            .ToArray();

        if (buildDerivedRoots.Length == 0)
        {
            return rawSourceRoots;
        }

        var filtered = rawSourceRoots
            .Where(root => !buildDerivedRoots.Contains(root, StringComparer.OrdinalIgnoreCase))
            .ToArray();

        return filtered.Length == 0 ? rawSourceRoots : filtered;
    }

    private static bool IsLikelyBuildDerivedRoot(string root)
    {
        return root.Contains("build", StringComparison.OrdinalIgnoreCase) ||
               root.Contains("cmake", StringComparison.OrdinalIgnoreCase);
    }

    private static bool ShouldReportNoisyWorkspace(int ignoredNoiseFileCount, int ignoredNoiseRootCount)
    {
        return ignoredNoiseFileCount >= 3 || ignoredNoiseRootCount >= 2;
    }

    private static string[] FindNestedNonSourcePayloadRoots(string workspaceRoot, IReadOnlyList<string> relevantFiles)
    {
        return relevantFiles
            .GroupBy(path => GetTopLevelRoot(workspaceRoot, path), StringComparer.OrdinalIgnoreCase)
            .Where(group => !string.Equals(group.Key, ".", StringComparison.OrdinalIgnoreCase))
            .Where(group => group.Any(IsDocumentFile) || group.Any(IsAssetFile) || group.Any(IsBinaryEvidenceFile))
            .Where(group => !group.Any(IsSourceFile) && !group.Any(IsBuildFile))
            .Select(static group => group.Key)
            .OrderBy(static value => value, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static string[] FindNestedGitProjectRoots(string workspaceRoot)
    {
        return Directory.EnumerateDirectories(workspaceRoot, ".git", SearchOption.AllDirectories)
            .Select(Path.GetDirectoryName)
            .Where(static path => !string.IsNullOrWhiteSpace(path))
            .Select(path => Path.GetRelativePath(workspaceRoot, path!))
            .Where(static relativePath => !string.IsNullOrWhiteSpace(relativePath) &&
                                          !string.Equals(relativePath, ".", StringComparison.OrdinalIgnoreCase))
            .Where(static relativePath => !IsIgnoredNoiseRelativePath(relativePath))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(static value => value, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static bool IsIgnoredNoiseRelativePath(string relativePath)
    {
        var segments = relativePath.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        return segments.Any(static segment => IgnoredDirectoryNames.Contains(segment));
    }

    private static string GetTopLevelRoot(string workspaceRoot, string path)
    {
        var relativePath = Path.GetRelativePath(workspaceRoot, path);
        if (string.Equals(relativePath, ".", StringComparison.Ordinal))
        {
            return ".";
        }

        var separatorIndex = relativePath.IndexOfAny(new[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar });
        return separatorIndex >= 0 ? relativePath[..separatorIndex] : ".";
    }
}
