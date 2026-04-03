namespace Srodkowy.Functions.Configuration;

public sealed record SourceDefinition(
    Guid Id,
    string Name,
    string BaseUrl,
    string DiscoveryUrl,
    string Camp,
    bool Active = true);
