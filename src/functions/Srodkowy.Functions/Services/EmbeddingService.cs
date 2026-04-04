using System.Data;
using System.Diagnostics;
using Microsoft.Data.SqlTypes;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Srodkowy.Functions.Configuration;
using Srodkowy.Functions.Models;
using Srodkowy.Functions.Persistence;

namespace Srodkowy.Functions.Services;

public sealed class EmbeddingService(
    IDbContextFactory<SrodkowyDbContext> dbContextFactory,
    IOptions<EmbeddingOptions> options,
    IOptions<OpenAiOptions> openAiOptions,
    IServiceProvider serviceProvider,
    ILogger<EmbeddingService> logger)
{
    private static readonly ActivitySource ActivitySource = new(ObservabilityOptions.ArticlePreparationSourceName);
    private static readonly TimeSpan RunningTimeout = TimeSpan.FromMinutes(30);

    public async Task<EmbeddingRunResult> RunAsync(string triggeredBy, CancellationToken cancellationToken)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var cutoff = DateTimeOffset.UtcNow.AddHours(-options.Value.LookbackHours);
        var configuredModel = openAiOptions.Value.EmbeddingModel;
        var claimedArticles = await ClaimBatchAsync(dbContext, cutoff, configuredModel, cancellationToken);

        using var activity = ActivitySource.StartActivity("article.embedding.run", ActivityKind.Internal);
        activity?.SetTag("article.embedding.triggered_by", triggeredBy);
        activity?.SetTag("article.embedding.candidate_count", claimedArticles.Count);

        var completedCount = 0;
        var failedCount = 0;
        var skippedCount = 0;
        var errors = new List<string>();

        foreach (var article in claimedArticles)
        {
            try
            {
                var result = await EmbedArticleAsync(article, configuredModel, cancellationToken);

                if (result == EmbedArticleResult.Skipped)
                {
                    skippedCount++;
                }
                else
                {
                    completedCount++;
                }

                await dbContext.SaveChangesAsync(cancellationToken);
            }
            catch (Exception exception)
            {
                failedCount++;
                article.EmbeddingStatus = ArticleEmbeddingStatus.Failed;
                article.EmbeddingError = exception.Message;
                article.EmbeddedAt = DateTimeOffset.UtcNow;
                article.EmbeddingRunId = null;
                await dbContext.SaveChangesAsync(cancellationToken);

                errors.Add($"{article.Url}: {exception.Message}");
                logger.LogWarning(exception, "Embedding failed for article {ArticleId}", article.Id);
            }
        }

        activity?.SetTag("article.embedding.completed_count", completedCount);
        activity?.SetTag("article.embedding.failed_count", failedCount);
        activity?.SetTag("article.embedding.skipped_count", skippedCount);

        return new EmbeddingRunResult(triggeredBy, claimedArticles.Count, completedCount, failedCount, skippedCount, errors);
    }

    private async Task<List<Article>> ClaimBatchAsync(
        SrodkowyDbContext dbContext,
        DateTimeOffset cutoff,
        string embeddingModel,
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
                .Where(article => article.CleanupStatus == ArticleCleanupStatus.Completed)
                .Where(article => !article.IsProbablyNonArticle)
                .Where(article => article.CleanedContentText != null)
                .Where(article =>
                    article.EmbeddingStatus == ArticleEmbeddingStatus.Pending
                    || article.EmbeddingStatus == ArticleEmbeddingStatus.Failed
                    || article.EmbeddingStatus == ArticleEmbeddingStatus.Stale
                    || article.EmbeddingStatus == ArticleEmbeddingStatus.Running && article.EmbeddingStartedAt != null && article.EmbeddingStartedAt < runningExpiredBefore
                    || article.EmbeddingStatus == ArticleEmbeddingStatus.Completed && (article.EmbeddingModel != embeddingModel || article.EmbeddingTextHash == null || article.EmbeddedAt == null))
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
                    .SetProperty(article => article.EmbeddingStatus, ArticleEmbeddingStatus.Running)
                    .SetProperty(article => article.EmbeddingStartedAt, claimStartedAt)
                    .SetProperty(article => article.EmbeddingRunId, runId)
                    .SetProperty(article => article.EmbeddingError, (string?)null), cancellationToken);

            var claimedArticles = await dbContext.Articles
                .Where(article => article.EmbeddingRunId == runId)
                .OrderBy(article => article.ScrapedAt)
                .ToListAsync(cancellationToken);

            await transaction.CommitAsync(cancellationToken);
            return claimedArticles;
        });
    }

    private async Task<EmbedArticleResult> EmbedArticleAsync(Article article, string configuredModel, CancellationToken cancellationToken)
    {
        var cleanedText = article.CleanedContentText;

        if (string.IsNullOrWhiteSpace(cleanedText))
        {
            article.EmbeddingRunId = null;
            article.EmbeddingStatus = ArticleEmbeddingStatus.Pending;
            return EmbedArticleResult.Skipped;
        }

        var embeddingInput = ArticlePreparationText.BuildEmbeddingInput(article.Title, cleanedText, options.Value.MaxInputCharacters);
        var embeddingHash = ArticlePreparationText.ComputeHash(embeddingInput);

        if (article.EmbeddingStatus == ArticleEmbeddingStatus.Completed
            && article.EmbeddingModel == configuredModel
            && string.Equals(article.EmbeddingTextHash, embeddingHash, StringComparison.Ordinal)
            && article.Embedding is not null)
        {
            article.EmbeddingRunId = null;
            return EmbedArticleResult.Skipped;
        }

        var embeddingGenerator = serviceProvider.GetRequiredService<IEmbeddingGenerator<string, Embedding<float>>>();
        var embedding = await embeddingGenerator.GenerateVectorAsync(embeddingInput, cancellationToken: cancellationToken);

        article.Embedding = new SqlVector<float>(embedding);
        article.EmbeddingModel = configuredModel;
        article.EmbeddingStatus = ArticleEmbeddingStatus.Completed;
        article.EmbeddedAt = DateTimeOffset.UtcNow;
        article.EmbeddingRunId = null;
        article.EmbeddingError = null;
        article.EmbeddingTextHash = embeddingHash;

        return EmbedArticleResult.Completed;
    }

    public sealed record EmbeddingRunResult(
        string TriggeredBy,
        int CandidateCount,
        int CompletedCount,
        int FailedCount,
        int SkippedCount,
        IReadOnlyList<string> Errors);

    private enum EmbedArticleResult
    {
        Completed,
        Skipped
    }
}
