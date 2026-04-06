using System.Diagnostics;
using System.Text;
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
    private const int MaxExcerptCandidatesPerArticle = 4;
    private const int MinExcerptCandidateLength = 45;
    private const int MaxExcerptCandidateLength = 320;

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
        var prompt = new StorySynthesisModelRequest(cluster.Id, cluster.Rank, options.Value.MaxMarkers, selectedArticles, excerptCandidates.Select(MapExcerptCandidate).ToList());
        var modelResponse = await synthesisModel.SynthesizeAsync(prompt, cancellationToken);
        var validated = Validate(cluster, excerptCandidates, modelResponse);

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

    private ValidatedStory? Validate(
        CandidateCluster cluster,
        IReadOnlyList<ExcerptCandidate> excerptCandidates,
        StorySynthesisModelResponse response)
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
            if (!TryFindNormalizedSpan(response.Synthesis, phrase, out var markerMatch))
            {
                logger.LogWarning(
                    "Synthesis validation rejected cluster {CandidateClusterId} rank {Rank} reason {ReasonCode} markerPhrase {MarkerPhrase}",
                    cluster.Id,
                    cluster.Rank,
                    "marker_phrase_not_in_synthesis",
                    phrase);
                return null;
            }

            if (!ContainsLiteral(response.Synthesis, phrase))
            {
                logger.LogWarning(
                    "Synthesis validation rejected cluster {CandidateClusterId} rank {Rank} reason {ReasonCode} markerPhrase {MarkerPhrase} normalizedStartOffset {NormalizedStartOffset}",
                    cluster.Id,
                    cluster.Rank,
                    "marker_phrase_only_matches_normalized_text",
                    phrase,
                    markerMatch.StartOffset);
            }

            persistedMarkers.Add(new StoryMarkerDto(
                markerMatch.RawText,
                markerMatch.StartOffset,
                markerMatch.Length,
                marker.Kind?.Trim() ?? string.Empty,
                marker.Explanation?.Trim() ?? string.Empty));
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

    private static bool ContainsLiteral(string sourceText, string phrase)
    {
        return sourceText.IndexOf(phrase, StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static bool ContainsNormalized(string? sourceText, string excerptText)
    {
        return TryFindNormalizedSpan(sourceText, excerptText, out _);
    }

    private static bool TryFindNormalizedSpan(string? sourceText, string excerptText, out NormalizedSpanMatch match)
    {
        match = new NormalizedSpanMatch(0, 0, string.Empty);

        if (string.IsNullOrWhiteSpace(sourceText) || string.IsNullOrWhiteSpace(excerptText))
        {
            return false;
        }

        var normalizedSource = BuildNormalizedTextMap(sourceText);
        var normalizedExcerpt = BuildNormalizedTextMap(excerptText);

        if (normalizedSource.Text.Length == 0 || normalizedExcerpt.Text.Length == 0)
        {
            return false;
        }

        var normalizedIndex = normalizedSource.Text.IndexOf(normalizedExcerpt.Text, StringComparison.OrdinalIgnoreCase);

        if (normalizedIndex < 0)
        {
            return false;
        }

        var startOffset = normalizedSource.RawIndexMap[normalizedIndex];
        var endOffset = normalizedSource.RawIndexMap[normalizedIndex + normalizedExcerpt.Text.Length - 1];
        var length = (endOffset - startOffset) + 1;
        match = new NormalizedSpanMatch(startOffset, length, sourceText.Substring(startOffset, length));
        return true;
    }

    private static NormalizedTextMap BuildNormalizedTextMap(string value)
    {
        var text = new StringBuilder(value.Length);
        var rawIndexMap = new List<int>(value.Length);
        var lastWasSpace = true;

        for (var index = 0; index < value.Length; index++)
        {
            var rawChar = value[index];

            if (char.IsWhiteSpace(rawChar) || rawChar == '\u00A0')
            {
                if (lastWasSpace)
                {
                    continue;
                }

                text.Append(' ');
                rawIndexMap.Add(index);
                lastWasSpace = true;
                continue;
            }

            var normalizedChunk = NormalizeCharacter(rawChar);

            foreach (var normalizedChar in normalizedChunk)
            {
                text.Append(normalizedChar);
                rawIndexMap.Add(index);
            }

            lastWasSpace = false;
        }

        if (text.Length > 0 && text[^1] == ' ')
        {
            text.Length--;
            rawIndexMap.RemoveAt(rawIndexMap.Count - 1);
        }

        return new NormalizedTextMap(text.ToString(), rawIndexMap);
    }

    private static string NormalizeCharacter(char value)
    {
        return value switch
        {
            '\u2018' or '\u2019' or '\u201A' or '\u201B' or '\u2032' or '\u0060' => "'",
            '\u201C' or '\u201D' or '\u201E' or '\u201F' or '\u2033' => "\"",
            '\u2010' or '\u2011' or '\u2012' or '\u2013' or '\u2014' or '\u2015' or '\u2212' => "-",
            '\u2026' => "...",
            _ => value.ToString()
        };
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

    private sealed record ExcerptCandidate(
        string SnippetId,
        Guid ArticleId,
        string Camp,
        string SourceName,
        string SourceUrl,
        string Text);

    private sealed record NormalizedTextMap(string Text, IReadOnlyList<int> RawIndexMap);

    private sealed record NormalizedSpanMatch(int StartOffset, int Length, string RawText);
}
