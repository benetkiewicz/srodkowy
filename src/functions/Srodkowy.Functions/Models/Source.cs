namespace Srodkowy.Functions.Models;

public sealed class Source
{
    public Guid Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string BaseUrl { get; set; } = string.Empty;

    public string DiscoveryUrl { get; set; } = string.Empty;

    public string Camp { get; set; } = string.Empty;

    public bool Active { get; set; }

    public ICollection<Article> Articles { get; set; } = new List<Article>();
}
