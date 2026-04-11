using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace zavod.Acceptance;

public static class AcceptanceGuard
{
    public static AcceptanceDecision Evaluate(AcceptanceEvidence evidence)
    {
        ArgumentNullException.ThrowIfNull(evidence);

        var workspaceRoot = evidence.WorkspaceObservation.WorkspaceRoot;
        var conflicts = new List<AcceptanceConflict>();
        var nonOverlappingChanges = new List<string>();
        var reasons = new List<string>();

        if (evidence.WorkspaceObservation.Health is not zavod.Workspace.WorkspaceHealthStatus.Healthy)
        {
            reasons.Add($"Workspace health is {evidence.WorkspaceObservation.Health}.");
            return BuildBlockedDecision(
                AcceptanceClassification.InvalidBase,
                conflicts,
                nonOverlappingChanges,
                reasons);
        }

        foreach (var touchedFile in evidence.ExecutionBase.Files)
        {
            var currentFullPath = Path.GetFullPath(Path.Combine(workspaceRoot, touchedFile.RelativePath));
            if (!currentFullPath.StartsWith(workspaceRoot, StringComparison.OrdinalIgnoreCase))
            {
                conflicts.Add(new AcceptanceConflict(touchedFile.RelativePath, "Touched path escaped workspace root."));
                reasons.Add("A touched path escaped the workspace root.");
                continue;
            }

            if (!File.Exists(currentFullPath))
            {
                conflicts.Add(new AcceptanceConflict(touchedFile.RelativePath, "Touched file is missing in current workspace."));
                reasons.Add($"Touched file '{touchedFile.RelativePath}' is missing.");
                continue;
            }

            var currentInfo = new FileInfo(currentFullPath);
            var fileChanged =
                currentInfo.Length != touchedFile.Length ||
                currentInfo.LastWriteTimeUtc.Ticks != touchedFile.LastWriteTimeUtcTicks;

            if (fileChanged)
            {
                conflicts.Add(new AcceptanceConflict(touchedFile.RelativePath, "Touched file changed since execution base was captured."));
                reasons.Add($"Touched file '{touchedFile.RelativePath}' changed after execution started.");
            }
        }

        if (conflicts.Count > 0)
        {
            return BuildBlockedDecision(
                AcceptanceClassification.Conflict,
                conflicts,
                nonOverlappingChanges,
                reasons);
        }

        if (evidence.WorkspaceObservation.DriftStatus == zavod.Workspace.WorkspaceDriftStatus.Drifted)
        {
            reasons.Add("Workspace drift exists outside the touched scope.");
            nonOverlappingChanges.Add("Workspace drift detected outside touched scope.");
            return new AcceptanceDecision(
                AcceptanceClassification.MergeRequired,
                AcceptanceDecisionStatus.Blocked,
                conflicts,
                nonOverlappingChanges,
                new AcceptanceReasonSummary(
                    "Workspace drift exists outside touched scope; manual merge/review is required in v1.",
                    reasons));
        }

        reasons.Add("No touched-file conflicts were detected against the current workspace.");
        return new AcceptanceDecision(
            AcceptanceClassification.SafeApply,
            AcceptanceDecisionStatus.Allowed,
            conflicts,
            nonOverlappingChanges,
            new AcceptanceReasonSummary(
                "Safe apply is allowed for the current touched scope.",
                reasons));
    }

    private static AcceptanceDecision BuildBlockedDecision(
        AcceptanceClassification classification,
        IReadOnlyList<AcceptanceConflict> conflicts,
        IReadOnlyList<string> nonOverlappingChanges,
        IReadOnlyList<string> reasons)
    {
        return new AcceptanceDecision(
            classification,
            AcceptanceDecisionStatus.Blocked,
            conflicts,
            nonOverlappingChanges,
            new AcceptanceReasonSummary(
                $"Acceptance is blocked with classification {classification}.",
                reasons));
    }
}
