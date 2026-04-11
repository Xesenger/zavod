using System;
using System.Collections.Generic;
using System.Linq;

namespace zavod.Flow;

public static class StepPhaseProjectionBuilder
{
    public static StepPhaseProjection Build(StepPhaseState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        var actions = BuildAllowedActions(state);
        var actionKeys = actions.Select(ToActionKey).ToArray();

        return new StepPhaseProjection(
            state.Phase,
            state.DiscussionSubphase,
            state.ExecutionSubphase,
            state.ResultSubphase,
            ShowChat: true,
            ShowExecution: state.Phase is SurfacePhase.Execution or SurfacePhase.Result,
            ShowResult: state.Phase == SurfacePhase.Result || state.ResultSubphase == ResultSubphase.RevisionRequested,
            CanSendChat: actions.Contains(StepPhaseAction.SendChat),
            CanStartIntentValidation: actions.Contains(StepPhaseAction.StartIntentValidation),
            CanConfirmPreflight: actions.Contains(StepPhaseAction.ConfirmPreflight),
            CanClarifyPreflight: actions.Contains(StepPhaseAction.ClarifyPreflight),
            CanCancelExecution: actions.Contains(StepPhaseAction.CancelExecution),
            CanResumeExecution: actions.Contains(StepPhaseAction.ResumeExecution),
            CanAcceptResult: actions.Contains(StepPhaseAction.AcceptResult),
            CanReturnForRevision: actions.Contains(StepPhaseAction.ReturnForRevision),
            CanReturnToLead: actions.Contains(StepPhaseAction.ReturnToLead),
            StatusTextKey: GetStatusTextKey(state),
            PrimaryHintKey: GetPrimaryHintKey(state),
            AllowedActionKeys: actionKeys,
            AllowedActions: actions);
    }

    private static IReadOnlyList<StepPhaseAction> BuildAllowedActions(StepPhaseState state)
    {
        return state.Phase switch
        {
            SurfacePhase.Discussion => state.DiscussionSubphase switch
            {
                DiscussionSubphase.Idle or DiscussionSubphase.Forming or DiscussionSubphase.Reopened => new[]
                {
                    StepPhaseAction.SendChat
                },
                DiscussionSubphase.Ready => new[]
                {
                    StepPhaseAction.SendChat,
                    StepPhaseAction.StartIntentValidation
                },
                _ => Array.Empty<StepPhaseAction>()
            },
            SurfacePhase.Execution => state.ExecutionSubphase switch
            {
                ExecutionSubphase.Preflight => new[]
                {
                    StepPhaseAction.ConfirmPreflight,
                    StepPhaseAction.ClarifyPreflight,
                    StepPhaseAction.BackFromPreflight
                },
                ExecutionSubphase.Running => new[]
                {
                    StepPhaseAction.MoveToQc,
                    StepPhaseAction.CancelExecution
                },
                ExecutionSubphase.Qc => new[]
                {
                    StepPhaseAction.MoveToQc,
                    StepPhaseAction.CancelExecution
                },
                ExecutionSubphase.Revision => new[]
                {
                    StepPhaseAction.MoveToQc,
                    StepPhaseAction.CancelExecution
                },
                ExecutionSubphase.Interrupted => new[]
                {
                    StepPhaseAction.ResumeExecution
                },
                _ => Array.Empty<StepPhaseAction>()
            },
            SurfacePhase.Result => state.ResultSubphase == ResultSubphase.Ready
                ? new[]
                {
                    StepPhaseAction.AcceptResult,
                    StepPhaseAction.ReturnForRevision,
                    StepPhaseAction.ReturnToLead,
                    StepPhaseAction.ConfirmCompletion
                }
                : Array.Empty<StepPhaseAction>(),
            SurfacePhase.Completed => Array.Empty<StepPhaseAction>(),
            _ => Array.Empty<StepPhaseAction>()
        };
    }

    private static string GetStatusTextKey(StepPhaseState state)
    {
        return state.Phase switch
        {
            SurfacePhase.Discussion => state.DiscussionSubphase switch
            {
                DiscussionSubphase.Idle => "FlowText.Status.DiscussionIdle",
                DiscussionSubphase.Forming => "FlowText.Status.DiscussionForming",
                DiscussionSubphase.Ready => "FlowText.Status.DiscussionReady",
                DiscussionSubphase.Reopened => "FlowText.Status.DiscussionReopened",
                _ => "FlowText.Status.DiscussionIdle"
            },
            SurfacePhase.Execution => state.ExecutionSubphase switch
            {
                ExecutionSubphase.Preflight when state.HasClarification => "FlowText.Status.ExecutionPreflightClarified",
                ExecutionSubphase.Preflight => "FlowText.Status.ExecutionPreflight",
                ExecutionSubphase.Running => "FlowText.Status.ExecutionRunning",
                ExecutionSubphase.Qc => "FlowText.Status.ExecutionQc",
                ExecutionSubphase.Revision => "FlowText.Status.ExecutionRevisionRequested",
                ExecutionSubphase.Interrupted => "FlowText.Status.ExecutionInterrupted",
                _ => "FlowText.Status.ExecutionRunning"
            },
            SurfacePhase.Result => state.ResultSubphase switch
            {
                ResultSubphase.Ready => "FlowText.Status.ResultReady",
                ResultSubphase.RevisionRequested => "FlowText.Status.ResultRevisionRequested",
                _ => "FlowText.Status.ResultReady"
            },
            SurfacePhase.Completed => "FlowText.Status.CompletedCleanDiscussion",
            _ => "FlowText.Status.DiscussionIdle"
        };
    }

    private static string GetPrimaryHintKey(StepPhaseState state)
    {
        return state.Phase switch
        {
            SurfacePhase.Discussion => state.DiscussionSubphase switch
            {
                DiscussionSubphase.Idle => "FlowText.Hint.DiscussionIdle",
                DiscussionSubphase.Forming => "FlowText.Hint.DiscussionForming",
                DiscussionSubphase.Ready => "FlowText.Hint.DiscussionReady",
                DiscussionSubphase.Reopened => "FlowText.Hint.DiscussionReopened",
                _ => "FlowText.Hint.DiscussionIdle"
            },
            SurfacePhase.Execution => state.ExecutionSubphase switch
            {
                ExecutionSubphase.Preflight when state.HasClarification => "FlowText.Hint.ExecutionPreflightClarified",
                ExecutionSubphase.Preflight => "FlowText.Hint.ExecutionPreflight",
                ExecutionSubphase.Running => "FlowText.Hint.ExecutionRunning",
                ExecutionSubphase.Qc => "FlowText.Hint.ExecutionQc",
                ExecutionSubphase.Revision => "FlowText.Hint.ExecutionRevisionRequested",
                ExecutionSubphase.Interrupted => "FlowText.Hint.ExecutionInterrupted",
                _ => "FlowText.Hint.ExecutionRunning"
            },
            SurfacePhase.Result => state.ResultSubphase switch
            {
                ResultSubphase.Ready => "FlowText.Hint.ResultReady",
                ResultSubphase.RevisionRequested => "FlowText.Hint.ResultRevisionRequested",
                _ => "FlowText.Hint.ResultReady"
            },
            SurfacePhase.Completed => "FlowText.Hint.CompletedCleanDiscussion",
            _ => "FlowText.Hint.DiscussionIdle"
        };
    }

    private static string ToActionKey(StepPhaseAction action)
    {
        return action switch
        {
            StepPhaseAction.SendChat => "FlowText.Action.SendChat",
            StepPhaseAction.StartIntentValidation => "FlowText.Action.StartIntentValidation",
            StepPhaseAction.ConfirmPreflight => "FlowText.Action.ConfirmPreflight",
            StepPhaseAction.ClarifyPreflight => "FlowText.Action.ClarifyPreflight",
            StepPhaseAction.BackFromPreflight => "FlowText.Action.BackFromPreflight",
            StepPhaseAction.CancelExecution => "FlowText.Action.CancelExecution",
            StepPhaseAction.ResumeExecution => "FlowText.Action.ResumeExecution",
            StepPhaseAction.MoveToQc => "FlowText.Action.MoveToQc",
            StepPhaseAction.AcceptResult => "FlowText.Action.AcceptResult",
            StepPhaseAction.ReturnForRevision => "FlowText.Action.ReturnForRevision",
            StepPhaseAction.ReturnToLead => "FlowText.Action.ReturnToLead",
            StepPhaseAction.ConfirmCompletion => "FlowText.Action.ConfirmCompletion",
            _ => throw new InvalidOperationException($"Unsupported action '{action}'.")
        };
    }
}
