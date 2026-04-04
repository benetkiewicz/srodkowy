namespace Srodkowy.Functions.Configuration;

public sealed class EmbeddingOptions
{
    public int BatchSize { get; set; } = 25;

    public int LookbackHours { get; set; } = 96;

    public int MaxInputCharacters { get; set; } = 12000;
}
