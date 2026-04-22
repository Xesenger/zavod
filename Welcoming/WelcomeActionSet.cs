using System.Collections.Generic;

namespace zavod.Welcoming;

// Output of WelcomeSurfaceSelector. Always 2-4 actions per canon.
// Rule tag identifies which selection rule (R1..R5) produced the primary set.
public sealed record WelcomeActionSet(
    WelcomeSelectionRule PrimaryRule,
    IReadOnlyList<WelcomeAction> Actions,
    bool StaleOverlayApplied);

public enum WelcomeSelectionRule
{
    R1_ActiveShiftOrTask = 1,
    R2_Canonical_5_of_5 = 2,
    R3_Canonical_Partial = 3,
    R4_Canonical_Zero_PreviewPresent = 4,
    R5_Canonical_Zero_PreviewZero = 5
}
