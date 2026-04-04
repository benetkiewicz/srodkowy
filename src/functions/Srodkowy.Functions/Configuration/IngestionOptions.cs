namespace Srodkowy.Functions.Configuration;

public sealed class IngestionOptions
{
    public int MaxCandidateLinksPerSource { get; set; } = 25;

    public int MaxArticlesPerSource { get; set; } = 10;
}
