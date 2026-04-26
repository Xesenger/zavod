using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using zavod.Workspace;

namespace zavod.Execution;

public sealed record RunProfileExecutionOutcome(
    bool Attempted,
    bool Success,
    string Summary,
    WorkspaceEvidenceRunProfile? Profile,
    ExternalProcessResult? ProcessResult,
    string? Diagnostic);

public sealed class RunProfileExecutionService(IExternalProcessRunner? processRunner = null)
{
    private static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(90);
    private static readonly HashSet<string> AllowedExecutables = new(StringComparer.OrdinalIgnoreCase)
    {
        "dotnet",
        "npm",
        "cargo",
        "go",
        "cmake",
        "make",
        "pytest"
    };

    private readonly IExternalProcessRunner _processRunner = processRunner ?? new ExternalProcessRunner();

    public static bool LooksLikeRunProfileTask(string taskDescription)
    {
        if (string.IsNullOrWhiteSpace(taskDescription))
        {
            return false;
        }

        return ContainsAnyRunIntent(taskDescription.Trim().ToLowerInvariant());
    }

    public RunProfileExecutionOutcome ExecuteFirstSupported(string projectRoot, string taskDescription)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(projectRoot);

        var profiles = LoadRunProfiles(projectRoot);
        if (profiles.Count == 0)
        {
            return new RunProfileExecutionOutcome(
                Attempted: false,
                Success: false,
                "No scanner run/build profiles are available for command execution.",
                null,
                null,
                "NO_RUN_PROFILE");
        }

        var profile = SelectProfile(projectRoot, profiles, taskDescription);
        if (profile is null)
        {
            return new RunProfileExecutionOutcome(
                Attempted: false,
                Success: false,
                "Scanner run/build profiles exist, but none are supported by this local command runner.",
                null,
                null,
                "NO_SUPPORTED_RUN_PROFILE");
        }

        if (!TryBuildProcessRequest(projectRoot, profile, out var request, out var diagnostic))
        {
            return new RunProfileExecutionOutcome(
                Attempted: false,
                Success: false,
                $"Run profile `{profile.Command}` could not be converted into a safe process request.",
                profile,
                null,
                diagnostic);
        }

        ExternalProcessResult result;
        try
        {
            result = _processRunner.Run(request!);
        }
        catch (Exception exception) when (exception is InvalidOperationException or IOException or UnauthorizedAccessException or System.ComponentModel.Win32Exception)
        {
            return new RunProfileExecutionOutcome(
                Attempted: true,
                Success: false,
                $"Command `{profile.Command}` failed to start: {exception.Message}",
                profile,
                null,
                exception.GetType().Name);
        }

        var success = !result.TimedOut && result.ExitCode == 0;
        var summary = success
            ? $"Command `{profile.Command}` completed successfully in `{profile.WorkingDirectory}`."
            : $"Command `{profile.Command}` did not complete successfully in `{profile.WorkingDirectory}` (exit={result.ExitCode}, timeout={result.TimedOut.ToString().ToLowerInvariant()}).";
        return new RunProfileExecutionOutcome(
            Attempted: true,
            Success: success,
            summary,
            profile,
            result,
            success ? null : "COMMAND_FAILED");
    }

    private static IReadOnlyList<WorkspaceEvidenceRunProfile> LoadRunProfiles(string projectRoot)
    {
        var path = Path.Combine(projectRoot, ".zavod", "import_evidence_bundle", "runprofiles.index.json");
        if (!File.Exists(path))
        {
            return BuildFallbackProfiles(projectRoot);
        }

        try
        {
            var profiles = JsonSerializer.Deserialize<IReadOnlyList<WorkspaceEvidenceRunProfile>>(File.ReadAllText(path)) ?? Array.Empty<WorkspaceEvidenceRunProfile>();
            return MergeFallbackProfiles(projectRoot, profiles);
        }
        catch (JsonException)
        {
            return BuildFallbackProfiles(projectRoot);
        }
        catch (IOException)
        {
            return BuildFallbackProfiles(projectRoot);
        }
        catch (UnauthorizedAccessException)
        {
            return BuildFallbackProfiles(projectRoot);
        }
    }

    private static IReadOnlyList<WorkspaceEvidenceRunProfile> MergeFallbackProfiles(
        string projectRoot,
        IReadOnlyList<WorkspaceEvidenceRunProfile> profiles)
    {
        var fallback = BuildFallbackProfiles(projectRoot);
        if (fallback.Count == 0)
        {
            return profiles;
        }

        return profiles
            .Concat(fallback)
            .GroupBy(static profile => $"{profile.Kind}|{profile.Command}|{profile.WorkingDirectory}", StringComparer.OrdinalIgnoreCase)
            .Select(static group => group.First())
            .ToArray();
    }

    private static IReadOnlyList<WorkspaceEvidenceRunProfile> BuildFallbackProfiles(string projectRoot)
    {
        var makefile = Path.Combine(projectRoot, "Makefile");
        if (!File.Exists(makefile))
        {
            return Array.Empty<WorkspaceEvidenceRunProfile>();
        }

        return new[]
        {
            new WorkspaceEvidenceRunProfile(
                "profile-root-make-build-fallback",
                "build",
                "make",
                ".",
                "Makefile",
                WorkspaceEvidenceConfidenceLevel.Confirmed,
                new[] { "manifest:Makefile", "fallback_direct_file_evidence" },
                new WorkspaceEvidenceMarker("manifest", "Makefile", "Makefile exists at project root.", WorkspaceEvidenceConfidenceLevel.Confirmed, IsPartial: false, IsBounded: false))
        };
    }

    private static WorkspaceEvidenceRunProfile? SelectProfile(
        string projectRoot,
        IReadOnlyList<WorkspaceEvidenceRunProfile> profiles,
        string taskDescription)
    {
        var wantsBuild = WantsBuild(taskDescription);
        return profiles
            .Where(IsSupportedProfile)
            .OrderBy(profile => ProfileRank(profile, wantsBuild))
            .ThenBy(profile => ProfileTaskMatchRank(projectRoot, taskDescription, profile))
            .ThenByDescending(profile => profile.Confidence)
            .ThenBy(profile => profile.WorkingDirectory, StringComparer.OrdinalIgnoreCase)
            .ThenBy(profile => profile.Command, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault();
    }

    private static int ProfileTaskMatchRank(
        string projectRoot,
        string taskDescription,
        WorkspaceEvidenceRunProfile profile)
    {
        var profileText = $"{profile.Id} {profile.WorkingDirectory} {profile.SourcePath} {profile.Command}".ToLowerInvariant();
        var taskTokens = ExtractMeaningfulTokens(taskDescription);
        if (taskTokens.Any(token => profileText.Contains(token, StringComparison.Ordinal)))
        {
            return 0;
        }

        var projectName = Path.GetFileName(Path.GetFullPath(projectRoot).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        var projectTokens = ExtractMeaningfulTokens(projectName);
        return projectTokens.Any(token => profileText.Contains(token, StringComparison.Ordinal))
            ? 1
            : 2;
    }

    private static IReadOnlyList<string> ExtractMeaningfulTokens(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return Array.Empty<string>();
        }

        var tokens = new List<string>();
        var current = new System.Text.StringBuilder();
        foreach (var ch in text.ToLowerInvariant())
        {
            if (char.IsLetterOrDigit(ch))
            {
                current.Append(ch);
                continue;
            }

            FlushToken(tokens, current);
        }

        FlushToken(tokens, current);
        return tokens
            .Where(static token => token.Length >= 4)
            .Where(static token => token is not ("build" or "source" or "sources" or "windows" or "using" or "project"))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
    }

    private static void FlushToken(ICollection<string> tokens, System.Text.StringBuilder current)
    {
        if (current.Length == 0)
        {
            return;
        }

        tokens.Add(current.ToString());
        current.Clear();
    }

    private static bool WantsBuild(string taskDescription)
    {
        var normalized = taskDescription.ToLowerInvariant();
        return normalized.Contains("build", StringComparison.Ordinal)
            || normalized.Contains("binary", StringComparison.Ordinal)
            || normalized.Contains("\u0441\u043e\u0431\u0435\u0440", StringComparison.Ordinal)
            || normalized.Contains("\u0441\u043e\u0431\u0440", StringComparison.Ordinal)
            || normalized.Contains("\u0431\u0438\u043d\u0430\u0440", StringComparison.Ordinal);
    }

    private static bool ContainsAnyRunIntent(string normalized)
    {
        return normalized.Contains("build", StringComparison.Ordinal)
            || normalized.Contains("run", StringComparison.Ordinal)
            || normalized.Contains("launch", StringComparison.Ordinal)
            || normalized.Contains("play", StringComparison.Ordinal)
            || normalized.Contains("binary", StringComparison.Ordinal)
            || normalized.Contains("\u0441\u043e\u0431\u0435\u0440", StringComparison.Ordinal)
            || normalized.Contains("\u0441\u043e\u0431\u0440", StringComparison.Ordinal)
            || normalized.Contains("\u0437\u0430\u043f\u0443\u0441", StringComparison.Ordinal)
            || normalized.Contains("\u043f\u043e\u0438\u0433\u0440", StringComparison.Ordinal)
            || normalized.Contains("\u0438\u0433\u0440", StringComparison.Ordinal)
            || normalized.Contains("\u0431\u0438\u043d\u0430\u0440", StringComparison.Ordinal);
    }

    private static int ProfileRank(WorkspaceEvidenceRunProfile profile, bool wantsBuild)
    {
        if (wantsBuild && string.Equals(profile.Kind, "build", StringComparison.OrdinalIgnoreCase))
        {
            return 0;
        }

        if (!wantsBuild && string.Equals(profile.Kind, "run", StringComparison.OrdinalIgnoreCase))
        {
            return 0;
        }

        return profile.Kind.ToLowerInvariant() switch
        {
            "build" => 1,
            "run" => 2,
            "configure" => 3,
            "test" => 4,
            _ => 9
        };
    }

    private static bool IsSupportedProfile(WorkspaceEvidenceRunProfile profile)
    {
        return TrySplitCommand(profile.Command, out var parts)
            && parts.Count > 0
            && AllowedExecutables.Contains(parts[0])
            && !ContainsShellOperator(profile.Command);
    }

    private static bool TryBuildProcessRequest(
        string projectRoot,
        WorkspaceEvidenceRunProfile profile,
        out ExternalProcessRequest? request,
        out string? diagnostic)
    {
        request = null;
        diagnostic = null;

        if (ContainsShellOperator(profile.Command) || !TrySplitCommand(profile.Command, out var parts) || parts.Count == 0)
        {
            diagnostic = "UNSAFE_COMMAND_SHAPE";
            return false;
        }

        var executable = parts[0];
        if (!AllowedExecutables.Contains(executable))
        {
            diagnostic = "UNSUPPORTED_EXECUTABLE";
            return false;
        }

        var workingDirectory = ResolveInsideRoot(projectRoot, profile.WorkingDirectory);
        if (workingDirectory is null)
        {
            diagnostic = "WORKING_DIRECTORY_OUTSIDE_PROJECT";
            return false;
        }

        request = new ExternalProcessRequest(
            executable,
            parts.Skip(1).ToArray(),
            DefaultTimeout,
            $"run_profile:{profile.Id}",
            workingDirectory);
        return true;
    }

    private static string? ResolveInsideRoot(string projectRoot, string relativePath)
    {
        var root = Path.GetFullPath(projectRoot);
        var candidate = string.IsNullOrWhiteSpace(relativePath) || relativePath == "."
            ? root
            : Path.GetFullPath(Path.Combine(root, relativePath));
        return IsSameOrChildPath(root, candidate) ? candidate : null;
    }

    private static bool IsSameOrChildPath(string root, string candidate)
    {
        var normalizedRoot = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
        var normalizedCandidate = Path.GetFullPath(candidate).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
        return normalizedCandidate.StartsWith(normalizedRoot, StringComparison.OrdinalIgnoreCase)
            || string.Equals(normalizedCandidate, normalizedRoot, StringComparison.OrdinalIgnoreCase);
    }

    private static bool ContainsShellOperator(string command)
    {
        return command.Contains("&&", StringComparison.Ordinal)
            || command.Contains("||", StringComparison.Ordinal)
            || command.Contains(';', StringComparison.Ordinal)
            || command.Contains('|', StringComparison.Ordinal)
            || command.Contains('>', StringComparison.Ordinal)
            || command.Contains('<', StringComparison.Ordinal);
    }

    private static bool TrySplitCommand(string command, out IReadOnlyList<string> parts)
    {
        parts = Array.Empty<string>();
        if (string.IsNullOrWhiteSpace(command))
        {
            return false;
        }

        var items = new List<string>();
        var current = new System.Text.StringBuilder();
        var inQuotes = false;
        for (var index = 0; index < command.Length; index++)
        {
            var ch = command[index];
            if (ch == '"')
            {
                inQuotes = !inQuotes;
                continue;
            }

            if (char.IsWhiteSpace(ch) && !inQuotes)
            {
                FlushPart(items, current);
                continue;
            }

            current.Append(ch);
        }

        if (inQuotes)
        {
            return false;
        }

        FlushPart(items, current);
        parts = items;
        return items.Count > 0;
    }

    private static void FlushPart(ICollection<string> items, System.Text.StringBuilder current)
    {
        if (current.Length == 0)
        {
            return;
        }

        items.Add(current.ToString());
        current.Clear();
    }
}
