using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Srodkowy.Functions.Configuration;
using Srodkowy.Functions.Models;
using Srodkowy.Functions.Persistence;
using Srodkowy.Functions.Services;

namespace Srodkowy.Functions.Tests;

public sealed class IngestionServiceTests
{
    [Fact]
    public async Task RunAsync_FillsAcceptedArticleBudgetUsingScrapedDiscoveryLinks()
    {
        var sourceId = Guid.NewGuid();
        var source = new Source
        {
            Id = sourceId,
            Name = "Test Source",
            BaseUrl = "https://example.com",
            DiscoveryUrl = "https://example.com/news",
            DiscoveryIncludeTags = "[\"main\"]",
            Camp = "left",
            Active = true
        };

        var handler = new RecordingHttpMessageHandler((request, body, _) =>
        {
            if (request.RequestUri?.AbsolutePath == "/v2/scrape" && body.Contains("\"formats\":[\"links\"]", StringComparison.Ordinal))
            {
                return Task.FromResult(CreateJsonResponse(new
                {
                    success = true,
                    data = new
                    {
                        links = new object[]
                        {
                            "https://example.com/video/not-an-article",
                            "https://example.com/news/failing-scrape",
                            "https://example.com/news/final-short",
                            "https://example.com/news/accepted-first",
                            "https://example.com/news/accepted-second",
                            "https://example.com/news/not-needed"
                        }
                    }
                }));
            }

            if (body.Contains("https://example.com/news/failing-scrape", StringComparison.Ordinal))
            {
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.InternalServerError)
                {
                    Content = JsonContent.Create(new { error = "boom" })
                });
            }

            if (body.Contains("https://example.com/news/final-short", StringComparison.Ordinal))
            {
                return Task.FromResult(CreateScrapeResponse("https://example.com/news/final-short", "Za krotki final", "# Za krotki final\n\nTresc testowa."));
            }

            if (body.Contains("https://example.com/news/accepted-first", StringComparison.Ordinal))
            {
                return Task.FromResult(CreateScrapeResponse(
                    "https://example.com/news/accepted-first",
                    "To jest finalny odpowiednio dlugi tytul pierwszego artykulu",
                    "# To jest finalny odpowiednio dlugi tytul pierwszego artykulu\n\nTo jest tresc pierwszego artykulu."));
            }

            if (body.Contains("https://example.com/news/accepted-second", StringComparison.Ordinal))
            {
                return Task.FromResult(CreateScrapeResponse(
                    "https://example.com/news/accepted-second",
                    "To jest finalny odpowiednio dlugi tytul drugiego artykulu",
                    "# To jest finalny odpowiednio dlugi tytul drugiego artykulu\n\nTo jest tresc drugiego artykulu."));
            }

            throw new InvalidOperationException($"Unexpected scrape request body: {body}");
        });

        var dbContextOptions = CreateDbContextOptions();
        await using var dbContext = CreateDbContext(dbContextOptions);
        dbContext.Database.EnsureCreated();
        dbContext.Sources.Add(source);
        await dbContext.SaveChangesAsync();

        var service = new IngestionService(
            new TestDbContextFactory(dbContextOptions),
            CreateFirecrawlClient(handler),
            Options.Create(new IngestionOptions
            {
                MaxCandidateLinksPerSource = 10,
                MaxArticlesPerSource = 2,
                MinCandidateTitleLength = 40
            }),
            NullLogger<IngestionService>.Instance);

        var result = await service.RunAsync(sourceId, "test", CancellationToken.None);

        result.Status.Should().Be("completed");
        result.DiscoveredLinkCount.Should().Be(6);
        result.CandidateLinkCount.Should().Be(5);
        result.ArticleCount.Should().Be(2);
        handler.RequestBodies.Should().Contain(body => body.Contains("\"includeTags\":[\"main\"]", StringComparison.Ordinal));
        handler.RequestBodies.Should().NotContain(body => body.Contains("not-an-article", StringComparison.Ordinal));
        handler.RequestBodies.Should().NotContain(body => body.Contains("not-needed", StringComparison.Ordinal));
        handler.RequestBodies.Should().Contain(body => body.Contains("failing-scrape", StringComparison.Ordinal));
        handler.RequestBodies.Should().Contain(body => body.Contains("final-short", StringComparison.Ordinal));
        handler.RequestBodies.Should().Contain(body => body.Contains("accepted-first", StringComparison.Ordinal));
        handler.RequestBodies.Should().Contain(body => body.Contains("accepted-second", StringComparison.Ordinal));

        await using var verificationContext = CreateDbContext(dbContextOptions);
        var persistedArticles = await verificationContext.Articles
            .Where(article => article.SourceId == sourceId)
            .OrderBy(article => article.Url)
            .ToListAsync();

        persistedArticles.Should().HaveCount(2);
        persistedArticles.Select(article => article.Url).Should().BeEquivalentTo(
            "https://example.com/news/accepted-first",
            "https://example.com/news/accepted-second");
    }

    private static FirecrawlClient CreateFirecrawlClient(HttpMessageHandler handler)
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

    private static DbContextOptions<SrodkowyDbContext> CreateDbContextOptions()
    {
        return new DbContextOptionsBuilder<SrodkowyDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;
    }

    private static SrodkowyDbContext CreateDbContext(DbContextOptions<SrodkowyDbContext>? options = null)
    {
        options ??= CreateDbContextOptions();

        return new SrodkowyDbContext(options);
    }

    private static HttpResponseMessage CreateJsonResponse<T>(T payload, HttpStatusCode statusCode = HttpStatusCode.OK)
    {
        return new HttpResponseMessage(statusCode)
        {
            Content = JsonContent.Create(payload)
        };
    }

    private static HttpResponseMessage CreateScrapeResponse(string url, string title, string markdown)
    {
        return CreateJsonResponse(new
        {
            success = true,
            data = new
            {
                markdown,
                metadata = new
                {
                    title,
                    sourceURL = url
                }
            }
        });
    }

    private sealed class RecordingHttpMessageHandler(
        Func<HttpRequestMessage, string, CancellationToken, Task<HttpResponseMessage>> responder) : HttpMessageHandler
    {
        public List<string> RequestBodies { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var body = request.Content is null
                ? string.Empty
                : await request.Content.ReadAsStringAsync(cancellationToken);

            RequestBodies.Add(body);
            return await responder(request, body, cancellationToken);
        }
    }

    private sealed class TestDbContextFactory(DbContextOptions<SrodkowyDbContext> options) : IDbContextFactory<SrodkowyDbContext>
    {
        public SrodkowyDbContext CreateDbContext() => new(options);

        public Task<SrodkowyDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(new SrodkowyDbContext(options));
    }
}
