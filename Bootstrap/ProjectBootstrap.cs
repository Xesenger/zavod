using System;
using System.IO;
using System.Linq;
using System.Text;
using zavod.Persistence;

namespace zavod.Bootstrap;

public static class ProjectBootstrap
{
    public static BootstrapResult Initialize(string projectRootPath)
    {
        if (string.IsNullOrWhiteSpace(projectRootPath))
        {
            throw new ArgumentException("Project root path is required.", nameof(projectRootPath));
        }

        var normalizedProjectRoot = Path.GetFullPath(projectRootPath);
        var projectName = DeriveProjectName(normalizedProjectRoot);
        return InitializeCore(normalizedProjectRoot, projectName);
    }

    /// <summary>
    /// Bootstraps a project at <paramref name="projectRootPath"/> with an explicit
    /// human-readable <paramref name="projectName"/>. Used by the "new project" flow
    /// where the project name is decoupled from the folder name (folder is fixed at
    /// create time; name is metadata the user can rename later).
    /// </summary>
    public static BootstrapResult Initialize(string projectRootPath, string projectName)
    {
        if (string.IsNullOrWhiteSpace(projectRootPath))
        {
            throw new ArgumentException("Project root path is required.", nameof(projectRootPath));
        }

        if (string.IsNullOrWhiteSpace(projectName))
        {
            throw new ArgumentException("Project name is required.", nameof(projectName));
        }

        var normalizedProjectRoot = Path.GetFullPath(projectRootPath);
        return InitializeCore(normalizedProjectRoot, projectName.Trim());
    }

    private static BootstrapResult InitializeCore(string normalizedProjectRoot, string projectName)
    {
        var projectId = DeriveProjectId(projectName);

        ProjectStateStorage.EnsureInitialized(normalizedProjectRoot, projectId, projectName);
        var state = ProjectStateStorage.Load(normalizedProjectRoot);

        return new BootstrapResult(
            ProjectStateStorage.IsColdStart(state),
            HasValidState: true,
            HasActiveShift: state.ActiveShiftId is not null);
    }

    private static string DeriveProjectName(string projectRootPath)
    {
        var directory = new DirectoryInfo(projectRootPath);
        var name = directory.Name.Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Project root path must resolve to a directory name.", nameof(projectRootPath));
        }

        return name;
    }

    private static string DeriveProjectId(string projectName)
    {
        var builder = new StringBuilder(projectName.Length);
        var previousWasSeparator = false;

        foreach (var character in projectName)
        {
            if (char.IsLetterOrDigit(character))
            {
                builder.Append(char.ToLowerInvariant(character));
                previousWasSeparator = false;
                continue;
            }

            if (previousWasSeparator)
            {
                continue;
            }

            builder.Append('-');
            previousWasSeparator = true;
        }

        var normalized = builder
            .ToString()
            .Trim('-');

        if (!string.IsNullOrWhiteSpace(normalized))
        {
            return normalized;
        }

        return new string(
            projectName
                .Where(static character => !char.IsWhiteSpace(character))
                .ToArray());
    }
}
