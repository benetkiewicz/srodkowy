namespace Srodkowy.Functions.Models;

public sealed class Story
{
    public Guid Id { get; set; }

    public Guid EditionId { get; set; }

    public Edition Edition { get; set; } = null!;

    public Guid CandidateClusterId { get; set; }

    public CandidateCluster CandidateCluster { get; set; } = null!;

    public int Rank { get; set; }

    public string Headline { get; set; } = string.Empty;

    public string Synthesis { get; set; } = string.Empty;

    public string MarkersJson { get; set; } = "[]";

    public ICollection<StorySide> Sides { get; set; } = new List<StorySide>();

    public ICollection<StoryArticle> StoryArticles { get; set; } = new List<StoryArticle>();
}
