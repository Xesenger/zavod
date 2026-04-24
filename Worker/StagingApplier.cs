using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace zavod.Worker;

/// <summary>
/// Copies the staged files for a task from the sandbox into the real project
/// on user Accept, hash-guarding each original against the sha256 recorded in
/// the staging manifest. Per-file drift blocks that file from being applied.
/// </summary>
public static class StagingApplier
{
    private const string StagingSubpath = ".zavod.local/staging";

    public static StagingApplyOutcome Apply(string projectRoot, string taskId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(projectRoot);
        ArgumentException.ThrowIfNullOrWhiteSpace(taskId);

        var taskPathSegment = StagingTaskIdPathSegment.Normalize(taskId);
        var normalizedRoot = Path.GetFullPath(projectRoot);
        var taskStagingRoot = Path.Combine(
            normalizedRoot,
            StagingSubpath.Replace('/', Path.DirectorySeparatorChar),
            taskPathSegment);

        if (!Directory.Exists(taskStagingRoot))
        {
            return new StagingApplyOutcome(
                AppliedFiles: Array.Empty<string>(),
                SkippedFiles: Array.Empty<string>(),
                HashMismatchWarnings: Array.Empty<string>(),
                StagingRoot: null);
        }

        var latestAttemptDir = FindLatestAttempt(taskStagingRoot);
        if (latestAttemptDir is null)
        {
            return new StagingApplyOutcome(
                AppliedFiles: Array.Empty<string>(),
                SkippedFiles: Array.Empty<string>(),
                HashMismatchWarnings: Array.Empty<string>(),
                StagingRoot: null);
        }

        var manifest = TryLoadManifest(latestAttemptDir);
        if (manifest is null)
        {
            return new StagingApplyOutcome(
                AppliedFiles: Array.Empty<string>(),
                SkippedFiles: new[] { $"{latestAttemptDir} (manifest unreadable)" },
                HashMismatchWarnings: Array.Empty<string>(),
                StagingRoot: latestAttemptDir);
        }

        var appliedFiles = new List<string>();
        var skippedFiles = new List<string>();
        var hashWarnings = new List<string>();

        foreach (var result in manifest.Results)
        {
            if (!result.Applied || string.IsNullOrWhiteSpace(result.StagedAbsolutePath))
            {
                skippedFiles.Add($"{result.Path} (was not staged: {result.SkipReason ?? "unknown"})");
                continue;
            }

            var relative = result.Path.Replace('/', Path.DirectorySeparatorChar).TrimStart(Path.DirectorySeparatorChar);
            if (Path.IsPathRooted(relative))
            {
                skippedFiles.Add($"{result.Path} (absolute path rejected)");
                continue;
            }

            var projectAbsolute = Path.GetFullPath(Path.Combine(normalizedRoot, relative));
            if (!IsInsideDirectory(projectAbsolute, normalizedRoot))
            {
                skippedFiles.Add($"{result.Path} (path escapes project root)");
                continue;
            }

            var stagedAbsolute = Path.GetFullPath(Path.Combine(latestAttemptDir, relative));
            if (!IsInsideDirectory(stagedAbsolute, latestAttemptDir))
            {
                skippedFiles.Add($"{result.Path} (staged path escapes staging root)");
                continue;
            }

            if (!File.Exists(stagedAbsolute))
            {
                skippedFiles.Add($"{result.Path} (staged file missing)");
                continue;
            }

            // Hash guard: check that the project file has not drifted since staging.
            if (!string.IsNullOrEmpty(result.OriginalSha256))
            {
                if (!File.Exists(projectAbsolute))
                {
                    hashWarnings.Add($"{result.Path}: project file was deleted since staging (staged-origin sha256={result.OriginalSha256[..8]}...). Apply blocked.");
                    skippedFiles.Add($"{result.Path} (project file changed since staging)");
                    continue;
                }

                try
                {
                    var currentContent = File.ReadAllText(projectAbsolute, Encoding.UTF8);
                    var currentHash = ComputeSha256(currentContent);
                    if (!string.Equals(currentHash, result.OriginalSha256, StringComparison.OrdinalIgnoreCase))
                    {
                        hashWarnings.Add($"{result.Path}: project file changed since staging (staged-origin sha256={result.OriginalSha256[..8]}..., current sha256={currentHash[..8]}...). Apply blocked.");
                        skippedFiles.Add($"{result.Path} (project file changed since staging)");
                        continue;
                    }
                }
                catch (IOException ex)
                {
                    hashWarnings.Add($"{result.Path}: could not re-read project file for hash guard ({ex.Message}). Apply blocked.");
                    skippedFiles.Add($"{result.Path} (hash guard read failed)");
                    continue;
                }
            }
            else if (File.Exists(projectAbsolute))
            {
                hashWarnings.Add($"{result.Path}: project file was created since staging. Apply blocked.");
                skippedFiles.Add($"{result.Path} (project file changed since staging)");
                continue;
            }

            try
            {
                var parentDir = Path.GetDirectoryName(projectAbsolute);
                if (!string.IsNullOrEmpty(parentDir))
                {
                    Directory.CreateDirectory(parentDir);
                }

                var stagedContent = File.ReadAllText(stagedAbsolute, Encoding.UTF8);
                // Atomic-ish write: staging layer already wrote UTF-8 no BOM.
                // Write to a .tmp next to target, then move into place.
                var tempPath = projectAbsolute + ".zavod.tmp";
                File.WriteAllText(tempPath, stagedContent, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
                if (File.Exists(projectAbsolute))
                {
                    File.Replace(tempPath, projectAbsolute, destinationBackupFileName: null, ignoreMetadataErrors: true);
                }
                else
                {
                    File.Move(tempPath, projectAbsolute);
                }

                appliedFiles.Add(result.Path);
            }
            catch (IOException ex)
            {
                skippedFiles.Add($"{result.Path} (apply failed: {ex.Message})");
            }
            catch (UnauthorizedAccessException ex)
            {
                skippedFiles.Add($"{result.Path} (apply failed: {ex.Message})");
            }
        }

        return new StagingApplyOutcome(
            AppliedFiles: appliedFiles,
            SkippedFiles: skippedFiles,
            HashMismatchWarnings: hashWarnings,
            StagingRoot: latestAttemptDir);
    }

    private static string? FindLatestAttempt(string taskStagingRoot)
    {
        try
        {
            var dirs = Directory.EnumerateDirectories(taskStagingRoot, "attempt-*", SearchOption.TopDirectoryOnly);
            return dirs
                .OrderByDescending(static d => d, StringComparer.OrdinalIgnoreCase)
                .FirstOrDefault();
        }
        catch (IOException)
        {
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }
    }

    private static StagingManifest? TryLoadManifest(string attemptDir)
    {
        var manifestPath = Path.Combine(attemptDir, "manifest.json");
        if (!File.Exists(manifestPath))
        {
            return null;
        }

        try
        {
            var json = File.ReadAllText(manifestPath, Encoding.UTF8);
            return JsonSerializer.Deserialize<StagingManifest>(json);
        }
        catch (IOException)
        {
            return null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string ComputeSha256(string content)
    {
        var bytes = Encoding.UTF8.GetBytes(content);
        var hash = SHA256.HashData(bytes);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static bool IsInsideDirectory(string candidatePath, string rootPath)
    {
        var normalizedRoot = Path.GetFullPath(rootPath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            + Path.DirectorySeparatorChar;
        var normalizedCandidate = Path.GetFullPath(candidatePath);
        return normalizedCandidate.StartsWith(normalizedRoot, StringComparison.OrdinalIgnoreCase);
    }
}

public sealed record StagingApplyOutcome(
    IReadOnlyList<string> AppliedFiles,
    IReadOnlyList<string> SkippedFiles,
    IReadOnlyList<string> HashMismatchWarnings,
    string? StagingRoot);
