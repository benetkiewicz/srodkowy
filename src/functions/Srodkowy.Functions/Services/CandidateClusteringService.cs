using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Srodkowy.Functions.Configuration;
using Srodkowy.Functions.Models;
using Srodkowy.Functions.Persistence;

namespace Srodkowy.Functions.Services;

public sealed class CandidateClusteringService(
    IDbContextFactory<SrodkowyDbContext> dbContextFactory,
    IOptions<ClusteringOptions> options,
    ILogger<CandidateClusteringService> logger)
{
    private const string CompletedStatus = "completed";
    private const string FailedStatus = "failed";
    private const string RunningStatus = "running";
    private const string CandidateStatus = "candidate";
    private const string SelectionVersion = "clustering-v1";
    private static readonly ActivitySource ActivitySource = new(ObservabilityOptions.ClusteringSourceName);

    public async Task<ClusteringRunResult> RunAsync(
        string triggeredBy,
        ClusteringRunRequest? request,
        CancellationToken cancellationToken)
    {
        var effectiveOptions = EffectiveOptions.Create(options.Value, request);
        var runId = Guid.NewGuid();
        var startedAt = DateTimeOffset.UtcNow;

        using var activity = ActivitySource.StartActivity("candidate.clustering.run", ActivityKind.Internal);
        activity?.SetTag("clustering.run_id", runId.ToString());
        activity?.SetTag("clustering.triggered_by", triggeredBy);
        activity?.SetTag("clustering.lookback_hours", effectiveOptions.LookbackHours);
        activity?.SetTag("clustering.dry_run", effectiveOptions.DryRun);

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);

        ClusterRun? run = null;

        if (!effectiveOptions.DryRun)
        {
            run = new ClusterRun
            {
                Id = runId,
                StartedAt = startedAt,
                Status = RunningStatus,
                TriggeredBy = triggeredBy,
                LookbackHours = effectiveOptions.LookbackHours
            };

            dbContext.ClusterRuns.Add(run);
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        try
        {
            var candidates = await LoadCandidatesAsync(dbContext, effectiveOptions, cancellationToken);
            var deduplicatedCandidates = DeduplicateCandidates(candidates, effectiveOptions);
            var clusters = ClusterCandidates(deduplicatedCandidates, effectiveOptions);
            var qualifiedClusters = clusters
                .Where(cluster => IsQualifiedCluster(cluster, effectiveOptions))
                .Select(cluster => BuildScoredCluster(cluster, startedAt, effectiveOptions))
                .OrderByDescending(cluster => cluster.RankScore)
                .ThenByDescending(cluster => cluster.DistinctSourceCount)
                .ThenByDescending(cluster => cluster.WindowEndAt)
                .ThenBy(cluster => cluster.RepresentativeArticle.Id)
                .Take(effectiveOptions.MaxClusters)
                .ToList();

            for (var index = 0; index < qualifiedClusters.Count; index++)
            {
                qualifiedClusters[index] = qualifiedClusters[index] with { Rank = index + 1 };
            }

            activity?.SetTag("clustering.candidate_count", candidates.Count);
            activity?.SetTag("clustering.deduplicated_count", deduplicatedCandidates.Count);
            activity?.SetTag("clustering.cluster_count", clusters.Count);
            activity?.SetTag("clustering.qualified_cluster_count", qualifiedClusters.Count);

            logger.LogInformation(
                "Clustering run {RunId} found {CandidateCount} candidates, {DeduplicatedCount} deduplicated candidates, {ClusterCount} raw clusters, and {QualifiedClusterCount} qualified clusters",
                runId,
                candidates.Count,
                deduplicatedCandidates.Count,
                clusters.Count,
                qualifiedClusters.Count);

            var persistedClusterIds = effectiveOptions.DryRun
                ? Array.Empty<Guid>()
                : await PersistClustersAsync(dbContext, run!, qualifiedClusters, cancellationToken);

            if (run is not null)
            {
                run.CandidateArticleCount = candidates.Count;
                run.DeduplicatedArticleCount = deduplicatedCandidates.Count;
                run.ClusterCount = clusters.Count;
                run.QualifiedClusterCount = qualifiedClusters.Count;
                run.Status = CompletedStatus;
                run.CompletedAt = DateTimeOffset.UtcNow;
                await dbContext.SaveChangesAsync(cancellationToken);
            }

            return new ClusteringRunResult(
                runId,
                triggeredBy,
                candidates.Count,
                deduplicatedCandidates.Count,
                clusters.Count,
                qualifiedClusters.Count,
                persistedClusterIds,
                []);
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Candidate clustering run {RunId} failed", runId);
            activity?.SetTag("clustering.failed", true);

            if (run is not null)
            {
                run.Status = FailedStatus;
                run.CompletedAt = DateTimeOffset.UtcNow;
                run.ErrorSummary = exception.Message;
                await dbContext.SaveChangesAsync(cancellationToken);
            }

            throw;
        }
    }

    private async Task<List<CandidateArticle>> LoadCandidatesAsync(
        SrodkowyDbContext dbContext,
        EffectiveOptions effectiveOptions,
        CancellationToken cancellationToken)
    {
        var cutoff = DateTimeOffset.UtcNow.AddHours(-effectiveOptions.LookbackHours);
        var articles = await dbContext.Articles
            .AsNoTracking()
            .Where(article => article.Source.Active)
            .Where(article => article.CleanupStatus == ArticleCleanupStatus.Completed)
            .Where(article => article.EmbeddingStatus == ArticleEmbeddingStatus.Completed)
            .Where(article => article.Embedding.HasValue)
            .Where(article => article.CleanedContentText != null)
            .Where(article => !article.IsProbablyNonArticle)
            .Where(article => article.QualityScore >= effectiveOptions.MinQualityScore)
            .Where(article => !effectiveOptions.ExcludeNeedsReview || !article.NeedsReview)
            .Where(article => (article.PublishedAt ?? article.ScrapedAt) >= cutoff)
            .Select(article => new
            {
                article.Id,
                article.SourceId,
                article.QualityScore,
                article.PublishedAt,
                article.ScrapedAt,
                Embedding = article.Embedding!.Value,
                Camp = article.Source.Camp
            })
            .ToListAsync(cancellationToken);

        return articles
            .Where(article => !article.Embedding.IsNull)
            .Select(article => new CandidateArticle(
                article.Id,
                article.SourceId,
                article.Camp,
                article.QualityScore,
                article.PublishedAt,
                article.ScrapedAt,
                article.Embedding.Memory.ToArray()))
            .ToList();
    }

    private static List<CandidateArticle> DeduplicateCandidates(
        IReadOnlyList<CandidateArticle> candidates,
        EffectiveOptions effectiveOptions)
    {
        var deduplicated = new List<CandidateArticle>();

        foreach (var sourceGroup in candidates
                     .GroupBy(article => article.SourceId)
                     .OrderBy(group => group.Key))
        {
            foreach (var article in sourceGroup
                         .OrderByDescending(candidate => candidate.QualityScore)
                         .ThenByDescending(candidate => candidate.Timestamp)
                         .ThenBy(candidate => candidate.Id))
            {
                if (deduplicated.Any(existing =>
                        existing.SourceId == article.SourceId
                        && Similarity(existing, article) >= effectiveOptions.NearDuplicateSimilarity
                        && Math.Abs((existing.Timestamp - article.Timestamp).TotalHours) <= 24))
                {
                    continue;
                }

                deduplicated.Add(article);
            }
        }

        return deduplicated;
    }

    private static List<List<CandidateArticle>> ClusterCandidates(
        IReadOnlyList<CandidateArticle> candidates,
        EffectiveOptions effectiveOptions)
    {
        var clusters = candidates
            .OrderBy(candidate => candidate.Timestamp)
            .ThenBy(candidate => candidate.Id)
            .Select(candidate => new List<CandidateArticle> { candidate })
            .ToList();

        while (true)
        {
            var bestMerge = FindBestMerge(clusters, effectiveOptions);

            if (bestMerge is null)
            {
                break;
            }

            var merged = bestMerge.Left
                .Concat(bestMerge.Right)
                .OrderBy(candidate => candidate.Timestamp)
                .ThenBy(candidate => candidate.Id)
                .ToList();

            clusters.Remove(bestMerge.Left);
            clusters.Remove(bestMerge.Right);
            clusters.Add(merged);
        }

        return clusters;
    }

    private static ClusterMerge? FindBestMerge(
        IReadOnlyList<List<CandidateArticle>> clusters,
        EffectiveOptions effectiveOptions)
    {
        ClusterMerge? bestMerge = null;

        for (var leftIndex = 0; leftIndex < clusters.Count; leftIndex++)
        {
            for (var rightIndex = leftIndex + 1; rightIndex < clusters.Count; rightIndex++)
            {
                var left = clusters[leftIndex];
                var right = clusters[rightIndex];
                var averageSimilarity = AverageSimilarity(left, right);

                if (!CanMerge(left, right, averageSimilarity, effectiveOptions))
                {
                    continue;
                }

                if (bestMerge is null
                    || averageSimilarity > bestMerge.AverageSimilarity
                    || averageSimilarity == bestMerge.AverageSimilarity && CompareClusters(left, bestMerge.Left) < 0)
                {
                    bestMerge = new ClusterMerge(left, right, averageSimilarity);
                }
            }
        }

        return bestMerge;
    }

    private static bool CanMerge(
        IReadOnlyList<CandidateArticle> left,
        IReadOnlyList<CandidateArticle> right,
        double averageSimilarity,
        EffectiveOptions effectiveOptions)
    {
        if (left.Count + right.Count > effectiveOptions.MaxClusterSize)
        {
            return false;
        }

        if (left.Select(article => article.SourceId).Intersect(right.Select(article => article.SourceId)).Any())
        {
            return false;
        }

        var combined = left.Concat(right).ToList();
        var timespan = combined.Max(article => article.Timestamp) - combined.Min(article => article.Timestamp);

        if (timespan.TotalHours > effectiveOptions.MaxClusterTimespanHours)
        {
            return false;
        }

        if (averageSimilarity < effectiveOptions.MergeSimilarityThreshold)
        {
            return false;
        }

        return left.Any(leftArticle =>
            right.Any(rightArticle =>
                Similarity(leftArticle, rightArticle) >= effectiveOptions.PairSimilarityThreshold
                && Math.Abs((leftArticle.Timestamp - rightArticle.Timestamp).TotalHours) <= effectiveOptions.MaxPairTimespanHours));
    }

    private static bool IsQualifiedCluster(
        IReadOnlyList<CandidateArticle> cluster,
        EffectiveOptions effectiveOptions)
    {
        if (cluster.Count < 2)
        {
            return false;
        }

        var distinctSourceCount = cluster.Select(article => article.SourceId).Distinct().Count();

        if (distinctSourceCount < 2)
        {
            return false;
        }

        var leftCount = cluster.Count(article => string.Equals(article.Camp, SourceCamp.Left, StringComparison.OrdinalIgnoreCase));
        var rightCount = cluster.Count(article => string.Equals(article.Camp, SourceCamp.Right, StringComparison.OrdinalIgnoreCase));

        if (leftCount == 0 || rightCount == 0)
        {
            return false;
        }

        var timespan = cluster.Max(article => article.Timestamp) - cluster.Min(article => article.Timestamp);
        return timespan.TotalHours <= effectiveOptions.MaxClusterTimespanHours;
    }

    private static ScoredCluster BuildScoredCluster(
        IReadOnlyList<CandidateArticle> cluster,
        DateTimeOffset runStartedAt,
        EffectiveOptions effectiveOptions)
    {
        var representative = cluster
            .Select(article => new
            {
                Article = article,
                Score = cluster.Where(other => other.Id != article.Id).Select(other => Similarity(article, other)).DefaultIfEmpty(1d).Average()
            })
            .OrderByDescending(item => item.Score)
            .ThenByDescending(item => item.Article.QualityScore)
            .ThenByDescending(item => item.Article.Timestamp)
            .ThenBy(item => item.Article.Id)
            .First()
            .Article;

        var leftArticles = cluster.Where(article => string.Equals(article.Camp, SourceCamp.Left, StringComparison.OrdinalIgnoreCase)).ToList();
        var rightArticles = cluster.Where(article => string.Equals(article.Camp, SourceCamp.Right, StringComparison.OrdinalIgnoreCase)).ToList();
        var articleCount = cluster.Count;
        var distinctSourceCount = cluster.Select(article => article.SourceId).Distinct().Count();
        var meanSimilarity = MeanSimilarity(cluster);
        var balanceScore = 1d - (Math.Abs(leftArticles.Count - rightArticles.Count) / (double)articleCount);
        var divergenceScore = Math.Clamp(1d - CosineSimilarity(Centroid(leftArticles), Centroid(rightArticles)), 0d, 1d);
        var sourceCoverage = Math.Min(1d, distinctSourceCount / 6d);
        var windowStartAt = cluster.Min(article => article.Timestamp);
        var windowEndAt = cluster.Max(article => article.Timestamp);
        var recencyScore = 1d - (Math.Min(effectiveOptions.LookbackHours, (runStartedAt - windowEndAt).TotalHours) / effectiveOptions.LookbackHours);
        var rankScore = (0.45 * sourceCoverage)
            + (0.25 * balanceScore)
            + (0.20 * divergenceScore)
            + (0.10 * recencyScore);

        return new ScoredCluster(
            Guid.NewGuid(),
            0,
            representative,
            cluster.OrderBy(article => article.Timestamp).ThenBy(article => article.Id).ToList(),
            rankScore,
            distinctSourceCount,
            leftArticles.Count,
            rightArticles.Count,
            windowStartAt,
            windowEndAt,
            meanSimilarity,
            divergenceScore,
            balanceScore);
    }

    private static async Task<IReadOnlyList<Guid>> PersistClustersAsync(
        SrodkowyDbContext dbContext,
        ClusterRun run,
        IReadOnlyList<ScoredCluster> clusters,
        CancellationToken cancellationToken)
    {
        if (clusters.Count == 0)
        {
            return [];
        }

        var persistedIds = new List<Guid>(clusters.Count);

        foreach (var cluster in clusters)
        {
            var entity = new CandidateCluster
            {
                Id = cluster.Id,
                ClusterRunId = run.Id,
                RepresentativeArticleId = cluster.RepresentativeArticle.Id,
                Rank = cluster.Rank,
                RankScore = cluster.RankScore,
                Status = CandidateStatus,
                ArticleCount = cluster.Articles.Count,
                DistinctSourceCount = cluster.DistinctSourceCount,
                LeftArticleCount = cluster.LeftArticleCount,
                RightArticleCount = cluster.RightArticleCount,
                WindowStartAt = cluster.WindowStartAt,
                WindowEndAt = cluster.WindowEndAt,
                MeanSimilarity = cluster.MeanSimilarity,
                NarrativeDivergenceScore = cluster.NarrativeDivergenceScore,
                BalanceScore = cluster.BalanceScore,
                SelectionVersion = SelectionVersion,
                Articles = cluster.Articles.Select(article => new CandidateClusterArticle
                {
                    CandidateClusterId = cluster.Id,
                    ArticleId = article.Id,
                    SourceId = article.SourceId,
                    Camp = article.Camp,
                    SimilarityToRepresentative = Similarity(article, cluster.RepresentativeArticle),
                    IsRepresentative = article.Id == cluster.RepresentativeArticle.Id
                }).ToList()
            };

            dbContext.CandidateClusters.Add(entity);
            persistedIds.Add(entity.Id);
        }

        run.Status = CompletedStatus;
        await dbContext.SaveChangesAsync(cancellationToken);
        return persistedIds;
    }

    private static int CompareClusters(IReadOnlyList<CandidateArticle> left, IReadOnlyList<CandidateArticle> right)
    {
        var leftKey = string.Join('|', left.Select(article => article.Id).OrderBy(id => id));
        var rightKey = string.Join('|', right.Select(article => article.Id).OrderBy(id => id));
        return string.CompareOrdinal(leftKey, rightKey);
    }

    private static double MeanSimilarity(IReadOnlyList<CandidateArticle> cluster)
    {
        if (cluster.Count < 2)
        {
            return 1d;
        }

        var similarities = new List<double>();

        for (var index = 0; index < cluster.Count; index++)
        {
            for (var otherIndex = index + 1; otherIndex < cluster.Count; otherIndex++)
            {
                similarities.Add(Similarity(cluster[index], cluster[otherIndex]));
            }
        }

        return similarities.Average();
    }

    private static double AverageSimilarity(IReadOnlyList<CandidateArticle> left, IReadOnlyList<CandidateArticle> right)
    {
        var similarities = new List<double>(left.Count * right.Count);

        foreach (var leftArticle in left)
        {
            foreach (var rightArticle in right)
            {
                similarities.Add(Similarity(leftArticle, rightArticle));
            }
        }

        return similarities.Average();
    }

    private static double Similarity(CandidateArticle left, CandidateArticle right) =>
        CosineSimilarity(left.Embedding, right.Embedding);

    private static double CosineSimilarity(float[] left, float[] right)
    {
        var dot = 0d;
        var leftMagnitude = 0d;
        var rightMagnitude = 0d;

        for (var index = 0; index < left.Length; index++)
        {
            dot += left[index] * right[index];
            leftMagnitude += left[index] * left[index];
            rightMagnitude += right[index] * right[index];
        }

        if (leftMagnitude == 0d || rightMagnitude == 0d)
        {
            return 0d;
        }

        return dot / (Math.Sqrt(leftMagnitude) * Math.Sqrt(rightMagnitude));
    }

    private static float[] Centroid(IReadOnlyList<CandidateArticle> articles)
    {
        if (articles.Count == 0)
        {
            return [];
        }

        var centroid = new float[articles[0].Embedding.Length];

        foreach (var article in articles)
        {
            for (var index = 0; index < article.Embedding.Length; index++)
            {
                centroid[index] += article.Embedding[index];
            }
        }

        for (var index = 0; index < centroid.Length; index++)
        {
            centroid[index] /= articles.Count;
        }

        return centroid;
    }

    public sealed record ClusteringRunRequest(
        int? LookbackHours = null,
        int? MaxClusters = null,
        bool DryRun = false);

    public sealed record ClusteringRunResult(
        Guid RunId,
        string TriggeredBy,
        int CandidateArticleCount,
        int DeduplicatedArticleCount,
        int ClusterCount,
        int QualifiedClusterCount,
        IReadOnlyList<Guid> QualifiedClusterIds,
        IReadOnlyList<string> Errors);

    private sealed record CandidateArticle(
        Guid Id,
        Guid SourceId,
        string Camp,
        int QualityScore,
        DateTimeOffset? PublishedAt,
        DateTimeOffset ScrapedAt,
        float[] Embedding)
    {
        public DateTimeOffset Timestamp => PublishedAt ?? ScrapedAt;
    }

    private sealed record EffectiveOptions(
        int LookbackHours,
        int MinQualityScore,
        double NearDuplicateSimilarity,
        double PairSimilarityThreshold,
        double MergeSimilarityThreshold,
        int MaxPairTimespanHours,
        int MaxClusterTimespanHours,
        int MaxClusterSize,
        int MaxClusters,
        bool ExcludeNeedsReview,
        bool DryRun)
    {
        public static EffectiveOptions Create(ClusteringOptions options, ClusteringRunRequest? request)
        {
            return new EffectiveOptions(
                request is { LookbackHours: > 0 } ? request.LookbackHours.Value : options.LookbackHours,
                options.MinQualityScore,
                options.NearDuplicateSimilarity,
                options.PairSimilarityThreshold,
                options.MergeSimilarityThreshold,
                options.MaxPairTimespanHours,
                options.MaxClusterTimespanHours,
                options.MaxClusterSize,
                request is { MaxClusters: > 0 } ? request.MaxClusters.Value : options.MaxClusters,
                options.ExcludeNeedsReview,
                request?.DryRun ?? false);
        }
    }

    private sealed record ClusterMerge(
        List<CandidateArticle> Left,
        List<CandidateArticle> Right,
        double AverageSimilarity);

    private sealed record ScoredCluster(
        Guid Id,
        int Rank,
        CandidateArticle RepresentativeArticle,
        IReadOnlyList<CandidateArticle> Articles,
        double RankScore,
        int DistinctSourceCount,
        int LeftArticleCount,
        int RightArticleCount,
        DateTimeOffset WindowStartAt,
        DateTimeOffset WindowEndAt,
        double MeanSimilarity,
        double NarrativeDivergenceScore,
        double BalanceScore);
}
