using zavod.Contexting;
using zavod.Flow;

namespace zavod.Demo;

public static class DemoDiscussionPhaseSeed
{
    public static StepPhaseState BuildContinuation(bool isReadyForValidation)
    {
        var discussion = StepPhaseMachine.ResumeActiveShiftDiscussion();
        return isReadyForValidation
            ? StepPhaseMachine.RecordIntent(discussion, ContextIntentState.ReadyForValidation)
            : discussion;
    }
}
