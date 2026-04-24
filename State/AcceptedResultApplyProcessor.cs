using System;
using System.Linq;
using zavod.Acceptance;
using zavod.Boundary;
using zavod.Execution;
using zavod.Persistence;
using zavod.Prompting;

namespace zavod.State;

public static class AcceptedResultApplyProcessor
{
    public static AcceptedResultApplyResult Apply(
        ProjectState projectState,
        ShiftState shiftState,
        TaskState taskState,
        ExecutionRuntimeState runtimeState,
        DateTimeOffset timestamp)
    {
        ArgumentNullException.ThrowIfNull(projectState);
        ArgumentNullException.ThrowIfNull(shiftState);
        ArgumentNullException.ThrowIfNull(taskState);
        ArgumentNullException.ThrowIfNull(runtimeState);

        ValidateCanApply(projectState, shiftState, taskState, runtimeState);

        var lifecycle = ResultCommitCoordinator.RegisterProducedResult(runtimeState.Result!);
        var acceptance = ResultCommitCoordinator.AcceptResult(lifecycle, runtimeState.Result!, runtimeState.Review!, timestamp);
        var acceptedResult = TryAttachCheckpointTruth(acceptance.AcceptedResult, runtimeState.Result!);
        var applyOutcome = ResultCommitCoordinator.ApplyAcceptedResult(
            acceptance.AcceptedLifecycle,
            acceptedResult,
            ApplyTarget.Codebase,
            BuildApplyChanges(runtimeState.Result!));
        var commit = ResultCommitCoordinator.Commit(
            applyOutcome.AppliedLifecycle,
            applyOutcome.ApplyOperation,
            timestamp,
            runtimeState.Result!.Summary,
            taskState.TaskId,
            runtimeState.Review!.DecisionAnchors);

        var completedTask = taskState.Complete(PromptRole.ShiftLead, timestamp);
        var updatedShift = shiftState.UpdateTask(completedTask);
        updatedShift = ResultCommitCoordinator.UpdateShiftState(updatedShift, commit.CommitRecord, PromptRole.ShiftLead);
        updatedShift = updatedShift with { CurrentTaskId = null };

        var updatedProjectState = projectState with
        {
            ActiveShiftId = shiftState.ShiftId,
            ActiveTaskId = null
        };

        var persistedProjectState = ProjectStateStorage.Save(updatedProjectState);
        var shiftFilePath = ShiftStateStorage.Save(persistedProjectState.Paths.ProjectRoot, updatedShift);

        return new AcceptedResultApplyResult(
            persistedProjectState,
            updatedShift,
            completedTask,
            acceptedResult,
            applyOutcome.ApplyOperation,
            commit.CommitRecord,
            shiftFilePath);
    }

    public static void ValidateCanApply(
        ProjectState projectState,
        ShiftState shiftState,
        TaskState taskState,
        ExecutionRuntimeState runtimeState)
    {
        ArgumentNullException.ThrowIfNull(projectState);
        ArgumentNullException.ThrowIfNull(shiftState);
        ArgumentNullException.ThrowIfNull(taskState);
        ArgumentNullException.ThrowIfNull(runtimeState);

        if (!string.Equals(projectState.ActiveShiftId, shiftState.ShiftId, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Accepted result apply requires active project shift to match target shift.");
        }

        if (!string.Equals(projectState.ActiveTaskId, taskState.TaskId, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Accepted result apply requires active project task to match target task.");
        }

        if (shiftState.Status != ShiftStateStatus.Active)
        {
            throw new InvalidOperationException("Accepted result apply requires active shift truth.");
        }

        if (!string.Equals(shiftState.CurrentTaskId, taskState.TaskId, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Accepted result apply requires task bound as current shift task.");
        }

        if (runtimeState.Session.State != ExecutionSessionState.Completed || runtimeState.Result is null || runtimeState.Review is null)
        {
            throw new InvalidOperationException("Accepted result apply requires completed runtime with accepted review.");
        }

        ValidateAcceptanceGate(runtimeState);
    }

    private static AcceptedResult TryAttachCheckpointTruth(AcceptedResult acceptedResult, WorkerExecutionResult result)
    {
        ArgumentNullException.ThrowIfNull(acceptedResult);
        ArgumentNullException.ThrowIfNull(result);

        if (!TouchesProjectLevelCheckpointTruth(result))
        {
            return acceptedResult;
        }

        return acceptedResult with
        {
            DecisionAffectsStructureOrDirection = true,
            CheckpointSummary = "Checkpoint: accepted result updated project-level direction or decision truth."
        };
    }

    private static bool TouchesProjectLevelCheckpointTruth(WorkerExecutionResult result)
    {
        return result.Modifications.Any(static modification => IsProjectLevelCheckpointPath(modification.Path));
    }

    private static bool IsProjectLevelCheckpointPath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return false;
        }

        var normalizedPath = path
            .Trim()
            .Replace('\\', '/');

        return normalizedPath.EndsWith("project/direction.md", StringComparison.OrdinalIgnoreCase)
            || normalizedPath.EndsWith("project/roadmap.md", StringComparison.OrdinalIgnoreCase)
            || normalizedPath.EndsWith("project/canon.md", StringComparison.OrdinalIgnoreCase)
            || normalizedPath.Contains("/decisions/", StringComparison.OrdinalIgnoreCase)
            || normalizedPath.StartsWith("decisions/", StringComparison.OrdinalIgnoreCase);
    }

    private static ApplyChange[] BuildApplyChanges(WorkerExecutionResult result)
    {
        var changes = result.Modifications
            .Where(static modification => !string.IsNullOrWhiteSpace(modification.Path))
            .Select(static modification => new ApplyChange(
                modification.Path,
                modification.ChangeKind,
                string.IsNullOrWhiteSpace(modification.Summary) ? modification.ChangeKind : modification.Summary))
            .ToArray();

        if (changes.Length > 0)
        {
            return changes;
        }

        return new[]
        {
            new ApplyChange(
                result.TaskId,
                "apply",
                string.IsNullOrWhiteSpace(result.Summary) ? "Apply accepted result." : result.Summary)
        };
    }

    private static void ValidateAcceptanceGate(ExecutionRuntimeState runtimeState)
    {
        if (runtimeState.AcceptanceEvaluation is null)
        {
            throw new InvalidOperationException("Accepted result apply requires acceptance evaluation.");
        }

        if (runtimeState.AcceptanceEvaluation.Decision.Status != AcceptanceDecisionStatus.Allowed)
        {
            throw new InvalidOperationException(
                $"Accepted result apply is blocked by acceptance gate: {runtimeState.AcceptanceEvaluation.Decision.Classification}.");
        }
    }
}

public sealed record AcceptedResultApplyResult(
    ProjectState ProjectState,
    ShiftState ShiftState,
    TaskState TaskState,
    AcceptedResult AcceptedResult,
    ApplyOperation ApplyOperation,
    CommitRecord CommitRecord,
    string ShiftFilePath);
