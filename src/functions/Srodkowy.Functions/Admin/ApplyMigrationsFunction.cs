using System.Net;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Srodkowy.Functions.Persistence;

namespace Srodkowy.Functions.Admin;

public sealed class ApplyMigrationsFunction(
    IConfiguration configuration,
    IDbContextFactory<SrodkowyDbContext> dbContextFactory)
{
    [Function("ApplyMigrations")]
    public async Task<HttpResponseData> RunAsync(
        [HttpTrigger(AuthorizationLevel.Function, "post", Route = "admin/migrations/apply")]
        HttpRequestData request,
        CancellationToken cancellationToken)
    {
        if (!IsEnabled())
        {
            return await WriteJsonAsync(
                request,
                HttpStatusCode.Forbidden,
                new { error = "Migrations are disabled." },
                cancellationToken);
        }

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        await dbContext.Database.MigrateAsync(cancellationToken);

        var appliedMigrations = await dbContext.Database.GetAppliedMigrationsAsync(cancellationToken);

        return await WriteJsonAsync(
            request,
            HttpStatusCode.OK,
            new
            {
                status = "ok",
                appliedMigrationCount = appliedMigrations.Count(),
                appliedMigrations
            },
            cancellationToken);
    }

    private bool IsEnabled() =>
        bool.TryParse(configuration["Admin:Migrations:Enabled"] ?? configuration["Values:Admin:Migrations:Enabled"], out var enabled)
        && enabled;

    private static async Task<HttpResponseData> WriteJsonAsync(
        HttpRequestData request,
        HttpStatusCode statusCode,
        object payload,
        CancellationToken cancellationToken)
    {
        var response = request.CreateResponse(statusCode);
        await response.WriteAsJsonAsync(payload, cancellationToken);
        return response;
    }
}
