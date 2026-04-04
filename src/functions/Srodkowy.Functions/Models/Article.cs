using Microsoft.Data.SqlTypes;

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

    public string? CleanedContentText { get; set; }

    public ArticleCleanupStatus CleanupStatus { get; set; } = ArticleCleanupStatus.Pending;

    public DateTimeOffset? CleanedAt { get; set; }

    public DateTimeOffset? CleanupStartedAt { get; set; }

    public Guid? CleanupRunId { get; set; }

    public string? CleanupProcessor { get; set; }

    public string? CleanupError { get; set; }

    public string? CleanupInputHash { get; set; }

    public string CleanupFlagsJson { get; set; } = "[]";

    public int QualityScore { get; set; }

    public bool NeedsReview { get; set; }

    public bool IsProbablyNonArticle { get; set; }

    public SqlVector<float>? Embedding { get; set; }

    public string? EmbeddingModel { get; set; }

    public ArticleEmbeddingStatus EmbeddingStatus { get; set; } = ArticleEmbeddingStatus.Pending;

    public DateTimeOffset? EmbeddedAt { get; set; }

    public DateTimeOffset? EmbeddingStartedAt { get; set; }

    public Guid? EmbeddingRunId { get; set; }

    public string? EmbeddingError { get; set; }

    public string? EmbeddingTextHash { get; set; }

    public DateTimeOffset? PublishedAt { get; set; }

    public DateTimeOffset ScrapedAt { get; set; }

    public string MetadataJson { get; set; } = "{}";
}
