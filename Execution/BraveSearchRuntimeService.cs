using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading.Tasks;

namespace zavod.Execution;

public sealed class BraveSearchRuntimeService
{
    private readonly BraveSearchConfiguration? _configuration;
    private readonly HttpClient _httpClient;
    private readonly NetworkBrokerService _networkBroker;

    public BraveSearchRuntimeService(
        BraveSearchConfiguration? configuration = null,
        HttpClient? httpClient = null,
        NetworkBrokerService? networkBroker = null,
        bool allowEnvironmentFallback = true)
    {
        _configuration = configuration ?? (allowEnvironmentFallback ? BraveSearchConfiguration.FromEnvironment() : null);
        _httpClient = httpClient ?? new HttpClient();
        _networkBroker = (networkBroker ?? RuntimeSubstrateBuilder.Build(RuntimeProfileCatalog.GetDefaultFor(RuntimeFamily.Container)).NetworkBroker)
            .Normalize();
        if (_configuration is not null)
        {
            _httpClient.Timeout = _configuration.Timeout;
        }
    }

    public BraveSearchResult Search(string query, int limit)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(query);
        if (limit <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(limit), limit, "Search limit must be positive.");
        }

        if (!AllowsBrokeredNetwork(_networkBroker))
        {
            return Failure(
                "BRAVE_BROKER_DENIED",
                $"Brave Search is denied by network broker policy: access={_networkBroker.AccessMode}.",
                null);
        }

        if (_configuration is null)
        {
            return new BraveSearchResult(
                false,
                Array.Empty<BraveSearchItem>(),
                new BraveSearchDiagnostic("BRAVE_CONFIG_MISSING", "Brave Search configuration is missing required api key."),
                null,
                $"Brave search failed fast: config_missing, broker={_networkBroker.AccessMode}.");
        }

        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            $"{_configuration.BaseUrl.TrimEnd('/')}/web/search?q={Uri.EscapeDataString(query.Trim())}&count={limit}");
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        request.Headers.TryAddWithoutValidation("X-Subscription-Token", _configuration.ApiKey);

        HttpResponseMessage response;
        string responseText;
        try
        {
            response = _httpClient.Send(request);
            responseText = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
        }
        catch (TaskCanceledException)
        {
            return Failure("BRAVE_TIMEOUT", "Brave Search request timed out.", null);
        }
        catch (HttpRequestException exception)
        {
            return Failure("BRAVE_UNAVAILABLE", $"Brave Search upstream is unavailable: {exception.Message}", null);
        }

        if (!response.IsSuccessStatusCode)
        {
            var diagnostic = response.StatusCode switch
            {
                HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden => new BraveSearchDiagnostic("BRAVE_AUTH_FAILED", "Brave Search rejected the request credentials."),
                (HttpStatusCode)429 => new BraveSearchDiagnostic("BRAVE_RATE_LIMIT", "Brave Search rate limit was reached."),
                _ => new BraveSearchDiagnostic("BRAVE_UPSTREAM_FAILED", $"Brave Search returned HTTP {(int)response.StatusCode}.")
            };

            return new BraveSearchResult(false, Array.Empty<BraveSearchItem>(), diagnostic, (int)response.StatusCode, $"Brave search failed: status={(int)response.StatusCode}, code={diagnostic.Code}.");
        }

        try
        {
            using var document = JsonDocument.Parse(responseText);
            if (!document.RootElement.TryGetProperty("web", out var webNode)
                || !webNode.TryGetProperty("results", out var resultsNode)
                || resultsNode.ValueKind != JsonValueKind.Array)
            {
                return Failure("BRAVE_MALFORMED_RESPONSE", "Brave Search response did not contain web.results.", (int)response.StatusCode);
            }

            var items = new List<BraveSearchItem>();
            foreach (var item in resultsNode.EnumerateArray())
            {
                var title = item.TryGetProperty("title", out var titleNode) ? titleNode.GetString() ?? string.Empty : string.Empty;
                var url = item.TryGetProperty("url", out var urlNode) ? urlNode.GetString() ?? string.Empty : string.Empty;
                var snippet = item.TryGetProperty("description", out var descNode) ? descNode.GetString() ?? string.Empty : string.Empty;
                if (string.IsNullOrWhiteSpace(url))
                {
                    continue;
                }

                items.Add(new BraveSearchItem(title.Trim(), url.Trim(), snippet.Trim()));
            }

            return new BraveSearchResult(
                true,
                items,
                null,
                (int)response.StatusCode,
                $"Brave search succeeded: results={items.Count}, broker={_networkBroker.AccessMode}.");
        }
        catch (JsonException)
        {
            return Failure("BRAVE_MALFORMED_RESPONSE", "Brave Search response was not valid JSON.", (int)response.StatusCode);
        }
    }

    private static BraveSearchResult Failure(string code, string message, int? statusCode)
    {
        return new BraveSearchResult(false, Array.Empty<BraveSearchItem>(), new BraveSearchDiagnostic(code, message), statusCode, $"Brave search failed: code={code}.");
    }

    private static bool AllowsBrokeredNetwork(NetworkBrokerService networkBroker)
    {
        return networkBroker.AccessMode is RuntimeAccessMode.BrokeredAllowlist or RuntimeAccessMode.TrustedHostEscape;
    }
}
