using System.Diagnostics;
using System.Text.Json;
using System.Text.RegularExpressions;
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
    private static readonly Regex SentenceCandidateRegex = new(@".+?(?:[.!?…]+(?:[\""'”’)]*)?(?=\s|$)|$)", RegexOptions.Compiled | RegexOptions.Singleline);
    private static readonly Regex MarkerTokenRegex = new(@"[\p{L}\p{Mn}\p{Nd}%]+(?:[-/][\p{L}\p{Mn}\p{Nd}%]+)*", RegexOptions.Compiled);
    private const int MaxExcerptCandidatesPerArticle = 4;
    private const int MinExcerptCandidateLength = 45;
    private const int MaxExcerptCandidateLength = 320;
    private const int MinMarkerWords = 2;
    private const int MaxMarkerWords = 6;
    private const int MinMarkerLength = 12;
    private const int MaxMarkerLength = 80;
    private const int MaxMarkerCandidates = 120;

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
        var excerptCandidates = BuildExcerptCandidates(selectedArticles);
        var draftRequest = new StorySynthesisDraftRequest(cluster.Id, cluster.Rank, selectedArticles, excerptCandidates.Select(MapExcerptCandidate).ToList());
        var draftResponse = await synthesisModel.SynthesizeDraftAsync(draftRequest, cancellationToken);
        var draft = ValidateDraft(cluster, excerptCandidates, draftResponse);

        if (draft is null)
        {
            return null;
        }

        var markerCandidates = BuildMarkerCandidates(draft.Synthesis);
        var markerRequest = new StorySynthesisMarkerSelectionRequest(
            cluster.Id,
            options.Value.MaxMarkers,
            draft.Synthesis,
            draft.Left.Summary,
            draft.Right.Summary,
            markerCandidates.Select(MapMarkerCandidate).ToList());
        var markerResponse = await synthesisModel.SelectMarkersAsync(markerRequest, cancellationToken);
        var markers = ValidateMarkers(cluster, draft.Synthesis, markerCandidates, markerResponse);

        if (markers is null)
        {
            return null;
        }

        return new Story
        {
            Id = Guid.NewGuid(),
            EditionId = editionId,
            CandidateClusterId = cluster.Id,
            Rank = cluster.Rank,
            Headline = draft.Headline,
            Synthesis = draft.Synthesis,
            MarkersJson = JsonSerializer.Serialize(markers, SerializerOptions),
            Sides =
            [
                new StorySide
                {
                    Id = Guid.NewGuid(),
                    Camp = SourceCamp.Left,
                    Summary = draft.Left.Summary,
                    ExcerptsJson = JsonSerializer.Serialize(draft.Left.Excerpts, SerializerOptions)
                },
                new StorySide
                {
                    Id = Guid.NewGuid(),
                    Camp = SourceCamp.Right,
                    Summary = draft.Right.Summary,
                    ExcerptsJson = JsonSerializer.Serialize(draft.Right.Excerpts, SerializerOptions)
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

    private static List<ExcerptCandidate> BuildExcerptCandidates(IReadOnlyList<StorySynthesisArticleInput> articles)
    {
        var candidates = new List<ExcerptCandidate>();

        for (var articleIndex = 0; articleIndex < articles.Count; articleIndex++)
        {
            var article = articles[articleIndex];
            var snippets = SentenceCandidateRegex.Matches(article.CleanedContentText)
                .Select(match => match.Value.Trim())
                .Where(snippet => !string.IsNullOrWhiteSpace(snippet))
                .Where(snippet => snippet.Length >= MinExcerptCandidateLength && snippet.Length <= MaxExcerptCandidateLength)
                .Distinct(StringComparer.Ordinal)
                .Take(MaxExcerptCandidatesPerArticle)
                .ToList();

            if (snippets.Count == 0)
            {
                var fallback = article.CleanedContentText.Trim();

                if (!string.IsNullOrWhiteSpace(fallback))
                {
                    snippets.Add(Truncate(fallback, MaxExcerptCandidateLength).Trim());
                }
            }

            for (var snippetIndex = 0; snippetIndex < snippets.Count; snippetIndex++)
            {
                candidates.Add(new ExcerptCandidate(
                    $"A{articleIndex + 1}-S{snippetIndex + 1}",
                    article.ArticleId,
                    article.Camp,
                    article.SourceName,
                    article.Url,
                    snippets[snippetIndex]));
            }
        }

        return candidates;
    }

    private static StorySynthesisExcerptCandidateInput MapExcerptCandidate(ExcerptCandidate candidate)
    {
        return new StorySynthesisExcerptCandidateInput(
            candidate.SnippetId,
            candidate.ArticleId,
            candidate.Camp,
            candidate.SourceName,
            candidate.Text);
    }

    private static List<MarkerCandidate> BuildMarkerCandidates(string synthesis)
    {
        var candidates = new List<MarkerCandidate>();
        var seenTexts = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (Match sentenceMatch in SentenceCandidateRegex.Matches(synthesis))
        {
            if (!sentenceMatch.Success)
            {
                continue;
            }

            var sentence = sentenceMatch.Value;
            var tokens = MarkerTokenRegex.Matches(sentence)
                .Cast<Match>()
                .ToList();

            for (var wordCount = MinMarkerWords; wordCount <= MaxMarkerWords; wordCount++)
            {
                for (var startIndex = 0; startIndex <= tokens.Count - wordCount; startIndex++)
                {
                    var firstToken = tokens[startIndex];
                    var lastToken = tokens[startIndex + wordCount - 1];
                    var relativeStart = firstToken.Index;
                    var relativeEnd = lastToken.Index + lastToken.Length;
                    var rawText = sentence[relativeStart..relativeEnd].Trim();

                    if (rawText.Length < MinMarkerLength || rawText.Length > MaxMarkerLength)
                    {
                        continue;
                    }

                    if (!seenTexts.Add(rawText))
                    {
                        continue;
                    }

                    var startOffset = sentenceMatch.Index + relativeStart;
                    candidates.Add(new MarkerCandidate($"M{candidates.Count + 1}", rawText, startOffset, rawText.Length));

                    if (candidates.Count >= MaxMarkerCandidates)
                    {
                        return candidates;
                    }
                }
            }
        }

        return candidates;
    }

    private static StorySynthesisMarkerCandidateInput MapMarkerCandidate(MarkerCandidate candidate)
    {
        return new StorySynthesisMarkerCandidateInput(
            candidate.MarkerCandidateId,
            candidate.Text,
            candidate.StartOffset,
            candidate.Length);
    }

    private ValidatedDraft? ValidateDraft(
        CandidateCluster cluster,
        IReadOnlyList<ExcerptCandidate> excerptCandidates,
        StorySynthesisDraftResponse response)
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

        var articleById = cluster.Articles.ToDictionary(clusterArticle => clusterArticle.ArticleId);
        var excerptCandidateById = excerptCandidates.ToDictionary(candidate => candidate.SnippetId, StringComparer.OrdinalIgnoreCase);
        var left = ValidateSide(cluster, SourceCamp.Left, response.Left, articleById, excerptCandidateById);

        if (left is null)
        {
            return null;
        }

        var right = ValidateSide(cluster, SourceCamp.Right, response.Right, articleById, excerptCandidateById);

        if (right is null)
        {
            return null;
        }

        return new ValidatedDraft(
            response.Headline.Trim(),
            response.Synthesis.Trim(),
            left,
            right);
    }

    private IReadOnlyList<StoryMarkerDto>? ValidateMarkers(
        CandidateCluster cluster,
        string synthesis,
        IReadOnlyList<MarkerCandidate> markerCandidates,
        StorySynthesisMarkerSelectionResponse response)
    {
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

        var markerCandidateById = markerCandidates.ToDictionary(candidate => candidate.MarkerCandidateId, StringComparer.OrdinalIgnoreCase);
        var seenCandidateIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var persistedMarkers = new List<StoryMarkerDto>(markers.Count);

        foreach (var marker in markers)
        {
            if (string.IsNullOrWhiteSpace(marker.MarkerCandidateId) || !seenCandidateIds.Add(marker.MarkerCandidateId.Trim()))
            {
                logger.LogWarning(
                    "Synthesis validation rejected cluster {CandidateClusterId} rank {Rank} reason {ReasonCode} markerCandidateId {MarkerCandidateId}",
                    cluster.Id,
                    cluster.Rank,
                    "blank_or_duplicate_marker_candidate_id",
                    marker.MarkerCandidateId);
                return null;
            }

            var markerCandidateId = marker.MarkerCandidateId.Trim();

            if (!markerCandidateById.TryGetValue(markerCandidateId, out var markerCandidate))
            {
                logger.LogWarning(
                    "Synthesis validation rejected cluster {CandidateClusterId} rank {Rank} reason {ReasonCode} markerCandidateId {MarkerCandidateId}",
                    cluster.Id,
                    cluster.Rank,
                    "invalid_marker_candidate_id",
                    markerCandidateId);
                return null;
            }

            if (markerCandidate.StartOffset < 0
                || markerCandidate.StartOffset + markerCandidate.Length > synthesis.Length
                || !string.Equals(synthesis.Substring(markerCandidate.StartOffset, markerCandidate.Length), markerCandidate.Text, StringComparison.Ordinal))
            {
                logger.LogWarning(
                    "Synthesis validation rejected cluster {CandidateClusterId} rank {Rank} reason {ReasonCode} markerCandidateId {MarkerCandidateId}",
                    cluster.Id,
                    cluster.Rank,
                    "marker_candidate_span_invalid",
                    markerCandidateId);
                return null;
            }

            persistedMarkers.Add(new StoryMarkerDto(
                markerCandidate.Text,
                markerCandidate.StartOffset,
                markerCandidate.Length,
                marker.Kind?.Trim() ?? string.Empty,
                marker.Explanation?.Trim() ?? string.Empty));
        }

        return persistedMarkers;
    }

    private ValidatedSide? ValidateSide(
        CandidateCluster cluster,
        string expectedCamp,
        StorySynthesisSideResponse side,
        IReadOnlyDictionary<Guid, CandidateClusterArticle> articleById,
        IReadOnlyDictionary<string, ExcerptCandidate> excerptCandidateById)
    {
        var excerptSnippetIds = side.ExcerptSnippetIds?
            .Where(snippetId => !string.IsNullOrWhiteSpace(snippetId))
            .Select(snippetId => snippetId.Trim())
            .ToList();

        if (excerptSnippetIds is null || excerptSnippetIds.Count == 0)
        {
            logger.LogWarning(
                "Synthesis validation rejected cluster {CandidateClusterId} rank {Rank} reason {ReasonCode} expectedCamp {ExpectedCamp}",
                cluster.Id,
                cluster.Rank,
                "missing_side_excerpt_snippet_ids",
                expectedCamp);
            return null;
        }

        var persistedExcerpts = new List<StoryExcerptDto>(excerptSnippetIds.Count);
        var seenSnippetIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var snippetId in excerptSnippetIds)
        {
            if (!seenSnippetIds.Add(snippetId))
            {
                logger.LogWarning(
                    "Synthesis validation rejected cluster {CandidateClusterId} rank {Rank} reason {ReasonCode} expectedCamp {ExpectedCamp} snippetId {SnippetId}",
                    cluster.Id,
                    cluster.Rank,
                    "duplicate_excerpt_snippet_id",
                    expectedCamp,
                    snippetId);
                return null;
            }

            if (!excerptCandidateById.TryGetValue(snippetId, out var excerptCandidate))
            {
                logger.LogWarning(
                    "Synthesis validation rejected cluster {CandidateClusterId} rank {Rank} reason {ReasonCode} expectedCamp {ExpectedCamp} snippetId {SnippetId}",
                    cluster.Id,
                    cluster.Rank,
                    "invalid_excerpt_snippet_id",
                    expectedCamp,
                    snippetId);
                return null;
            }

            if (!string.Equals(excerptCandidate.Camp, expectedCamp, StringComparison.OrdinalIgnoreCase))
            {
                logger.LogWarning(
                    "Synthesis validation rejected cluster {CandidateClusterId} rank {Rank} reason {ReasonCode} expectedCamp {ExpectedCamp} actualCamp {ActualCamp} snippetId {SnippetId} articleId {ArticleId} sourceName {SourceName}",
                    cluster.Id,
                    cluster.Rank,
                    "excerpt_snippet_wrong_camp",
                    expectedCamp,
                    excerptCandidate.Camp,
                    snippetId,
                    excerptCandidate.ArticleId,
                    excerptCandidate.SourceName);
                return null;
            }

            if (!articleById.TryGetValue(excerptCandidate.ArticleId, out _))
            {
                logger.LogWarning(
                    "Synthesis validation rejected cluster {CandidateClusterId} rank {Rank} reason {ReasonCode} expectedCamp {ExpectedCamp} snippetId {SnippetId} articleId {ArticleId}",
                    cluster.Id,
                    cluster.Rank,
                    "excerpt_snippet_not_in_cluster",
                    expectedCamp,
                    snippetId,
                    excerptCandidate.ArticleId);
                return null;
            }

            persistedExcerpts.Add(new StoryExcerptDto(
                excerptCandidate.ArticleId,
                excerptCandidate.Text,
                excerptCandidate.SourceName,
                excerptCandidate.SourceUrl));
        }

        return new ValidatedSide(side.Summary.Trim(), persistedExcerpts);
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

    private sealed record ValidatedDraft(
        string Headline,
        string Synthesis,
        ValidatedSide Left,
        ValidatedSide Right);

    private sealed record ValidatedSide(
        string Summary,
        IReadOnlyList<StoryExcerptDto> Excerpts);

    private sealed record ExcerptCandidate(
        string SnippetId,
        Guid ArticleId,
        string Camp,
        string SourceName,
        string SourceUrl,
        string Text);

    private sealed record MarkerCandidate(
        string MarkerCandidateId,
        string Text,
        int StartOffset,
        int Length);
}
