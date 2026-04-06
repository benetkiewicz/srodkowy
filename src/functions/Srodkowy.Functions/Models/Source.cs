using System.Text.Json;

namespace Srodkowy.Functions.Models;

public sealed class Source
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    public Guid Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string BaseUrl { get; set; } = string.Empty;

    public string DiscoveryUrl { get; set; } = string.Empty;

    public string? DiscoveryIncludeTags { get; set; }

    public string? DiscoveryExcludeTags { get; set; }

    public string Camp { get; set; } = string.Empty;

    public bool Active { get; set; }

    public ICollection<Article> Articles { get; set; } = new List<Article>();

    public IReadOnlyList<string> GetDiscoveryIncludeTags() => DeserializeTags(DiscoveryIncludeTags);

    public IReadOnlyList<string> GetDiscoveryExcludeTags() => DeserializeTags(DiscoveryExcludeTags);

    private static IReadOnlyList<string> DeserializeTags(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return [];
        }

        return JsonSerializer.Deserialize<string[]>(json, SerializerOptions)
            ?.Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value.Trim())
            .Distinct(StringComparer.Ordinal)
            .ToArray()
            ?? [];
    }
}
