using FluentAssertions;
using Microsoft.Data.SqlTypes;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Srodkowy.Functions.Configuration;
using Srodkowy.Functions.Models;
using Srodkowy.Functions.Persistence;
using Srodkowy.Functions.Services;

namespace Srodkowy.Functions.Tests;

public sealed class CandidateClusteringServiceTests
{
    [Fact]
    public async Task RunAsync_persists_cross_camp_cluster()
    {
        var leftSourceId = Guid.NewGuid();
        var rightSourceId = Guid.NewGuid();
        var dbContextOptions = CreateDbContextOptions();

        await using (var seedContext = CreateDbContext(dbContextOptions))
        {
            seedContext.Database.EnsureCreated();
            seedContext.Sources.AddRange(
                CreateSource(leftSourceId, SourceCamp.Left, "Lewy 1"),
                CreateSource(rightSourceId, SourceCamp.Right, "Prawy 1"));
            seedContext.Articles.AddRange(
                CreateArticle(leftSourceId, [1f, 0f, 0f]),
                CreateArticle(rightSourceId, [0.99f, 0.01f, 0f]));
            await seedContext.SaveChangesAsync();
        }

        var service = CreateService(dbContextOptions);

        var result = await service.RunAsync("test", null, CancellationToken.None);

        result.CandidateArticleCount.Should().Be(2);
        result.DeduplicatedArticleCount.Should().Be(2);
        result.ClusterCount.Should().Be(1);
        result.QualifiedClusterCount.Should().Be(1);
        result.QualifiedClusterIds.Should().HaveCount(1);

        await using var verificationContext = CreateDbContext(dbContextOptions);
        var persistedCluster = await verificationContext.CandidateClusters
            .Include(cluster => cluster.Articles)
            .SingleAsync();

        persistedCluster.Status.Should().Be("candidate");
        persistedCluster.SelectionVersion.Should().Be("clustering-v1");
        persistedCluster.ArticleCount.Should().Be(2);
        persistedCluster.DistinctSourceCount.Should().Be(2);
        persistedCluster.LeftArticleCount.Should().Be(1);
        persistedCluster.RightArticleCount.Should().Be(1);
        persistedCluster.Articles.Should().HaveCount(2);
        persistedCluster.Articles.Count(article => article.IsRepresentative).Should().Be(1);
        (await verificationContext.ClusterRuns.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task RunAsync_rejects_same_camp_clusters()
    {
        var leftSourceOneId = Guid.NewGuid();
        var leftSourceTwoId = Guid.NewGuid();
        var dbContextOptions = CreateDbContextOptions();

        await using (var seedContext = CreateDbContext(dbContextOptions))
        {
            seedContext.Database.EnsureCreated();
            seedContext.Sources.AddRange(
                CreateSource(leftSourceOneId, SourceCamp.Left, "Lewy 1"),
                CreateSource(leftSourceTwoId, SourceCamp.Left, "Lewy 2"));
            seedContext.Articles.AddRange(
                CreateArticle(leftSourceOneId, [1f, 0f, 0f]),
                CreateArticle(leftSourceTwoId, [0.99f, 0.01f, 0f]));
            await seedContext.SaveChangesAsync();
        }

        var service = CreateService(dbContextOptions);

        var result = await service.RunAsync("test", null, CancellationToken.None);

        result.ClusterCount.Should().Be(1);
        result.QualifiedClusterCount.Should().Be(0);

        await using var verificationContext = CreateDbContext(dbContextOptions);
        (await verificationContext.CandidateClusters.CountAsync()).Should().Be(0);
        (await verificationContext.ClusterRuns.SingleAsync()).QualifiedClusterCount.Should().Be(0);
    }

    [Fact]
    public async Task RunAsync_deduplicates_same_source_near_duplicates()
    {
        var leftSourceId = Guid.NewGuid();
        var rightSourceId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        var dbContextOptions = CreateDbContextOptions();

        await using (var seedContext = CreateDbContext(dbContextOptions))
        {
            seedContext.Database.EnsureCreated();
            seedContext.Sources.AddRange(
                CreateSource(leftSourceId, SourceCamp.Left, "Lewy 1"),
                CreateSource(rightSourceId, SourceCamp.Right, "Prawy 1"));
            seedContext.Articles.AddRange(
                CreateArticle(leftSourceId, [1f, 0f, 0f], qualityScore: 80, publishedAt: now.AddHours(-2)),
                CreateArticle(leftSourceId, [0.995f, 0.005f, 0f], qualityScore: 60, publishedAt: now.AddHours(-1)),
                CreateArticle(rightSourceId, [0.99f, 0.01f, 0f], qualityScore: 70, publishedAt: now.AddHours(-2)));
            await seedContext.SaveChangesAsync();
        }

        var service = CreateService(dbContextOptions);

        var result = await service.RunAsync("test", null, CancellationToken.None);

        result.CandidateArticleCount.Should().Be(3);
        result.DeduplicatedArticleCount.Should().Be(2);
        result.QualifiedClusterCount.Should().Be(1);

        await using var verificationContext = CreateDbContext(dbContextOptions);
        var persistedCluster = await verificationContext.CandidateClusters.SingleAsync();
        persistedCluster.ArticleCount.Should().Be(2);
    }

    [Fact]
    public async Task RunAsync_excludes_needs_review_articles_and_skips_persistence_for_dry_run()
    {
        var leftReviewSourceId = Guid.NewGuid();
        var leftGoodSourceId = Guid.NewGuid();
        var rightSourceId = Guid.NewGuid();
        var dbContextOptions = CreateDbContextOptions();

        await using (var seedContext = CreateDbContext(dbContextOptions))
        {
            seedContext.Database.EnsureCreated();
            seedContext.Sources.AddRange(
                CreateSource(leftReviewSourceId, SourceCamp.Left, "Lewy review"),
                CreateSource(leftGoodSourceId, SourceCamp.Left, "Lewy dobry"),
                CreateSource(rightSourceId, SourceCamp.Right, "Prawy 1"));
            seedContext.Articles.AddRange(
                CreateArticle(leftReviewSourceId, [1f, 0f, 0f], needsReview: true),
                CreateArticle(leftGoodSourceId, [0.995f, 0.005f, 0f]),
                CreateArticle(rightSourceId, [0.99f, 0.01f, 0f]));
            await seedContext.SaveChangesAsync();
        }

        var service = CreateService(dbContextOptions);

        var result = await service.RunAsync(
            "test",
            new CandidateClusteringService.ClusteringRunRequest(DryRun: true),
            CancellationToken.None);

        result.CandidateArticleCount.Should().Be(2);
        result.QualifiedClusterCount.Should().Be(1);
        result.QualifiedClusterIds.Should().BeEmpty();

        await using var verificationContext = CreateDbContext(dbContextOptions);
        (await verificationContext.ClusterRuns.CountAsync()).Should().Be(0);
        (await verificationContext.CandidateClusters.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task RunAsync_ranks_larger_balanced_cluster_ahead_of_smaller_cluster()
    {
        var dbContextOptions = CreateDbContextOptions();
        var leftOneId = Guid.NewGuid();
        var leftTwoId = Guid.NewGuid();
        var rightOneId = Guid.NewGuid();
        var rightTwoId = Guid.NewGuid();
        var rightThreeId = Guid.NewGuid();
        var leftThreeId = Guid.NewGuid();

        await using (var seedContext = CreateDbContext(dbContextOptions))
        {
            seedContext.Database.EnsureCreated();
            seedContext.Sources.AddRange(
                CreateSource(leftOneId, SourceCamp.Left, "Lewy 1"),
                CreateSource(leftTwoId, SourceCamp.Left, "Lewy 2"),
                CreateSource(rightOneId, SourceCamp.Right, "Prawy 1"),
                CreateSource(rightTwoId, SourceCamp.Right, "Prawy 2"),
                CreateSource(rightThreeId, SourceCamp.Right, "Prawy 3"),
                CreateSource(leftThreeId, SourceCamp.Left, "Lewy 3"));
            seedContext.Articles.AddRange(
                CreateArticle(leftOneId, [1f, 0f, 0f]),
                CreateArticle(leftTwoId, [0.98f, 0.02f, 0f]),
                CreateArticle(rightOneId, [0.99f, 0.01f, 0f]),
                CreateArticle(rightTwoId, [0.97f, 0.03f, 0f]),
                CreateArticle(rightThreeId, [0f, 1f, 0f]),
                CreateArticle(leftThreeId, [0.02f, 0.98f, 0f]));
            await seedContext.SaveChangesAsync();
        }

        var service = CreateService(dbContextOptions);

        var result = await service.RunAsync("test", null, CancellationToken.None);

        result.QualifiedClusterCount.Should().Be(2);

        await using var verificationContext = CreateDbContext(dbContextOptions);
        var clusters = await verificationContext.CandidateClusters
            .OrderBy(cluster => cluster.Rank)
            .ToListAsync();

        clusters.Should().HaveCount(2);
        clusters[0].Rank.Should().Be(1);
        clusters[0].DistinctSourceCount.Should().Be(4);
        clusters[0].ArticleCount.Should().Be(4);
        clusters[1].Rank.Should().Be(2);
        clusters[1].DistinctSourceCount.Should().Be(2);
        clusters[1].ArticleCount.Should().Be(2);
    }

    private static CandidateClusteringService CreateService(DbContextOptions<SrodkowyDbContext> dbContextOptions, ClusteringOptions? options = null)
    {
        return new CandidateClusteringService(
            new TestDbContextFactory(dbContextOptions),
            Options.Create(options ?? new ClusteringOptions()),
            NullLogger<CandidateClusteringService>.Instance);
    }

    private static Source CreateSource(Guid id, string camp, string name)
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

    private static Article CreateArticle(
        Guid sourceId,
        float[] embedding,
        int qualityScore = 70,
        bool needsReview = false,
        DateTimeOffset? publishedAt = null)
    {
        var timestamp = publishedAt ?? DateTimeOffset.UtcNow.AddHours(-2);

        return new Article
        {
            Id = Guid.NewGuid(),
            SourceId = sourceId,
            Url = $"https://example.com/{Guid.NewGuid():N}",
            Title = "Tytul testowy artykulu",
            ContentMarkdown = "# Tytul\n\nTresc testowa",
            ContentText = "Tytul Tresc testowa",
            CleanedContentText = "Tresc po cleanupie",
            CleanupStatus = ArticleCleanupStatus.Completed,
            CleanedAt = timestamp,
            CleanupFlagsJson = "[]",
            QualityScore = qualityScore,
            NeedsReview = needsReview,
            IsProbablyNonArticle = false,
            Embedding = new SqlVector<float>(embedding),
            EmbeddingStatus = ArticleEmbeddingStatus.Completed,
            EmbeddedAt = timestamp,
            PublishedAt = publishedAt,
            ScrapedAt = timestamp,
            MetadataJson = "{}"
        };
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

    private sealed class TestDbContextFactory(DbContextOptions<SrodkowyDbContext> options) : IDbContextFactory<SrodkowyDbContext>
    {
        public SrodkowyDbContext CreateDbContext() => new(options);

        public Task<SrodkowyDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(new SrodkowyDbContext(options));
    }
}
