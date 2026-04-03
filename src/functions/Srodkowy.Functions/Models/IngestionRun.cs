namespace Srodkowy.Functions.Models;

public sealed class IngestionRun
{
    public Guid Id { get; set; }

    public DateTimeOffset StartedAt { get; set; }

    public DateTimeOffset? CompletedAt { get; set; }

    public string Status { get; set; } = string.Empty;

    public string TriggeredBy { get; set; } = string.Empty;

    public int SourceCount { get; set; }

    public int DiscoveredLinkCount { get; set; }

    public int CandidateLinkCount { get; set; }

    public int ArticleCount { get; set; }

    public string? ErrorSummary { get; set; }
}
