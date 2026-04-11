using System;

namespace zavod.State;

public sealed record IntentData;

public static class CheckpointDetectorV1
{
    public static bool IsCheckpoint(
        TaskState completedTask,
        IntentData? nextIntent,
        DecisionSignal? decisionSignal)
    {
        ArgumentNullException.ThrowIfNull(completedTask);

        if (completedTask.Status != TaskStateStatus.Completed)
        {
            return false;
        }

        return decisionSignal is
        {
            Exists: true,
            AffectsStructureOrDirection: true
        };
    }
}
