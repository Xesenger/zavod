using System;
using System.Collections.Generic;
using System.Linq;

namespace zavod.Contexting;

public static class CapsuleBuilder
{
    public static Capsule Build(CapsuleSourceInput input)
    {
        ArgumentNullException.ThrowIfNull(input);

        Require(!string.IsNullOrWhiteSpace(input.ProjectIdentity), "capsule", "project identity", "Project identity is required.");
        Require(!string.IsNullOrWhiteSpace(input.CurrentDirection), "capsule", "current direction", "Current direction is required.");
        Require(!string.IsNullOrWhiteSpace(input.CurrentRoadmapPhase), "capsule", "current roadmap phase", "Current roadmap phase is required.");

        var canonRules = NormalizeLines(input.CoreCanonRules);
        var activeConstraints = NormalizeLines(input.ActiveConstraints);
        var knownRisks = NormalizeLines(input.KnownRisks);
        var currentFocus = NormalizeLines(input.CurrentFocus);

        Require(canonRules.Count > 0, "capsule", "core canon rules", "At least one canon rule is required.");
        Require(activeConstraints.Count > 0, "capsule", "active constraints", "At least one active constraint is required.");
        Require(currentFocus.Count > 0, "capsule", "current focus", "At least one current focus item is required.");

        return new Capsule(
            input.ProjectIdentity.Trim(),
            input.CurrentDirection.Trim(),
            input.CurrentRoadmapPhase.Trim(),
            canonRules,
            activeConstraints,
            knownRisks,
            knownRisks.Count > 0,
            currentFocus);
    }

    private static IReadOnlyList<string> NormalizeLines(IReadOnlyList<string> lines)
    {
        return lines
            .Where(static line => !string.IsNullOrWhiteSpace(line))
            .Select(static line => line.Trim())
            .Distinct(StringComparer.Ordinal)
            .OrderBy(static line => line, StringComparer.Ordinal)
            .ToArray();
    }

    private static void Require(bool condition, string area, string missingRequirement, string reason)
    {
        if (!condition)
        {
            throw new ContextBuildException(area, missingRequirement, reason);
        }
    }
}
