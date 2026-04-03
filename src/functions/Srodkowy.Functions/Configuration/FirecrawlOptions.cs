namespace Srodkowy.Functions.Configuration;

public sealed class FirecrawlOptions
{
    public const string DefaultBaseUrl = "https://api.firecrawl.dev";

    public const int DefaultRequestsPerMinute = 10;

    public string ApiKey { get; set; } = string.Empty;

    public string BaseUrl { get; set; } = DefaultBaseUrl;

    public int TimeoutSeconds { get; set; } = 60;

    public int RequestsPerMinute { get; set; } = DefaultRequestsPerMinute;
}
