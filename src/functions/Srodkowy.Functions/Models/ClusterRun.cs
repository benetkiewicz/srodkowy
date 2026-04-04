namespace Srodkowy.Functions.Models;

public sealed class ClusterRun
{
    public Guid Id { get; set; }

    public DateTimeOffset StartedAt { get; set; }

    public DateTimeOffset? CompletedAt { get; set; }

    public string Status { get; set; } = string.Empty;

    public string TriggeredBy { get; set; } = string.Empty;

    public int LookbackHours { get; set; }

    public int CandidateArticleCount { get; set; }

    public int DeduplicatedArticleCount { get; set; }

    public int ClusterCount { get; set; }

    public int QualifiedClusterCount { get; set; }

    public string? ErrorSummary { get; set; }

    public ICollection<CandidateCluster> CandidateClusters { get; set; } = new List<CandidateCluster>();
}
