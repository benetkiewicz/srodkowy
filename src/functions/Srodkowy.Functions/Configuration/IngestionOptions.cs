namespace Srodkowy.Functions.Configuration;

public sealed class IngestionOptions
{
    public int MaxCandidateLinksPerSource { get; set; } = 25;

    public int MaxArticlesPerSource { get; set; } = 10;

    public int MinCandidateTitleLength { get; set; } = 40;
}
