using System;

namespace zavod.Execution;

public static class ShiftUpdateApplier
{
    public static ShiftUpdateResult Apply(ShiftUpdateCandidate candidate)
    {
        ArgumentNullException.ThrowIfNull(candidate);

        var status = candidate switch
        {
            { HasRejectedOutcome: true } => ShiftUpdateStatus.Rejected,
            { IsEligibleToClose: true } => ShiftUpdateStatus.WouldClose,
            { ShouldKeepShiftOpen: true } => ShiftUpdateStatus.WouldKeepOpen,
            _ => ShiftUpdateStatus.NoChange
        };

        return new ShiftUpdateResult(
            candidate.Target,
            candidate.OutcomeStatus,
            candidate.Message,
            status);
    }
}
