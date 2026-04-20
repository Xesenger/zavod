using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace zavod.Persistence;

/// <summary>
/// User-level (cross-project) registry of imported / created project roots.
/// Persisted at <c>~/Documents/ZAVOD/projects.json</c> so the user can see, back up,
/// or hand-edit the list of known projects without spelunking AppData. Project content
/// itself stays inside each project's own <c>.zavod/</c> and <c>.zavod.local/</c>.
/// </summary>
public sealed record ProjectRegistry(
    int Version,
    string? LastOpenedProjectId,
    IReadOnlyList<ProjectRegistryEntry> Projects);

public sealed record ProjectRegistryEntry(
    string Id,
    string Name,
    string RootPath,
    DateTimeOffset AddedAt,
    DateTimeOffset LastOpenedAt);

public static class ProjectRegistryStorage
{
    private const int CurrentVersion = 1;
    private const string DirectoryName = "ZAVOD";
    private const string FileName = "projects.json";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    /// <summary>Resolves the on-disk file path for the registry. Directory may not exist yet.</summary>
    public static string GetFilePath()
    {
        var documents = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        return Path.Combine(documents, DirectoryName, FileName);
    }

    /// <summary>
    /// Loads the registry. Missing or unreadable files yield an empty registry —
    /// callers should not fail launch over a missing/corrupt registry. Corrupt content
    /// is logged via Debug for forensic review.
    /// </summary>
    public static ProjectRegistry Load()
    {
        var path = GetFilePath();
        if (!File.Exists(path))
        {
            return Empty();
        }

        try
        {
            var json = File.ReadAllText(path, Encoding.UTF8);
            if (string.IsNullOrWhiteSpace(json))
            {
                return Empty();
            }

            var registry = JsonSerializer.Deserialize<ProjectRegistry>(json, JsonOptions);
            if (registry is null)
            {
                return Empty();
            }

            return new ProjectRegistry(
                Version: registry.Version > 0 ? registry.Version : CurrentVersion,
                LastOpenedProjectId: registry.LastOpenedProjectId,
                Projects: registry.Projects ?? Array.Empty<ProjectRegistryEntry>());
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ProjectRegistryStorage] Failed to load {path}: {ex.Message}");
            return Empty();
        }
    }

    /// <summary>
    /// Adds a project to the registry. If <paramref name="rootPath"/> is already present
    /// (case-insensitive full path match), the existing entry is touched instead and returned.
    /// Id is derived from the directory name with a numeric suffix on collision.
    /// </summary>
    public static ProjectRegistryEntry Add(string name, string rootPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(rootPath);

        var resolvedRoot = Path.GetFullPath(rootPath);
        var registry = Load();
        var now = DateTimeOffset.Now;

        var existing = registry.Projects.FirstOrDefault(entry =>
            string.Equals(entry.RootPath, resolvedRoot, StringComparison.OrdinalIgnoreCase));
        if (existing is not null)
        {
            var touched = existing with { LastOpenedAt = now };
            var updatedList = registry.Projects
                .Select(entry => string.Equals(entry.Id, existing.Id, StringComparison.Ordinal) ? touched : entry)
                .ToArray();
            Save(new ProjectRegistry(CurrentVersion, touched.Id, updatedList));
            return touched;
        }

        var entryName = name.Trim();
        var entryId = AllocateId(entryName, registry.Projects);
        var freshEntry = new ProjectRegistryEntry(
            Id: entryId,
            Name: entryName,
            RootPath: resolvedRoot,
            AddedAt: now,
            LastOpenedAt: now);

        var nextProjects = registry.Projects.Append(freshEntry).ToArray();
        Save(new ProjectRegistry(CurrentVersion, freshEntry.Id, nextProjects));
        return freshEntry;
    }

    /// <summary>Removes a project entry from the registry. No-op if id is unknown.</summary>
    public static void Remove(string projectId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(projectId);
        var registry = Load();
        var nextProjects = registry.Projects
            .Where(entry => !string.Equals(entry.Id, projectId, StringComparison.Ordinal))
            .ToArray();
        if (nextProjects.Length == registry.Projects.Count)
        {
            return;
        }

        var lastOpened = string.Equals(registry.LastOpenedProjectId, projectId, StringComparison.Ordinal)
            ? null
            : registry.LastOpenedProjectId;
        Save(new ProjectRegistry(CurrentVersion, lastOpened, nextProjects));
    }

    /// <summary>Marks a project as the most recently opened.</summary>
    public static void Touch(string projectId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(projectId);
        var registry = Load();
        var entry = registry.Projects.FirstOrDefault(item =>
            string.Equals(item.Id, projectId, StringComparison.Ordinal));
        if (entry is null)
        {
            return;
        }

        var touched = entry with { LastOpenedAt = DateTimeOffset.Now };
        var updatedList = registry.Projects
            .Select(item => string.Equals(item.Id, projectId, StringComparison.Ordinal) ? touched : item)
            .ToArray();
        Save(new ProjectRegistry(CurrentVersion, projectId, updatedList));
    }

    private static void Save(ProjectRegistry registry)
    {
        var path = GetFilePath();
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var json = JsonSerializer.Serialize(registry, JsonOptions);
        File.WriteAllText(path, json, Encoding.UTF8);
    }

    private static ProjectRegistry Empty() =>
        new(CurrentVersion, LastOpenedProjectId: null, Projects: Array.Empty<ProjectRegistryEntry>());

    private static string AllocateId(string name, IReadOnlyList<ProjectRegistryEntry> existing)
    {
        var baseSlug = Slugify(name);
        if (string.IsNullOrWhiteSpace(baseSlug))
        {
            baseSlug = "project";
        }

        var candidate = baseSlug;
        var suffix = 2;
        while (existing.Any(entry => string.Equals(entry.Id, candidate, StringComparison.Ordinal)))
        {
            candidate = $"{baseSlug}-{suffix}";
            suffix++;
        }

        return candidate;
    }

    private static string Slugify(string value)
    {
        var builder = new StringBuilder(value.Length);
        var previousWasSeparator = false;
        foreach (var character in value)
        {
            if (char.IsLetterOrDigit(character))
            {
                builder.Append(char.ToLowerInvariant(character));
                previousWasSeparator = false;
                continue;
            }

            if (previousWasSeparator || builder.Length == 0)
            {
                continue;
            }

            builder.Append('-');
            previousWasSeparator = true;
        }

        return builder.ToString().Trim('-');
    }
}
