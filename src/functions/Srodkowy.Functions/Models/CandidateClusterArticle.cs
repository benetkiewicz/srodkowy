namespace Srodkowy.Functions.Models;

public sealed class CandidateClusterArticle
{
    public Guid CandidateClusterId { get; set; }

    public CandidateCluster CandidateCluster { get; set; } = null!;

    public Guid ArticleId { get; set; }

    public Article Article { get; set; } = null!;

    public Guid SourceId { get; set; }

    public string Camp { get; set; } = string.Empty;

    public double SimilarityToRepresentative { get; set; }

    public bool IsRepresentative { get; set; }
}
