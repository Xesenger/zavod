using System;

namespace zavod.Execution;

public static class ShiftUpdateDecisionMaker
{
    public static ShiftUpdateDecision Decide(ShiftUpdateResult result)
    {
        ArgumentNullException.ThrowIfNull(result);

        return result.Status switch
        {
            ShiftUpdateStatus.WouldClose => new ShiftUpdateDecision(
                result.Target,
                result.OutcomeStatus,
                ShiftUpdateDecisionStatus.AllowApply,
                "Dry apply indicates the shift update would close and may proceed to apply."),

            ShiftUpdateStatus.WouldKeepOpen => new ShiftUpdateDecision(
                result.Target,
                result.OutcomeStatus,
                ShiftUpdateDecisionStatus.AllowApply,
                "Dry apply indicates the shift should remain open and may proceed to apply."),

            ShiftUpdateStatus.Rejected => new ShiftUpdateDecision(
                result.Target,
                result.OutcomeStatus,
                ShiftUpdateDecisionStatus.DenyApply,
                "Rejected dry apply result must not proceed to mutation."),

            ShiftUpdateStatus.NoChange => new ShiftUpdateDecision(
                result.Target,
                result.OutcomeStatus,
                ShiftUpdateDecisionStatus.DenyApply,
                "No-change dry apply result does not justify mutation."),

            _ => throw new ArgumentOutOfRangeException(nameof(result), result.Status, "Unsupported shift update status.")
        };
    }
}
