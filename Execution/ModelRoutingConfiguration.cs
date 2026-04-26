using System;
using System.IO;
using System.Text.Json;

namespace zavod.Execution;

public sealed record ModelRoutingConfiguration(
    RoleProfile Importer,
    RoleProfile Lead,
    RoleProfile Worker,
    RoleProfile Qc,
    RoleProfile SeniorSpecialist,
    RoleProfile Sage,
    string Source)
{
    public const string DefaultConfigRelativePath = @"app\config\model-routing.defaults.json";
    public const string DefaultLocalConfigFileName = "model-routing.local.json";

    public const string NemotronSuperFreeModelId = "nvidia/nemotron-3-super-120b-a12b:free";

    public static RoleProfile DefaultImporter { get; } = new(OpenRouterConfiguration.DefaultImportModelId, 0.0, 90, 2500);
    public static RoleProfile DefaultLead { get; } = new("openai/gpt-4.1-mini", 0.3, 60, 800);
    public static RoleProfile DefaultWorker { get; } = new(NemotronSuperFreeModelId, 0.2, 240, 3200);
    public static RoleProfile DefaultQc { get; } = new(NemotronSuperFreeModelId, 0.0, 60, 1000);
    public static RoleProfile DefaultSeniorSpecialist { get; } = new("openai/gpt-4.1", 0.1, 120, 3000);
    public static RoleProfile DefaultSage { get; } = new(OpenRouterConfiguration.DefaultImportModelId, 0.0, 45, 1000);

    public static ModelRoutingConfiguration LoadOrDefault()
    {
        var defaults = LoadFromFile(
            Path.Combine(Environment.CurrentDirectory, DefaultConfigRelativePath),
            CreateCodeDefaults(),
            "code-defaults");
        var localPath = ResolveLocalConfigPath();
        var withLocalOverrides = File.Exists(localPath)
            ? LoadFromFile(localPath, defaults.Configuration, defaults.Source)
            : defaults;

        var configuration = withLocalOverrides.Configuration with
        {
            Importer = ApplyModelOverride(withLocalOverrides.Configuration.Importer, "ZAVOD_MODEL_IMPORTER", "OPENROUTER_MODEL"),
            Lead = ApplyModelOverride(withLocalOverrides.Configuration.Lead, "ZAVOD_MODEL_LEAD"),
            Worker = ApplyModelOverride(withLocalOverrides.Configuration.Worker, "ZAVOD_MODEL_WORKER"),
            Qc = ApplyModelOverride(withLocalOverrides.Configuration.Qc, "ZAVOD_MODEL_QC"),
            SeniorSpecialist = ApplyModelOverride(withLocalOverrides.Configuration.SeniorSpecialist, "ZAVOD_MODEL_SENIOR_SPECIALIST"),
            Sage = ApplyModelOverride(withLocalOverrides.Configuration.Sage, "ZAVOD_MODEL_SAGE")
        };

        return configuration;
    }

    private static (ModelRoutingConfiguration Configuration, string Source) LoadFromFile(
        string path,
        ModelRoutingConfiguration fallback,
        string fallbackSource)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            return (fallback, fallbackSource);
        }

        try
        {
            var json = File.ReadAllText(path);
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                return (fallback, fallbackSource);
            }

            var routes = root.TryGetProperty("routes", out var routesNode) && routesNode.ValueKind == JsonValueKind.Object
                ? routesNode
                : root;
            var source = $"file:{Path.GetFullPath(path)}";
            var configuration = new ModelRoutingConfiguration(
                ReadProfile(routes, "importer", fallback.Importer),
                ReadProfile(routes, "lead", fallback.Lead),
                ReadProfile(routes, "worker", fallback.Worker),
                ReadProfile(routes, "qc", fallback.Qc),
                ReadProfile(routes, "seniorSpecialist", fallback.SeniorSpecialist),
                ReadProfile(routes, "sage", fallback.Sage),
                string.Equals(fallbackSource, "code-defaults", StringComparison.Ordinal)
                    ? source
                    : $"{fallbackSource};{source}");
            return (configuration, configuration.Source);
        }
        catch
        {
            return (fallback, fallbackSource);
        }
    }

    private static ModelRoutingConfiguration CreateCodeDefaults()
    {
        return new ModelRoutingConfiguration(
            DefaultImporter,
            DefaultLead,
            DefaultWorker,
            DefaultQc,
            DefaultSeniorSpecialist,
            DefaultSage,
            "code-defaults");
    }

    private static RoleProfile ReadProfile(JsonElement root, string property, RoleProfile fallback)
    {
        if (!root.TryGetProperty(property, out var node) || node.ValueKind != JsonValueKind.Object)
        {
            return fallback;
        }

        var model = node.TryGetProperty("model", out var modelNode) && modelNode.ValueKind == JsonValueKind.String
            ? modelNode.GetString()?.Trim()
            : null;
        var temperature = node.TryGetProperty("temperature", out var tempNode) && tempNode.TryGetDouble(out var parsedTemp)
            ? Math.Clamp(parsedTemp, 0.0, 2.0)
            : fallback.Temperature;
        var timeoutSeconds = node.TryGetProperty("timeoutSeconds", out var timeoutNode) && timeoutNode.TryGetInt32(out var parsedTimeout)
            ? Math.Clamp(parsedTimeout, 5, 600)
            : fallback.TimeoutSeconds;
        var maxTokens = node.TryGetProperty("maxTokens", out var tokensNode) && tokensNode.TryGetInt32(out var parsedTokens)
            ? Math.Clamp(parsedTokens, 64, 16000)
            : fallback.MaxTokens;

        return new RoleProfile(
            string.IsNullOrWhiteSpace(model) ? fallback.Model : model,
            temperature,
            timeoutSeconds,
            maxTokens);
    }

    private static RoleProfile ApplyModelOverride(RoleProfile profile, params string[] variableNames)
    {
        foreach (var variableName in variableNames)
        {
            var value = Environment.GetEnvironmentVariable(variableName);
            if (!string.IsNullOrWhiteSpace(value))
            {
                return profile with { Model = value.Trim() };
            }
        }

        return profile;
    }

    private static string ResolveLocalConfigPath()
    {
        var configuredPath = Environment.GetEnvironmentVariable("ZAVOD_MODEL_ROUTING_CONFIG_FILE");
        if (!string.IsNullOrWhiteSpace(configuredPath))
        {
            return configuredPath.Trim();
        }

        var documentsPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        return string.IsNullOrWhiteSpace(documentsPath)
            ? string.Empty
            : Path.Combine(documentsPath, "ZAVOD", DefaultLocalConfigFileName);
    }
}
