namespace Srodkowy.Functions.Configuration;

public sealed class CleanupOptions
{
    public int BatchSize { get; set; } = 25;

    public int LookbackHours { get; set; } = 96;

    public int MaxInputCharacters { get; set; } = 24000;

    public int MinCleanedLength { get; set; } = 320;
}
