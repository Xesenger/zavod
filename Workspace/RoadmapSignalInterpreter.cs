using System;
using System.Collections.Generic;
using System.Linq;

namespace zavod.Workspace;

public static class RoadmapSignalInterpreter
{
    private static readonly string[] PhaseMarkers = { "phase", "milestone", "release", "feat:", "feature:", "v" };

    public static RoadmapSignalInterpretation Interpret(GitRoadmapHistory history, WorkspaceImportMaterialInterpreterRunResult runResult)
    {
        ArgumentNullException.ThrowIfNull(history);
        ArgumentNullException.ThrowIfNull(runResult);

        if (!history.IsGitRepository || !history.IsReadable || history.Commits.Count == 0)
        {
            return new RoadmapSignalInterpretation(
                Array.Empty<RoadmapCandidatePhase>(),
                BuildUnknowns(history),
                HasCandidateEvidence: false);
        }

        var candidates = new List<RoadmapCandidatePhase>();
        foreach (var commit in history.Commits.Where(IsCandidateCommit).Take(8))
        {
            candidates.Add(new RoadmapCandidatePhase(
                BuildCommitCandidateLabel(commit.Subject),
                $"commit {commit.ShortSha}"));
        }

        foreach (var tag in history.Tags.Take(5))
        {
            candidates.Add(new RoadmapCandidatePhase(
                "Release/tag marker may indicate a roadmap phase boundary.",
                $"tag {tag}"));
        }

        foreach (var branch in history.Branches.Where(IsCandidateBranch).Take(5))
        {
            candidates.Add(new RoadmapCandidatePhase(
                "Branch marker may indicate active or past roadmap work.",
                $"branch {branch}"));
        }

        var roadmapMaterial = (runResult.Interpretation.Materials ?? Array.Empty<WorkspaceMaterialPreviewInterpretation>())
            .FirstOrDefault(static material => material.RelativePath.Contains("roadmap", StringComparison.OrdinalIgnoreCase));
        if (roadmapMaterial is not null)
        {
            candidates.Add(new RoadmapCandidatePhase(
                "Imported ROADMAP-like material may contain contributor-authored phase information.",
                $"material `{roadmapMaterial.RelativePath}` [{roadmapMaterial.Confidence}]"));
        }

        if (candidates.Count == 0)
        {
            candidates.Add(new RoadmapCandidatePhase(
                "Recent git history exists but no explicit phase marker was detected.",
                $"recent commits {history.Commits.First().ShortSha}..{history.Commits.Last().ShortSha}"));
        }

        return new RoadmapSignalInterpretation(
            candidates,
            new[]
            {
                "Current phase is not contributor-confirmed.",
                "Upcoming phases are not contributor-confirmed.",
                "Done criteria are not derivable from git history.",
                "Phase ordering is not derivable from git history."
            },
            HasCandidateEvidence: true);
    }

    private static string[] BuildUnknowns(GitRoadmapHistory history)
    {
        if (!history.IsGitRepository)
        {
            return new[]
            {
                "No git history was available at the project root.",
                "Contributor-authored roadmap is required before canonical promotion.",
                "Evidence that would unblock candidate phases: git history, release tags, branch names, or authored roadmap material."
            };
        }

        if (!history.IsReadable)
        {
            return new[]
            {
                $"Git history could not be read: {history.FailureReason}",
                "Contributor-authored roadmap is required before canonical promotion.",
                "Evidence that would unblock candidate phases: readable git log, release tags, branch names, or authored roadmap material."
            };
        }

        return new[]
        {
            "Git history contained no commits for candidate roadmap extraction.",
            "Contributor-authored roadmap is required before canonical promotion.",
            "Evidence that would unblock candidate phases: commits, release tags, branch names, or authored roadmap material."
        };
    }

    private static bool IsCandidateCommit(GitRoadmapCommit commit)
    {
        return PhaseMarkers.Any(marker => commit.Subject.Contains(marker, StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsCandidateBranch(string branch)
    {
        return PhaseMarkers.Any(marker => branch.Contains(marker, StringComparison.OrdinalIgnoreCase));
    }

    private static string BuildCommitCandidateLabel(string subject)
    {
        if (subject.StartsWith("feat:", StringComparison.OrdinalIgnoreCase) ||
            subject.StartsWith("feature:", StringComparison.OrdinalIgnoreCase))
        {
            return "Feature commit may indicate a delivered roadmap increment.";
        }

        if (subject.Contains("phase", StringComparison.OrdinalIgnoreCase))
        {
            return "Phase marker in commit may indicate a roadmap phase boundary.";
        }

        if (subject.Contains("release", StringComparison.OrdinalIgnoreCase))
        {
            return "Release marker in commit may indicate a roadmap phase boundary.";
        }

        return "Git history marker may indicate roadmap-relevant work.";
    }
}

public sealed record RoadmapSignalInterpretation(
    IReadOnlyList<RoadmapCandidatePhase> Candidates,
    IReadOnlyList<string> Unknowns,
    bool HasCandidateEvidence);

public sealed record RoadmapCandidatePhase(
    string Label,
    string Evidence);
