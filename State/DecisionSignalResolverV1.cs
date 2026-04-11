using System;
using zavod.Boundary;

namespace zavod.State;

public static class DecisionSignalResolverV1
{
    public static DecisionSignal? TryResolve(AcceptedResult acceptedResult)
    {
        ArgumentNullException.ThrowIfNull(acceptedResult);

        if (!DecisionCheckpointRuleV1.IsStructuralOrDirectionalDecision(acceptedResult))
        {
            return null;
        }

        return new DecisionSignal(
            Exists: true,
            AffectsStructureOrDirection: true);
    }
}
