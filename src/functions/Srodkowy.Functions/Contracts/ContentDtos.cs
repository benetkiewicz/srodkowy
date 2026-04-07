namespace Srodkowy.Functions.Contracts;

public sealed record EditionSummaryDto(
    Guid Id,
    string Status,
    string Cycle,
    DateTimeOffset CreatedAt,
    DateTimeOffset? PublishedAt,
    IReadOnlyList<StoryCardDto> Stories);

public sealed record StoryCardDto(
    Guid StoryId,
    int Rank,
    string Headline,
    string Synthesis,
    int MarkerCount,
    int LeftSourceCount,
    int RightSourceCount);

public sealed record StoryDetailDto(
    Guid Id,
    Guid EditionId,
    int Rank,
    string Headline,
    string Synthesis,
    IReadOnlyList<StoryMarkerDto> Markers,
    StorySideDto Left,
    StorySideDto Right);

public sealed record StoryMarkerDto
{
    public string Phrase { get; init; } = string.Empty;

    public int StartOffset { get; init; }

    public int Length { get; init; }

    public string Kind { get; init; } = string.Empty;

    public string Explanation { get; init; } = string.Empty;

    public IReadOnlyList<StoryExcerptDto> LeftExcerpts { get; init; } = [];

    public IReadOnlyList<StoryExcerptDto> RightExcerpts { get; init; } = [];
}

public sealed record StorySideDto(
    string Camp,
    string Summary,
    IReadOnlyList<StoryExcerptDto> Excerpts);

public sealed record StoryExcerptDto(
    Guid ArticleId,
    string Text,
    string SourceName,
    string SourceUrl);

public sealed record SourceDto(
    Guid Id,
    string Name,
    string BaseUrl,
    string Camp,
    bool Active);
