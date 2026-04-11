using System.Collections.Generic;
using zavod.Contexting;

namespace zavod.Flow;

public enum SurfacePhase
{
    Discussion,
    Execution,
    Result,
    Completed
}

public enum DiscussionSubphase
{
    None,
    Idle,
    Forming,
    Ready,
    Reopened
}

public enum ExecutionSubphase
{
    None,
    Preflight,
    Running,
    Qc,
    Revision,
    Interrupted
}

public enum ResultSubphase
{
    None,
    Ready,
    RevisionRequested
}

public enum StepPhaseAction
{
    SendChat,
    StartIntentValidation,
    ConfirmPreflight,
    ClarifyPreflight,
    BackFromPreflight,
    CancelExecution,
    ResumeExecution,
    MoveToQc,
    AcceptResult,
    ReturnForRevision,
    ReturnToLead,
    ConfirmCompletion
}

public sealed record StepPhaseState(
    SurfacePhase Phase,
    DiscussionSubphase DiscussionSubphase,
    ExecutionSubphase ExecutionSubphase,
    ResultSubphase ResultSubphase,
    ContextIntentState IntentState,
    bool HasActiveShift,
    bool HasActiveTask,
    bool HasClarification,
    bool HasReopenedContext)
{
    public static StepPhaseState DiscussionIdle() =>
        new(
            SurfacePhase.Discussion,
            DiscussionSubphase.Idle,
            ExecutionSubphase.None,
            ResultSubphase.None,
            ContextIntentState.None,
            HasActiveShift: false,
            HasActiveTask: false,
            HasClarification: false,
            HasReopenedContext: false);
}

public sealed record StepPhaseProjection(
    SurfacePhase Phase,
    DiscussionSubphase DiscussionSubphase,
    ExecutionSubphase ExecutionSubphase,
    ResultSubphase ResultSubphase,
    bool ShowChat,
    bool ShowExecution,
    bool ShowResult,
    bool CanSendChat,
    bool CanStartIntentValidation,
    bool CanConfirmPreflight,
    bool CanClarifyPreflight,
    bool CanCancelExecution,
    bool CanResumeExecution,
    bool CanAcceptResult,
    bool CanReturnForRevision,
    bool CanReturnToLead,
    string StatusTextKey,
    string PrimaryHintKey,
    IReadOnlyList<string> AllowedActionKeys,
    IReadOnlyList<StepPhaseAction> AllowedActions);
