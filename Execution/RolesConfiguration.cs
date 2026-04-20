using System;
using System.IO;
using System.Text.Json;

namespace zavod.Execution;

public sealed record RoleProfile(
    string Model,
    double Temperature,
    int TimeoutSeconds,
    int MaxTokens);

public sealed record RolesConfiguration(
    RoleProfile Lead,
    RoleProfile Worker,
    RoleProfile Qc,
    string Source)
{
    public const string DefaultConfigRelativePath = @"app\config\roles.json";

    public static RoleProfile DefaultLead { get; } = new("openai/gpt-4.1-mini", 0.3, 60, 800);
    public static RoleProfile DefaultWorker { get; } = new("deepseek/deepseek-chat-v3", 0.2, 120, 2000);
    public static RoleProfile DefaultQc { get; } = new("anthropic/claude-haiku-4.5", 0.0, 45, 800);

    public static RolesConfiguration LoadOrDefault()
    {
        var configuredPath = Environment.GetEnvironmentVariable("ZAVOD_ROLES_CONFIG_FILE");
        var candidatePath = string.IsNullOrWhiteSpace(configuredPath)
            ? Path.Combine(Environment.CurrentDirectory, DefaultConfigRelativePath)
            : configuredPath.Trim();

        if (!File.Exists(candidatePath))
        {
            return new RolesConfiguration(DefaultLead, DefaultWorker, DefaultQc, "defaults");
        }

        try
        {
            var json = File.ReadAllText(candidatePath);
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                return new RolesConfiguration(DefaultLead, DefaultWorker, DefaultQc, "defaults");
            }

            return new RolesConfiguration(
                ReadProfile(root, "lead", DefaultLead),
                ReadProfile(root, "worker", DefaultWorker),
                ReadProfile(root, "qc", DefaultQc),
                $"file:{Path.GetFullPath(candidatePath)}");
        }
        catch
        {
            return new RolesConfiguration(DefaultLead, DefaultWorker, DefaultQc, "defaults");
        }
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
}
