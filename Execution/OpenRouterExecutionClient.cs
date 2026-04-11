using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace zavod.Execution;

public sealed class OpenRouterExecutionClient : IOpenRouterExecutionClient
{
    private readonly OpenRouterConfiguration? _configuration;
    private readonly HttpClient _httpClient;

    public OpenRouterExecutionClient(OpenRouterConfiguration? configuration = null, HttpClient? httpClient = null, bool allowEnvironmentFallback = true)
    {
        _configuration = configuration ?? (allowEnvironmentFallback ? OpenRouterConfiguration.FromEnvironment() : null);
        _httpClient = httpClient ?? new HttpClient();
        if (_configuration is not null)
        {
            _httpClient.Timeout = _configuration.Timeout;
        }
    }

    public OpenRouterExecutionResponse Execute(OpenRouterExecutionRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (_configuration is null)
        {
            return new OpenRouterExecutionResponse(false, string.Empty, request.ModelId ?? string.Empty, null, new OpenRouterDiagnostic("OPENROUTER_CONFIG_MISSING", "OpenRouter configuration is missing required api key or model id."), $"OpenRouter execution failed fast: route={request.RouteId}, reason=config_missing.");
        }

        var modelId = string.IsNullOrWhiteSpace(request.ModelId) ? _configuration.ModelId : request.ModelId.Trim();
        var payload = JsonSerializer.Serialize(new
        {
            model = modelId,
            temperature = request.Temperature,
            messages = new object[]
            {
                new { role = "system", content = request.SystemPrompt },
                new { role = "user", content = request.UserPrompt }
            }
        });

        using var message = new HttpRequestMessage(HttpMethod.Post, $"{_configuration.BaseUrl.TrimEnd('/')}/chat/completions");
        message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _configuration.ApiKey);
        if (!string.IsNullOrWhiteSpace(_configuration.Referer))
        {
            message.Headers.TryAddWithoutValidation("HTTP-Referer", _configuration.Referer);
        }

        if (!string.IsNullOrWhiteSpace(_configuration.Title))
        {
            message.Headers.TryAddWithoutValidation("X-Title", _configuration.Title);
        }

        message.Content = new StringContent(payload, Encoding.UTF8, "application/json");

        HttpResponseMessage response;
        string responseText;
        try
        {
            response = _httpClient.Send(message);
            responseText = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
        }
        catch (TaskCanceledException)
        {
            return Failure(request.RouteId, modelId, "OPENROUTER_TIMEOUT", "OpenRouter request timed out.", null);
        }
        catch (HttpRequestException exception)
        {
            return Failure(request.RouteId, modelId, "OPENROUTER_UNAVAILABLE", $"OpenRouter upstream is unavailable: {exception.Message}", null);
        }

        if (!response.IsSuccessStatusCode)
        {
            var diagnostic = response.StatusCode switch
            {
                HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden => new OpenRouterDiagnostic("OPENROUTER_AUTH_FAILED", "OpenRouter rejected the request credentials."),
                (HttpStatusCode)429 => new OpenRouterDiagnostic("OPENROUTER_RATE_LIMIT", "OpenRouter rate limit was reached."),
                _ => new OpenRouterDiagnostic("OPENROUTER_UPSTREAM_FAILED", $"OpenRouter returned HTTP {(int)response.StatusCode}.")
            };

            return new OpenRouterExecutionResponse(false, string.Empty, modelId, (int)response.StatusCode, diagnostic, $"OpenRouter execution failed: route={request.RouteId}, status={(int)response.StatusCode}, code={diagnostic.Code}.");
        }

        try
        {
            using var document = JsonDocument.Parse(responseText);
            var content = ExtractContent(document.RootElement);
            if (string.IsNullOrWhiteSpace(content))
            {
                return Failure(request.RouteId, modelId, "OPENROUTER_MALFORMED_RESPONSE", "OpenRouter response did not contain assistant content.", (int)response.StatusCode);
            }

            return new OpenRouterExecutionResponse(true, content.Trim(), modelId, (int)response.StatusCode, null, $"OpenRouter execution succeeded: route={request.RouteId}, model={modelId}.");
        }
        catch (Exception) when (response.IsSuccessStatusCode)
        {
            return Failure(request.RouteId, modelId, "OPENROUTER_MALFORMED_RESPONSE", "OpenRouter response was not valid JSON.", (int)response.StatusCode);
        }
    }

    private static string ExtractContent(JsonElement root)
    {
        if (!root.TryGetProperty("choices", out var choices) || choices.ValueKind != JsonValueKind.Array || choices.GetArrayLength() == 0)
        {
            return string.Empty;
        }

        if (!choices[0].TryGetProperty("message", out var message))
        {
            return string.Empty;
        }

        if (!message.TryGetProperty("content", out var contentNode))
        {
            return string.Empty;
        }

        if (contentNode.ValueKind == JsonValueKind.String)
        {
            return contentNode.GetString() ?? string.Empty;
        }

        if (contentNode.ValueKind != JsonValueKind.Array)
        {
            return string.Empty;
        }

        var builder = new StringBuilder();
        foreach (var item in contentNode.EnumerateArray())
        {
            if (item.ValueKind == JsonValueKind.String)
            {
                builder.Append(item.GetString());
                continue;
            }

            if (item.ValueKind == JsonValueKind.Object
                && item.TryGetProperty("text", out var textNode)
                && textNode.ValueKind == JsonValueKind.String)
            {
                builder.Append(textNode.GetString());
            }
        }

        return builder.ToString();
    }

    private static OpenRouterExecutionResponse Failure(string routeId, string modelId, string code, string message, int? statusCode)
    {
        return new OpenRouterExecutionResponse(false, string.Empty, modelId, statusCode, new OpenRouterDiagnostic(code, message), $"OpenRouter execution failed: route={routeId}, code={code}.");
    }
}
