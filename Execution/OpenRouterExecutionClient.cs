using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.IO;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace zavod.Execution;

public sealed class OpenRouterExecutionClient : IOpenRouterStreamingExecutionClient
{
    private static readonly JsonSerializerOptions PayloadJsonOptions = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    private readonly OpenRouterConfiguration? _configuration;
    private readonly HttpClient _httpClient;

    public OpenRouterExecutionClient(OpenRouterConfiguration? configuration = null, HttpClient? httpClient = null, bool allowEnvironmentFallback = true)
    {
        _configuration = configuration ?? (allowEnvironmentFallback ? OpenRouterConfiguration.FromEnvironment() : null);
        if (httpClient is not null)
        {
            _httpClient = httpClient;
        }
        else
        {
            _httpClient = new HttpClient();
            if (_configuration is not null)
            {
                _httpClient.Timeout = _configuration.Timeout;
            }
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
        var payload = JsonSerializer.Serialize(BuildPayloadObject(request, modelId, stream: false), PayloadJsonOptions);

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

    public OpenRouterExecutionResponse ExecuteStreaming(OpenRouterExecutionRequest request, Action<string> onContentDelta)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(onContentDelta);

        if (_configuration is null)
        {
            return new OpenRouterExecutionResponse(false, string.Empty, request.ModelId ?? string.Empty, null, new OpenRouterDiagnostic("OPENROUTER_CONFIG_MISSING", "OpenRouter configuration is missing required api key or model id."), $"OpenRouter execution failed fast: route={request.RouteId}, reason=config_missing.");
        }

        var modelId = string.IsNullOrWhiteSpace(request.ModelId) ? _configuration.ModelId : request.ModelId.Trim();
        var payload = JsonSerializer.Serialize(BuildPayloadObject(request, modelId, stream: true), PayloadJsonOptions);

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
        try
        {
            response = _httpClient.SendAsync(message, HttpCompletionOption.ResponseHeadersRead).GetAwaiter().GetResult();
        }
        catch (TaskCanceledException)
        {
            return Failure(request.RouteId, modelId, "OPENROUTER_TIMEOUT", "OpenRouter request timed out.", null);
        }
        catch (HttpRequestException exception)
        {
            return Failure(request.RouteId, modelId, "OPENROUTER_UNAVAILABLE", $"OpenRouter upstream is unavailable: {exception.Message}", null);
        }

        using (response)
        {
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

            var builder = new StringBuilder();
            try
            {
                using var stream = response.Content.ReadAsStreamAsync().GetAwaiter().GetResult();
                using var reader = new StreamReader(stream, Encoding.UTF8);
                while (reader.ReadLine() is { } line)
                {
                    var payloadLine = line.Trim();
                    if (payloadLine.Length == 0)
                    {
                        continue;
                    }

                    if (!payloadLine.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    var data = payloadLine[5..].Trim();
                    if (string.Equals(data, "[DONE]", StringComparison.Ordinal))
                    {
                        break;
                    }

                    using var document = JsonDocument.Parse(data);
                    var delta = ExtractStreamingDelta(document.RootElement);
                    if (string.IsNullOrEmpty(delta))
                    {
                        continue;
                    }

                    builder.Append(delta);
                    onContentDelta(delta);
                }
            }
            catch (TaskCanceledException)
            {
                return Failure(request.RouteId, modelId, "OPENROUTER_TIMEOUT", "OpenRouter streaming request timed out.", (int)response.StatusCode);
            }
            catch (Exception)
            {
                return Failure(request.RouteId, modelId, "OPENROUTER_MALFORMED_RESPONSE", "OpenRouter streaming response was not valid SSE JSON.", (int)response.StatusCode);
            }

            var content = builder.ToString();
            if (string.IsNullOrWhiteSpace(content))
            {
                return Failure(request.RouteId, modelId, "OPENROUTER_MALFORMED_RESPONSE", "OpenRouter streaming response did not contain assistant content.", (int)response.StatusCode);
            }

            return new OpenRouterExecutionResponse(true, content.Trim(), modelId, (int)response.StatusCode, null, $"OpenRouter streaming execution succeeded: route={request.RouteId}, model={modelId}.");
        }
    }

    private static Dictionary<string, object?> BuildPayloadObject(OpenRouterExecutionRequest request, string modelId, bool stream)
    {
        var userPrompt = ConversationAttachmentPromptBuilder.BuildUserPrompt(request.UserPrompt, request.Attachments);
        var payloadObject = new Dictionary<string, object?>
        {
            ["model"] = modelId,
            ["temperature"] = request.Temperature,
            ["messages"] = new object[]
            {
                new { role = "system", content = request.SystemPrompt },
                new { role = "user", content = userPrompt }
            }
        };
        if (stream)
        {
            payloadObject["stream"] = true;
        }

        if (request.MaxTokens is int maxTokensValue && maxTokensValue > 0)
        {
            payloadObject["max_tokens"] = maxTokensValue;
        }

        if (request.ResponseFormatJsonObject)
        {
            payloadObject["response_format"] = new { type = "json_object" };
        }

        if (!string.IsNullOrWhiteSpace(request.ReasoningEffort))
        {
            payloadObject["reasoning"] = new { effort = request.ReasoningEffort.Trim() };
        }

        return payloadObject;
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

        return ExtractContentNode(contentNode);
    }

    private static string ExtractStreamingDelta(JsonElement root)
    {
        if (!root.TryGetProperty("choices", out var choices) || choices.ValueKind != JsonValueKind.Array || choices.GetArrayLength() == 0)
        {
            return string.Empty;
        }

        var choice = choices[0];
        if (choice.TryGetProperty("delta", out var delta)
            && delta.TryGetProperty("content", out var deltaContent))
        {
            return ExtractContentNode(deltaContent);
        }

        if (choice.TryGetProperty("message", out var message)
            && message.TryGetProperty("content", out var messageContent))
        {
            return ExtractContentNode(messageContent);
        }

        if (choice.TryGetProperty("text", out var textNode) && textNode.ValueKind == JsonValueKind.String)
        {
            return textNode.GetString() ?? string.Empty;
        }

        return string.Empty;
    }

    private static string ExtractContentNode(JsonElement contentNode)
    {
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
