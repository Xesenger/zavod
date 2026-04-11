using System;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using zavod.Contexting;
using zavod.Demo;
using zavod.Execution;
using zavod.Flow;

namespace zavod.Persistence;

public sealed record DemoResumeState(
    int CurrentStepIndex,
    int CurrentCycleIndex);

public sealed record ResumeStageSnapshot(
    string Version,
    StepPhaseState PhaseState,
    ContextIntentState IntentState,
    string IntentSummary,
    bool IsExecutionPreflightActive,
    bool IsPreflightClarificationActive,
    bool IsResultAccepted,
    string? ExecutionRefinement,
    string PreflightClarificationText,
    string RevisionIntakeText,
    ExecutionRuntimeState? RuntimeState,
    DemoResumeState? DemoState);

public static class ResumeStageStorage
{
    private const string SnapshotVersion = "1.0";
    private const string FileName = "resume-stage.json";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public static ResumeStageSnapshot? Load(string projectRootPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(projectRootPath);

        var path = GetFilePath(projectRootPath);
        if (!File.Exists(path))
        {
            return null;
        }

        try
        {
            var snapshot = JsonSerializer.Deserialize<ResumeStageSnapshot>(File.ReadAllText(path, Encoding.UTF8), JsonOptions);
            return snapshot?.Version == SnapshotVersion ? snapshot : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    public static void Save(string projectRootPath, ResumeStageSnapshot snapshot)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(projectRootPath);
        ArgumentNullException.ThrowIfNull(snapshot);

        Directory.CreateDirectory(Path.GetDirectoryName(GetFilePath(projectRootPath))!);
        var serialized = JsonSerializer.Serialize(snapshot with { Version = SnapshotVersion }, JsonOptions);
        File.WriteAllText(GetFilePath(projectRootPath), serialized, Encoding.UTF8);
    }

    public static void Delete(string projectRootPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(projectRootPath);

        var path = GetFilePath(projectRootPath);
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }

    private static string GetFilePath(string projectRootPath)
    {
        return Path.Combine(ZavodLocalStorageLayout.GetResumeRoot(projectRootPath), FileName);
    }
}
