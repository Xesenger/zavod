using System;
using System.IO;

namespace zavod.Demo;

public static class BaselineUiMode
{
    private const string ScenarioEnvVar = "ZAVOD_UI_BASELINE_SCENARIO";
    private const string ScenarioFileRelativePath = ".zavod\\ui-diagnostic\\baseline-ui.txt";

    public static bool IsEnabled(string repositoryRoot)
    {
        return !string.IsNullOrWhiteSpace(ReadScenario(repositoryRoot));
    }

    public static string? ReadScenario(string repositoryRoot)
    {
        var envValue = Environment.GetEnvironmentVariable(ScenarioEnvVar)?.Trim();
        if (!string.IsNullOrWhiteSpace(envValue))
        {
            return envValue;
        }

        var filePath = Path.Combine(repositoryRoot, ScenarioFileRelativePath);
        if (!File.Exists(filePath))
        {
            return null;
        }

        var fileValue = File.ReadAllText(filePath).Trim();
        return string.IsNullOrWhiteSpace(fileValue) ? null : fileValue;
    }
}
