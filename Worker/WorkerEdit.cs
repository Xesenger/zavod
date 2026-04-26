using System;
using System.Collections.Generic;

namespace zavod.Worker;

/// <summary>
/// Typed edit operation emitted by Worker LLM. This is the breaking change
/// that moves Worker from "plan a modification" to "actually produce the
/// content of a modification".
///
/// Operation types (MVP):
/// - write_full: replace the entire file contents with <c>Content</c>.
///   Use for small files or when the whole file is being rewritten. The
///   parent directory is created on stage if it does not exist.
/// - insert_at_slot: resolve <c>SlotId</c> against the current target file and
///   insert <c>Content</c> at the deterministic insertion point. This is the
///   preferred DSL operation when Worker was given an EDIT SLOTS map.
/// - insert_after: find <c>Anchor</c> in the file (exact string match) and
///   insert <c>Content</c> immediately after it. Use for targeted additions
///   to larger files where emitting the whole file is not justified.
///
/// Content constraints: <c>Content</c> is the literal text to write / insert.
/// No placeholder tokens, no diff markers, no markdown fences. The stager
/// writes it verbatim.
///
/// Anchor constraints (insert_after only): <c>Anchor</c> must appear exactly
/// once in the target file. If zero or multiple matches, the edit is skipped
/// and a warning is emitted to Worker metadata. Anchors are line-level or
/// multi-line exact strings drawn from the file's current contents (Worker
/// sees those via the snippet pack).
/// </summary>
public sealed record WorkerEdit(
    string Path,
    string Operation,
    string Content,
    string? Anchor = null,
    string? SlotId = null)
{
    public const string OperationWriteFull = "write_full";
    public const string OperationInsertAfter = "insert_after";
    public const string OperationInsertAtSlot = "insert_at_slot";

    public bool IsKnownOperation()
    {
        return string.Equals(Operation, OperationWriteFull, StringComparison.OrdinalIgnoreCase)
            || string.Equals(Operation, OperationInsertAfter, StringComparison.OrdinalIgnoreCase)
            || string.Equals(Operation, OperationInsertAtSlot, StringComparison.OrdinalIgnoreCase);
    }
}

public sealed record WorkerEditSlot(
    string Path,
    string SlotId,
    string Kind,
    string Reason);

/// <summary>
/// Outcome of staging a single edit onto disk. Captures the target path,
/// the operation, and either the absolute staged location + size-delta or
/// the reason it was skipped.
/// </summary>
public sealed record StagedEditResult(
    string Path,
    string Operation,
    bool Applied,
    string? StagedAbsolutePath,
    int OriginalBytes,
    int StagedBytes,
    string? OriginalSha256,
    string? SkipReason,
    string? SlotId = null);

/// <summary>
/// Aggregate manifest produced by StagingWriter. Serialized to
/// <c>.zavod/staging/&lt;taskId&gt;/manifest.json</c> as the durable artifact
/// describing the staged change set.
/// </summary>
public sealed record StagingManifest(
    string TaskId,
    string TaskDescription,
    string StagingRoot,
    DateTimeOffset StagedAt,
    IReadOnlyList<StagedEditResult> Results)
{
    public int AppliedCount
    {
        get
        {
            var count = 0;
            foreach (var result in Results)
            {
                if (result.Applied)
                {
                    count++;
                }
            }

            return count;
        }
    }

    public int SkippedCount => Results.Count - AppliedCount;
}
