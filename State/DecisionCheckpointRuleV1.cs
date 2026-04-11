using System;
using zavod.Boundary;

namespace zavod.State;

public static class DecisionCheckpointRuleV1
{
    public static bool IsStructuralOrDirectionalDecision(AcceptedResult acceptedResult)
    {
        ArgumentNullException.ThrowIfNull(acceptedResult);

        return acceptedResult.DecisionAffectsStructureOrDirection;
    }
}
