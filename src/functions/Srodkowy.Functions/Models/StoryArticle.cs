namespace Srodkowy.Functions.Models;

public sealed class StoryArticle
{
    public Guid StoryId { get; set; }

    public Story Story { get; set; } = null!;

    public Guid ArticleId { get; set; }

    public Article Article { get; set; } = null!;
}
