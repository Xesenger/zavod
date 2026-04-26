using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace zavod.Worker;

/// <summary>
/// Writes Worker edits to a sandboxed staging area under
/// <c>&lt;projectRoot&gt;/.zavod.local/staging/&lt;taskId&gt;/attempt-&lt;N&gt;/</c>.
/// Never touches the project tree. Returns a <see cref="StagingManifest"/>
/// describing what landed and what was skipped; the manifest is also
/// serialised to <c>manifest.json</c> next to the staged files so the
/// apply path (AcceptedResultApplyProcessor) can hash-guard originals.
/// </summary>
public static class StagingWriter
{
    private const string StagingSubpath = ".zavod.local/staging";

    public static StagingManifest Stage(
        string projectRoot,
        string taskId,
        int attemptNumber,
        string taskDescription,
        IReadOnlyList<WorkerEdit> edits)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(projectRoot);
        ArgumentException.ThrowIfNullOrWhiteSpace(taskId);
        ArgumentNullException.ThrowIfNull(edits);

        var taskPathSegment = StagingTaskIdPathSegment.Normalize(taskId);
        var normalizedRoot = Path.GetFullPath(projectRoot);
        var stagingRoot = Path.Combine(
            normalizedRoot,
            StagingSubpath.Replace('/', Path.DirectorySeparatorChar),
            taskPathSegment,
            $"attempt-{attemptNumber:D2}");

        // Clear any half-written previous run of the same attempt. Staging is
        // aggressive-cleanup per policy: we never merge with prior attempts.
        if (Directory.Exists(stagingRoot))
        {
            Directory.Delete(stagingRoot, recursive: true);
        }

        Directory.CreateDirectory(stagingRoot);

        var results = new List<StagedEditResult>(edits.Count);
        foreach (var edit in edits)
        {
            results.Add(StageOne(normalizedRoot, stagingRoot, edit));
        }

        var manifest = new StagingManifest(
            taskPathSegment,
            taskDescription?.Trim() ?? string.Empty,
            stagingRoot,
            DateTimeOffset.UtcNow,
            results);

        PersistManifest(stagingRoot, manifest);
        if (!File.Exists(Path.Combine(stagingRoot, "manifest.json")))
        {
            throw new InvalidOperationException("Staging manifest is load-bearing and must be written before apply.");
        }

        return manifest;
    }

    /// <summary>
    /// Called after user Accept applied the staged files to the project.
    /// The staged tree is disposable; delete it outright. Diagnostic history
    /// lives in <c>.zavod.local/lab/</c>, not in staging.
    /// </summary>
    public static void Cleanup(string projectRoot, string taskId)
    {
        if (string.IsNullOrWhiteSpace(projectRoot) || !StagingTaskIdPathSegment.TryNormalize(taskId, out var taskPathSegment))
        {
            return;
        }

        var normalizedRoot = Path.GetFullPath(projectRoot);
        var taskStagingRoot = Path.Combine(
            normalizedRoot,
            StagingSubpath.Replace('/', Path.DirectorySeparatorChar),
            taskPathSegment);

        if (Directory.Exists(taskStagingRoot))
        {
            try
            {
                Directory.Delete(taskStagingRoot, recursive: true);
            }
            catch (IOException)
            {
                // Best effort â€” directory may be locked by an external tool.
            }
            catch (UnauthorizedAccessException)
            {
                // Best effort.
            }
        }
    }

    /// <summary>
    /// Called when a task is abandoned (QC REJECT, Worker refused, LLM
    /// unavailable on fresh cycle). The staged tree is NOT discarded â€”
    /// users may want to inspect what the Worker actually produced before
    /// QC killed it. Instead we move the task's staging root into
    /// <c>.zavod.local/staging/_abandoned/&lt;taskId&gt;-&lt;utc&gt;/</c> so the
    /// live-taskId namespace stays clean and future tasks won't collide
    /// with an orphan sharing the same ID.
    /// </summary>
    public static string? Quarantine(string projectRoot, string taskId)
    {
        if (string.IsNullOrWhiteSpace(projectRoot) || !StagingTaskIdPathSegment.TryNormalize(taskId, out var taskPathSegment))
        {
            return null;
        }

        var normalizedRoot = Path.GetFullPath(projectRoot);
        var taskStagingRoot = Path.Combine(
            normalizedRoot,
            StagingSubpath.Replace('/', Path.DirectorySeparatorChar),
            taskPathSegment);

        if (!Directory.Exists(taskStagingRoot))
        {
            return null;
        }

        var abandonedRoot = Path.Combine(
            normalizedRoot,
            StagingSubpath.Replace('/', Path.DirectorySeparatorChar),
            "_abandoned");
        try
        {
            Directory.CreateDirectory(abandonedRoot);
        }
        catch (IOException)
        {
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }

        var timestamp = DateTimeOffset.UtcNow.ToString("yyyyMMddTHHmmssfffZ");
        var quarantinedPath = Path.Combine(abandonedRoot, $"{taskPathSegment}-{timestamp}");

        try
        {
            Directory.Move(taskStagingRoot, quarantinedPath);
            return quarantinedPath;
        }
        catch (IOException)
        {
            // If move fails (e.g. cross-volume), fall back to delete to keep
            // the live namespace clean. Forensic loss is the cost.
            try
            {
                Directory.Delete(taskStagingRoot, recursive: true);
            }
            catch { /* best effort */ }
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }
    }

    private static StagedEditResult StageOne(string projectRoot, string stagingRoot, WorkerEdit edit)
    {
        if (string.IsNullOrWhiteSpace(edit.Path))
        {
            return Skip(edit, "empty path");
        }

        if (!edit.IsKnownOperation())
        {
            return Skip(edit, $"unknown operation '{edit.Operation}'");
        }

        // Reject absolute paths and traversal attempts.
        var normalizedRelative = edit.Path.Replace('/', Path.DirectorySeparatorChar).TrimStart(Path.DirectorySeparatorChar);
        if (Path.IsPathRooted(normalizedRelative))
        {
            return Skip(edit, "absolute path rejected");
        }

        var projectAbsolute = Path.GetFullPath(Path.Combine(projectRoot, normalizedRelative));
        if (!IsInsideDirectory(projectAbsolute, projectRoot))
        {
            return Skip(edit, "path escapes project root");
        }

        var stagedAbsolute = Path.Combine(stagingRoot, normalizedRelative);
        var stagedDir = Path.GetDirectoryName(stagedAbsolute);
        if (!string.IsNullOrEmpty(stagedDir))
        {
            Directory.CreateDirectory(stagedDir);
        }

        string? originalSha256 = null;
        var originalBytes = 0;
        string? originalContent = null;
        if (File.Exists(projectAbsolute))
        {
            try
            {
                originalContent = File.ReadAllText(projectAbsolute, Encoding.UTF8);
                originalBytes = Encoding.UTF8.GetByteCount(originalContent);
                originalSha256 = ComputeSha256(originalContent);
            }
            catch (IOException ex)
            {
                return Skip(edit, $"original read failed: {ex.Message}");
            }
        }

        string newContent;
        if (string.Equals(edit.Operation, WorkerEdit.OperationWriteFull, StringComparison.OrdinalIgnoreCase))
        {
            newContent = edit.Content ?? string.Empty;
        }
        else if (string.Equals(edit.Operation, WorkerEdit.OperationInsertAfter, StringComparison.OrdinalIgnoreCase))
        {
            if (originalContent is null)
            {
                return Skip(edit, "insert_after requires existing file");
            }

            if (string.IsNullOrWhiteSpace(edit.Anchor))
            {
                return Skip(edit, "insert_after requires anchor");
            }

            var anchorIndex = originalContent.IndexOf(edit.Anchor, StringComparison.Ordinal);
            if (anchorIndex < 0)
            {
                return Skip(edit, "anchor not found");
            }

            var lastIndex = originalContent.LastIndexOf(edit.Anchor, StringComparison.Ordinal);
            if (lastIndex != anchorIndex)
            {
                return Skip(edit, "anchor not unique");
            }

            var spliceAt = anchorIndex + edit.Anchor.Length;
            newContent = string.Concat(
                originalContent.AsSpan(0, spliceAt),
                (edit.Content ?? string.Empty).AsSpan(),
                originalContent.AsSpan(spliceAt));
        }
        else if (string.Equals(edit.Operation, WorkerEdit.OperationInsertAtSlot, StringComparison.OrdinalIgnoreCase))
        {
            if (originalContent is null)
            {
                return Skip(edit, "insert_at_slot requires existing file");
            }

            if (!WorkerEditSlotResolver.TryResolveInsertionIndex(normalizedRelative, originalContent, edit.SlotId, out var insertionIndex, out var slotReason))
            {
                return Skip(edit, slotReason ?? "slot not found");
            }

            newContent = string.Concat(
                originalContent.AsSpan(0, insertionIndex),
                (edit.Content ?? string.Empty).AsSpan(),
                originalContent.AsSpan(insertionIndex));
        }
        else
        {
            return Skip(edit, "operation not supported");
        }

        try
        {
            // UTF-8 without BOM. Line endings preserved verbatim.
            File.WriteAllText(stagedAbsolute, newContent, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        }
        catch (IOException ex)
        {
            return Skip(edit, $"staged write failed: {ex.Message}");
        }

        var stagedBytes = Encoding.UTF8.GetByteCount(newContent);
        return new StagedEditResult(
            Path: edit.Path,
            Operation: edit.Operation,
            Applied: true,
            StagedAbsolutePath: stagedAbsolute,
            OriginalBytes: originalBytes,
            StagedBytes: stagedBytes,
            OriginalSha256: originalSha256,
            SkipReason: null,
            SlotId: string.IsNullOrWhiteSpace(edit.SlotId) ? null : edit.SlotId);
    }

    private static StagedEditResult Skip(WorkerEdit edit, string reason)
    {
        return new StagedEditResult(
            Path: edit.Path ?? string.Empty,
            Operation: edit.Operation ?? string.Empty,
            Applied: false,
            StagedAbsolutePath: null,
            OriginalBytes: 0,
            StagedBytes: 0,
            OriginalSha256: null,
            SkipReason: reason,
            SlotId: string.IsNullOrWhiteSpace(edit.SlotId) ? null : edit.SlotId);
    }

    private static void PersistManifest(string stagingRoot, StagingManifest manifest)
    {
        var manifestPath = Path.Combine(stagingRoot, "manifest.json");
        var tempPath = manifestPath + ".tmp";
        try
        {
            var json = JsonSerializer.Serialize(manifest, new JsonSerializerOptions
            {
                WriteIndented = true
            });
            File.WriteAllText(tempPath, json, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
            if (File.Exists(manifestPath))
            {
                File.Replace(tempPath, manifestPath, destinationBackupFileName: null, ignoreMetadataErrors: true);
            }
            else
            {
                File.Move(tempPath, manifestPath);
            }
        }
        catch (IOException ex)
        {
            throw new InvalidOperationException("Staging manifest is load-bearing and must be written before apply.", ex);
        }
        catch (UnauthorizedAccessException ex)
        {
            throw new InvalidOperationException("Staging manifest is load-bearing and must be written before apply.", ex);
        }
        finally
        {
            try
            {
                if (File.Exists(tempPath))
                {
                    File.Delete(tempPath);
                }
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
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
