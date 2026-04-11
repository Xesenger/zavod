using System;
using System.Collections.Generic;
using System.Linq;
using zavod.Execution;

namespace zavod.State;

public sealed record SoftCheckpointSignal(
    bool ShouldCreateSnapshot,
    int Score,
    IReadOnlyList<string> Reasons);

public static class SoftCheckpointSignalResolverV1
{
    public static SoftCheckpointSignal Resolve(
        ShiftState shiftState,
        TaskState completedTask,
        WorkerExecutionResult result)
    {
        ArgumentNullException.ThrowIfNull(shiftState);
        ArgumentNullException.ThrowIfNull(completedTask);
        ArgumentNullException.ThrowIfNull(result);

        if (completedTask.Status != TaskStateStatus.Completed)
        {
            return new SoftCheckpointSignal(false, 0, Array.Empty<string>());
        }

        var reasons = new List<string>();
        var score = 0;
        var previousFinalizedTask = shiftState.Tasks
            .Where(task => !string.Equals(task.TaskId, completedTask.TaskId, StringComparison.Ordinal))
            .LastOrDefault(task => task.Status is TaskStateStatus.Completed or TaskStateStatus.Abandoned);

        var currentDomain = ResolveCurrentDomain(result, completedTask);
        var previousDomain = previousFinalizedTask is null
            ? WorkDomain.Unknown
            : ResolveTaskDomain(previousFinalizedTask);

        if (previousFinalizedTask is not null
            && currentDomain != WorkDomain.Unknown
            && previousDomain != WorkDomain.Unknown
            && currentDomain != previousDomain)
        {
            score += 2;
            reasons.Add("domain_shift");
        }

        var currentIntentType = ResolveIntentType(completedTask.Description);
        var previousIntentType = previousFinalizedTask is null
            ? IntentType.Unknown
            : ResolveIntentType(previousFinalizedTask.Description);

        if (previousFinalizedTask is not null
            && currentIntentType != IntentType.Unknown
            && previousIntentType != IntentType.Unknown
            && currentIntentType != previousIntentType)
        {
            score += 1;
            reasons.Add("intent_type_change");
        }

        return new SoftCheckpointSignal(
            score >= 2,
            score,
            reasons
                .Distinct(StringComparer.Ordinal)
                .OrderBy(reason => reason, StringComparer.Ordinal)
                .ToArray());
    }

    private static WorkDomain ResolveCurrentDomain(WorkerExecutionResult result, TaskState completedTask)
    {
        var pathDomain = ResolvePathDomain(result.Modifications.Select(modification => modification.Path));
        return pathDomain != WorkDomain.Unknown
            ? pathDomain
            : ResolveTaskDomain(completedTask);
    }

    private static WorkDomain ResolveTaskDomain(TaskState task)
    {
        ArgumentNullException.ThrowIfNull(task);

        var description = task.Description ?? string.Empty;
        var scopeDomain = ResolvePathDomain(task.Scope);
        if (scopeDomain != WorkDomain.Unknown)
        {
            return scopeDomain;
        }

        if (ContainsAny(description, "xaml", "qml", "screen", "layout", "ui", "button", "экран", "размет", "кноп"))
        {
            return WorkDomain.Ui;
        }

        if (ContainsAny(description, "state", "lifecycle", "phase", "flow", "pipeline", "core", "execution", "runtime", "shift", "task", "состоя", "жизненн", "фаз", "поток", "ядр", "смен", "задач"))
        {
            return WorkDomain.CoreLifecycle;
        }

        if (ContainsAny(description, "roadmap", "canon", "direction", "decision", "road map", "канон", "роадмап", "направлен", "решени"))
        {
            return WorkDomain.ProjectTruth;
        }

        return WorkDomain.Unknown;
    }

    private static WorkDomain ResolvePathDomain(IEnumerable<string> paths)
    {
        var hasUi = false;
        var hasCore = false;
        var hasTruth = false;

        foreach (var rawPath in paths)
        {
            if (string.IsNullOrWhiteSpace(rawPath))
            {
                continue;
            }

            var path = rawPath.Trim().Replace('\\', '/');

            if (path.EndsWith(".xaml", StringComparison.OrdinalIgnoreCase)
                || path.EndsWith(".qml", StringComparison.OrdinalIgnoreCase)
                || path.Contains("/ui/", StringComparison.OrdinalIgnoreCase)
                || path.Contains("/views/", StringComparison.OrdinalIgnoreCase)
                || path.Contains("/screens/", StringComparison.OrdinalIgnoreCase)
                || path.Contains("/layout/", StringComparison.OrdinalIgnoreCase)
                || path.EndsWith("MainWindow.xaml", StringComparison.OrdinalIgnoreCase))
            {
                hasUi = true;
            }

            if (path.StartsWith("Flow/", StringComparison.OrdinalIgnoreCase)
                || path.StartsWith("State/", StringComparison.OrdinalIgnoreCase)
                || path.StartsWith("Execution/", StringComparison.OrdinalIgnoreCase)
                || path.StartsWith("Persistence/", StringComparison.OrdinalIgnoreCase)
                || path.StartsWith("Boundary/", StringComparison.OrdinalIgnoreCase)
                || path.StartsWith("Router/", StringComparison.OrdinalIgnoreCase)
                || path.StartsWith("Dispatching/", StringComparison.OrdinalIgnoreCase)
                || path.StartsWith("Bootstrap/", StringComparison.OrdinalIgnoreCase)
                || path.StartsWith("Orchestration/", StringComparison.OrdinalIgnoreCase)
                || path.StartsWith("Planning/", StringComparison.OrdinalIgnoreCase)
                || path.StartsWith("Outcome/", StringComparison.OrdinalIgnoreCase)
                || path.StartsWith("Prompting/", StringComparison.OrdinalIgnoreCase)
                || path.StartsWith("Retrieval/", StringComparison.OrdinalIgnoreCase)
                || path.StartsWith("Contexting/", StringComparison.OrdinalIgnoreCase))
            {
                hasCore = true;
            }

            if (path.EndsWith("project/direction.md", StringComparison.OrdinalIgnoreCase)
                || path.EndsWith("project/roadmap.md", StringComparison.OrdinalIgnoreCase)
                || path.EndsWith("project/canon.md", StringComparison.OrdinalIgnoreCase)
                || path.Contains("/decisions/", StringComparison.OrdinalIgnoreCase)
                || path.StartsWith("decisions/", StringComparison.OrdinalIgnoreCase))
            {
                hasTruth = true;
            }
        }

        if (hasTruth)
        {
            return WorkDomain.ProjectTruth;
        }

        if (hasCore)
        {
            return WorkDomain.CoreLifecycle;
        }

        if (hasUi)
        {
            return WorkDomain.Ui;
        }

        return WorkDomain.Unknown;
    }

    private static IntentType ResolveIntentType(string description)
    {
        if (string.IsNullOrWhiteSpace(description))
        {
            return IntentType.Unknown;
        }

        if (ContainsAny(description, "architect", "lifecycle", "phase", "flow", "pipeline", "state", "core", "direction", "architecture", "архит", "жизненн", "фаз", "поток", "состоя", "ядр", "направлен"))
        {
            return IntentType.Architecture;
        }

        if (ContainsAny(description, "redesign", "refactor", "rework", "reshape", "передел", "перестро", "рефактор", "переработ"))
        {
            return IntentType.Redesign;
        }

        if (ContainsAny(description, "add", "implement", "create", "build", "introduce", "добав", "сдела", "реализ", "созда", "внедр"))
        {
            return IntentType.Implementation;
        }

        if (ContainsAny(description, "fix", "correct", "adjust", "align", "patch", "исправ", "поправ", "выров", "подправ", "почин"))
        {
            return IntentType.Fix;
        }

        return IntentType.Unknown;
    }

    private static bool ContainsAny(string value, params string[] markers)
    {
        return markers.Any(marker => value.Contains(marker, StringComparison.OrdinalIgnoreCase));
    }

    private enum WorkDomain
    {
        Unknown,
        Ui,
        CoreLifecycle,
        ProjectTruth
    }

    private enum IntentType
    {
        Unknown,
        Fix,
        Implementation,
        Redesign,
        Architecture
    }
}
