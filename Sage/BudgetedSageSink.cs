using System;
using System.Collections.Generic;

namespace zavod.Sage;

// v2.1a guardrail #5: core-enforced observation budgets.
//
// Per-task:   max 8 observations
// Per-hook:   max 3 observations (per SageStage within one task)
//
// Overflow policy:
//   1. drop the incoming observation
//   2. emit exactly ONE flood_suppressed meta per (task, scope) the first
//      time the limit is hit; subsequent drops are silent (no flood
//      spam)
//
// The sink is a decorator over SageObservationSink. S2b wires it into
// SageHookRunner via constructor but S2a/S2b do not emit anything —
// bodies remain empty. The first real write happens in S3 when a
// semantic_gap / pattern_repeat / attention_miss emitter calls
// TryEmit from within a hook.
public sealed class BudgetedSageSink
{
    public const int PerHookBudget = 3;
    public const int PerTaskBudget = 8;

    private sealed class TaskCounters
    {
        public int TaskTotal;
        public bool TaskFloodReported;
        public readonly Dictionary<SageStage, int> PerHook = new();
        public readonly HashSet<SageStage> HookFloodReported = new();
    }

    private readonly SageObservationSink _inner;
    private readonly Dictionary<string, TaskCounters> _byTask = new(StringComparer.Ordinal);
    private readonly object _lock = new();

    public BudgetedSageSink(SageObservationSink? inner = null)
    {
        _inner = inner ?? new SageObservationSink();
    }

    public void TryEmit(string projectRootPath, SageObservation observation)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(projectRootPath);
        ArgumentNullException.ThrowIfNull(observation);

        SageObservation? writeObservation;
        SageObservation? floodMeta = null;

        lock (_lock)
        {
            // Taskless observations (AfterIntent fires before a task exists)
            // must not share one global "<no-task>" bucket across projects.
            // Falling back to ProjectId isolates per-project; cross-turn
            // sharing within a project is acceptable because hook-level
            // emitters in S3+ are capped at 1 obs/hook per user turn anyway.
            var taskKey = observation.TaskId ?? observation.ProjectId ?? "<global>";
            if (!_byTask.TryGetValue(taskKey, out var counters))
            {
                counters = new TaskCounters();
                _byTask[taskKey] = counters;
            }

            if (counters.TaskTotal >= PerTaskBudget)
            {
                writeObservation = null;
                if (!counters.TaskFloodReported)
                {
                    counters.TaskFloodReported = true;
                    floodMeta = BuildFloodMeta(observation, scope: "task");
                }
            }
            else
            {
                counters.PerHook.TryGetValue(observation.Stage, out var hookCount);
                if (hookCount >= PerHookBudget)
                {
                    writeObservation = null;
                    if (counters.HookFloodReported.Add(observation.Stage))
                    {
                        floodMeta = BuildFloodMeta(observation, scope: "hook");
                    }
                }
                else
                {
                    counters.PerHook[observation.Stage] = hookCount + 1;
                    counters.TaskTotal += 1;
                    writeObservation = observation;
                }
            }
        }

        if (writeObservation is not null)
        {
            _inner.Write(projectRootPath, writeObservation);
        }
        if (floodMeta is not null)
        {
            _inner.Write(projectRootPath, floodMeta);
        }
    }

    private static SageObservation BuildFloodMeta(SageObservation original, string scope)
    {
        return new SageObservation(
            Type: SageObservationType.FloodSuppressed,
            Severity: SageSeverity.Hint,
            Message: $"Observation budget exceeded ({scope}).",
            Stage: original.Stage,
            Channel: SageChannel.SageOnly,
            ObservedAt: DateTimeOffset.UtcNow,
            ProjectId: original.ProjectId,
            ShiftId: original.ShiftId,
            TaskId: original.TaskId);
    }
}
