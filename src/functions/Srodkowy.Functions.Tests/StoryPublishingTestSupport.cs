using Microsoft.EntityFrameworkCore;
using Srodkowy.Functions.Models;
using Srodkowy.Functions.Persistence;
using Srodkowy.Functions.Services;

namespace Srodkowy.Functions.Tests;

internal static class StoryPublishingTestSupport
{
    public static DbContextOptions<SrodkowyDbContext> CreateDbContextOptions()
    {
        return new DbContextOptionsBuilder<SrodkowyDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;
    }

    public static SrodkowyDbContext CreateDbContext(DbContextOptions<SrodkowyDbContext> options) => new(options);

    public static Source CreateSource(Guid id, string camp, string name)
    {
        return new Source
        {
            Id = id,
            Name = name,
            BaseUrl = $"https://{id:N}.example.com",
            DiscoveryUrl = $"https://{id:N}.example.com/news",
            Camp = camp,
            Active = true
        };
    }

    public static Article CreateArticle(Guid id, Guid sourceId, string title, string contentText, string? cleanedContentText = null)
    {
        return new Article
        {
            Id = id,
            SourceId = sourceId,
            Url = $"https://example.com/{id:N}",
            Title = title,
            ContentMarkdown = $"# {title}\n\n{contentText}",
            ContentText = contentText,
            CleanedContentText = cleanedContentText ?? contentText,
            CleanupStatus = ArticleCleanupStatus.Completed,
            EmbeddingStatus = ArticleEmbeddingStatus.Completed,
            CleanupFlagsJson = "[]",
            QualityScore = 80,
            ScrapedAt = DateTimeOffset.UtcNow.AddHours(-2),
            PublishedAt = DateTimeOffset.UtcNow.AddHours(-3),
            MetadataJson = "{}"
        };
    }

    public static ClusterRun CreateClusterRun(Guid id)
    {
        return new ClusterRun
        {
            Id = id,
            StartedAt = DateTimeOffset.UtcNow.AddHours(-1),
            CompletedAt = DateTimeOffset.UtcNow,
            Status = "completed",
            TriggeredBy = "test",
            LookbackHours = 72,
            CandidateArticleCount = 2,
            DeduplicatedArticleCount = 2,
            ClusterCount = 1,
            QualifiedClusterCount = 1
        };
    }

    public static CandidateCluster CreateCandidateCluster(Guid id, Guid clusterRunId, Guid representativeArticleId, int rank = 1)
    {
        return new CandidateCluster
        {
            Id = id,
            ClusterRunId = clusterRunId,
            RepresentativeArticleId = representativeArticleId,
            Rank = rank,
            RankScore = 0.9,
            Status = "candidate",
            ArticleCount = 2,
            DistinctSourceCount = 2,
            LeftArticleCount = 1,
            RightArticleCount = 1,
            WindowStartAt = DateTimeOffset.UtcNow.AddHours(-3),
            WindowEndAt = DateTimeOffset.UtcNow.AddHours(-2),
            MeanSimilarity = 0.9,
            NarrativeDivergenceScore = 0.2,
            BalanceScore = 1,
            SelectionVersion = "clustering-v1"
        };
    }

    public static CandidateClusterArticle CreateClusterArticle(CandidateCluster cluster, Article article, string camp, bool isRepresentative)
    {
        return new CandidateClusterArticle
        {
            CandidateClusterId = cluster.Id,
            CandidateCluster = cluster,
            ArticleId = article.Id,
            Article = article,
            SourceId = article.SourceId,
            Camp = camp,
            SimilarityToRepresentative = isRepresentative ? 1 : 0.92,
            IsRepresentative = isRepresentative
        };
    }

    public sealed class TestDbContextFactory(DbContextOptions<SrodkowyDbContext> options) : IDbContextFactory<SrodkowyDbContext>
    {
        public SrodkowyDbContext CreateDbContext() => new(options);

        public Task<SrodkowyDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(new SrodkowyDbContext(options));
    }

    public sealed class FakeStorySynthesisModel(Func<StorySynthesisModelRequest, StorySynthesisModelResponse> responder) : IStorySynthesisModel
    {
        public Task<StorySynthesisModelResponse> SynthesizeAsync(StorySynthesisModelRequest request, CancellationToken cancellationToken) =>
            Task.FromResult(responder(request));
    }
}
