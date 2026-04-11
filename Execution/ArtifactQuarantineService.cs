using System;

namespace zavod.Execution;

public sealed record ArtifactQuarantineService(
    bool Enabled,
    bool PromotionRequiresReview,
    bool SuspiciousArtifactsStayOutOfTruth,
    string Summary)
{
    public ArtifactQuarantineService Normalize()
    {
        return this with { Summary = Summary.Trim() };
    }

    public void Validate()
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(Summary);
    }
}
