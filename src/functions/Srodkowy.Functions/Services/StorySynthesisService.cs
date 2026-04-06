using System.Diagnostics;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Srodkowy.Functions.Configuration;
using Srodkowy.Functions.Contracts;
using Srodkowy.Functions.Models;
using Srodkowy.Functions.Persistence;

namespace Srodkowy.Functions.Services;

public sealed class StorySynthesisService(
    IDbContextFactory<SrodkowyDbContext> dbContextFactory,
    IOptions<SynthesisOptions> options,
    IStorySynthesisModel synthesisModel,
    ILogger<StorySynthesisService> logger)
{
    private static readonly ActivitySource ActivitySource = new(ObservabilityOptions.ChatSourceName);
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    public async Task<SynthesisRunResult> RunAsync(string triggeredBy, SynthesisRunRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Cycle))
        {
            throw new ArgumentException("Edition cycle is required.", nameof(request));
        }

        if (!Enum.TryParse<EditionCycle>(request.Cycle, true, out var cycle))
        {
            throw new ArgumentException($"Unsupported edition cycle '{request.Cycle}'.", nameof(request));
        }

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var candidateClusters = await LoadCandidateClustersAsync(dbContext, request, cancellationToken);

        if (candidateClusters.Count == 0)
        {
            throw new KeyNotFoundException("No qualified candidate clusters were found for synthesis.");
        }

        var clusterRunId = candidateClusters[0].ClusterRunId;
        Guid? editionId = request.DryRun ? null : Guid.NewGuid();
        var createdAt = DateTimeOffset.UtcNow;
        var skippedClusterIds = new List<Guid>();
        var errors = new List<string>();
        var stories = new List<Story>();

        using var activity = ActivitySource.StartActivity("story.synthesis.run", ActivityKind.Internal);
        activity?.SetTag("story.synthesis.triggered_by", triggeredBy);
        activity?.SetTag("story.synthesis.cluster_run_id", clusterRunId.ToString());
        activity?.SetTag("story.synthesis.candidate_cluster_count", candidateClusters.Count);
        activity?.SetTag("story.synthesis.dry_run", request.DryRun);

        foreach (var cluster in candidateClusters.OrderBy(cluster => cluster.Rank))
        {
            try
            {
                var story = await BuildStoryAsync(cluster, editionId ?? Guid.Empty, cancellationToken);

                if (story is null)
                {
                    skippedClusterIds.Add(cluster.Id);
                    errors.Add($"{cluster.Id}: synthesis output failed validation.");
                    continue;
                }

                stories.Add(story);
            }
            catch (Exception exception)
            {
                skippedClusterIds.Add(cluster.Id);
                errors.Add($"{cluster.Id}: {exception.Message}");
                logger.LogWarning(exception, "Synthesis failed for candidate cluster {CandidateClusterId}", cluster.Id);
            }
        }

        activity?.SetTag("story.synthesis.story_count", stories.Count);
        activity?.SetTag("story.synthesis.skipped_count", skippedClusterIds.Count);

        if (request.DryRun)
        {
            return new SynthesisRunResult(null, clusterRunId, candidateClusters.Count, stories.Count, skippedClusterIds, errors);
        }

        var edition = new Edition
        {
            Id = editionId!.Value,
            CreatedAt = createdAt,
            Status = stories.Count == 0 ? EditionStatus.Failed : EditionStatus.Building,
            Cycle = cycle,
            ClusterRunId = clusterRunId,
            Stories = stories
        };

        dbContext.Editions.Add(edition);
        await dbContext.SaveChangesAsync(cancellationToken);

        return new SynthesisRunResult(edition.Id, clusterRunId, candidateClusters.Count, stories.Count, skippedClusterIds, errors);
    }

    private async Task<List<CandidateCluster>> LoadCandidateClustersAsync(
        SrodkowyDbContext dbContext,
        SynthesisRunRequest request,
        CancellationToken cancellationToken)
    {
        var selectedClusterIds = request.ClusterIds?
            .Where(id => id != Guid.Empty)
            .Distinct()
            .ToArray();

        IQueryable<CandidateCluster> query = dbContext.CandidateClusters
            .AsNoTracking()
            .Include(cluster => cluster.Articles)
                .ThenInclude(clusterArticle => clusterArticle.Article)
                    .ThenInclude(article => article.Source)
            .Where(cluster => cluster.Status == "candidate");

        if (selectedClusterIds is { Length: > 0 })
        {
            query = query.Where(cluster => selectedClusterIds.Contains(cluster.Id));
        }
        else
        {
            var clusterRunId = request.ClusterRunId ?? await dbContext.ClusterRuns
                .AsNoTracking()
                .Where(run => run.Status == "completed")
                .OrderByDescending(run => run.StartedAt)
                .Select(run => (Guid?)run.Id)
                .FirstOrDefaultAsync(cancellationToken);

            if (clusterRunId is null)
            {
                return [];
            }

            query = query.Where(cluster => cluster.ClusterRunId == clusterRunId.Value);
        }

        var clusters = await query
            .OrderBy(cluster => cluster.Rank)
            .ToListAsync(cancellationToken);

        if (selectedClusterIds is { Length: > 0 } && clusters.Count != selectedClusterIds.Length)
        {
            throw new KeyNotFoundException("One or more candidate clusters were not found.");
        }

        var clusterRunIds = clusters.Select(cluster => cluster.ClusterRunId).Distinct().ToArray();

        if (clusterRunIds.Length > 1)
        {
            throw new InvalidOperationException("Synthesis input must come from a single cluster run.");
        }

        return clusters
            .Take(options.Value.MaxClustersPerRun)
            .ToList();
    }

    private async Task<Story?> BuildStoryAsync(CandidateCluster cluster, Guid editionId, CancellationToken cancellationToken)
    {
        var selectedArticles = SelectArticles(cluster);
        var prompt = new StorySynthesisModelRequest(cluster.Id, cluster.Rank, options.Value.MaxMarkers, selectedArticles);
        var modelResponse = await synthesisModel.SynthesizeAsync(prompt, cancellationToken);
        var validated = Validate(cluster, modelResponse);

        if (validated is null)
        {
            return null;
        }

        return new Story
        {
            Id = Guid.NewGuid(),
            EditionId = editionId,
            CandidateClusterId = cluster.Id,
            Rank = cluster.Rank,
            Headline = validated.Headline,
            Synthesis = validated.Synthesis,
            MarkersJson = JsonSerializer.Serialize(validated.Markers, SerializerOptions),
            Sides =
            [
                new StorySide
                {
                    Id = Guid.NewGuid(),
                    Camp = SourceCamp.Left,
                    Summary = validated.Left.Summary,
                    ExcerptsJson = JsonSerializer.Serialize(validated.Left.Excerpts, SerializerOptions)
                },
                new StorySide
                {
                    Id = Guid.NewGuid(),
                    Camp = SourceCamp.Right,
                    Summary = validated.Right.Summary,
                    ExcerptsJson = JsonSerializer.Serialize(validated.Right.Excerpts, SerializerOptions)
                }
            ],
            StoryArticles = cluster.Articles
                .Select(clusterArticle => new StoryArticle
                {
                    ArticleId = clusterArticle.ArticleId
                })
                .ToList()
        };
    }

    private List<StorySynthesisArticleInput> SelectArticles(CandidateCluster cluster)
    {
        return cluster.Articles
            .Select(clusterArticle => new
            {
                clusterArticle.IsRepresentative,
                clusterArticle.SimilarityToRepresentative,
                clusterArticle.Article,
                Timestamp = clusterArticle.Article.PublishedAt ?? clusterArticle.Article.ScrapedAt
            })
            .GroupBy(item => item.Article.Source.Camp, StringComparer.OrdinalIgnoreCase)
            .SelectMany(group => group
                .OrderByDescending(item => item.IsRepresentative)
                .ThenByDescending(item => item.SimilarityToRepresentative)
                .ThenByDescending(item => item.Timestamp)
                .ThenBy(item => item.Article.Id)
                .Take(options.Value.MaxArticlesPerCamp)
                .Select(item => new StorySynthesisArticleInput(
                    item.Article.Id,
                    item.Article.Source.Name,
                    item.Article.Source.Camp,
                    item.Article.Url,
                    item.Article.PublishedAt,
                    item.Article.Title,
                    Truncate(item.Article.CleanedContentText ?? item.Article.ContentText, options.Value.MaxInputCharactersPerArticle))))
            .OrderBy(article => article.Camp, StringComparer.OrdinalIgnoreCase)
            .ThenBy(article => article.SourceName, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private ValidatedStory? Validate(CandidateCluster cluster, StorySynthesisModelResponse response)
    {
        if (string.IsNullOrWhiteSpace(response.Headline) || string.IsNullOrWhiteSpace(response.Synthesis))
        {
            logger.LogWarning(
                "Synthesis validation rejected cluster {CandidateClusterId} rank {Rank} reason {ReasonCode}",
                cluster.Id,
                cluster.Rank,
                "missing_headline_or_synthesis");
            return null;
        }

        if (response.Left is null || response.Right is null)
        {
            logger.LogWarning(
                "Synthesis validation rejected cluster {CandidateClusterId} rank {Rank} reason {ReasonCode} hasLeft {HasLeft} hasRight {HasRight}",
                cluster.Id,
                cluster.Rank,
                "missing_side_object",
                response.Left is not null,
                response.Right is not null);
            return null;
        }

        if (string.IsNullOrWhiteSpace(response.Left.Summary) || string.IsNullOrWhiteSpace(response.Right.Summary))
        {
            logger.LogWarning(
                "Synthesis validation rejected cluster {CandidateClusterId} rank {Rank} reason {ReasonCode} leftSummaryLength {LeftSummaryLength} rightSummaryLength {RightSummaryLength}",
                cluster.Id,
                cluster.Rank,
                "missing_side_summary",
                response.Left.Summary?.Length ?? 0,
                response.Right.Summary?.Length ?? 0);
            return null;
        }

        var markers = response.Markers?
            .Where(marker => marker is not null)
            .Select(marker => marker!)
            .ToList();

        if (markers is null || markers.Count == 0 || markers.Count > options.Value.MaxMarkers)
        {
            logger.LogWarning(
                "Synthesis validation rejected cluster {CandidateClusterId} rank {Rank} reason {ReasonCode} markerCount {MarkerCount} maxMarkers {MaxMarkers}",
                cluster.Id,
                cluster.Rank,
                "invalid_marker_count",
                markers?.Count ?? 0,
                options.Value.MaxMarkers);
            return null;
        }

        var persistedMarkers = new List<StoryMarkerDto>(markers.Count);
        var seenPhrases = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var marker in markers)
        {
            if (string.IsNullOrWhiteSpace(marker.Phrase) || !seenPhrases.Add(marker.Phrase.Trim()))
            {
                logger.LogWarning(
                    "Synthesis validation rejected cluster {CandidateClusterId} rank {Rank} reason {ReasonCode} markerPhrase {MarkerPhrase}",
                    cluster.Id,
                    cluster.Rank,
                    "blank_or_duplicate_marker_phrase",
                    marker.Phrase);
                return null;
            }

            var phrase = marker.Phrase.Trim();
            var startOffset = response.Synthesis.IndexOf(phrase, StringComparison.OrdinalIgnoreCase);

            if (startOffset < 0)
            {
                logger.LogWarning(
                    "Synthesis validation rejected cluster {CandidateClusterId} rank {Rank} reason {ReasonCode} markerPhrase {MarkerPhrase}",
                    cluster.Id,
                    cluster.Rank,
                    "marker_phrase_not_in_synthesis",
                    phrase);
                return null;
            }

            persistedMarkers.Add(new StoryMarkerDto(
                phrase,
                startOffset,
                phrase.Length,
                marker.Kind?.Trim() ?? string.Empty,
                marker.Explanation?.Trim() ?? string.Empty));
        }

        var articleById = cluster.Articles.ToDictionary(clusterArticle => clusterArticle.ArticleId);
        var left = ValidateSide(cluster, SourceCamp.Left, response.Left, articleById);

        if (left is null)
        {
            return null;
        }

        var right = ValidateSide(cluster, SourceCamp.Right, response.Right, articleById);

        if (right is null)
        {
            return null;
        }

        return new ValidatedStory(
            response.Headline.Trim(),
            response.Synthesis.Trim(),
            persistedMarkers,
            left,
            right);
    }

    private ValidatedSide? ValidateSide(
        CandidateCluster cluster,
        string expectedCamp,
        StorySynthesisSideResponse side,
        IReadOnlyDictionary<Guid, CandidateClusterArticle> articleById)
    {
        var excerpts = side.Excerpts?
            .Where(excerpt => excerpt is not null)
            .Select(excerpt => excerpt!)
            .ToList();

        if (excerpts is null || excerpts.Count == 0)
        {
            logger.LogWarning(
                "Synthesis validation rejected cluster {CandidateClusterId} rank {Rank} reason {ReasonCode} expectedCamp {ExpectedCamp}",
                cluster.Id,
                cluster.Rank,
                "missing_side_excerpts",
                expectedCamp);
            return null;
        }

        var persistedExcerpts = new List<StoryExcerptDto>(excerpts.Count);

        foreach (var excerpt in excerpts)
        {
            if (!Guid.TryParse(excerpt.ArticleId, out var articleId))
            {
                logger.LogWarning(
                    "Synthesis validation rejected cluster {CandidateClusterId} rank {Rank} reason {ReasonCode} expectedCamp {ExpectedCamp} articleId {ArticleId}",
                    cluster.Id,
                    cluster.Rank,
                    "invalid_excerpt_article_id_format",
                    expectedCamp,
                    excerpt.ArticleId);
                return null;
            }

            if (!articleById.TryGetValue(articleId, out var clusterArticle))
            {
                logger.LogWarning(
                    "Synthesis validation rejected cluster {CandidateClusterId} rank {Rank} reason {ReasonCode} expectedCamp {ExpectedCamp} articleId {ArticleId}",
                    cluster.Id,
                    cluster.Rank,
                    "excerpt_article_not_in_cluster",
                    expectedCamp,
                    excerpt.ArticleId);
                return null;
            }

            if (!string.Equals(clusterArticle.Camp, expectedCamp, StringComparison.OrdinalIgnoreCase))
            {
                logger.LogWarning(
                    "Synthesis validation rejected cluster {CandidateClusterId} rank {Rank} reason {ReasonCode} expectedCamp {ExpectedCamp} actualCamp {ActualCamp} articleId {ArticleId} sourceName {SourceName}",
                    cluster.Id,
                    cluster.Rank,
                    "excerpt_wrong_camp",
                    expectedCamp,
                    clusterArticle.Camp,
                    clusterArticle.ArticleId,
                    clusterArticle.Article.Source.Name);
                return null;
            }

            if (string.IsNullOrWhiteSpace(excerpt.Text))
            {
                logger.LogWarning(
                    "Synthesis validation rejected cluster {CandidateClusterId} rank {Rank} reason {ReasonCode} expectedCamp {ExpectedCamp} articleId {ArticleId} sourceName {SourceName}",
                    cluster.Id,
                    cluster.Rank,
                    "blank_excerpt_text",
                    expectedCamp,
                    clusterArticle.ArticleId,
                    clusterArticle.Article.Source.Name);
                return null;
            }

            if (options.Value.RequireVerbatimExcerpts
                && !ContainsVerbatimExcerpt(clusterArticle.Article, excerpt.Text))
            {
                logger.LogWarning(
                    "Synthesis validation rejected cluster {CandidateClusterId} rank {Rank} reason {ReasonCode} expectedCamp {ExpectedCamp} articleId {ArticleId} sourceName {SourceName} excerptLength {ExcerptLength}",
                    cluster.Id,
                    cluster.Rank,
                    "excerpt_not_found_in_validation_text",
                    expectedCamp,
                    clusterArticle.ArticleId,
                    clusterArticle.Article.Source.Name,
                    excerpt.Text.Length);
                return null;
            }

            persistedExcerpts.Add(new StoryExcerptDto(
                clusterArticle.ArticleId,
                excerpt.Text.Trim(),
                clusterArticle.Article.Source.Name,
                clusterArticle.Article.Url));
        }

        return new ValidatedSide(side.Summary.Trim(), persistedExcerpts);
    }

    private static bool ContainsVerbatimExcerpt(Article article, string excerptText)
    {
        if (ContainsNormalized(article.CleanedContentText, excerptText))
        {
            return true;
        }

        return ContainsNormalized(article.ContentText, excerptText);
    }

    private static bool ContainsNormalized(string? sourceText, string excerptText)
    {
        if (string.IsNullOrWhiteSpace(sourceText) || string.IsNullOrWhiteSpace(excerptText))
        {
            return false;
        }

        return NormalizeWhitespace(sourceText).Contains(NormalizeWhitespace(excerptText), StringComparison.Ordinal);
    }

    private static string NormalizeWhitespace(string value)
    {
        return string.Join(' ', value
            .Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
    }

    private static string Truncate(string value, int maxLength)
    {
        return value.Length <= maxLength
            ? value
            : value[..maxLength];
    }

    public sealed record SynthesisRunRequest(
        Guid? ClusterRunId,
        IReadOnlyList<Guid>? ClusterIds,
        string? Cycle,
        bool DryRun = false);

    public sealed record SynthesisRunResult(
        Guid? EditionId,
        Guid ClusterRunId,
        int CandidateClusterCount,
        int StoryCount,
        IReadOnlyList<Guid> SkippedClusterIds,
        IReadOnlyList<string> Errors);

    private sealed record ValidatedStory(
        string Headline,
        string Synthesis,
        IReadOnlyList<StoryMarkerDto> Markers,
        ValidatedSide Left,
        ValidatedSide Right);

    private sealed record ValidatedSide(
        string Summary,
        IReadOnlyList<StoryExcerptDto> Excerpts);
}
