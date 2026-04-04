namespace Srodkowy.Functions.Configuration;

public sealed class ClusteringOptions
{
    public int LookbackHours { get; set; } = 72;

    public int MinQualityScore { get; set; } = 35;

    public double NearDuplicateSimilarity { get; set; } = 0.96;

    public double PairSimilarityThreshold { get; set; } = 0.84;

    public double MergeSimilarityThreshold { get; set; } = 0.81;

    public int MaxPairTimespanHours { get; set; } = 48;

    public int MaxClusterTimespanHours { get; set; } = 72;

    public int MaxClusterSize { get; set; } = 6;

    public int MaxClusters { get; set; } = 12;

    public bool ExcludeNeedsReview { get; set; } = true;
}
