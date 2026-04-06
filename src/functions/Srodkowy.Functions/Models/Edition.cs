namespace Srodkowy.Functions.Models;

public sealed class Edition
{
    public Guid Id { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset? PublishedAt { get; set; }

    public EditionStatus Status { get; set; } = EditionStatus.Building;

    public EditionCycle Cycle { get; set; }

    public Guid ClusterRunId { get; set; }

    public ClusterRun ClusterRun { get; set; } = null!;

    public ICollection<Story> Stories { get; set; } = new List<Story>();
}
