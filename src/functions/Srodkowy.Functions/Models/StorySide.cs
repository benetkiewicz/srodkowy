namespace Srodkowy.Functions.Models;

public sealed class StorySide
{
    public Guid Id { get; set; }

    public Guid StoryId { get; set; }

    public Story Story { get; set; } = null!;

    public string Camp { get; set; } = string.Empty;

    public string Summary { get; set; } = string.Empty;

    public string ExcerptsJson { get; set; } = "[]";
}
