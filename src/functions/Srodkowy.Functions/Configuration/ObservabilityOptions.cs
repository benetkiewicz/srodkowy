namespace Srodkowy.Functions.Configuration;

public sealed class ObservabilityOptions
{
    public const string ChatSourceName = "Srodkowy.Functions.AI.Chat";

    public const string EmbeddingSourceName = "Srodkowy.Functions.AI.Embedding";

    public bool EnableSensitiveData { get; set; }
}
