namespace Srodkowy.Functions.Configuration;

public sealed class ObservabilityOptions
{
    public const string ArticlePreparationSourceName = "Srodkowy.Functions.ArticlePreparation";

    public const string ClusteringSourceName = "Srodkowy.Functions.Clustering";

    public const string CleanupChatSourceName = "Srodkowy.Functions.AI.Cleanup";

    public const string ChatSourceName = "Srodkowy.Functions.AI.Chat";

    public const string EmbeddingSourceName = "Srodkowy.Functions.AI.Embedding";

    public bool EnableSensitiveData { get; set; }
}
