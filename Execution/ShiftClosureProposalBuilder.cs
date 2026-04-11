using System;

namespace zavod.Execution;

public static class ShiftClosureProposalBuilder
{
    public static ShiftClosureProposal Build(ExecutionClosureCandidate candidate)
    {
        ArgumentNullException.ThrowIfNull(candidate);

        var effect = candidate switch
        {
            { RequiresFollowup: true } => ProposedShiftEffect.KeepOpen,
            { IsRejected: true } => ProposedShiftEffect.KeepOpen,
            { IsClosable: true } => ProposedShiftEffect.EligibleToClose,
            _ => ProposedShiftEffect.None
        };

        return new ShiftClosureProposal(
            candidate.Target,
            candidate.OutcomeStatus,
            candidate.Message,
            effect,
            candidate.RequiresFollowup,
            candidate.IsClosable);
    }
}
