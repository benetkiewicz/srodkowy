using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Srodkowy.Functions.Configuration;
using Srodkowy.Functions.Contracts;
using Srodkowy.Functions.Models;
using Srodkowy.Functions.Persistence;

namespace Srodkowy.Functions.Services;

public sealed class ContentReadService(IDbContextFactory<SrodkowyDbContext> dbContextFactory)
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    public async Task<EditionSummaryDto?> GetCurrentEditionAsync(CancellationToken cancellationToken)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var edition = await dbContext.Editions
            .AsNoTracking()
            .Include(item => item.Stories)
                .ThenInclude(story => story.StoryArticles)
                    .ThenInclude(storyArticle => storyArticle.Article)
                        .ThenInclude(article => article.Source)
            .Where(edition => edition.Status == EditionStatus.Live)
            .OrderByDescending(edition => edition.PublishedAt)
            .FirstOrDefaultAsync(cancellationToken);

        return edition is null ? null : MapEdition(edition);
    }

    public async Task<EditionSummaryDto?> GetEditionAsync(Guid editionId, CancellationToken cancellationToken)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var edition = await dbContext.Editions
            .AsNoTracking()
            .Include(item => item.Stories)
                .ThenInclude(story => story.StoryArticles)
                    .ThenInclude(storyArticle => storyArticle.Article)
                        .ThenInclude(article => article.Source)
            .SingleOrDefaultAsync(edition => edition.Id == editionId, cancellationToken);

        return edition is null ? null : MapEdition(edition);
    }

    public async Task<StoryDetailDto?> GetStoryAsync(Guid storyId, CancellationToken cancellationToken)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var story = await dbContext.Stories
            .AsNoTracking()
            .Include(item => item.Sides)
            .SingleOrDefaultAsync(story => story.Id == storyId, cancellationToken);

        if (story is null)
        {
            return null;
        }

        var markers = DeserializeMarkers(story.MarkersJson);
        var left = story.Sides.Single(side => string.Equals(side.Camp, SourceCamp.Left, StringComparison.OrdinalIgnoreCase));
        var right = story.Sides.Single(side => string.Equals(side.Camp, SourceCamp.Right, StringComparison.OrdinalIgnoreCase));

        return new StoryDetailDto(
            story.Id,
            story.EditionId,
            story.Rank,
            story.Headline,
            story.Synthesis,
            markers,
            new StorySideDto(left.Camp, left.Summary, DeserializeExcerpts(left.ExcerptsJson)),
            new StorySideDto(right.Camp, right.Summary, DeserializeExcerpts(right.ExcerptsJson)));
    }

    public async Task<IReadOnlyList<SourceDto>> GetSourcesAsync(CancellationToken cancellationToken)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        return await dbContext.Sources
            .AsNoTracking()
            .OrderBy(source => source.Camp)
            .ThenBy(source => source.Name)
            .Select(source => new SourceDto(source.Id, source.Name, source.BaseUrl, source.Camp, source.Active))
            .ToListAsync(cancellationToken);
    }

    private static EditionSummaryDto MapEdition(Edition edition)
    {
        var storyCards = edition.Stories
            .OrderBy(story => story.Rank)
            .Select(story =>
            {
                var markers = DeserializeMarkers(story.MarkersJson);
                var leftSourceCount = story.StoryArticles
                    .Select(storyArticle => storyArticle.Article.Source)
                    .Count(source => string.Equals(source.Camp, SourceCamp.Left, StringComparison.OrdinalIgnoreCase));
                var rightSourceCount = story.StoryArticles
                    .Select(storyArticle => storyArticle.Article.Source)
                    .Count(source => string.Equals(source.Camp, SourceCamp.Right, StringComparison.OrdinalIgnoreCase));

                return new StoryCardDto(story.Id, story.Rank, story.Headline, story.Synthesis, markers.Count, leftSourceCount, rightSourceCount);
            })
            .ToList();

        return new EditionSummaryDto(
            edition.Id,
            edition.Status.ToString().ToLowerInvariant(),
            edition.Cycle.ToString().ToLowerInvariant(),
            edition.CreatedAt,
            edition.PublishedAt,
            storyCards);
    }

    private static List<StoryMarkerDto> DeserializeMarkers(string json)
    {
        return JsonSerializer.Deserialize<List<StoryMarkerDto>>(json, SerializerOptions) ?? [];
    }

    private static List<StoryExcerptDto> DeserializeExcerpts(string json)
    {
        return JsonSerializer.Deserialize<List<StoryExcerptDto>>(json, SerializerOptions) ?? [];
    }
}
