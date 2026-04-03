namespace Srodkowy.Functions.Models;

public sealed class Article
{
    public Guid Id { get; set; }

    public Guid SourceId { get; set; }

    public Source Source { get; set; } = null!;

    public string Url { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;

    public string ContentMarkdown { get; set; } = string.Empty;

    public string ContentText { get; set; } = string.Empty;

    public DateTimeOffset? PublishedAt { get; set; }

    public DateTimeOffset ScrapedAt { get; set; }

    public string MetadataJson { get; set; } = "{}";
}
