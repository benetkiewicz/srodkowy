namespace Srodkowy.Functions.Configuration;

public sealed class OpenAiOptions
{
    public const string DefaultChatModel = "gpt-4o";

    public const string DefaultEmbeddingModel = "text-embedding-3-small";

    public string ApiKey { get; set; } = string.Empty;

    public string ChatModel { get; set; } = DefaultChatModel;

    public string EmbeddingModel { get; set; } = DefaultEmbeddingModel;
}
