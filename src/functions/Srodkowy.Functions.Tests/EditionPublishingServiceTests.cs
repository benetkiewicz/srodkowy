using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Srodkowy.Functions.Models;
using Srodkowy.Functions.Services;

namespace Srodkowy.Functions.Tests;

public sealed class EditionPublishingServiceTests
{
    [Fact]
    public async Task PublishAsync_marks_target_live_and_archives_previous_live()
    {
        var dbContextOptions = StoryPublishingTestSupport.CreateDbContextOptions();
        var previousLiveId = Guid.NewGuid();
        var targetId = Guid.NewGuid();
        var clusterRunId = Guid.NewGuid();

        await using (var dbContext = StoryPublishingTestSupport.CreateDbContext(dbContextOptions))
        {
            dbContext.Database.EnsureCreated();
            dbContext.ClusterRuns.Add(StoryPublishingTestSupport.CreateClusterRun(clusterRunId));
            dbContext.Editions.AddRange(
                new Edition
                {
                    Id = previousLiveId,
                    CreatedAt = DateTimeOffset.UtcNow.AddDays(-1),
                    PublishedAt = DateTimeOffset.UtcNow.AddDays(-1),
                    Status = EditionStatus.Live,
                    Cycle = EditionCycle.Evening,
                    ClusterRunId = clusterRunId
                },
                new Edition
                {
                    Id = targetId,
                    CreatedAt = DateTimeOffset.UtcNow,
                    Status = EditionStatus.Building,
                    Cycle = EditionCycle.Morning,
                    ClusterRunId = clusterRunId
                });
            await dbContext.SaveChangesAsync();
        }

        var service = new EditionPublishingService(
            new StoryPublishingTestSupport.TestDbContextFactory(dbContextOptions),
            NullLogger<EditionPublishingService>.Instance);

        var result = await service.PublishAsync(targetId, CancellationToken.None);

        result.EditionId.Should().Be(targetId);
        result.Status.Should().Be("live");
        result.PublishedAt.Should().NotBeNull();

        await using var verificationContext = StoryPublishingTestSupport.CreateDbContext(dbContextOptions);
        var editions = await verificationContext.Editions.OrderBy(edition => edition.CreatedAt).ToListAsync();
        editions.Single(edition => edition.Id == previousLiveId).Status.Should().Be(EditionStatus.Archived);
        editions.Single(edition => edition.Id == targetId).Status.Should().Be(EditionStatus.Live);
    }

    [Fact]
    public async Task PublishAsync_rejects_non_building_edition()
    {
        var dbContextOptions = StoryPublishingTestSupport.CreateDbContextOptions();
        var clusterRunId = Guid.NewGuid();
        var failedEditionId = Guid.NewGuid();

        await using (var dbContext = StoryPublishingTestSupport.CreateDbContext(dbContextOptions))
        {
            dbContext.Database.EnsureCreated();
            dbContext.ClusterRuns.Add(StoryPublishingTestSupport.CreateClusterRun(clusterRunId));
            dbContext.Editions.Add(new Edition
            {
                Id = failedEditionId,
                CreatedAt = DateTimeOffset.UtcNow,
                Status = EditionStatus.Failed,
                Cycle = EditionCycle.Morning,
                ClusterRunId = clusterRunId
            });
            await dbContext.SaveChangesAsync();
        }

        var service = new EditionPublishingService(
            new StoryPublishingTestSupport.TestDbContextFactory(dbContextOptions),
            NullLogger<EditionPublishingService>.Instance);

        var action = () => service.PublishAsync(failedEditionId, CancellationToken.None);

        await action.Should().ThrowAsync<InvalidOperationException>();
    }
}
