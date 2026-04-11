using System;
using System.Collections.Generic;
using zavod.Execution;
using zavod.Tooling;

namespace zavod.Boundary;

public sealed record ResultLifecycle(
    string LifecycleId,
    string SourceResultId,
    string TaskId,
    ResultLifecycleStatus Status,
    string? ReviewId = null,
    string? AcceptedResultId = null,
    string? ApplyId = null,
    string? CommitId = null);

public sealed record AcceptedResult(
    string AcceptedResultId,
    string SourceResultId,
    string TaskId,
    string Summary,
    DateTimeOffset AcceptedAt,
    string DecisionReference,
    IReadOnlyList<IntakeArtifact> AssociatedArtifacts)
{
    public bool DecisionAffectsStructureOrDirection { get; init; } = false;

    public string? CheckpointSummary { get; init; } = null;
}

public sealed record ApplyChange(
    string Reference,
    string ChangeKind,
    string Summary);

public sealed record ApplyOperation(
    string ApplyId,
    string AcceptedResultId,
    ApplyTarget Target,
    IReadOnlyList<ApplyChange> ChangeSet,
    ApplyStatus ApplyStatus);

public sealed record CommitRecord(
    string CommitId,
    string ApplyId,
    DateTimeOffset Timestamp,
    string Summary,
    string LinkedTaskId,
    IReadOnlyList<string> LinkedAnchors);

public sealed record AcceptResultOutcome(
    ResultLifecycle ReviewedLifecycle,
    ResultLifecycle AcceptedLifecycle,
    AcceptedResult AcceptedResult);

public sealed record ApplyOperationOutcome(
    ResultLifecycle AppliedLifecycle,
    ApplyOperation ApplyOperation);

public sealed record CommitOutcome(
    ResultLifecycle CommittedLifecycle,
    CommitRecord CommitRecord);
