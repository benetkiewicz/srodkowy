using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Polly.Timeout;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Srodkowy.Functions.Configuration;

namespace Srodkowy.Functions.Services;

public sealed class FirecrawlClient(
    HttpClient httpClient,
    IOptions<FirecrawlOptions> options,
    ILogger<FirecrawlClient> logger)
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };

    private static readonly SemaphoreSlim RequestStartGate = new(1, 1);

    private static DateTimeOffset _nextAllowedRequestAtUtc = DateTimeOffset.MinValue;

    public async Task<IReadOnlyList<DiscoveredPageLink>> GetDiscoveredLinksAsync(string url, CancellationToken cancellationToken)
    {
        var payload = await PostAsync("v2/map", new FirecrawlMapRequest(url), cancellationToken);

        var root = payload.TryGetProperty("data", out var data) ? data : payload;

        if (!root.TryGetProperty("links", out var linksElement) || linksElement.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        return linksElement
            .EnumerateArray()
            .Where(element => element.ValueKind == JsonValueKind.Object)
            .Select(element => new DiscoveredPageLink(
                GetString(element, "url") ?? string.Empty,
                GetString(element, "title"),
                GetString(element, "description")))
            .Where(link => !string.IsNullOrWhiteSpace(link.Url))
            .ToArray();
    }

    public async Task<ScrapedArticlePage> ScrapeArticleAsync(string url, CancellationToken cancellationToken)
    {
        var payload = await PostAsync("v2/scrape", new FirecrawlArticleScrapeRequest(
            url,
            ["markdown"],
            onlyMainContent: true,
            waitFor: 1000), cancellationToken);

        var root = payload.TryGetProperty("data", out var data) ? data : payload;
        var markdown = GetString(root, "markdown") ?? string.Empty;
        var metadata = root.TryGetProperty("metadata", out var metadataElement) && metadataElement.ValueKind == JsonValueKind.Object
            ? metadataElement
            : default;

        var title = GetString(metadata, "title")
            ?? GetString(root, "title")
            ?? ArticleContentConverter.ExtractTitle(markdown);

        var canonicalUrl = GetString(metadata, "sourceURL")
            ?? GetString(metadata, "ogUrl")
            ?? GetString(root, "url")
            ?? url;

        return new ScrapedArticlePage(
            Url: canonicalUrl,
            Title: title,
            Markdown: markdown,
            PublishedAt: ParsePublishedAt(metadata),
            MetadataJson: metadata.ValueKind == JsonValueKind.Object ? metadata.GetRawText() : root.GetRawText());
    }

    private async Task<JsonElement> PostAsync<TRequest>(string requestPath, TRequest request, CancellationToken cancellationToken)
        where TRequest : class
    {
        if (string.IsNullOrWhiteSpace(options.Value.ApiKey))
        {
            throw new InvalidOperationException("Missing configuration value 'Firecrawl:ApiKey'.");
        }

        var requestBody = JsonSerializer.Serialize(request, SerializerOptions);

        for (var attempt = 0; attempt < 2; attempt++)
        {
            await WaitForRequestSlotAsync(cancellationToken);

            httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", options.Value.ApiKey);

            try
            {
                using var response = await httpClient.PostAsJsonAsync(requestPath, request, SerializerOptions, cancellationToken);
                var body = await response.Content.ReadAsStringAsync(cancellationToken);

                if (response.StatusCode == System.Net.HttpStatusCode.TooManyRequests && attempt == 0)
                {
                    var retryDelay = GetRetryDelay(response) ?? TimeSpan.FromSeconds(60);
                    logger.LogWarning(
                        "Firecrawl rate limited the request. Retrying after {DelaySeconds}s. Request: {RequestBody}. Response: {Body}",
                        retryDelay.TotalSeconds,
                        requestBody,
                        body);
                    await Task.Delay(retryDelay, cancellationToken);
                    continue;
                }

                if (!response.IsSuccessStatusCode)
                {
                    logger.LogWarning("Firecrawl request failed with status {StatusCode}. Request: {RequestBody}. Response: {Body}", response.StatusCode, requestBody, body);
                    throw new InvalidOperationException($"Firecrawl scrape failed with status {(int)response.StatusCode}. Response: {body}");
                }

                using var document = JsonDocument.Parse(body);

                if (document.RootElement.TryGetProperty("success", out var successElement)
                    && successElement.ValueKind == JsonValueKind.False)
                {
                    throw new InvalidOperationException($"Firecrawl scrape did not succeed. Request: {requestBody}. Response: {body}");
                }

                return document.RootElement.Clone();
            }
            catch (Exception exception) when (attempt == 0 && IsTransient(exception))
            {
                logger.LogWarning(exception, "Firecrawl transient failure on attempt {Attempt}. Retrying request: {RequestBody}", attempt + 1, requestBody);
            }
        }

        throw new InvalidOperationException($"Firecrawl scrape failed after retry. Request: {requestBody}");
    }

    private async Task WaitForRequestSlotAsync(CancellationToken cancellationToken)
    {
        await RequestStartGate.WaitAsync(cancellationToken);

        try
        {
            var minSpacing = TimeSpan.FromMinutes(1d / Math.Max(1, options.Value.RequestsPerMinute));
            var now = DateTimeOffset.UtcNow;
            var delay = _nextAllowedRequestAtUtc - now;

            if (delay > TimeSpan.Zero)
            {
                await Task.Delay(delay, cancellationToken);
                now = DateTimeOffset.UtcNow;
            }

            _nextAllowedRequestAtUtc = now.Add(minSpacing);
        }
        finally
        {
            RequestStartGate.Release();
        }
    }

    private static bool IsTransient(Exception exception) =>
        exception is HttpRequestException or TaskCanceledException or TimeoutRejectedException;

    private static TimeSpan? GetRetryDelay(HttpResponseMessage response)
    {
        var retryAfter = response.Headers.RetryAfter;

        if (retryAfter?.Delta is { } delta)
        {
            return delta;
        }

        if (retryAfter?.Date is { } date)
        {
            var delay = date - DateTimeOffset.UtcNow;
            return delay > TimeSpan.Zero ? delay : TimeSpan.Zero;
        }

        return null;
    }

    private static DateTimeOffset? ParsePublishedAt(JsonElement metadata)
    {
        var candidates = new[]
        {
            GetString(metadata, "publishedTime"),
            GetString(metadata, "article:published_time"),
            GetString(metadata, "published_at"),
            GetString(metadata, "datePublished")
        };

        foreach (var candidate in candidates)
        {
            if (DateTimeOffset.TryParse(candidate, out var publishedAt))
            {
                return publishedAt;
            }
        }

        return null;
    }

    private static string? GetString(JsonElement element, string propertyName)
    {
        if (element.ValueKind == JsonValueKind.Object
            && element.TryGetProperty(propertyName, out var property)
            && property.ValueKind == JsonValueKind.String)
        {
            return property.GetString();
        }

        return null;
    }

    private sealed record FirecrawlMapRequest(
        string Url);

    private sealed record FirecrawlArticleScrapeRequest(
        string Url,
        IReadOnlyList<string> Formats,
        bool? onlyMainContent,
        int? waitFor);

    public sealed record ScrapedArticlePage(
        string Url,
        string Title,
        string Markdown,
        DateTimeOffset? PublishedAt,
        string MetadataJson);

    public sealed record DiscoveredPageLink(
        string Url,
        string? Title,
        string? Description);
}
