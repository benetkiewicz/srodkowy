namespace Srodkowy.Functions.Models;

public sealed class CandidateCluster
{
    public Guid Id { get; set; }

    public Guid ClusterRunId { get; set; }

    public ClusterRun ClusterRun { get; set; } = null!;

    public Guid RepresentativeArticleId { get; set; }

    public Article RepresentativeArticle { get; set; } = null!;

    public int Rank { get; set; }

    public double RankScore { get; set; }

    public string Status { get; set; } = string.Empty;

    public int ArticleCount { get; set; }

    public int DistinctSourceCount { get; set; }

    public int LeftArticleCount { get; set; }

    public int RightArticleCount { get; set; }

    public DateTimeOffset WindowStartAt { get; set; }

    public DateTimeOffset WindowEndAt { get; set; }

    public double MeanSimilarity { get; set; }

    public double NarrativeDivergenceScore { get; set; }

    public double BalanceScore { get; set; }

    public string SelectionVersion { get; set; } = string.Empty;

    public ICollection<CandidateClusterArticle> Articles { get; set; } = new List<CandidateClusterArticle>();
}
