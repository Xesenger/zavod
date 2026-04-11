using System.Collections.Generic;

namespace zavod.Contexting;

public sealed record ShiftContext(
    string ShiftId,
    string ShiftGoal,
    string CurrentStep,
    string CurrentStatus,
    IReadOnlyList<string> Scope,
    IReadOnlyList<string> AcceptedResultsSummary,
    IReadOnlyList<string> RelevantConstraints,
    IReadOnlyList<string> OpenIssues,
    ContextIntentState CurrentIntentState,
    IReadOnlyList<string> ContextSourceSummary,
    IReadOnlyList<string> PreviousStepSummary,
    string? NextExpectedAction);

public sealed record ShiftContextSourceInput(
    string ShiftId,
    string ShiftGoal,
    string CurrentStep,
    string CurrentStatus,
    IReadOnlyList<string> Scope,
    IReadOnlyList<string> AcceptedResultsSummary,
    IReadOnlyList<string> RelevantConstraints,
    IReadOnlyList<string> OpenIssues,
    ContextIntentState CurrentIntentState,
    IReadOnlyList<string> ContextSourceSummary,
    IReadOnlyList<string>? PreviousStepSummary = null,
    string? NextExpectedAction = null);

public sealed record ProjectedShiftContext(
    string Role,
    string ShiftId,
    string ShiftGoal,
    string CurrentStep,
    string CurrentStatus,
    IReadOnlyList<string> Scope,
    IReadOnlyList<string> AcceptedResultsSummary,
    IReadOnlyList<string> RelevantConstraints,
    IReadOnlyList<string> OpenIssues,
    ContextIntentState CurrentIntentState,
    IReadOnlyList<string> ContextSourceSummary,
    IReadOnlyList<string> PreviousStepSummary,
    string? NextExpectedAction);
