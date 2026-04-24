using System;
using System.IO;
using System.Text.Json;

namespace zavod.Execution;

public sealed record OpenRouterConfiguration(
    string ApiKey,
    string ModelId,
    string BaseUrl,
    TimeSpan Timeout,
    string? Referer,
    string? Title,
    string Source)
{
    public const string DefaultImportModelId = "openai/gpt-4.1-nano";
    public const string DefaultBaseUrl = "https://openrouter.ai/api/v1";
    public const string DefaultConfigFileName = "openrouter.local.json";

    public static OpenRouterConfiguration? FromEnvironment()
    {
        var fileConfiguration = TryLoadFromFile();

        var apiKey = FirstNonEmpty(
            Environment.GetEnvironmentVariable("OPENROUTER_API_KEY"),
            fileConfiguration?.ApiKey);
        var modelId = FirstNonEmpty(
            Environment.GetEnvironmentVariable("OPENROUTER_MODEL"),
            fileConfiguration?.ModelId);
        var baseUrl = FirstNonEmpty(
            Environment.GetEnvironmentVariable("OPENROUTER_BASE_URL"),
            fileConfiguration?.BaseUrl);
        var referer = FirstNonEmpty(
            Environment.GetEnvironmentVariable("OPENROUTER_REFERER"),
            fileConfiguration?.Referer);
        var title = FirstNonEmpty(
            Environment.GetEnvironmentVariable("OPENROUTER_TITLE"),
            fileConfiguration?.Title);

        if (string.IsNullOrWhiteSpace(apiKey))
        {
            return null;
        }

        var source = fileConfiguration is null ? "environment" : fileConfiguration.Source;
        if (!string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("OPENROUTER_API_KEY")))
        {
            source = fileConfiguration is null ? "environment" : "environment+file";
        }

        return new OpenRouterConfiguration(
            apiKey,
            string.IsNullOrWhiteSpace(modelId) ? DefaultImportModelId : modelId,
            string.IsNullOrWhiteSpace(baseUrl) ? DefaultBaseUrl : baseUrl,
            ResolveTimeout(fileConfiguration),
            string.IsNullOrWhiteSpace(referer) ? null : referer,
            string.IsNullOrWhiteSpace(title) ? null : title,
            source);
    }

    private static OpenRouterConfiguration? TryLoadFromFile()
    {
        var configuredPath = Environment.GetEnvironmentVariable("OPENROUTER_CONFIG_FILE");
        var candidatePath = string.IsNullOrWhiteSpace(configuredPath)
            ? GetDefaultConfigPath()
            : configuredPath.Trim();
        if (string.IsNullOrWhiteSpace(candidatePath) || !File.Exists(candidatePath))
        {
            return null;
        }

        try
        {
            var json = File.ReadAllText(candidatePath);
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                return null;
            }

            var apiKey = ReadOptionalString(root, "apiKey");
            if (string.IsNullOrWhiteSpace(apiKey))
            {
                return null;
            }

            var modelId = ReadOptionalString(root, "modelId");
            var baseUrl = ReadOptionalString(root, "baseUrl");
            var referer = ReadOptionalString(root, "referer");
            var title = ReadOptionalString(root, "title");
            var timeoutSeconds = root.TryGetProperty("timeoutSeconds", out var timeoutNode) && timeoutNode.TryGetInt32(out var parsedTimeout)
                ? Math.Clamp(parsedTimeout, 5, 600)
                : 60;

            return new OpenRouterConfiguration(
                apiKey.Trim(),
                string.IsNullOrWhiteSpace(modelId) ? DefaultImportModelId : modelId.Trim(),
                string.IsNullOrWhiteSpace(baseUrl) ? DefaultBaseUrl : baseUrl.Trim(),
                TimeSpan.FromSeconds(timeoutSeconds),
                string.IsNullOrWhiteSpace(referer) ? null : referer.Trim(),
                string.IsNullOrWhiteSpace(title) ? null : title.Trim(),
                $"file:{Path.GetFullPath(candidatePath)}");
        }
        catch
        {
            return null;
        }
    }

    private static string GetDefaultConfigPath()
    {
        var documentsPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        return string.IsNullOrWhiteSpace(documentsPath)
            ? string.Empty
            : Path.Combine(documentsPath, "ZAVOD", DefaultConfigFileName);
    }

    private static string? ReadOptionalString(JsonElement root, string propertyName)
    {
        if (!root.TryGetProperty(propertyName, out var node))
        {
            return null;
        }

        return node.ValueKind == JsonValueKind.String ? node.GetString() : null;
    }

    private static string? FirstNonEmpty(params string?[] values)
    {
        foreach (var value in values)
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value.Trim();
            }
        }

        return null;
    }

    private static TimeSpan ResolveTimeout(OpenRouterConfiguration? fileConfiguration)
    {
        var timeoutText = Environment.GetEnvironmentVariable("OPENROUTER_TIMEOUT_SECONDS");
        if (int.TryParse(timeoutText, out var parsedTimeout))
        {
            return TimeSpan.FromSeconds(Math.Clamp(parsedTimeout, 5, 600));
        }

        return fileConfiguration?.Timeout ?? TimeSpan.FromSeconds(60);
    }
}
