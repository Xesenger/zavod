using System;
using System.Collections.Generic;
using System.Linq;
using zavod.Prompting;
using zavod.State;

namespace zavod.Contexting;

public static class ShiftContextBuilder
{
    public static ShiftContext Build(ShiftState shiftState, TaskState taskState)
    {
        ArgumentNullException.ThrowIfNull(shiftState);
        ArgumentNullException.ThrowIfNull(taskState);

        Require(shiftState.Tasks.Any(task => task.TaskId == taskState.TaskId),
            "shift context",
            "task membership",
            "Task must belong to shift.");

        return Build(new ShiftContextSourceInput(
            shiftState.ShiftId,
            shiftState.Goal,
            DescribeCurrentStep(taskState),
            $"{shiftState.Status}/{taskState.Status}",
            taskState.Scope,
            shiftState.AcceptedResults,
            shiftState.Constraints,
            shiftState.OpenIssues,
            taskState.IntentState,
            new[]
            {
                "shift_state",
                "task_state",
                "active_truth"
            },
            new[]
            {
                $"TaskId: {taskState.TaskId}",
                $"AssignedRole: {taskState.AssignedRole}"
            },
            DescribeNextExpectedAction(taskState)));
    }

    public static ShiftContext Build(ShiftContextSourceInput input)
    {
        ArgumentNullException.ThrowIfNull(input);

        Require(!string.IsNullOrWhiteSpace(input.ShiftId), "shift context", "shift id", "Shift id is required.");
        Require(!string.IsNullOrWhiteSpace(input.ShiftGoal), "shift context", "shift goal", "Shift goal is required.");
        Require(!string.IsNullOrWhiteSpace(input.CurrentStep), "shift context", "current step", "Current step is required.");
        Require(!string.IsNullOrWhiteSpace(input.CurrentStatus), "shift context", "current status", "Current status is required.");

        var scope = NormalizeLines(input.Scope);
        var acceptedResults = NormalizeLines(input.AcceptedResultsSummary);
        var constraints = NormalizeLines(input.RelevantConstraints);
        var openIssues = NormalizeLines(input.OpenIssues);
        var sources = NormalizeLines(input.ContextSourceSummary);
        var previousStep = NormalizeLines(input.PreviousStepSummary ?? Array.Empty<string>());

        Require(scope.Count > 0, "shift context", "scope", "At least one scope item is required.");
        Require(sources.Count > 0, "shift context", "context source summary", "At least one context source summary item is required.");
        Require(constraints.Count > 0, "shift context", "relevant constraints", "At least one relevant constraint is required.");

        return new ShiftContext(
            input.ShiftId.Trim(),
            input.ShiftGoal.Trim(),
            input.CurrentStep.Trim(),
            input.CurrentStatus.Trim(),
            scope,
            acceptedResults,
            constraints,
            openIssues,
            input.CurrentIntentState,
            sources,
            previousStep,
            string.IsNullOrWhiteSpace(input.NextExpectedAction) ? null : input.NextExpectedAction.Trim());
    }

    private static string DescribeCurrentStep(TaskState taskState)
    {
        return taskState.Status switch
        {
            TaskStateStatus.Active => "Active task",
            TaskStateStatus.Abandoned => "Abandoned task",
            TaskStateStatus.Completed => "Completed task",
            _ => "Task flow"
        };
    }

    private static string? DescribeNextExpectedAction(TaskState taskState)
    {
        return taskState.Status switch
        {
            TaskStateStatus.Active => "Continue task execution until closure review is requested",
            TaskStateStatus.Abandoned => "Review abandoned task history before opening a new step",
            TaskStateStatus.Completed => "Initiate shift closure",
            _ => null
        };
    }

    public static ProjectedShiftContext ProjectForRole(ShiftContext context, Capsule capsule, PromptRole role)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(capsule);

        return role switch
        {
            PromptRole.Worker => ProjectForWorker(context),
            PromptRole.ShiftLead => ProjectForShiftLead(context, capsule),
            PromptRole.Qc => ProjectForQc(context),
            PromptRole.SeniorSpecialist => ProjectForSeniorSpecialist(context, capsule),
            _ => throw new ArgumentOutOfRangeException(nameof(role), role, "Unknown prompt role.")
        };
    }

    private static ProjectedShiftContext ProjectForWorker(ShiftContext context)
    {
        Require(context.CurrentIntentState == ContextIntentState.Validated,
            "worker shift projection",
            "validated intent state",
            "Worker projection requires validated intent state.");
        Require(context.Scope.Count > 0,
            "worker shift projection",
            "scope",
            "Worker projection requires scope.");
        Require(context.RelevantConstraints.Count > 0,
            "worker shift projection",
            "relevant constraints",
            "Worker projection requires relevant constraints.");

        return new ProjectedShiftContext(
            "Worker",
            context.ShiftId,
            context.ShiftGoal,
            context.CurrentStep,
            context.CurrentStatus,
            context.Scope,
            context.AcceptedResultsSummary.Take(3).ToArray(),
            context.RelevantConstraints,
            Array.Empty<string>(),
            context.CurrentIntentState,
            context.ContextSourceSummary,
            context.PreviousStepSummary.Take(2).ToArray(),
            context.NextExpectedAction);
    }

    private static ProjectedShiftContext ProjectForShiftLead(ShiftContext context, Capsule capsule)
    {
        Require(context.Scope.Count > 0,
            "shift lead projection",
            "scope",
            "Shift Lead projection requires scope.");
        Require(context.ContextSourceSummary.Count > 0,
            "shift lead projection",
            "context source summary",
            "Shift Lead projection requires context source summary.");

        var broaderSources = context.ContextSourceSummary
            .Concat(capsule.CurrentFocus.Take(2))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(static value => value, StringComparer.Ordinal)
            .ToArray();

        var broaderConstraints = context.RelevantConstraints
            .Concat(capsule.ActiveConstraints.Take(2))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(static value => value, StringComparer.Ordinal)
            .ToArray();

        return new ProjectedShiftContext(
            "Shift Lead",
            context.ShiftId,
            context.ShiftGoal,
            context.CurrentStep,
            context.CurrentStatus,
            context.Scope,
            context.AcceptedResultsSummary.Take(3).ToArray(),
            broaderConstraints,
            context.OpenIssues,
            context.CurrentIntentState,
            broaderSources,
            context.PreviousStepSummary,
            context.NextExpectedAction);
    }

    private static ProjectedShiftContext ProjectForQc(ShiftContext context)
    {
        Require(context.CurrentIntentState == ContextIntentState.Validated,
            "qc shift projection",
            "validated intent state",
            "QC projection requires validated intent state.");
        Require(context.Scope.Count > 0,
            "qc shift projection",
            "scope",
            "QC projection requires scope.");
        Require(context.RelevantConstraints.Count > 0,
            "qc shift projection",
            "relevant constraints",
            "QC projection requires relevant constraints.");

        return new ProjectedShiftContext(
            "QC",
            context.ShiftId,
            context.ShiftGoal,
            context.CurrentStep,
            context.CurrentStatus,
            context.Scope,
            context.AcceptedResultsSummary,
            context.RelevantConstraints,
            context.OpenIssues,
            context.CurrentIntentState,
            context.ContextSourceSummary,
            context.PreviousStepSummary.Take(2).ToArray(),
            context.NextExpectedAction);
    }

    private static ProjectedShiftContext ProjectForSeniorSpecialist(ShiftContext context, Capsule capsule)
    {
        Require(context.OpenIssues.Count > 0,
            "senior specialist projection",
            "open issues",
            "Senior Specialist projection requires conflict or issue summary.");

        var truthSensitiveConstraints = context.RelevantConstraints
            .Concat(capsule.CoreCanonRules.Take(2))
            .Concat(capsule.ActiveConstraints.Take(2))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(static value => value, StringComparer.Ordinal)
            .ToArray();

        Require(truthSensitiveConstraints.Length > 0,
            "senior specialist projection",
            "truth-sensitive constraints",
            "Senior Specialist projection requires truth-sensitive constraints.");

        return new ProjectedShiftContext(
            "Senior Specialist",
            context.ShiftId,
            context.ShiftGoal,
            context.CurrentStep,
            context.CurrentStatus,
            context.Scope,
            context.AcceptedResultsSummary.Take(2).ToArray(),
            truthSensitiveConstraints,
            context.OpenIssues,
            context.CurrentIntentState,
            context.ContextSourceSummary,
            context.PreviousStepSummary,
            context.NextExpectedAction);
    }

    private static IReadOnlyList<string> NormalizeLines(IReadOnlyList<string> lines)
    {
        return lines
            .Where(static line => !string.IsNullOrWhiteSpace(line))
            .Select(static line => line.Trim())
            .Distinct(StringComparer.Ordinal)
            .OrderBy(static line => line, StringComparer.Ordinal)
            .ToArray();
    }

    private static void Require(bool condition, string area, string missingRequirement, string reason)
    {
        if (!condition)
        {
            throw new ContextBuildException(area, missingRequirement, reason);
        }
    }
}
