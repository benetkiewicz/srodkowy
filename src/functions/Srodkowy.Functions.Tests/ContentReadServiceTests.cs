using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Srodkowy.Functions.Configuration;
using Srodkowy.Functions.Contracts;
using Srodkowy.Functions.Models;
using Srodkowy.Functions.Persistence;
using Srodkowy.Functions.Services;

namespace Srodkowy.Functions.Tests;

public sealed class ContentReadServiceTests
{
    [Fact]
    public async Task GetCurrentEditionAsync_returns_live_edition_with_story_cards()
    {
        var dbContextOptions = StoryPublishingTestSupport.CreateDbContextOptions();
        var seeded = await SeedReadModelAsync(dbContextOptions);
        var service = new ContentReadService(new StoryPublishingTestSupport.TestDbContextFactory(dbContextOptions));

        var edition = await service.GetCurrentEditionAsync(CancellationToken.None);

        edition.Should().NotBeNull();
        edition!.Id.Should().Be(seeded.LiveEditionId);
        edition.Stories.Should().ContainSingle();
        edition.Stories[0].StoryId.Should().Be(seeded.StoryId);
        edition.Stories[0].MarkerCount.Should().Be(1);
        edition.Stories[0].LeftSourceCount.Should().Be(1);
        edition.Stories[0].RightSourceCount.Should().Be(1);
    }

    [Fact]
    public async Task GetEditionAsync_returns_requested_edition()
    {
        var dbContextOptions = StoryPublishingTestSupport.CreateDbContextOptions();
        var seeded = await SeedReadModelAsync(dbContextOptions);
        var service = new ContentReadService(new StoryPublishingTestSupport.TestDbContextFactory(dbContextOptions));

        var edition = await service.GetEditionAsync(seeded.LiveEditionId, CancellationToken.None);

        edition.Should().NotBeNull();
        edition!.Id.Should().Be(seeded.LiveEditionId);
    }

    [Fact]
    public async Task GetStoryAsync_returns_story_detail_with_markers_and_sides()
    {
        var dbContextOptions = StoryPublishingTestSupport.CreateDbContextOptions();
        var seeded = await SeedReadModelAsync(dbContextOptions);
        var service = new ContentReadService(new StoryPublishingTestSupport.TestDbContextFactory(dbContextOptions));

        var story = await service.GetStoryAsync(seeded.StoryId, CancellationToken.None);

        story.Should().NotBeNull();
        story!.Markers.Should().ContainSingle(marker => marker.Phrase == "napięcie polityczne");
        story.Left.Excerpts.Should().ContainSingle(excerpt => excerpt.SourceName == "Lewy portal");
        story.Right.Excerpts.Should().ContainSingle(excerpt => excerpt.SourceName == "Prawy portal");
    }

    [Fact]
    public async Task GetSourcesAsync_returns_curated_sources()
    {
        var dbContextOptions = StoryPublishingTestSupport.CreateDbContextOptions();
        await SeedReadModelAsync(dbContextOptions);
        var service = new ContentReadService(new StoryPublishingTestSupport.TestDbContextFactory(dbContextOptions));

        var sources = await service.GetSourcesAsync(CancellationToken.None);

        sources.Should().Contain(source => source.Name == "Lewy portal" && source.Camp == SourceCamp.Left);
        sources.Should().Contain(source => source.Name == "Prawy portal" && source.Camp == SourceCamp.Right);
    }

    private static async Task<ReadModelSetup> SeedReadModelAsync(DbContextOptions<SrodkowyDbContext> dbContextOptions)
    {
        var clusterRunId = Guid.NewGuid();
        var liveEditionId = Guid.NewGuid();
        var archivedEditionId = Guid.NewGuid();
        var storyId = Guid.NewGuid();
        var leftSourceId = Guid.NewGuid();
        var rightSourceId = Guid.NewGuid();
        var leftArticleId = Guid.NewGuid();
        var rightArticleId = Guid.NewGuid();
        var clusterId = Guid.NewGuid();

        await using var dbContext = StoryPublishingTestSupport.CreateDbContext(dbContextOptions);
        dbContext.Database.EnsureCreated();

        var leftSource = StoryPublishingTestSupport.CreateSource(leftSourceId, SourceCamp.Left, "Lewy portal");
        var rightSource = StoryPublishingTestSupport.CreateSource(rightSourceId, SourceCamp.Right, "Prawy portal");
        var leftArticle = StoryPublishingTestSupport.CreateArticle(leftArticleId, leftSourceId, "Lewy tytul", "Lewy cytat i dalsza analiza.");
        var rightArticle = StoryPublishingTestSupport.CreateArticle(rightArticleId, rightSourceId, "Prawy tytul", "Prawy cytat i dalsza analiza.");
        var clusterRun = StoryPublishingTestSupport.CreateClusterRun(clusterRunId);
        var cluster = StoryPublishingTestSupport.CreateCandidateCluster(clusterId, clusterRunId, leftArticleId);

        leftArticle.Source = leftSource;
        rightArticle.Source = rightSource;
        cluster.Articles =
        [
            StoryPublishingTestSupport.CreateClusterArticle(cluster, leftArticle, SourceCamp.Left, true),
            StoryPublishingTestSupport.CreateClusterArticle(cluster, rightArticle, SourceCamp.Right, false)
        ];

        dbContext.Sources.AddRange(leftSource, rightSource);
        dbContext.Articles.AddRange(leftArticle, rightArticle);
        dbContext.ClusterRuns.Add(clusterRun);
        dbContext.CandidateClusters.Add(cluster);

        dbContext.Editions.AddRange(
            new Edition
            {
                Id = archivedEditionId,
                CreatedAt = DateTimeOffset.UtcNow.AddDays(-1),
                PublishedAt = DateTimeOffset.UtcNow.AddDays(-1),
                Status = EditionStatus.Archived,
                Cycle = EditionCycle.Evening,
                ClusterRunId = clusterRunId
            },
            new Edition
            {
                Id = liveEditionId,
                CreatedAt = DateTimeOffset.UtcNow,
                PublishedAt = DateTimeOffset.UtcNow,
                Status = EditionStatus.Live,
                Cycle = EditionCycle.Morning,
                ClusterRunId = clusterRunId,
                Stories =
                [
                    new Story
                    {
                        Id = storyId,
                        CandidateClusterId = clusterId,
                        Rank = 1,
                        Headline = "Spór o projekt ustawy",
                        Synthesis = "W centrum debaty pozostaje napięcie polityczne wokół projektu ustawy.",
                        MarkersJson = JsonSerializer.Serialize(new List<StoryMarkerDto>
                        {
                            new("napięcie polityczne", 29, 18, "framing", "Opis")
                        }),
                        Sides =
                        [
                            new StorySide
                            {
                                Id = Guid.NewGuid(),
                                Camp = SourceCamp.Left,
                                Summary = "Lewa narracja",
                                ExcerptsJson = JsonSerializer.Serialize(new List<StoryExcerptDto>
                                {
                                    new(leftArticleId, "Lewy cytat", "Lewy portal", leftArticle.Url)
                                })
                            },
                            new StorySide
                            {
                                Id = Guid.NewGuid(),
                                Camp = SourceCamp.Right,
                                Summary = "Prawa narracja",
                                ExcerptsJson = JsonSerializer.Serialize(new List<StoryExcerptDto>
                                {
                                    new(rightArticleId, "Prawy cytat", "Prawy portal", rightArticle.Url)
                                })
                            }
                        ],
                        StoryArticles =
                        [
                            new StoryArticle { ArticleId = leftArticleId },
                            new StoryArticle { ArticleId = rightArticleId }
                        ]
                    }
                ]
            });

        await dbContext.SaveChangesAsync();
        return new ReadModelSetup(liveEditionId, storyId);
    }

    private sealed record ReadModelSetup(Guid LiveEditionId, Guid StoryId);
}
