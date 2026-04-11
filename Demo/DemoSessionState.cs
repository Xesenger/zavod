using System;
using System.Collections.Generic;
using zavod.Execution;

namespace zavod.Demo;

public sealed class DemoSessionState
{
    private readonly DemoScenarioSeed _scenario;
    private int _currentStepIndex;
    private int _currentCycleIndex;

    public DemoSessionState(DemoScenarioSeed scenario)
    {
        _scenario = scenario ?? throw new ArgumentNullException(nameof(scenario));
    }

    public bool IsComplete => _currentStepIndex >= _scenario.Steps.Count;
    public bool DisableInputs => true;
    public bool DisableSend => true;
    public bool AutoAdvanceResult => !IsComplete;
    public int CurrentStepIndex => _currentStepIndex;
    public int CurrentCycleIndex => _currentCycleIndex;

    public string ChatDraft => IsComplete ? _scenario.CompletionDraft : CurrentStep.ChatDraft;
    public string ClarifyDraft => IsComplete ? string.Empty : CurrentStep.ClarifyDraft;
    public string RevisionDraft => IsComplete ? string.Empty : CurrentStep.RevisionDraft;

    public IReadOnlyList<string> BuildAgreementItems(string? clarification)
    {
        return IsComplete
            ? Array.Empty<string>()
            : CurrentStep.BuildAgreementItems(clarification);
    }

    public IReadOnlyList<string> CurrentWorkLogLines =>
        IsComplete ? Array.Empty<string>() : CurrentStep.GetCycle(_currentCycleIndex).WorkLogLines;

    public WorkerExecutionResult BuildResult(string taskId)
    {
        if (IsComplete)
        {
            throw new InvalidOperationException("Completed demo session cannot build more results.");
        }

        return CurrentStep.BuildResult(taskId, _currentCycleIndex);
    }

    public bool HasNextCycle()
    {
        return !IsComplete && _currentCycleIndex < CurrentStep.WorkCycles.Count - 1;
    }

    public void AdvanceToRevisionCycle()
    {
        if (!HasNextCycle())
        {
            return;
        }

        _currentCycleIndex++;
    }

    public void AdvanceAfterAccept()
    {
        if (IsComplete)
        {
            return;
        }

        _currentStepIndex++;
        _currentCycleIndex = 0;
    }

    public void ResetToStart()
    {
        _currentStepIndex = 0;
        _currentCycleIndex = 0;
    }

    public void RestorePosition(int stepIndex, int cycleIndex)
    {
        if (stepIndex >= _scenario.Steps.Count)
        {
            _currentStepIndex = _scenario.Steps.Count;
            _currentCycleIndex = 0;
            return;
        }

        _currentStepIndex = Math.Clamp(stepIndex, 0, Math.Max(0, _scenario.Steps.Count - 1));
        _currentCycleIndex = Math.Clamp(cycleIndex, 0, Math.Max(0, CurrentStep.WorkCycles.Count - 1));
    }

    private DemoStepScenario CurrentStep => _scenario.Steps[_currentStepIndex];
}
