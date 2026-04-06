using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Srodkowy.Functions.Configuration;
using Srodkowy.Functions.Contracts;
using Srodkowy.Functions.Models;
using Srodkowy.Functions.Persistence;
using Srodkowy.Functions.Services;

namespace Srodkowy.Functions.Tests;

public sealed class StorySynthesisServiceTests
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task RunAsync_persists_building_edition_and_story_for_valid_cluster()
    {
        var dbContextOptions = StoryPublishingTestSupport.CreateDbContextOptions();
        var setup = await SeedSingleClusterAsync(dbContextOptions);
        var service = CreateService(
            dbContextOptions,
            new StoryPublishingTestSupport.FakeStorySynthesisModel(request => CreateValidDraftResponse(request, setup.LeftArticleId, setup.RightArticleId)));

        var result = await service.RunAsync(
            "test",
            new StorySynthesisService.SynthesisRunRequest(setup.ClusterRunId, null, "morning"),
            CancellationToken.None);

        result.EditionId.Should().NotBeNull();
        result.StoryCount.Should().Be(1);
        result.SkippedClusterIds.Should().BeEmpty();

        await using var verificationContext = StoryPublishingTestSupport.CreateDbContext(dbContextOptions);
        var edition = await verificationContext.Editions
            .Include(item => item.Stories)
                .ThenInclude(story => story.Sides)
            .Include(item => item.Stories)
                .ThenInclude(story => story.StoryArticles)
            .SingleAsync();

        edition.Status.Should().Be(EditionStatus.Building);
        edition.Cycle.Should().Be(EditionCycle.Morning);
        edition.ClusterRunId.Should().Be(setup.ClusterRunId);
        edition.Stories.Should().HaveCount(1);
        edition.Stories.Single().Sides.Should().HaveCount(2);
        edition.Stories.Single().StoryArticles.Should().HaveCount(2);

        var markers = JsonSerializer.Deserialize<List<StoryMarkerDto>>(edition.Stories.Single().MarkersJson, SerializerOptions);
        markers.Should().ContainSingle(marker => marker!.Phrase == "napięcie polityczne");
    }

    [Fact]
    public async Task RunAsync_skips_story_when_marker_candidate_id_is_invalid()
    {
        var dbContextOptions = StoryPublishingTestSupport.CreateDbContextOptions();
        var setup = await SeedSingleClusterAsync(dbContextOptions);
        var service = CreateService(
            dbContextOptions,
            new StoryPublishingTestSupport.FakeStorySynthesisModel(
                request => CreateValidDraftResponse(request, setup.LeftArticleId, setup.RightArticleId),
                request => new StorySynthesisMarkerSelectionResponse(
                [
                    new StorySynthesisMarkerSelectionItem("missing-marker-candidate", "framing", "Opis")
                ])));

        var result = await service.RunAsync(
            "test",
            new StorySynthesisService.SynthesisRunRequest(setup.ClusterRunId, null, "morning"),
            CancellationToken.None);

        result.StoryCount.Should().Be(0);
        result.SkippedClusterIds.Should().Contain(setup.ClusterId);

        await using var verificationContext = StoryPublishingTestSupport.CreateDbContext(dbContextOptions);
        var edition = await verificationContext.Editions.SingleAsync();
        edition.Status.Should().Be(EditionStatus.Failed);
    }

    [Fact]
    public async Task RunAsync_skips_story_when_excerpt_article_uses_wrong_camp()
    {
        var dbContextOptions = StoryPublishingTestSupport.CreateDbContextOptions();
        var setup = await SeedSingleClusterAsync(dbContextOptions);
        var service = CreateService(
            dbContextOptions,
            new StoryPublishingTestSupport.FakeStorySynthesisModel(request => CreateValidDraftResponse(request, setup.LeftArticleId, setup.RightArticleId) with
            {
                Left = new StorySynthesisSideResponse(
                    "Lewa narracja",
                    [GetFirstSnippetId(request, setup.RightArticleId)])
            }));

        var result = await service.RunAsync(
            "test",
            new StorySynthesisService.SynthesisRunRequest(setup.ClusterRunId, null, "morning"),
            CancellationToken.None);

        result.StoryCount.Should().Be(0);
        result.SkippedClusterIds.Should().Contain(setup.ClusterId);
    }

    [Fact]
    public async Task RunAsync_skips_story_when_side_has_no_excerpt_snippet_ids()
    {
        var dbContextOptions = StoryPublishingTestSupport.CreateDbContextOptions();
        var setup = await SeedSingleClusterAsync(dbContextOptions);
        var service = CreateService(
            dbContextOptions,
            new StoryPublishingTestSupport.FakeStorySynthesisModel(request => CreateValidDraftResponse(request, setup.LeftArticleId, setup.RightArticleId) with
            {
                Left = new StorySynthesisSideResponse(
                    "Lewa narracja",
                    [])
            }));

        var result = await service.RunAsync(
            "test",
            new StorySynthesisService.SynthesisRunRequest(setup.ClusterRunId, null, "morning"),
            CancellationToken.None);

        result.StoryCount.Should().Be(0);
        result.SkippedClusterIds.Should().Contain(setup.ClusterId);
    }

    [Fact]
    public async Task RunAsync_skips_story_when_excerpt_snippet_id_is_unknown()
    {
        var dbContextOptions = StoryPublishingTestSupport.CreateDbContextOptions();
        var setup = await SeedSingleClusterAsync(dbContextOptions);
        var service = CreateService(
            dbContextOptions,
            new StoryPublishingTestSupport.FakeStorySynthesisModel(request => CreateValidDraftResponse(request, setup.LeftArticleId, setup.RightArticleId) with
            {
                Left = new StorySynthesisSideResponse(
                    "Lewa narracja",
                    ["missing-snippet-id"])
            }));

        var result = await service.RunAsync(
            "test",
            new StorySynthesisService.SynthesisRunRequest(setup.ClusterRunId, null, "morning"),
            CancellationToken.None);

        result.StoryCount.Should().Be(0);
        result.SkippedClusterIds.Should().Contain(setup.ClusterId);
    }

    [Fact]
    public async Task RunAsync_skips_story_when_excerpt_snippet_id_is_duplicated()
    {
        var dbContextOptions = StoryPublishingTestSupport.CreateDbContextOptions();
        var setup = await SeedSingleClusterAsync(dbContextOptions);
        var service = CreateService(
            dbContextOptions,
            new StoryPublishingTestSupport.FakeStorySynthesisModel(request => CreateValidDraftResponse(request, setup.LeftArticleId, setup.RightArticleId) with
            {
                Left = new StorySynthesisSideResponse(
                    "Lewa narracja",
                    [GetFirstSnippetId(request, setup.LeftArticleId), GetFirstSnippetId(request, setup.LeftArticleId)])
            }));

        var result = await service.RunAsync(
            "test",
            new StorySynthesisService.SynthesisRunRequest(setup.ClusterRunId, null, "morning"),
            CancellationToken.None);

        result.StoryCount.Should().Be(0);
        result.SkippedClusterIds.Should().Contain(setup.ClusterId);
    }

    [Fact]
    public async Task RunAsync_reconstructs_excerpt_from_selected_snippet_id()
    {
        var dbContextOptions = StoryPublishingTestSupport.CreateDbContextOptions();
        var setup = await SeedSingleClusterAsync(
            dbContextOptions,
            leftContentText: "Surowy lewy tekst z szumem i innym ukladem.",
            leftCleanedContentText: "Lewy cytat tylko po cleanupie oraz dalsza analiza wydarzen.");
        var service = CreateService(
            dbContextOptions,
            new StoryPublishingTestSupport.FakeStorySynthesisModel(request => CreateValidDraftResponse(request, setup.LeftArticleId, setup.RightArticleId) with
            {
                Left = new StorySynthesisSideResponse(
                    "Lewa narracja",
                    [GetFirstSnippetId(request, setup.LeftArticleId)])
            }));

        var result = await service.RunAsync(
            "test",
            new StorySynthesisService.SynthesisRunRequest(setup.ClusterRunId, null, "morning"),
            CancellationToken.None);

        result.StoryCount.Should().Be(1);
        result.SkippedClusterIds.Should().BeEmpty();

        await using var verificationContext = StoryPublishingTestSupport.CreateDbContext(dbContextOptions);
        var edition = await verificationContext.Editions
            .Include(item => item.Stories)
                .ThenInclude(story => story.Sides)
            .SingleAsync();
        var leftSide = edition.Stories.Single().Sides.Single(side => side.Camp == SourceCamp.Left);
        var excerpts = JsonSerializer.Deserialize<List<StoryExcerptDto>>(leftSide.ExcerptsJson, SerializerOptions);

        excerpts.Should().ContainSingle();
        excerpts![0].Text.Should().Be("Lewy cytat tylko po cleanupie oraz dalsza analiza wydarzen.");
    }

    [Fact]
    public async Task RunAsync_accepts_excerpt_with_quote_dash_and_whitespace_variants()
    {
        var dbContextOptions = StoryPublishingTestSupport.CreateDbContextOptions();
        var setup = await SeedSingleClusterAsync(
            dbContextOptions,
            leftCleanedContentText: "Lewy portal\u00A0nazwal to \u201Enowym porzadkiem\u201D \u2014\n i wskazal dalszy plan.");
        var service = CreateService(
            dbContextOptions,
            new StoryPublishingTestSupport.FakeStorySynthesisModel(request => CreateValidDraftResponse(request, setup.LeftArticleId, setup.RightArticleId) with
            {
                Left = new StorySynthesisSideResponse(
                    "Lewa narracja",
                    [GetFirstSnippetId(request, setup.LeftArticleId)])
            }));

        var result = await service.RunAsync(
            "test",
            new StorySynthesisService.SynthesisRunRequest(setup.ClusterRunId, null, "morning"),
            CancellationToken.None);

        result.StoryCount.Should().Be(1);
        result.SkippedClusterIds.Should().BeEmpty();
    }

    [Fact]
    public async Task RunAsync_accepts_marker_with_typographic_variants_and_persists_literal_span()
    {
        var dbContextOptions = StoryPublishingTestSupport.CreateDbContextOptions();
        var setup = await SeedSingleClusterAsync(dbContextOptions);
        var service = CreateService(
            dbContextOptions,
            new StoryPublishingTestSupport.FakeStorySynthesisModel(
                request => new StorySynthesisDraftResponse(
                    "Spor o projekt ustawy",
                    "W centrum debaty pozostaje \u201Enapiecie\u201D \u2014 polityczne wokol projektu ustawy i sposobu jego procedowania przez obie strony.",
                    new StorySynthesisSideResponse(
                        "Lewa narracja",
                        [GetFirstSnippetId(request, setup.LeftArticleId)]),
                    new StorySynthesisSideResponse(
                        "Prawa narracja",
                        [GetFirstSnippetId(request, setup.RightArticleId)])),
                request => new StorySynthesisMarkerSelectionResponse(
                [
                    new StorySynthesisMarkerSelectionItem(GetMarkerCandidateId(request, "napiecie” — polityczne"), "framing", "Obie strony inaczej opisuja skale sporu.")
                ])));

        var result = await service.RunAsync(
            "test",
            new StorySynthesisService.SynthesisRunRequest(setup.ClusterRunId, null, "morning"),
            CancellationToken.None);

        result.StoryCount.Should().Be(1);

        await using var verificationContext = StoryPublishingTestSupport.CreateDbContext(dbContextOptions);
        var edition = await verificationContext.Editions
            .Include(item => item.Stories)
            .SingleAsync();
        var markers = JsonSerializer.Deserialize<List<StoryMarkerDto>>(edition.Stories.Single().MarkersJson, SerializerOptions);

        markers.Should().ContainSingle();
        markers![0].Phrase.Should().Be("napiecie\u201D \u2014 polityczne");
        markers[0].StartOffset.Should().BeGreaterThanOrEqualTo(0);
        markers[0].Length.Should().Be(markers[0].Phrase.Length);
    }

    private static StorySynthesisService CreateService(
        DbContextOptions<SrodkowyDbContext> dbContextOptions,
        IStorySynthesisModel model,
        SynthesisOptions? options = null)
    {
        return new StorySynthesisService(
            new StoryPublishingTestSupport.TestDbContextFactory(dbContextOptions),
            Options.Create(options ?? new SynthesisOptions()),
            model,
            NullLogger<StorySynthesisService>.Instance);
    }

    private static StorySynthesisDraftResponse CreateValidDraftResponse(StorySynthesisDraftRequest request, Guid leftArticleId, Guid rightArticleId)
    {
        return new StorySynthesisDraftResponse(
            "Spór o projekt ustawy",
            "W centrum debaty pozostaje napięcie polityczne wokół projektu ustawy i sposobu jego procedowania przez obie strony.",
            new StorySynthesisSideResponse(
                "Lewa narracja",
                [GetFirstSnippetId(request, leftArticleId)]),
            new StorySynthesisSideResponse(
                "Prawa narracja",
                [GetFirstSnippetId(request, rightArticleId)]));
    }

    private static string GetFirstSnippetId(StorySynthesisDraftRequest request, Guid articleId)
    {
        return request.ExcerptCandidates.First(candidate => candidate.ArticleId == articleId).SnippetId;
    }

    private static string GetMarkerCandidateId(StorySynthesisMarkerSelectionRequest request, string exactText)
    {
        return request.MarkerCandidates.First(candidate => string.Equals(candidate.Text, exactText, StringComparison.Ordinal)).MarkerCandidateId;
    }

    private static async Task<ClusterSetup> SeedSingleClusterAsync(
        DbContextOptions<SrodkowyDbContext> dbContextOptions,
        string? leftContentText = null,
        string? leftCleanedContentText = null,
        string? rightContentText = null,
        string? rightCleanedContentText = null)
    {
        var clusterRunId = Guid.NewGuid();
        var clusterId = Guid.NewGuid();
        var leftSourceId = Guid.NewGuid();
        var rightSourceId = Guid.NewGuid();
        var leftArticleId = Guid.NewGuid();
        var rightArticleId = Guid.NewGuid();
        const string leftExcerpt = "Lewy portal opisal spokojny przebieg debaty";
        const string rightExcerpt = "Prawy portal podkreslil sporny tryb procedowania";

        await using var dbContext = StoryPublishingTestSupport.CreateDbContext(dbContextOptions);
        dbContext.Database.EnsureCreated();

        var leftSource = StoryPublishingTestSupport.CreateSource(leftSourceId, SourceCamp.Left, "Lewy portal");
        var rightSource = StoryPublishingTestSupport.CreateSource(rightSourceId, SourceCamp.Right, "Prawy portal");
        var leftArticle = StoryPublishingTestSupport.CreateArticle(
            leftArticleId,
            leftSourceId,
            "Lewy tytul",
            leftContentText ?? $"{leftExcerpt} oraz dalsza analiza wydarzen.",
            leftCleanedContentText);
        var rightArticle = StoryPublishingTestSupport.CreateArticle(
            rightArticleId,
            rightSourceId,
            "Prawy tytul",
            rightContentText ?? $"{rightExcerpt} oraz dalsza analiza wydarzen.",
            rightCleanedContentText);
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
        await dbContext.SaveChangesAsync();

        return new ClusterSetup(clusterRunId, clusterId, leftArticleId, rightArticleId, leftExcerpt, rightExcerpt);
    }

    private sealed record ClusterSetup(
        Guid ClusterRunId,
        Guid ClusterId,
        Guid LeftArticleId,
        Guid RightArticleId,
        string LeftExcerpt,
        string RightExcerpt);
}
