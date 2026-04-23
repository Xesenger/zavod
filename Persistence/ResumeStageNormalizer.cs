using System;
using zavod.Contexting;
using zavod.Execution;
using zavod.Flow;

namespace zavod.Persistence;

public static class ResumeStageNormalizer
{
    public static ResumeStageSnapshot? Normalize(
        ResumeStageSnapshot? snapshot,
        bool hasActiveWork,
        bool preserveLiveRuntimePhase = false,
        bool hasActiveShift = false)
    {
        if (snapshot is null)
        {
            return null;
        }

        var phaseState = NormalizePhaseState(snapshot.PhaseState, snapshot.RuntimeState, hasActiveWork, preserveLiveRuntimePhase, hasActiveShift);
        var isPreflight = phaseState.Phase == SurfacePhase.Execution
            && phaseState.ExecutionSubphase == ExecutionSubphase.Preflight
            && !hasActiveWork;
        var isRevisionIntake = phaseState.Phase == SurfacePhase.Execution
            && phaseState.ExecutionSubphase == ExecutionSubphase.Revision
            && phaseState.ResultSubphase == ResultSubphase.RevisionRequested
            && hasActiveWork
            && HasRevisionRuntime(snapshot.RuntimeState);
        var isResult = phaseState.Phase == SurfacePhase.Result
            && HasResultRuntime(snapshot.RuntimeState);

        return snapshot with
        {
            PhaseState = phaseState,
            IntentState = NormalizeIntentState(snapshot.IntentState, phaseState, hasActiveWork, hasActiveShift),
            IsExecutionPreflightActive = isPreflight,
            IsPreflightClarificationActive = isPreflight && snapshot.IsPreflightClarificationActive,
            IsResultAccepted = isResult && snapshot.IsResultAccepted,
            ExecutionRefinement = phaseState.Phase is SurfacePhase.Discussion or SurfacePhase.Execution
                ? snapshot.ExecutionRefinement
                : null,
            PreflightClarificationText = isPreflight ? snapshot.PreflightClarificationText ?? string.Empty : string.Empty,
            RevisionIntakeText = isRevisionIntake ? snapshot.RevisionIntakeText ?? string.Empty : string.Empty,
            RuntimeState = isResult || isRevisionIntake || (preserveLiveRuntimePhase && HasLiveExecutionRuntime(phaseState, snapshot.RuntimeState))
                ? snapshot.RuntimeState
                : null
        };
    }

    private static StepPhaseState NormalizePhaseState(
        StepPhaseState phaseState,
        ExecutionRuntimeState? runtimeState,
        bool hasActiveWork,
        bool preserveLiveRuntimePhase,
        bool hasActiveShift)
    {
        if (!hasActiveWork)
        {
            if (hasActiveShift)
            {
                // Preserve legitimate Preflight-before-MaterializeIntent: post-Accept
                // EnterActiveShiftPreflight saves Preflight + hasActiveShift=true +
                // hasActiveTask=false (task materializes only inside ConfirmPreflight).
                // Without this branch the normalizer wipes the Preflight back to
                // Discussion on the next BuildContext, so the pf-card never renders.
                if (phaseState.Phase == SurfacePhase.Execution && phaseState.ExecutionSubphase == ExecutionSubphase.Preflight)
                {
                    return phaseState with
                    {
                        DiscussionSubphase = DiscussionSubphase.None,
                        ResultSubphase = ResultSubphase.None,
                        HasActiveShift = true,
                        HasActiveTask = false,
                        HasReopenedContext = false
                    };
                }

                return BuildSafeActiveShiftDiscussionState(phaseState.IntentState, phaseState.HasClarification);
            }

            return phaseState.Phase == SurfacePhase.Execution && phaseState.ExecutionSubphase == ExecutionSubphase.Preflight
                ? phaseState with
                {
                    DiscussionSubphase = DiscussionSubphase.None,
                    ResultSubphase = ResultSubphase.None,
                    IntentState = NormalizeInactiveIntentState(phaseState.IntentState),
                    HasActiveShift = false,
                    HasActiveTask = false,
                    HasReopenedContext = false
                }
                : BuildSafeDiscussionState(phaseState.IntentState);
        }

        if (phaseState.Phase == SurfacePhase.Result && HasResultRuntime(runtimeState))
        {
            return StepPhaseMachine.ResumeResult() with
            {
                HasClarification = phaseState.HasClarification
            };
        }

        if (phaseState.Phase == SurfacePhase.Execution
            && phaseState.ExecutionSubphase == ExecutionSubphase.Running
            && runtimeState?.Session.State == ExecutionSessionState.InProgress)
        {
            return preserveLiveRuntimePhase
                ? StepPhaseMachine.ResumeWork() with
                {
                    HasClarification = phaseState.HasClarification
                }
                : StepPhaseMachine.ResumeInterrupted() with
            {
                HasClarification = phaseState.HasClarification
            };
        }

        if (phaseState.Phase == SurfacePhase.Execution
            && phaseState.ExecutionSubphase == ExecutionSubphase.Qc
            && runtimeState is not null
            && (runtimeState.Session.State == ExecutionSessionState.ResultProduced
                || runtimeState.Session.State == ExecutionSessionState.UnderReview))
        {
            return preserveLiveRuntimePhase
                ? new StepPhaseState(
                SurfacePhase.Execution,
                DiscussionSubphase.None,
                ExecutionSubphase.Qc,
                ResultSubphase.None,
                ContextIntentState.Validated,
                HasActiveShift: true,
                HasActiveTask: true,
                HasClarification: phaseState.HasClarification,
                HasReopenedContext: false)
                : StepPhaseMachine.ResumeInterrupted() with
                {
                    HasClarification = phaseState.HasClarification
                };
        }

        if (phaseState.Phase == SurfacePhase.Execution
            && phaseState.ExecutionSubphase == ExecutionSubphase.Revision
            && phaseState.ResultSubphase == ResultSubphase.None
            && runtimeState?.Session.State == ExecutionSessionState.InProgress)
        {
            return preserveLiveRuntimePhase
                ? new StepPhaseState(
                SurfacePhase.Execution,
                DiscussionSubphase.None,
                ExecutionSubphase.Revision,
                ResultSubphase.None,
                ContextIntentState.Validated,
                HasActiveShift: true,
                HasActiveTask: true,
                HasClarification: phaseState.HasClarification,
                HasReopenedContext: false)
                : StepPhaseMachine.ResumeInterrupted() with
                {
                    HasClarification = phaseState.HasClarification
                };
        }

        if (phaseState.Phase == SurfacePhase.Execution
            && phaseState.ExecutionSubphase == ExecutionSubphase.Revision
            && phaseState.ResultSubphase == ResultSubphase.RevisionRequested
            && HasRevisionRuntime(runtimeState))
        {
            return new StepPhaseState(
                SurfacePhase.Execution,
                DiscussionSubphase.None,
                ExecutionSubphase.Revision,
                ResultSubphase.RevisionRequested,
                ContextIntentState.Validated,
                HasActiveShift: true,
                HasActiveTask: true,
                HasClarification: phaseState.HasClarification,
                HasReopenedContext: false);
        }

        if (phaseState.Phase == SurfacePhase.Discussion
            && phaseState.DiscussionSubphase == DiscussionSubphase.Ready
            && phaseState.IntentState == ContextIntentState.ReadyForValidation)
        {
            return new StepPhaseState(
                SurfacePhase.Discussion,
                DiscussionSubphase.Ready,
                ExecutionSubphase.None,
                ResultSubphase.None,
                ContextIntentState.ReadyForValidation,
                HasActiveShift: true,
                HasActiveTask: true,
                HasClarification: phaseState.HasClarification,
                HasReopenedContext: true);
        }

        if (phaseState.Phase == SurfacePhase.Discussion)
        {
            return BuildSafeReopenedDiscussionState(phaseState.HasClarification);
        }

        if (phaseState.Phase == SurfacePhase.Execution
            && phaseState.ExecutionSubphase == ExecutionSubphase.Interrupted)
        {
            return StepPhaseMachine.ResumeInterrupted() with
            {
                HasClarification = phaseState.HasClarification
            };
        }

        // Running/Qc/Revision require a live runtime; if it's gone, the session was
        // abandoned mid-flow (crash/force-close). Keep the user in explicit
        // execution recovery instead of presenting it as ordinary discussion.
        // Preflight is ambiguous: legitimate Preflight before ConfirmPreflight has no
        // runtime AND no materialized active work; pathological Preflight (after
        // Confirm materialized the task but the runtime save never landed) has
        // runtime=null AND hasActiveWork=true. Only the latter gets recovered.
        var shouldRecover =
            phaseState.Phase == SurfacePhase.Execution
            && runtimeState is null
            && (phaseState.ExecutionSubphase is ExecutionSubphase.Running
                    or ExecutionSubphase.Qc
                    or ExecutionSubphase.Revision
                || (phaseState.ExecutionSubphase == ExecutionSubphase.Preflight && hasActiveWork));
        if (shouldRecover)
        {
            return StepPhaseMachine.ResumeInterrupted() with
            {
                HasClarification = phaseState.HasClarification
            };
        }

        return StepPhaseMachine.ResumeInterrupted() with
        {
            HasClarification = phaseState.HasClarification
        };
    }

    private static StepPhaseState BuildSafeDiscussionState(ContextIntentState intentState)
    {
        return StepPhaseMachine.ResumeDiscussion(NormalizeInactiveIntentState(intentState));
    }

    private static StepPhaseState BuildSafeActiveShiftDiscussionState(ContextIntentState intentState, bool hasClarification)
    {
        return StepPhaseMachine.ResumeActiveShiftDiscussion(NormalizeInactiveIntentState(intentState)) with
        {
            HasClarification = hasClarification
        };
    }

    private static StepPhaseState BuildSafeReopenedDiscussionState(bool hasClarification)
    {
        return new StepPhaseState(
            SurfacePhase.Discussion,
            DiscussionSubphase.Reopened,
            ExecutionSubphase.None,
            ResultSubphase.None,
            ContextIntentState.Refining,
            HasActiveShift: true,
            HasActiveTask: true,
            HasClarification: hasClarification,
            HasReopenedContext: true);
    }

    private static ContextIntentState NormalizeIntentState(
        ContextIntentState intentState,
        StepPhaseState phaseState,
        bool hasActiveWork,
        bool hasActiveShift)
    {
        if (phaseState.Phase == SurfacePhase.Discussion)
        {
            if (hasActiveWork)
            {
                return phaseState.DiscussionSubphase == DiscussionSubphase.Ready
                    ? ContextIntentState.ReadyForValidation
                    : ContextIntentState.Refining;
            }

            if (hasActiveShift)
            {
                return NormalizeInactiveIntentState(intentState);
            }

            return NormalizeInactiveIntentState(intentState);
        }

        if (phaseState.Phase == SurfacePhase.Execution && phaseState.ExecutionSubphase == ExecutionSubphase.Preflight)
        {
            return ContextIntentState.ReadyForValidation;
        }

        if (phaseState.Phase is SurfacePhase.Execution or SurfacePhase.Result)
        {
            return ContextIntentState.Validated;
        }

        return ContextIntentState.None;
    }

    private static ContextIntentState NormalizeInactiveIntentState(ContextIntentState intentState)
    {
        return intentState switch
        {
            ContextIntentState.Orientation => ContextIntentState.Orientation,
            ContextIntentState.Candidate => ContextIntentState.Candidate,
            ContextIntentState.Refining => ContextIntentState.Refining,
            ContextIntentState.ReadyForValidation => ContextIntentState.ReadyForValidation,
            _ => ContextIntentState.None
        };
    }

    private static bool HasResultRuntime(ExecutionRuntimeState? runtimeState)
    {
        return runtimeState is not null
            && runtimeState.Session.State == ExecutionSessionState.Completed;
    }

    private static bool HasRevisionRuntime(ExecutionRuntimeState? runtimeState)
    {
        // InProgress is valid in revision context: both A.2 QC-REVISE path
        // (RejectQcReview → RestartRevision) and legacy RequestRevisionAsync
        // path (RestartCompletedResultForRevision) leave the session at
        // InProgress while phase stays on Revision/RevisionRequested, because
        // the worker is ready to run as soon as the user submits revision
        // intake. Rejecting this combo sends the normalizer to ResumeInterrupted
        // and softlocks the composer.
        return runtimeState is not null
            && (runtimeState.Session.State == ExecutionSessionState.Completed
                || runtimeState.Session.State == ExecutionSessionState.UnderReview
                || runtimeState.Session.State == ExecutionSessionState.ReturnedForRevision
                || runtimeState.Session.State == ExecutionSessionState.InProgress);
    }

    private static bool HasLiveExecutionRuntime(StepPhaseState phaseState, ExecutionRuntimeState? runtimeState)
    {
        return runtimeState is not null
            && phaseState.Phase == SurfacePhase.Execution
            && phaseState.ExecutionSubphase is ExecutionSubphase.Running or ExecutionSubphase.Qc or ExecutionSubphase.Revision;
    }
}
