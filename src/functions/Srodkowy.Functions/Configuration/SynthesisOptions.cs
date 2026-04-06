namespace Srodkowy.Functions.Configuration;

public sealed class SynthesisOptions
{
    public int MaxClustersPerRun { get; set; } = 12;

    public int MaxArticlesPerCamp { get; set; } = 3;

    public int MaxInputCharactersPerArticle { get; set; } = 4000;

    public int MaxMarkers { get; set; } = 4;

    public bool RequireVerbatimExcerpts { get; set; } = true;
}
