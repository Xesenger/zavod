using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using zavod.Execution;

namespace zavod.Workspace;

public sealed class GitRoadmapHistoryReader(IExternalProcessRunner? processRunner = null)
{
    private readonly IExternalProcessRunner _processRunner = processRunner ?? new ExternalProcessRunner();

    public GitRoadmapHistory Read(string projectRootPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(projectRootPath);

        var normalizedRoot = Path.GetFullPath(projectRootPath);
        if (!Directory.Exists(Path.Combine(normalizedRoot, ".git")))
        {
            return new GitRoadmapHistory(
                Array.Empty<GitRoadmapCommit>(),
                Array.Empty<string>(),
                Array.Empty<string>(),
                IsGitRepository: false,
                IsReadable: false,
                "No .git directory found at project root.");
        }

        var commitsResult = _processRunner.Run(new ExternalProcessRequest(
            "git",
            new[] { "-C", normalizedRoot, "log", "--max-count=40", "--pretty=format:%H%x1f%s" },
            TimeSpan.FromSeconds(10),
            "roadmap_git_log"));
        if (commitsResult.ExitCode != 0 || commitsResult.TimedOut)
        {
            return new GitRoadmapHistory(
                Array.Empty<GitRoadmapCommit>(),
                Array.Empty<string>(),
                Array.Empty<string>(),
                IsGitRepository: true,
                IsReadable: false,
                commitsResult.TimedOut ? "git log timed out." : "git log failed.");
        }

        var tagsResult = _processRunner.Run(new ExternalProcessRequest(
            "git",
            new[] { "-C", normalizedRoot, "tag", "--list", "--sort=-creatordate" },
            TimeSpan.FromSeconds(10),
            "roadmap_git_tags"));
        var branchesResult = _processRunner.Run(new ExternalProcessRequest(
            "git",
            new[] { "-C", normalizedRoot, "branch", "--format=%(refname:short)" },
            TimeSpan.FromSeconds(10),
            "roadmap_git_branches"));

        return new GitRoadmapHistory(
            ParseCommits(commitsResult.StdOut),
            ParseLines(tagsResult.ExitCode == 0 && !tagsResult.TimedOut ? tagsResult.StdOut : string.Empty),
            ParseLines(branchesResult.ExitCode == 0 && !branchesResult.TimedOut ? branchesResult.StdOut : string.Empty),
            IsGitRepository: true,
            IsReadable: true,
            string.Empty);
    }

    private static IReadOnlyList<GitRoadmapCommit> ParseCommits(string stdout)
    {
        return stdout.Replace("\r\n", "\n")
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(static line =>
            {
                var parts = line.Split('\u001f', 2);
                return parts.Length == 2
                    ? new GitRoadmapCommit(ShortSha(parts[0]), parts[1])
                    : new GitRoadmapCommit(string.Empty, line);
            })
            .Where(static commit => !string.IsNullOrWhiteSpace(commit.Subject))
            .Take(40)
            .ToArray();
    }

    private static IReadOnlyList<string> ParseLines(string stdout)
    {
        return stdout.Replace("\r\n", "\n")
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(static line => !string.IsNullOrWhiteSpace(line))
            .Take(20)
            .ToArray();
    }

    private static string ShortSha(string sha)
    {
        return sha.Length <= 12 ? sha : sha[..12];
    }
}

public sealed record GitRoadmapHistory(
    IReadOnlyList<GitRoadmapCommit> Commits,
    IReadOnlyList<string> Tags,
    IReadOnlyList<string> Branches,
    bool IsGitRepository,
    bool IsReadable,
    string FailureReason);

public sealed record GitRoadmapCommit(
    string ShortSha,
    string Subject);
