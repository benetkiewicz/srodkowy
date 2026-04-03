using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Srodkowy.Functions.Configuration;
using Srodkowy.Functions.Models;
using Srodkowy.Functions.Persistence;

namespace Srodkowy.Functions.Services;

public sealed class IngestionService(
    IDbContextFactory<SrodkowyDbContext> dbContextFactory,
    FirecrawlClient firecrawlClient,
    IOptions<IngestionOptions> options,
    ILogger<IngestionService> logger)
{
    public async Task<IngestionResult> RunAsync(Guid? sourceId, string triggeredBy, CancellationToken cancellationToken)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);

        var run = new IngestionRun
        {
            Id = Guid.NewGuid(),
            StartedAt = DateTimeOffset.UtcNow,
            Status = "running",
            TriggeredBy = triggeredBy
        };

        dbContext.IngestionRuns.Add(run);
        await dbContext.SaveChangesAsync(cancellationToken);

        try
        {
            var sourcesQuery = dbContext.Sources
                .AsNoTracking()
                .Where(source => source.Active);

            if (sourceId.HasValue)
            {
                sourcesQuery = sourcesQuery.Where(source => source.Id == sourceId.Value);
            }

            var sources = await sourcesQuery.OrderBy(source => source.Name).ToListAsync(cancellationToken);

            if (sourceId.HasValue && sources.Count == 0)
            {
                throw new KeyNotFoundException($"Source '{sourceId}' was not found or is inactive.");
            }

            run.SourceCount = sources.Count;
            await dbContext.SaveChangesAsync(cancellationToken);

            var errors = new List<string>();
            var seenUrls = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var source in sources)
            {
                try
                {
                    var sourceResult = await ProcessSourceAsync(source, seenUrls, cancellationToken);

                    run.DiscoveredLinkCount += sourceResult.DiscoveredLinks;
                    run.CandidateLinkCount += sourceResult.CandidateLinks;

                    if (sourceResult.Articles.Count == 0)
                    {
                        continue;
                    }

                    var candidateUrls = sourceResult.Articles.Select(article => article.Url).ToArray();
                    var existingUrls = await dbContext.Articles
                        .Where(article => candidateUrls.Contains(article.Url))
                        .Select(article => article.Url)
                        .ToListAsync(cancellationToken);

                    var existingUrlSet = existingUrls.ToHashSet(StringComparer.OrdinalIgnoreCase);
                    var newArticles = sourceResult.Articles
                        .Where(article => !existingUrlSet.Contains(article.Url))
                        .ToArray();

                    if (newArticles.Length > 0)
                    {
                        dbContext.Articles.AddRange(newArticles);
                        run.ArticleCount += newArticles.Length;
                        await dbContext.SaveChangesAsync(cancellationToken);
                    }
                }
                catch (Exception exception)
                {
                    logger.LogError(exception, "Ingestion failed for source {SourceName}", source.Name);
                    errors.Add($"{source.Name}: {exception.Message}");
                }
            }

            run.Status = errors.Count == 0
                ? "completed"
                : run.ArticleCount > 0 ? "completed_with_errors" : "failed";
            run.ErrorSummary = errors.Count == 0 ? null : string.Join(Environment.NewLine, errors.Take(10));
            run.CompletedAt = DateTimeOffset.UtcNow;

            await dbContext.SaveChangesAsync(cancellationToken);

            return new IngestionResult(
                run.Id,
                run.Status,
                run.SourceCount,
                run.DiscoveredLinkCount,
                run.CandidateLinkCount,
                run.ArticleCount,
                errors);
        }
        catch
        {
            run.Status = "failed";
            run.CompletedAt = DateTimeOffset.UtcNow;
            await dbContext.SaveChangesAsync(cancellationToken);
            throw;
        }
    }

    private async Task<SourceIngestionResult> ProcessSourceAsync(
        Source source,
        ISet<string> seenUrls,
        CancellationToken cancellationToken)
    {
        var discoveredLinks = await firecrawlClient.GetLinksAsync(source.DiscoveryUrl, cancellationToken);

        var candidateLinks = discoveredLinks
            .Select(UrlNormalizer.Normalize)
            .Where(url => UrlNormalizer.IsCandidateArticleUrl(source.BaseUrl, url))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(options.Value.MaxCandidateLinksPerSource)
            .ToArray();

        logger.LogInformation(
            "Source {SourceName}: discovered {DiscoveredLinks} links, {CandidateLinks} candidates",
            source.Name,
            discoveredLinks.Count,
            candidateLinks.Length);

        var articles = new List<Article>();

        foreach (var candidateLink in candidateLinks.Take(options.Value.MaxArticlesPerSource))
        {
            try
            {
                var page = await firecrawlClient.ScrapeArticleAsync(candidateLink, cancellationToken);
                var normalizedUrl = UrlNormalizer.Normalize(page.Url);

                if (!UrlNormalizer.IsCandidateArticleUrl(source.BaseUrl, normalizedUrl) || !seenUrls.Add(normalizedUrl))
                {
                    continue;
                }

                var plainText = ArticleContentConverter.ToPlainText(page.Markdown);

                if (plainText.Length < options.Value.MinContentLength)
                {
                    continue;
                }

                var title = string.IsNullOrWhiteSpace(page.Title)
                    ? ArticleContentConverter.ExtractTitle(page.Markdown)
                    : page.Title.Trim();

                if (string.IsNullOrWhiteSpace(title))
                {
                    continue;
                }

                articles.Add(new Article
                {
                    Id = Guid.NewGuid(),
                    SourceId = source.Id,
                    Url = normalizedUrl,
                    Title = title,
                    ContentMarkdown = page.Markdown.Trim(),
                    ContentText = plainText,
                    PublishedAt = page.PublishedAt,
                    ScrapedAt = DateTimeOffset.UtcNow,
                    MetadataJson = page.MetadataJson
                });
            }
            catch (Exception exception)
            {
                logger.LogWarning(exception, "Failed to scrape article candidate {CandidateLink} for source {SourceName}", candidateLink, source.Name);
            }
        }

        return new SourceIngestionResult(discoveredLinks.Count, candidateLinks.Length, articles);
    }

    public sealed record IngestionResult(
        Guid RunId,
        string Status,
        int SourceCount,
        int DiscoveredLinkCount,
        int CandidateLinkCount,
        int ArticleCount,
        IReadOnlyList<string> Errors);

    private sealed record SourceIngestionResult(
        int DiscoveredLinks,
        int CandidateLinks,
        IReadOnlyList<Article> Articles);
}
