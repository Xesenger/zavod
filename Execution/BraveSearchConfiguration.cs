using System;

namespace zavod.Execution;

public sealed record BraveSearchConfiguration(
    string ApiKey,
    string BaseUrl,
    TimeSpan Timeout)
{
    public static BraveSearchConfiguration? FromEnvironment()
    {
        var apiKey = Environment.GetEnvironmentVariable("BRAVE_SEARCH_API_KEY")?.Trim();
        var baseUrl = Environment.GetEnvironmentVariable("BRAVE_SEARCH_BASE_URL")?.Trim();

        if (string.IsNullOrWhiteSpace(apiKey))
        {
            return null;
        }

        return new BraveSearchConfiguration(
            apiKey,
            string.IsNullOrWhiteSpace(baseUrl) ? "https://api.search.brave.com/res/v1" : baseUrl,
            TimeSpan.FromSeconds(20));
    }
}
