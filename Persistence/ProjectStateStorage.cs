using System;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace zavod.Persistence;

public static class ProjectStateStorage
{
    private const string ContractVersion = "1.0";
    private const string LayoutVersion = "v1";
    private const string DefaultEntryMode = "cold_start";
    private const string ZavodDirectoryName = ".zavod";
    private const string MetaDirectoryName = "meta";
    private const string ProjectDirectoryName = "project";
    private const string ShiftsDirectoryName = "shifts";
    private const string SnapshotsDirectoryName = "snapshots";
    private const string MetaFileName = "project.json";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    public static ProjectState EnsureInitialized(string projectRootPath, string projectId, string projectName)
    {
        ValidateRequiredValue(projectRootPath, nameof(projectRootPath));
        ValidateRequiredValue(projectId, nameof(projectId));
        ValidateRequiredValue(projectName, nameof(projectName));

        var normalizedProjectRoot = Path.GetFullPath(projectRootPath);
        var zavodRoot = GetZavodRoot(normalizedProjectRoot);
        var metaFilePath = GetMetaFilePath(normalizedProjectRoot);

        if (Directory.Exists(zavodRoot) && File.Exists(metaFilePath))
        {
            return Load(normalizedProjectRoot);
        }

        Directory.CreateDirectory(zavodRoot);
        Directory.CreateDirectory(GetProjectTruthRoot(normalizedProjectRoot));
        Directory.CreateDirectory(Path.Combine(zavodRoot, ShiftsDirectoryName));
        Directory.CreateDirectory(Path.Combine(zavodRoot, SnapshotsDirectoryName));
        Directory.CreateDirectory(Path.Combine(zavodRoot, MetaDirectoryName));
        ZavodLocalStorageLayout.EnsureInitialized(normalizedProjectRoot);

        var meta = new ProjectMetaFile(
            ContractVersion,
            projectId.Trim(),
            projectName.Trim(),
            LayoutVersion,
            DefaultEntryMode,
            null,
            null);

        SaveMeta(metaFilePath, meta);
        return BuildState(normalizedProjectRoot, meta);
    }

    public static ProjectState Load(string projectRootPath)
    {
        ValidateRequiredValue(projectRootPath, nameof(projectRootPath));

        var normalizedProjectRoot = Path.GetFullPath(projectRootPath);
        var zavodRoot = GetZavodRoot(normalizedProjectRoot);

        if (!Directory.Exists(zavodRoot))
        {
            throw new ZavodPersistenceException("ZavodNotInitialized", "Project does not contain a .zavod storage root.");
        }

        var metaFilePath = GetMetaFilePath(normalizedProjectRoot);
        if (!File.Exists(metaFilePath))
        {
            throw new ZavodPersistenceException("InvalidProjectMeta", "Project meta file is missing.");
        }

        ProjectMetaFile? meta;
        try
        {
            meta = JsonSerializer.Deserialize<ProjectMetaFile>(File.ReadAllText(metaFilePath, Encoding.UTF8), JsonOptions);
        }
        catch (JsonException exception)
        {
            throw new ZavodPersistenceException("InvalidProjectMeta", $"Project meta file is invalid JSON: {exception.Message}");
        }

        if (meta is null)
        {
            throw new ZavodPersistenceException("InvalidProjectMeta", "Project meta file is empty.");
        }

        ValidateMeta(meta);
        return BuildState(normalizedProjectRoot, meta);
    }

    public static ProjectState Save(ProjectState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        ValidateState(state);
        EnsureStorageDirectories(state.Paths.ProjectRoot);

        var meta = new ProjectMetaFile(
            state.Version,
            state.ProjectId,
            state.ProjectName,
            state.LayoutVersion,
            state.EntryMode,
            state.ActiveShiftId,
            state.ActiveTaskId);

        var path = state.Paths.MetaFilePath;
        SaveMeta(path, meta);
        Console.WriteLine("ProjectState saved to: " + path);
        return Load(state.Paths.ProjectRoot);
    }

    public static bool IsColdStart(ProjectState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        return state.IsColdStart;
    }

    private static void EnsureStorageDirectories(string projectRootPath)
    {
        Directory.CreateDirectory(GetZavodRoot(projectRootPath));
        Directory.CreateDirectory(GetProjectTruthRoot(projectRootPath));
        Directory.CreateDirectory(Path.Combine(GetZavodRoot(projectRootPath), ShiftsDirectoryName));
        Directory.CreateDirectory(Path.Combine(GetZavodRoot(projectRootPath), SnapshotsDirectoryName));
        Directory.CreateDirectory(Path.Combine(GetZavodRoot(projectRootPath), MetaDirectoryName));
        ZavodLocalStorageLayout.EnsureInitialized(projectRootPath);
    }

    private static ProjectState BuildState(string projectRootPath, ProjectMetaFile meta)
    {
        var projectTruthRoot = GetProjectTruthRoot(projectRootPath);
        return new ProjectState(
            meta.Version!,
            meta.ProjectId!,
            meta.ProjectName!,
            meta.LayoutVersion!,
            meta.EntryMode!,
            meta.ActiveShiftId,
            meta.ActiveTaskId,
            new ProjectPaths(
                projectRootPath,
                GetZavodRoot(projectRootPath),
                GetMetaFilePath(projectRootPath),
                projectTruthRoot),
            new TruthPointers(
                Path.Combine(projectTruthRoot, "project.md"),
                Path.Combine(projectTruthRoot, "direction.md"),
                Path.Combine(projectTruthRoot, "roadmap.md"),
                Path.Combine(projectTruthRoot, "canon.md"),
                Path.Combine(projectTruthRoot, "capsule.md")));
    }

    private static void SaveMeta(string metaFilePath, ProjectMetaFile meta)
    {
        ValidateMeta(meta);
        var serialized = JsonSerializer.Serialize(meta, JsonOptions);
        File.WriteAllText(metaFilePath, serialized, Encoding.UTF8);
    }

    private static void ValidateState(ProjectState state)
    {
        ValidateRequiredValue(state.Version, nameof(state.Version));
        ValidateRequiredValue(state.ProjectId, nameof(state.ProjectId));
        ValidateRequiredValue(state.ProjectName, nameof(state.ProjectName));
        ValidateRequiredValue(state.LayoutVersion, nameof(state.LayoutVersion));
        ValidateRequiredValue(state.EntryMode, nameof(state.EntryMode));
        ArgumentNullException.ThrowIfNull(state.Paths);
        ArgumentNullException.ThrowIfNull(state.TruthPointers);

        if (!string.Equals(state.Version, ContractVersion, StringComparison.Ordinal))
        {
            throw new ZavodPersistenceException("InvalidProjectMeta", $"Unsupported project meta version '{state.Version}'.");
        }

        if (!string.Equals(state.LayoutVersion, LayoutVersion, StringComparison.Ordinal))
        {
            throw new ZavodPersistenceException("InvalidProjectMeta", $"Unsupported .zavod layout version '{state.LayoutVersion}'.");
        }
    }

    private static void ValidateMeta(ProjectMetaFile meta)
    {
        ValidateRequiredValue(meta.Version, nameof(meta.Version));
        ValidateRequiredValue(meta.ProjectId, nameof(meta.ProjectId));
        ValidateRequiredValue(meta.ProjectName, nameof(meta.ProjectName));
        ValidateRequiredValue(meta.LayoutVersion, nameof(meta.LayoutVersion));
        ValidateRequiredValue(meta.EntryMode, nameof(meta.EntryMode));

        if (!string.Equals(meta.Version, ContractVersion, StringComparison.Ordinal))
        {
            throw new ZavodPersistenceException("InvalidProjectMeta", $"Unsupported project meta version '{meta.Version}'.");
        }

        if (!string.Equals(meta.LayoutVersion, LayoutVersion, StringComparison.Ordinal))
        {
            throw new ZavodPersistenceException("InvalidProjectMeta", $"Unsupported .zavod layout version '{meta.LayoutVersion}'.");
        }
    }

    private static void ValidateRequiredValue(string? value, string paramName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ZavodPersistenceException("InvalidProjectMeta", $"Required value '{paramName}' is missing.");
        }
    }

    private static string GetZavodRoot(string projectRootPath) => Path.Combine(projectRootPath, ZavodDirectoryName);

    private static string GetProjectTruthRoot(string projectRootPath) => Path.Combine(GetZavodRoot(projectRootPath), ProjectDirectoryName);

    private static string GetMetaFilePath(string projectRootPath) => Path.Combine(GetZavodRoot(projectRootPath), MetaDirectoryName, MetaFileName);

    private sealed record ProjectMetaFile(
        [property: JsonPropertyName("version")] string? Version,
        [property: JsonPropertyName("projectId")] string? ProjectId,
        [property: JsonPropertyName("projectName")] string? ProjectName,
        [property: JsonPropertyName("layoutVersion")] string? LayoutVersion,
        [property: JsonPropertyName("entryMode")] string? EntryMode,
        [property: JsonPropertyName("activeShiftId")] string? ActiveShiftId,
        [property: JsonPropertyName("activeTaskId")] string? ActiveTaskId);
}
