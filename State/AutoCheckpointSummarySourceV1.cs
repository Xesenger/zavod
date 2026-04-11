using System;
using zavod.Boundary;

namespace zavod.State;

public static class AutoCheckpointSummarySourceV1
{
    public static string? TryBuildSummary(AcceptedResult acceptedResult)
    {
        ArgumentNullException.ThrowIfNull(acceptedResult);

        return string.IsNullOrWhiteSpace(acceptedResult.CheckpointSummary)
            ? null
            : acceptedResult.CheckpointSummary;
    }
}
