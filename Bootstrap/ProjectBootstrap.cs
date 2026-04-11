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
