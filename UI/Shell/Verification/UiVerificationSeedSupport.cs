using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.Json;
using zavod.Contexting;
using zavod.Flow;
using zavod.Persistence;

namespace zavod.UI.Shell.Verification;

internal sealed record UiVerificationSeed(string Target);

internal static class UiVerificationSeedSupport
{
    private const string SeedFileName = "ui-seed.json";
    private const string LogFileName = "ui-seed.log";
    private const string VerificationEnvVar = "ZAVOD_UI_VERIFICATION";
    private static readonly JsonSerializerOptions SeedJsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    internal static void TryApplyAtStartup(string projectRoot)
    {
        var seedPath = GetSeedPath(projectRoot);
        var envValue = Environment.GetEnvironmentVariable(VerificationEnvVar);

        Log(projectRoot, $"Startup seed check. {VerificationEnvVar}='{envValue ?? "<null>"}'. SeedPath='{seedPath}'.");
        if (!File.Exists(seedPath))
        {
            Log(projectRoot, "Seed file not found.");
            return;
        }

        Log(projectRoot, "Seed file found.");
        if (!string.Equals(Environment.GetEnvironmentVariable(VerificationEnvVar), "1", StringComparison.Ordinal))
        {
            Log(projectRoot, $"Seed file present but ignored because {VerificationEnvVar}=1 was not set.");
            return;
        }

        UiVerificationSeed? seed;
        try
        {
            seed = JsonSerializer.Deserialize<UiVerificationSeed>(File.ReadAllText(seedPath, Encoding.UTF8), SeedJsonOptions);
            Log(projectRoot, $"Seed parsed successfully. Target='{seed?.Target ?? "<null>"}'.");
        }
        catch (JsonException ex)
        {
            Log(projectRoot, $"Failed to parse verification seed: {ex.Message}");
            return;
        }

        if (seed is null || string.IsNullOrWhiteSpace(seed.Target))
        {
            Log(projectRoot, "Verification seed was empty or invalid and was ignored.");
            return;
        }

        if (!TryMap(seed.Target.Trim(), out var snapshot))
        {
            Log(projectRoot, $"Unsupported verification seed target '{seed.Target}'. Seed ignored.");
            return;
        }

        Log(projectRoot, $"Apply attempted for target '{seed.Target}'.");
        ResumeStageStorage.Save(projectRoot, snapshot);
        Log(projectRoot, $"Apply succeeded for target '{seed.Target}'.");
        Log(projectRoot, "Delete attempted for seed file.");
        File.Delete(seedPath);
        Log(projectRoot, $"Delete succeeded. Applied verification seed target '{seed.Target}' and consumed the seed file.");
    }

    private static bool TryMap(string target, out ResumeStageSnapshot snapshot)
    {
        snapshot = default!;

        switch (target)
        {
            case "DiscussionReady":
                snapshot = BuildSnapshot(
                    StepPhaseMachine.ResumeDiscussion(ContextIntentState.ReadyForValidation),
                    isPreflight: false);
                return true;

            case "Preflight":
                snapshot = BuildSnapshot(
                    new StepPhaseState(
                        SurfacePhase.Execution,
                        DiscussionSubphase.None,
                        ExecutionSubphase.Preflight,
                        ResultSubphase.None,
                        ContextIntentState.ReadyForValidation,
                        HasActiveShift: false,
                        HasActiveTask: false,
                        HasClarification: false,
                        HasReopenedContext: false),
                    isPreflight: true);
                return true;

            default:
                return false;
        }
    }

    private static ResumeStageSnapshot BuildSnapshot(StepPhaseState phaseState, bool isPreflight)
    {
        return new ResumeStageSnapshot(
            Version: "1.0",
            PhaseState: phaseState,
            IntentState: phaseState.IntentState,
            IntentSummary: string.Empty,
            IsExecutionPreflightActive: isPreflight,
            IsPreflightClarificationActive: false,
            IsResultAccepted: false,
            ExecutionRefinement: null,
            PreflightClarificationText: string.Empty,
            RevisionIntakeText: string.Empty,
            RuntimeState: null,
            DemoState: null);
    }

    private static string GetVerificationRoot(string projectRoot)
    {
        return Path.Combine(ZavodLocalStorageLayout.GetRoot(projectRoot), "verification");
    }

    private static string GetSeedPath(string projectRoot)
    {
        return Path.Combine(GetVerificationRoot(projectRoot), SeedFileName);
    }

    private static string GetLogPath(string projectRoot)
    {
        return Path.Combine(GetVerificationRoot(projectRoot), LogFileName);
    }

    private static void Log(string projectRoot, string message)
    {
        try
        {
            var verificationRoot = GetVerificationRoot(projectRoot);
            Directory.CreateDirectory(verificationRoot);
            File.AppendAllLines(
                GetLogPath(projectRoot),
                new[] { $"[{DateTimeOffset.Now:O}] {message}" },
                Encoding.UTF8);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to write UI verification seed log: {ex}");
        }
    }
}
