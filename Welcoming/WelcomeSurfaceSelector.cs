using System;
using System.Collections.Generic;
using zavod.Persistence;

namespace zavod.Welcoming;

// Deterministic state -> action selector per project_welcome_surface_v1.md.
//
// Pure function. Same input -> same output. No LLM call. No IO.
//
// Selection rules R1..R5 are mutually exclusive and checked in priority order.
// R6 (stale overlay) is additive when HasStaleSections is true.
//
// Output is bounded to 2..4 actions. When a rule produces fewer than 2,
// ReviewProjectAudit is appended as a safe default.
public static class WelcomeSurfaceSelector
{
    private const int MaxActions = 4;
    private const int MinActions = 2;

    public static WelcomeActionSet Select(WelcomeStateInput input)
    {
        ArgumentNullException.ThrowIfNull(input);
        if (input.DocumentSelection is null)
        {
            throw new WelcomingException("WelcomeStateInput.DocumentSelection must not be null.");
        }

        var statuses = ClassifyDocuments(input.DocumentSelection);
        var canonicalCount = CountStage(statuses, ProjectDocumentStage.CanonicalDocs);
        var previewCount = CountAtLeastStage(statuses, ProjectDocumentStage.PreviewDocs);

        var rule = DetermineRule(input, canonicalCount, previewCount);
        var primary = BuildPrimaryActions(rule, statuses, input.HasThinMemoryModeConfirmed);

        // R6 overlay: stale sections add review_stale_sections to the set
        // (still subject to the 4-action cap; stale wins over lowest-priority entry).
        var withOverlay = ApplyStaleOverlay(primary, input.HasStaleSections);

        // Safe default: if primary produces fewer than 2, append ReviewProjectAudit.
        var padded = PadToMinimum(withOverlay);

        // Final cap at MaxActions.
        var capped = Cap(padded, MaxActions);

        return new WelcomeActionSet(rule, capped, input.HasStaleSections);
    }

    private static Dictionary<ProjectDocumentKind, ProjectDocumentStage?> ClassifyDocuments(
        ProjectDocumentSourceSelection selection)
    {
        var result = new Dictionary<ProjectDocumentKind, ProjectDocumentStage?>
        {
            [ProjectDocumentKind.Project] = ResolveStage(selection.ProjectDocument),
            [ProjectDocumentKind.Direction] = ResolveStage(selection.DirectionDocument),
            [ProjectDocumentKind.Roadmap] = ResolveStage(selection.RoadmapDocument),
            [ProjectDocumentKind.Canon] = ResolveStage(selection.CanonDocument),
            [ProjectDocumentKind.Capsule] = ResolveStage(selection.CapsuleDocument)
        };
        return result;
    }

    private static ProjectDocumentStage? ResolveStage(ProjectDocumentSourceDescriptor? descriptor)
    {
        if (descriptor is null)
        {
            return null;
        }

        return descriptor.Exists ? descriptor.Stage : null;
    }

    private static int CountStage(
        Dictionary<ProjectDocumentKind, ProjectDocumentStage?> statuses,
        ProjectDocumentStage stage)
    {
        var count = 0;
        foreach (var entry in statuses.Values)
        {
            if (entry == stage)
            {
                count++;
            }
        }
        return count;
    }

    private static int CountAtLeastStage(
        Dictionary<ProjectDocumentKind, ProjectDocumentStage?> statuses,
        ProjectDocumentStage minimum)
    {
        var count = 0;
        foreach (var entry in statuses.Values)
        {
            if (entry.HasValue && entry.Value >= minimum)
            {
                count++;
            }
        }
        return count;
    }

    private static WelcomeSelectionRule DetermineRule(
        WelcomeStateInput input,
        int canonicalCount,
        int previewCount)
    {
        if (input.HasActiveShift || input.HasActiveTask)
        {
            return WelcomeSelectionRule.R1_ActiveShiftOrTask;
        }

        if (canonicalCount == 5)
        {
            return WelcomeSelectionRule.R2_Canonical_5_of_5;
        }

        if (canonicalCount >= 1 && canonicalCount <= 4)
        {
            return WelcomeSelectionRule.R3_Canonical_Partial;
        }

        // canonicalCount == 0 from here
        if (previewCount >= 1)
        {
            return WelcomeSelectionRule.R4_Canonical_Zero_PreviewPresent;
        }

        return WelcomeSelectionRule.R5_Canonical_Zero_PreviewZero;
    }

    private static List<WelcomeAction> BuildPrimaryActions(
        WelcomeSelectionRule rule,
        Dictionary<ProjectDocumentKind, ProjectDocumentStage?> statuses,
        bool hasThinMemoryModeConfirmed)
    {
        return rule switch
        {
            WelcomeSelectionRule.R1_ActiveShiftOrTask => BuildR1(statuses),
            WelcomeSelectionRule.R2_Canonical_5_of_5 => new List<WelcomeAction>
            {
                WelcomeAction.StartWorkCycle,
                WelcomeAction.OpenRoadmap,
                WelcomeAction.OpenDirection
            },
            WelcomeSelectionRule.R3_Canonical_Partial => BuildR3(hasThinMemoryModeConfirmed),
            WelcomeSelectionRule.R4_Canonical_Zero_PreviewPresent => new List<WelcomeAction>
            {
                WelcomeAction.ReviewPreviewDocs,
                WelcomeAction.StartWorkCycle,
                WelcomeAction.PromotePreviewToCanonical,
                WelcomeAction.ReviewProjectAudit
            },
            WelcomeSelectionRule.R5_Canonical_Zero_PreviewZero => new List<WelcomeAction>
            {
                WelcomeAction.ImportRetry,
                WelcomeAction.AuthorCanonicalDoc,
                WelcomeAction.ReviewProjectAudit
            },
            _ => throw new WelcomingException($"Unhandled WelcomeSelectionRule: {rule}.")
        };
    }

    private static List<WelcomeAction> BuildR3(bool hasThinMemoryModeConfirmed)
    {
        var actions = new List<WelcomeAction>
        {
            WelcomeAction.PromotePreviewToCanonical,
            WelcomeAction.AuthorCanonicalDoc
        };
        if (hasThinMemoryModeConfirmed)
        {
            actions.Add(WelcomeAction.StartWorkCycle);
        }
        return actions;
    }

    // R1 defensively omits OpenRoadmap when the roadmap doc is entirely absent.
    // Canon forbids offering actions against material the system does not have.
    private static List<WelcomeAction> BuildR1(
        Dictionary<ProjectDocumentKind, ProjectDocumentStage?> statuses)
    {
        var actions = new List<WelcomeAction> { WelcomeAction.ContinueWorkCycle };
        if (statuses[ProjectDocumentKind.Roadmap].HasValue)
        {
            actions.Add(WelcomeAction.OpenRoadmap);
        }
        return actions;
    }

    private static List<WelcomeAction> ApplyStaleOverlay(List<WelcomeAction> primary, bool hasStale)
    {
        if (!hasStale)
        {
            return primary;
        }

        // Overlay is additive. Append if not already present.
        if (!primary.Contains(WelcomeAction.ReviewStaleSections))
        {
            primary.Add(WelcomeAction.ReviewStaleSections);
        }
        return primary;
    }

    private static List<WelcomeAction> PadToMinimum(List<WelcomeAction> actions)
    {
        if (actions.Count >= MinActions)
        {
            return actions;
        }

        if (!actions.Contains(WelcomeAction.ReviewProjectAudit))
        {
            actions.Add(WelcomeAction.ReviewProjectAudit);
        }
        return actions;
    }

    private static IReadOnlyList<WelcomeAction> Cap(List<WelcomeAction> actions, int max)
    {
        if (actions.Count <= max)
        {
            return actions.AsReadOnly();
        }

        var capped = new List<WelcomeAction>(max);
        for (var i = 0; i < max; i++)
        {
            capped.Add(actions[i]);
        }
        return capped.AsReadOnly();
    }
}
