using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Srodkowy.Functions.Configuration;
using Srodkowy.Functions.Services;

namespace Srodkowy.Functions.Tests;

public sealed class FirecrawlClientTests
{
    [Fact]
    public async Task GetDiscoveredLinksAsync_UsesScrapeLinksAndParsesObjectLinks()
    {
        var handler = new RecordingHttpMessageHandler((request, _, _) =>
            Task.FromResult(CreateJsonResponse(new
            {
                success = true,
                data = new
                {
                    links = new object[]
                    {
                        new
                        {
                            url = "https://example.com/articles/alpha",
                            title = "Bardzo dlugi tytul artykulu do testu mapowania",
                            description = "Opis linku"
                        },
                        new
                        {
                            url = "https://example.com/articles/beta"
                        }
                    }
                }
            })));
        var client = CreateClient(handler);

        var links = await client.GetDiscoveredLinksAsync(
            "https://example.com/news",
            ["main"],
            ["nav"],
            CancellationToken.None);

        links.Should().HaveCount(2);
        links[0].Url.Should().Be("https://example.com/articles/alpha");
        links[0].Title.Should().Be("Bardzo dlugi tytul artykulu do testu mapowania");
        links[0].Description.Should().Be("Opis linku");
        links[1].Url.Should().Be("https://example.com/articles/beta");
        links[1].Title.Should().BeNull();
        links[1].Description.Should().BeNull();
        handler.RequestPaths.Should().ContainSingle().Which.Should().Be("/v2/scrape");
        handler.RequestBodies.Should().ContainSingle();
        handler.RequestBodies[0].Should().Contain("\"formats\":[\"links\"]");
        handler.RequestBodies[0].Should().Contain("\"includeTags\":[\"main\"]");
        handler.RequestBodies[0].Should().Contain("\"excludeTags\":[\"nav\"]");
        handler.RequestBodies[0].Should().Contain("\"onlyMainContent\":false");
    }

    [Fact]
    public async Task GetDiscoveredLinksAsync_ParsesStringLinksFromArrayResponse()
    {
        var handler = new RecordingHttpMessageHandler((request, _, _) =>
            Task.FromResult(CreateJsonResponse(new
            {
                success = true,
                data = new object[]
                {
                    new
                    {
                        links = new object[]
                        {
                            "https://example.com/articles/alpha",
                            "https://example.com/articles/alpha",
                            "https://example.com/articles/beta"
                        }
                    }
                }
            })));
        var client = CreateClient(handler);

        var links = await client.GetDiscoveredLinksAsync(
            "https://example.com/news",
            null,
            null,
            CancellationToken.None);

        links.Should().HaveCount(2);
        links.Select(link => link.Url).Should().BeEquivalentTo(
            "https://example.com/articles/alpha",
            "https://example.com/articles/beta");
    }

    private static FirecrawlClient CreateClient(HttpMessageHandler handler)
    {
        var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://api.firecrawl.dev/")
        };

        return new FirecrawlClient(
            httpClient,
            Options.Create(new FirecrawlOptions
            {
                ApiKey = "test-key",
                BaseUrl = "https://api.firecrawl.dev",
                RequestsPerMinute = 60000,
                TimeoutSeconds = 5
            }),
            NullLogger<FirecrawlClient>.Instance);
    }

    private static HttpResponseMessage CreateJsonResponse<T>(T payload, HttpStatusCode statusCode = HttpStatusCode.OK)
    {
        return new HttpResponseMessage(statusCode)
        {
            Content = JsonContent.Create(payload)
        };
    }

    private sealed class RecordingHttpMessageHandler(
        Func<HttpRequestMessage, string, CancellationToken, Task<HttpResponseMessage>> responder) : HttpMessageHandler
    {
        public List<string> RequestPaths { get; } = [];

        public List<string> RequestBodies { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var body = request.Content is null
                ? string.Empty
                : await request.Content.ReadAsStringAsync(cancellationToken);

            RequestPaths.Add(request.RequestUri?.AbsolutePath ?? string.Empty);
            RequestBodies.Add(body);
            return await responder(request, body, cancellationToken);
        }
    }
}
