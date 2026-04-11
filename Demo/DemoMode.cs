using System;
using System.IO;

namespace zavod.Demo;

public static class DemoMode
{
    private const string MarkerFileName = ".zavod-demo-mode";
    private const string DemoProjectFolderName = ".zavod-demo-project";

    public static bool IsEnabled(string repositoryRoot)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(repositoryRoot);
        return File.Exists(Path.Combine(repositoryRoot, MarkerFileName));
    }

    public static string ResolveRuntimeProjectRoot(string repositoryRoot)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(repositoryRoot);
        return IsEnabled(repositoryRoot)
            ? Path.Combine(repositoryRoot, DemoProjectFolderName)
            : repositoryRoot;
    }
}
