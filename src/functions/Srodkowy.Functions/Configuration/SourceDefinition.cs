namespace Srodkowy.Functions.Configuration;

public sealed record SourceDefinition(
    Guid Id,
    string Name,
    string BaseUrl,
    string DiscoveryUrl,
    string Camp,
    string[]? DiscoveryIncludeTags = null,
    string[]? DiscoveryExcludeTags = null,
    bool Active = true);
