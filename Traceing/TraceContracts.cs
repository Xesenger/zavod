using System;
using System.Collections.Generic;
using zavod.Contexting;
using zavod.Prompting;
using zavod.State;
using zavod.Boundary;

namespace zavod.Traceing;

public sealed record ExecutionStep(
    ExecutionStepType StepType,
    DateTimeOffset Timestamp,
    PromptRole Role,
    string ReferenceId,
    string Summary);

public sealed record ExecutionTrace(
    string TraceId,
    string SessionId,
    string TaskId,
    IReadOnlyList<ExecutionStep> Steps);

public sealed record SnapshotOpenTask(
    string TaskId,
    ContextIntentState IntentState,
    TaskStateStatus Status);

public sealed record Snapshot(
    string SnapshotId,
    string ShiftStateReference,
    string ExecutionReference,
    string TaskReference,
    IReadOnlyList<CommitRecord> Commits,
    IReadOnlyList<SnapshotOpenTask> OpenTasks,
    IReadOnlyList<string> Constraints,
    DateTimeOffset Timestamp,
    string CheckpointKind = "closure",
    int TriggerScore = 0,
    IReadOnlyList<string>? TriggerReasons = null,
    string? DedupeKey = null);
