using System.Collections.Generic;

namespace zavod.Contexting;

public sealed record Capsule(
    string ProjectIdentity,
    string CurrentDirection,
    string CurrentRoadmapPhase,
    IReadOnlyList<string> CoreCanonRules,
    IReadOnlyList<string> ActiveConstraints,
    IReadOnlyList<string> KnownRisks,
    bool HasKnownRisks,
    IReadOnlyList<string> CurrentFocus);

public sealed record CapsuleSourceInput(
    string ProjectIdentity,
    string CurrentDirection,
    string CurrentRoadmapPhase,
    IReadOnlyList<string> CoreCanonRules,
    IReadOnlyList<string> ActiveConstraints,
    IReadOnlyList<string> KnownRisks,
    IReadOnlyList<string> CurrentFocus);
