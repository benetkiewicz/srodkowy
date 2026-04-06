using System.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Srodkowy.Functions.Models;
using Srodkowy.Functions.Persistence;

namespace Srodkowy.Functions.Services;

public sealed class EditionPublishingService(
    IDbContextFactory<SrodkowyDbContext> dbContextFactory,
    ILogger<EditionPublishingService> logger)
{
    public async Task<PublishEditionResult> PublishAsync(Guid editionId, CancellationToken cancellationToken)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var strategy = dbContext.Database.CreateExecutionStrategy();

        return await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = dbContext.Database.IsRelational()
                ? await dbContext.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken)
                : null;

            var targetEdition = await dbContext.Editions
                .SingleOrDefaultAsync(edition => edition.Id == editionId, cancellationToken);

            if (targetEdition is null)
            {
                throw new KeyNotFoundException($"Edition '{editionId}' was not found.");
            }

            if (targetEdition.Status != EditionStatus.Building)
            {
                throw new InvalidOperationException($"Edition '{editionId}' cannot be published from status '{targetEdition.Status}'.");
            }

            var previousLiveEditions = await dbContext.Editions
                .Where(edition => edition.Status == EditionStatus.Live && edition.Id != editionId)
                .ToListAsync(cancellationToken);

            foreach (var edition in previousLiveEditions)
            {
                edition.Status = EditionStatus.Archived;
            }

            targetEdition.Status = EditionStatus.Live;
            targetEdition.PublishedAt = DateTimeOffset.UtcNow;

            await dbContext.SaveChangesAsync(cancellationToken);

            if (transaction is not null)
            {
                await transaction.CommitAsync(cancellationToken);
            }

            logger.LogInformation("Published edition {EditionId}", editionId);

            return new PublishEditionResult(targetEdition.Id, targetEdition.Status.ToString().ToLowerInvariant(), targetEdition.PublishedAt);
        });
    }

    public sealed record PublishEditionResult(Guid EditionId, string Status, DateTimeOffset? PublishedAt);
}
