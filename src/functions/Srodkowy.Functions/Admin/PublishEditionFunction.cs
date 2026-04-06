using System.Net;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Srodkowy.Functions.Services;

namespace Srodkowy.Functions.Admin;

public sealed class PublishEditionFunction(EditionPublishingService publishingService)
{
    [Function("PublishEdition")]
    public async Task<HttpResponseData> RunAsync(
        [HttpTrigger(AuthorizationLevel.Function, "post", Route = "ops/editions/{editionId:guid}/publish")]
        HttpRequestData request,
        string editionId,
        CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(editionId, out var parsedEditionId))
        {
            return await WriteJsonAsync(request, HttpStatusCode.BadRequest, new { error = $"Invalid edition id '{editionId}'." }, cancellationToken);
        }

        try
        {
            var result = await publishingService.PublishAsync(parsedEditionId, cancellationToken);
            return await WriteJsonAsync(request, HttpStatusCode.OK, result, cancellationToken);
        }
        catch (KeyNotFoundException exception)
        {
            return await WriteJsonAsync(request, HttpStatusCode.NotFound, new { error = exception.Message }, cancellationToken);
        }
        catch (InvalidOperationException exception)
        {
            return await WriteJsonAsync(request, HttpStatusCode.BadRequest, new { error = exception.Message }, cancellationToken);
        }
    }

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
