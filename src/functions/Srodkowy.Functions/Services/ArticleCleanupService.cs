using System.Data;
using System.Diagnostics;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Srodkowy.Functions.Configuration;
using Srodkowy.Functions.Models;
using Srodkowy.Functions.Persistence;

namespace Srodkowy.Functions.Services;

public sealed class ArticleCleanupService(
    IDbContextFactory<SrodkowyDbContext> dbContextFactory,
    IOptions<CleanupOptions> options,
    IOptions<OpenAiOptions> openAiOptions,
    IServiceProvider serviceProvider,
    ILogger<ArticleCleanupService> logger)
{
    private static readonly ActivitySource ActivitySource = new(ObservabilityOptions.ArticlePreparationSourceName);
    private static readonly TimeSpan RunningTimeout = TimeSpan.FromMinutes(30);

    public async Task<CleanupRunResult> RunAsync(string triggeredBy, CancellationToken cancellationToken)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var cutoff = DateTimeOffset.UtcNow.AddHours(-options.Value.LookbackHours);
        var cleanupProcessor = GetCleanupProcessor();
        var claimedArticles = await ClaimBatchAsync(dbContext, cutoff, cleanupProcessor, cancellationToken);

        using var activity = ActivitySource.StartActivity("article.cleanup.run", ActivityKind.Internal);
        activity?.SetTag("article.cleanup.triggered_by", triggeredBy);
        activity?.SetTag("article.cleanup.candidate_count", claimedArticles.Count);

        var completedCount = 0;
        var failedCount = 0;
        var skippedCount = 0;
        var needsReviewCount = 0;
        var nonArticleCount = 0;
        var errors = new List<string>();

        foreach (var article in claimedArticles)
        {
            try
            {
                var result = await CleanupArticleAsync(article, cleanupProcessor, cancellationToken);

                if (result == CleanupArticleResult.Skipped)
                {
                    skippedCount++;
                }
                else
                {
                    completedCount++;
                }

                if (article.NeedsReview)
                {
                    needsReviewCount++;
                }

                if (article.IsProbablyNonArticle)
                {
                    nonArticleCount++;
                }

                await dbContext.SaveChangesAsync(cancellationToken);
            }
            catch (Exception exception)
            {
                failedCount++;
                article.CleanupStatus = ArticleCleanupStatus.Failed;
                article.CleanupError = exception.Message;
                article.CleanedAt = DateTimeOffset.UtcNow;
                article.CleanupRunId = null;
                await dbContext.SaveChangesAsync(cancellationToken);

                errors.Add($"{article.Url}: {exception.Message}");
                logger.LogWarning(exception, "Cleanup failed for article {ArticleId}", article.Id);
            }
        }

        activity?.SetTag("article.cleanup.completed_count", completedCount);
        activity?.SetTag("article.cleanup.failed_count", failedCount);
        activity?.SetTag("article.cleanup.skipped_count", skippedCount);

        return new CleanupRunResult(
            triggeredBy,
            claimedArticles.Count,
            completedCount,
            failedCount,
            skippedCount,
            needsReviewCount,
            nonArticleCount,
            errors);
    }

    private async Task<List<Article>> ClaimBatchAsync(
        SrodkowyDbContext dbContext,
        DateTimeOffset cutoff,
        string cleanupProcessor,
        CancellationToken cancellationToken)
    {
        var strategy = dbContext.Database.CreateExecutionStrategy();

        return await strategy.ExecuteAsync(async () =>
        {
            var runId = Guid.NewGuid();
            var claimStartedAt = DateTimeOffset.UtcNow;
            var runningExpiredBefore = claimStartedAt - RunningTimeout;

            await using var transaction = await dbContext.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);

            var eligibleIds = await dbContext.Articles
                .Where(article => article.ScrapedAt >= cutoff)
                .Where(article =>
                    article.CleanupStatus == ArticleCleanupStatus.Pending
                    || article.CleanupStatus == ArticleCleanupStatus.Failed
                    || article.CleanupStatus == ArticleCleanupStatus.Stale
                    || article.CleanupStatus == ArticleCleanupStatus.Running && article.CleanupStartedAt != null && article.CleanupStartedAt < runningExpiredBefore
                    || article.CleanupStatus == ArticleCleanupStatus.Completed && (article.CleanupProcessor != cleanupProcessor || article.CleanupInputHash == null))
                .OrderBy(article => article.ScrapedAt)
                .Select(article => article.Id)
                .Take(options.Value.BatchSize)
                .ToArrayAsync(cancellationToken);

            if (eligibleIds.Length == 0)
            {
                await transaction.CommitAsync(cancellationToken);
                return [];
            }

            await dbContext.Articles
                .Where(article => eligibleIds.Contains(article.Id))
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(article => article.CleanupStatus, ArticleCleanupStatus.Running)
                    .SetProperty(article => article.CleanupStartedAt, claimStartedAt)
                    .SetProperty(article => article.CleanupRunId, runId)
                    .SetProperty(article => article.CleanupError, (string?)null), cancellationToken);

            var claimedArticles = await dbContext.Articles
                .Where(article => article.CleanupRunId == runId)
                .OrderBy(article => article.ScrapedAt)
                .ToListAsync(cancellationToken);

            await transaction.CommitAsync(cancellationToken);
            return claimedArticles;
        });
    }

    private async Task<CleanupArticleResult> CleanupArticleAsync(Article article, string cleanupProcessor, CancellationToken cancellationToken)
    {
        using var activity = ActivitySource.StartActivity("article.cleanup.process", ActivityKind.Internal);
        activity?.SetTag("article.id", article.Id.ToString());
        activity?.SetTag("article.url", article.Url);
        activity?.SetTag("source.id", article.SourceId.ToString());
        activity?.SetTag("cleanup.run_id", article.CleanupRunId?.ToString());
        activity?.SetTag("cleanup.model", openAiOptions.Value.CleanupModel);

        var normalization = ArticleCleanupHeuristics.NormalizeForCleanup(article.ContentMarkdown, options.Value.MaxInputCharacters);
        var cleanupInput = ArticlePreparationText.BuildCleanupInput(article.Title, normalization.Markdown);
        var cleanupHash = ArticlePreparationText.ComputeHash(cleanupInput);

        logger.LogInformation(
            "Cleanup starting for article {ArticleId} source {SourceId} run {CleanupRunId} model {CleanupModel} normalizedChars {NormalizedChars}",
            article.Id,
            article.SourceId,
            article.CleanupRunId,
            openAiOptions.Value.CleanupModel,
            normalization.Markdown.Length);

        activity?.SetTag("cleanup.normalized_input_chars", normalization.Markdown.Length);
        activity?.SetTag("cleanup.flags_count", normalization.Flags.Count);

        if (article.CleanupStatus == ArticleCleanupStatus.Completed
            && article.CleanupInputHash == cleanupHash
            && string.Equals(article.CleanupProcessor, cleanupProcessor, StringComparison.Ordinal))
        {
            logger.LogInformation(
                "Cleanup skipped for article {ArticleId} because content hash and processor match current state",
                article.Id);

            activity?.SetTag("cleanup.skipped", true);
            article.CleanupRunId = null;
            article.CleanupStatus = ArticleCleanupStatus.Completed;
            return CleanupArticleResult.Skipped;
        }

        var llmResult = await CleanupWithLlmAsync(article.Title, normalization.Markdown, cancellationToken);
        activity?.SetTag("cleanup.is_non_article", llmResult.IsProbablyNonArticle);
        var cleanedText = string.IsNullOrWhiteSpace(llmResult.CleanedText)
            ? null
            : ArticleCleanupHeuristics.AnalyzeOutput(article.Title, llmResult.CleanedText, options.Value.MinCleanedLength);

        if (!llmResult.IsProbablyNonArticle && cleanedText is null)
        {
            throw new InvalidOperationException("Cleanup produced an empty article body for an article response.");
        }

        var flags = normalization.Flags.ToList();

        if (!string.IsNullOrWhiteSpace(llmResult.Reason))
        {
            flags.Add($"reason:{llmResult.Reason.Trim()}" );
        }

        if (llmResult.IsProbablyNonArticle)
        {
            flags.Add("non_article");
        }

        if (cleanedText is not null)
        {
            flags.AddRange(cleanedText.Flags);
        }

        var previousEmbeddingInput = article.CleanedContentText is null
            ? null
            : ArticlePreparationText.BuildEmbeddingInput(article.Title, article.CleanedContentText, int.MaxValue);
        var nextCleanedText = llmResult.IsProbablyNonArticle ? null : cleanedText!.CleanedText;
        var nextEmbeddingInput = nextCleanedText is null
            ? null
            : ArticlePreparationText.BuildEmbeddingInput(article.Title, nextCleanedText, int.MaxValue);
        var embeddingInputChanged = previousEmbeddingInput != nextEmbeddingInput;

        article.CleanedContentText = nextCleanedText;
        article.CleanupStatus = ArticleCleanupStatus.Completed;
        article.CleanedAt = DateTimeOffset.UtcNow;
        article.CleanupRunId = null;
        article.CleanupProcessor = cleanupProcessor;
        article.CleanupError = null;
        article.CleanupInputHash = cleanupHash;
        article.CleanupFlagsJson = JsonSerializer.Serialize(flags.Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(flag => flag));
        article.QualityScore = llmResult.IsProbablyNonArticle ? 0 : cleanedText!.QualityScore;
        article.NeedsReview = !llmResult.IsProbablyNonArticle && cleanedText!.NeedsReview;
        article.IsProbablyNonArticle = llmResult.IsProbablyNonArticle;

        activity?.SetTag("cleanup.quality_score", article.QualityScore);
        activity?.SetTag("cleanup.needs_review", article.NeedsReview);
        activity?.SetTag("cleanup.output_chars", article.CleanedContentText?.Length ?? 0);

        logger.LogInformation(
            "Cleanup completed for article {ArticleId} run {CleanupRunId} nonArticle {IsProbablyNonArticle} needsReview {NeedsReview} qualityScore {QualityScore} outputChars {OutputChars}",
            article.Id,
            article.CleanupRunId,
            article.IsProbablyNonArticle,
            article.NeedsReview,
            article.QualityScore,
            article.CleanedContentText?.Length ?? 0);

        if (llmResult.IsProbablyNonArticle)
        {
            article.Embedding = null;
            article.EmbeddingModel = null;
            article.EmbeddingStatus = ArticleEmbeddingStatus.Pending;
            article.EmbeddedAt = null;
            article.EmbeddingStartedAt = null;
            article.EmbeddingRunId = null;
            article.EmbeddingError = null;
            article.EmbeddingTextHash = null;
            return CleanupArticleResult.Completed;
        }

        if (embeddingInputChanged)
        {
            article.EmbeddingError = null;

            if (article.EmbeddingStatus == ArticleEmbeddingStatus.Completed && article.Embedding is not null)
            {
                article.EmbeddingStatus = ArticleEmbeddingStatus.Stale;
            }
            else
            {
                article.EmbeddingStatus = ArticleEmbeddingStatus.Pending;
            }
        }

        return CleanupArticleResult.Completed;
    }

    private async Task<LlmCleanupResponse> CleanupWithLlmAsync(string title, string normalizedMarkdown, CancellationToken cancellationToken)
    {
        var cleanupClient = serviceProvider.GetRequiredKeyedService<IChatClient>("cleanup");
        var prompt = BuildCleanupPrompt(title, normalizedMarkdown);
        logger.LogInformation(
            "Calling cleanup model {CleanupModel} for title {Title} with {InputChars} chars",
            openAiOptions.Value.CleanupModel,
            title,
            normalizedMarkdown.Length);
        var response = await cleanupClient.GetResponseAsync<LlmCleanupResponse>(prompt, cancellationToken: cancellationToken);
        logger.LogInformation(
            "Cleanup model {CleanupModel} returned nonArticle {IsProbablyNonArticle} outputChars {OutputChars}",
            openAiOptions.Value.CleanupModel,
            response.Result.IsProbablyNonArticle,
            response.Result.CleanedText?.Length ?? 0);
        return response.Result;
    }

    private static string BuildCleanupPrompt(string title, string normalizedMarkdown)
    {
        return $"""
You extract the main article body from a scraped news page.

Return strict JSON with these fields:
- cleanedText: string
- isProbablyNonArticle: boolean
- reason: string

Rules:
- use the title to identify what belongs to the article
- keep only the article body relevant to the title
- remove navigation, tickers, embeds, captions, related links, promo blocks, share labels, paywall chrome, and footer junk
- preserve wording and order as much as possible
- do not summarize
- do not rewrite
- do not add facts
- if the page is probably not a real article body, set isProbablyNonArticle=true and cleanedText=""

Title:
{title}

Normalized markdown:
{normalizedMarkdown}
""";
    }

    private string GetCleanupProcessor() => $"{ArticleCleanupHeuristics.CleanupProcessorVersion}:{openAiOptions.Value.CleanupModel}";

    public sealed record CleanupRunResult(
        string TriggeredBy,
        int CandidateCount,
        int CompletedCount,
        int FailedCount,
        int SkippedCount,
        int NeedsReviewCount,
        int NonArticleCount,
        IReadOnlyList<string> Errors);

    private sealed record LlmCleanupResponse(string CleanedText, bool IsProbablyNonArticle, string Reason);

    private enum CleanupArticleResult
    {
        Completed,
        Skipped
    }
}
