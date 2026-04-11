using System.Collections.Generic;
using zavod.Prompting;
using zavod.Retrieval;
using zavod.Tooling;

namespace zavod.Execution;

public sealed record ExecutionSession(
    string SessionId,
    PromptRole CurrentRole,
    string AssociatedTaskId,
    string AssociatedShiftId,
    ExecutionSessionState State,
    string? BindingReference = null,
    string? ResultId = null,
    string? ReviewId = null,
    string? FailureReason = null);

public sealed record ExecutionTaskBinding(
    string TaskId,
    string ValidatedIntentReference,
    string ScopedContextReference,
    ScopedContext ScopedContext,
    IReadOnlyList<PromptRole> AllowedRoles);

public sealed record WorkerExecutionModification(
    string Path,
    string ChangeKind,
    string Summary);

public sealed record WorkerExecutionResult(
    string ResultId,
    string TaskId,
    WorkerExecutionStatus Status,
    string Summary,
    IReadOnlyList<IntakeArtifact> ProducedArtifacts,
    IReadOnlyList<WorkerExecutionModification> Modifications,
    IReadOnlyList<ToolWarning> Warnings,
    ToolDiagnostic? Diagnostics = null);

public sealed record QCReviewResult(
    string ReviewId,
    string ResultId,
    QCReviewStatus Status,
    IReadOnlyList<string> Comments,
    IReadOnlyList<string> DecisionAnchors,
    string? RejectReason = null);

public sealed record ExecutionPreparation(
    ExecutionSession Session,
    ExecutionTaskBinding Binding);
