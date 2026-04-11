using System;
using zavod.Contexting;

namespace zavod.Flow;

public static class StepPhaseMachine
{
    public static StepPhaseState StartDiscussion() => StepPhaseState.DiscussionIdle();

    public static StepPhaseState RecordIntent(StepPhaseState state, ContextIntentState intentState)
    {
        ArgumentNullException.ThrowIfNull(state);

        EnsurePhase(state, SurfacePhase.Discussion);

        return intentState switch
        {
            ContextIntentState.None => StepPhaseState.DiscussionIdle(),
            ContextIntentState.Orientation or ContextIntentState.Candidate or ContextIntentState.Refining => state with
            {
                DiscussionSubphase = state.HasReopenedContext ? DiscussionSubphase.Reopened : DiscussionSubphase.Forming,
                IntentState = intentState,
                HasClarification = false
            },
            ContextIntentState.ReadyForValidation => state with
            {
                DiscussionSubphase = DiscussionSubphase.Ready,
                IntentState = ContextIntentState.ReadyForValidation,
                HasClarification = false
            },
            _ => throw new InvalidOperationException("Only discussion intent states may be recorded inside the step phase machine.")
        };
    }

    public static StepPhaseState EnterPreflight(StepPhaseState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        EnsureDiscussionReady(state);

        if (state.HasActiveShift || state.HasActiveTask)
        {
            throw new InvalidOperationException("Fresh preflight entry requires discussion truth without active work.");
        }

        return new StepPhaseState(
            SurfacePhase.Execution,
            DiscussionSubphase.None,
            ExecutionSubphase.Preflight,
            ResultSubphase.None,
            ContextIntentState.ReadyForValidation,
            HasActiveShift: false,
            HasActiveTask: false,
            HasClarification: false,
            HasReopenedContext: false);
    }

    public static StepPhaseState EnterActiveShiftPreflight(StepPhaseState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        EnsureDiscussionReady(state);

        if (!state.HasActiveShift || state.HasActiveTask)
        {
            throw new InvalidOperationException("Active-shift preflight entry requires active shift truth without active task.");
        }

        return new StepPhaseState(
            SurfacePhase.Execution,
            DiscussionSubphase.None,
            ExecutionSubphase.Preflight,
            ResultSubphase.None,
            ContextIntentState.ReadyForValidation,
            HasActiveShift: true,
            HasActiveTask: false,
            HasClarification: false,
            HasReopenedContext: false);
    }

    public static StepPhaseState EnterReopenedPreflight(StepPhaseState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        EnsureDiscussionReady(state);

        if (!state.HasActiveShift || !state.HasActiveTask)
        {
            throw new InvalidOperationException("Reopened preflight entry requires active shift/task truth.");
        }

        return new StepPhaseState(
            SurfacePhase.Execution,
            DiscussionSubphase.None,
            ExecutionSubphase.Preflight,
            ResultSubphase.None,
            ContextIntentState.ReadyForValidation,
            HasActiveShift: true,
            HasActiveTask: true,
            HasClarification: false,
            HasReopenedContext: false);
    }

    public static StepPhaseState ApplyClarification(StepPhaseState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        EnsureExecutionSubphase(state, ExecutionSubphase.Preflight);

        return state with { HasClarification = true };
    }

    public static StepPhaseState CancelPreflight(StepPhaseState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        EnsureExecutionSubphase(state, ExecutionSubphase.Preflight);

        if (state.HasActiveShift && state.HasActiveTask)
        {
            return new StepPhaseState(
                SurfacePhase.Discussion,
                DiscussionSubphase.Ready,
                ExecutionSubphase.None,
                ResultSubphase.None,
                ContextIntentState.ReadyForValidation,
                HasActiveShift: true,
                HasActiveTask: true,
                HasClarification: state.HasClarification,
                HasReopenedContext: true);
        }

        if (state.HasActiveShift)
        {
            return new StepPhaseState(
                SurfacePhase.Discussion,
                DiscussionSubphase.Ready,
                ExecutionSubphase.None,
                ResultSubphase.None,
                ContextIntentState.ReadyForValidation,
                HasActiveShift: true,
                HasActiveTask: false,
                HasClarification: state.HasClarification,
                HasReopenedContext: false);
        }

        return new StepPhaseState(
            SurfacePhase.Discussion,
            DiscussionSubphase.Ready,
            ExecutionSubphase.None,
            ResultSubphase.None,
            ContextIntentState.ReadyForValidation,
            HasActiveShift: false,
            HasActiveTask: false,
            HasClarification: state.HasClarification,
            HasReopenedContext: false);
    }

    public static StepPhaseState ConfirmPreflight(StepPhaseState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        EnsureExecutionSubphase(state, ExecutionSubphase.Preflight);

        return new StepPhaseState(
            SurfacePhase.Execution,
            DiscussionSubphase.None,
            ExecutionSubphase.Running,
            ResultSubphase.None,
            ContextIntentState.Validated,
            HasActiveShift: true,
            HasActiveTask: true,
            HasClarification: state.HasClarification,
            HasReopenedContext: false);
    }

    public static StepPhaseState MoveToQc(StepPhaseState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        if (state.ExecutionSubphase is not (ExecutionSubphase.Running or ExecutionSubphase.Revision))
        {
            throw new InvalidOperationException("QC transition requires active execution running or revision state.");
        }

        return state with
        {
            Phase = SurfacePhase.Execution,
            ExecutionSubphase = ExecutionSubphase.Qc,
            ResultSubphase = ResultSubphase.None,
            IntentState = ContextIntentState.Validated,
            HasActiveShift = true,
            HasActiveTask = true
        };
    }

    public static StepPhaseState RejectQc(StepPhaseState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        EnsureExecutionSubphase(state, ExecutionSubphase.Qc);

        return state with
        {
            Phase = SurfacePhase.Execution,
            ExecutionSubphase = ExecutionSubphase.Revision,
            ResultSubphase = ResultSubphase.None,
            IntentState = ContextIntentState.Validated,
            HasActiveShift = true,
            HasActiveTask = true
        };
    }

    public static StepPhaseState AcceptQc(StepPhaseState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        EnsureExecutionSubphase(state, ExecutionSubphase.Qc);

        return new StepPhaseState(
            SurfacePhase.Result,
            DiscussionSubphase.None,
            ExecutionSubphase.None,
            ResultSubphase.Ready,
            ContextIntentState.Validated,
            HasActiveShift: true,
            HasActiveTask: true,
            HasClarification: state.HasClarification,
            HasReopenedContext: false);
    }

    public static StepPhaseState ReturnForRevision(StepPhaseState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        EnsureResultReady(state);

        return new StepPhaseState(
            SurfacePhase.Execution,
            DiscussionSubphase.None,
            ExecutionSubphase.Revision,
            ResultSubphase.RevisionRequested,
            ContextIntentState.Validated,
            HasActiveShift: true,
            HasActiveTask: true,
            HasClarification: state.HasClarification,
            HasReopenedContext: false);
    }

    public static StepPhaseState ReturnToLead(StepPhaseState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        EnsureResultReady(state);

        return new StepPhaseState(
            SurfacePhase.Discussion,
            DiscussionSubphase.Reopened,
            ExecutionSubphase.None,
            ResultSubphase.None,
            ContextIntentState.Refining,
            HasActiveShift: false,
            HasActiveTask: false,
            HasClarification: state.HasClarification,
            HasReopenedContext: true);
    }

    public static StepPhaseState CancelExecution(StepPhaseState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        if (state.ExecutionSubphase is not (ExecutionSubphase.Running or ExecutionSubphase.Qc or ExecutionSubphase.Revision))
        {
            throw new InvalidOperationException("Execution cancel requires running, QC, or revision subphase.");
        }

        return new StepPhaseState(
            SurfacePhase.Execution,
            DiscussionSubphase.None,
            ExecutionSubphase.Interrupted,
            ResultSubphase.None,
            ContextIntentState.Validated,
            HasActiveShift: true,
            HasActiveTask: true,
            HasClarification: state.HasClarification,
            HasReopenedContext: false);
    }

    public static StepPhaseState ResumeExecution(StepPhaseState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        EnsureExecutionSubphase(state, ExecutionSubphase.Interrupted);

        return state with
        {
            ExecutionSubphase = ExecutionSubphase.Running,
            ResultSubphase = ResultSubphase.None,
            IntentState = ContextIntentState.Validated,
            HasActiveShift = true,
            HasActiveTask = true
        };
    }

    public static StepPhaseState OpenInterruptedDiscussion(StepPhaseState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        EnsureExecutionSubphase(state, ExecutionSubphase.Interrupted);

        return new StepPhaseState(
            SurfacePhase.Discussion,
            DiscussionSubphase.Reopened,
            ExecutionSubphase.None,
            ResultSubphase.None,
            ContextIntentState.Refining,
            HasActiveShift: true,
            HasActiveTask: true,
            HasClarification: state.HasClarification,
            HasReopenedContext: true);
    }

    public static StepPhaseState StartRevisionCycle(StepPhaseState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        if (state.Phase != SurfacePhase.Execution
            || state.ExecutionSubphase != ExecutionSubphase.Revision
            || state.ResultSubphase != ResultSubphase.RevisionRequested)
        {
            throw new InvalidOperationException("Revision restart requires revision intake execution state.");
        }

        return state with
        {
            ExecutionSubphase = ExecutionSubphase.Running,
            ResultSubphase = ResultSubphase.None,
            IntentState = ContextIntentState.Validated,
            HasActiveShift = true,
            HasActiveTask = true
        };
    }

    public static StepPhaseState ReturnToResultFromRevisionIntake(StepPhaseState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        if (state.Phase != SurfacePhase.Execution
            || state.ExecutionSubphase != ExecutionSubphase.Revision
            || state.ResultSubphase != ResultSubphase.RevisionRequested)
        {
            throw new InvalidOperationException("Return to result requires revision intake execution state.");
        }

        return new StepPhaseState(
            SurfacePhase.Result,
            DiscussionSubphase.None,
            ExecutionSubphase.None,
            ResultSubphase.Ready,
            ContextIntentState.Validated,
            HasActiveShift: true,
            HasActiveTask: true,
            HasClarification: state.HasClarification,
            HasReopenedContext: false);
    }

    public static StepPhaseState AbandonResult(StepPhaseState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        EnsureResultReady(state);

        return ResumeActiveShiftDiscussion();
    }

    public static StepPhaseState ConfirmCompletion(StepPhaseState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        EnsureResultReady(state);

        return new StepPhaseState(
            SurfacePhase.Completed,
            DiscussionSubphase.None,
            ExecutionSubphase.None,
            ResultSubphase.None,
            ContextIntentState.None,
            HasActiveShift: false,
            HasActiveTask: false,
            HasClarification: false,
            HasReopenedContext: false);
    }

    public static StepPhaseState ResumeDiscussion(ContextIntentState intentState = ContextIntentState.None)
    {
        return intentState switch
        {
            ContextIntentState.None => StepPhaseState.DiscussionIdle(),
            ContextIntentState.Orientation or ContextIntentState.Candidate or ContextIntentState.Refining => new StepPhaseState(
                SurfacePhase.Discussion,
                DiscussionSubphase.Forming,
                ExecutionSubphase.None,
                ResultSubphase.None,
                intentState,
                HasActiveShift: false,
                HasActiveTask: false,
                HasClarification: false,
                HasReopenedContext: false),
            ContextIntentState.ReadyForValidation => new StepPhaseState(
                SurfacePhase.Discussion,
                DiscussionSubphase.Ready,
                ExecutionSubphase.None,
                ResultSubphase.None,
                ContextIntentState.ReadyForValidation,
                HasActiveShift: false,
                HasActiveTask: false,
                HasClarification: false,
                HasReopenedContext: false),
            _ => throw new InvalidOperationException("Discussion resume only supports non-active intent states.")
        };
    }

    public static StepPhaseState ResumeActiveShiftDiscussion(ContextIntentState intentState = ContextIntentState.None)
    {
        return intentState switch
        {
            ContextIntentState.None => new StepPhaseState(
                SurfacePhase.Discussion,
                DiscussionSubphase.Idle,
                ExecutionSubphase.None,
                ResultSubphase.None,
                ContextIntentState.None,
                HasActiveShift: true,
                HasActiveTask: false,
                HasClarification: false,
                HasReopenedContext: false),
            ContextIntentState.Orientation or ContextIntentState.Candidate or ContextIntentState.Refining => new StepPhaseState(
                SurfacePhase.Discussion,
                DiscussionSubphase.Forming,
                ExecutionSubphase.None,
                ResultSubphase.None,
                intentState,
                HasActiveShift: true,
                HasActiveTask: false,
                HasClarification: false,
                HasReopenedContext: false),
            ContextIntentState.ReadyForValidation => new StepPhaseState(
                SurfacePhase.Discussion,
                DiscussionSubphase.Ready,
                ExecutionSubphase.None,
                ResultSubphase.None,
                ContextIntentState.ReadyForValidation,
                HasActiveShift: true,
                HasActiveTask: false,
                HasClarification: false,
                HasReopenedContext: false),
            _ => throw new InvalidOperationException("Active-shift discussion resume only supports non-active intent states.")
        };
    }

    public static StepPhaseState ResumeWork()
    {
        return new StepPhaseState(
            SurfacePhase.Execution,
            DiscussionSubphase.None,
            ExecutionSubphase.Running,
            ResultSubphase.None,
            ContextIntentState.Validated,
            HasActiveShift: true,
            HasActiveTask: true,
            HasClarification: false,
            HasReopenedContext: false);
    }

    public static StepPhaseState ResumeResult()
    {
        return new StepPhaseState(
            SurfacePhase.Result,
            DiscussionSubphase.None,
            ExecutionSubphase.None,
            ResultSubphase.Ready,
            ContextIntentState.Validated,
            HasActiveShift: true,
            HasActiveTask: true,
            HasClarification: false,
            HasReopenedContext: false);
    }

    public static StepPhaseState ResumeInterrupted()
    {
        return new StepPhaseState(
            SurfacePhase.Execution,
            DiscussionSubphase.None,
            ExecutionSubphase.Interrupted,
            ResultSubphase.None,
            ContextIntentState.Validated,
            HasActiveShift: true,
            HasActiveTask: true,
            HasClarification: false,
            HasReopenedContext: false);
    }

    private static void EnsureDiscussionReady(StepPhaseState state)
    {
        EnsurePhase(state, SurfacePhase.Discussion);
        if (state.DiscussionSubphase != DiscussionSubphase.Ready || state.IntentState != ContextIntentState.ReadyForValidation)
        {
            throw new InvalidOperationException("Preflight entry requires discussion readiness.");
        }
    }

    private static void EnsureResultReady(StepPhaseState state)
    {
        EnsurePhase(state, SurfacePhase.Result);
        if (state.ResultSubphase != ResultSubphase.Ready)
        {
            throw new InvalidOperationException("Result action requires ready result state.");
        }
    }

    private static void EnsureExecutionSubphase(StepPhaseState state, ExecutionSubphase expected)
    {
        EnsurePhase(state, SurfacePhase.Execution);
        if (state.ExecutionSubphase != expected)
        {
            throw new InvalidOperationException($"Expected execution subphase '{expected}', got '{state.ExecutionSubphase}'.");
        }
    }

    private static void EnsurePhase(StepPhaseState state, SurfacePhase expected)
    {
        if (state.Phase != expected)
        {
            throw new InvalidOperationException($"Expected phase '{expected}', got '{state.Phase}'.");
        }
    }
}
