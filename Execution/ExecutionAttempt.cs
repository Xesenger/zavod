using System;
using zavod.Outcome;

namespace zavod.Execution;

public sealed record ExecutionAttempt(
    int AttemptIndex,
    ExecutionOutcomeStatus OutcomeStatus,
    QCReviewStatus QcStatus,
    string? RejectReason,
    DateTime CreatedAt);
