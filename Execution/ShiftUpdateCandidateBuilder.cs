using System;

namespace zavod.Execution;

public static class ShiftUpdateCandidateBuilder
{
    public static ShiftUpdateCandidate Build(ShiftClosureProposal proposal)
    {
        ArgumentNullException.ThrowIfNull(proposal);

        return new ShiftUpdateCandidate(
            proposal.Target,
            proposal.OutcomeStatus,
            proposal.Message,
            proposal.ProposedShiftEffect,
            ShouldKeepShiftOpen: proposal.ProposedShiftEffect == ProposedShiftEffect.KeepOpen,
            IsEligibleToClose: proposal.ProposedShiftEffect == ProposedShiftEffect.EligibleToClose,
            HasRejectedOutcome: proposal.OutcomeStatus == Outcome.ExecutionOutcomeStatus.Rejected);
    }
}
