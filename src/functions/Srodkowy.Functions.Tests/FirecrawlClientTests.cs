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
    public async Task GetDiscoveredLinksAsync_ReturnsStructuredLinksFromMapResponse()
    {
        var handler = new RecordingHttpMessageHandler((request, _, _) =>
            Task.FromResult(CreateJsonResponse(new
            {
                success = true,
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
            })));
        var client = CreateClient(handler);

        var links = await client.GetDiscoveredLinksAsync("https://example.com", CancellationToken.None);

        links.Should().HaveCount(2);
        links[0].Url.Should().Be("https://example.com/articles/alpha");
        links[0].Title.Should().Be("Bardzo dlugi tytul artykulu do testu mapowania");
        links[0].Description.Should().Be("Opis linku");
        links[1].Url.Should().Be("https://example.com/articles/beta");
        links[1].Title.Should().BeNull();
        links[1].Description.Should().BeNull();
        handler.RequestPaths.Should().ContainSingle().Which.Should().Be("/v2/map");
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

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var body = request.Content is null
                ? string.Empty
                : await request.Content.ReadAsStringAsync(cancellationToken);

            RequestPaths.Add(request.RequestUri?.AbsolutePath ?? string.Empty);
            return await responder(request, body, cancellationToken);
        }
    }
}
