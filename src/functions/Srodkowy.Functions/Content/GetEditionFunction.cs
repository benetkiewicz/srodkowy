using System.Net;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Srodkowy.Functions.Services;

namespace Srodkowy.Functions.Content;

public sealed class GetEditionFunction(ContentReadService contentReadService)
{
    [Function("GetEdition")]
    public async Task<HttpResponseData> RunAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "editions/{editionId:guid}")]
        HttpRequestData request,
        string editionId,
        CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(editionId, out var parsedEditionId))
        {
            return await WriteJsonAsync(request, HttpStatusCode.BadRequest, new { error = $"Invalid edition id '{editionId}'." }, cancellationToken);
        }

        var edition = await contentReadService.GetEditionAsync(parsedEditionId, cancellationToken);

        if (edition is null)
        {
            return await WriteJsonAsync(request, HttpStatusCode.NotFound, new { error = $"Edition '{parsedEditionId}' was not found." }, cancellationToken);
        }

        return await WriteJsonAsync(request, HttpStatusCode.OK, edition, cancellationToken);
    }

    private static async Task<HttpResponseData> WriteJsonAsync(HttpRequestData request, HttpStatusCode statusCode, object payload, CancellationToken cancellationToken)
    {
        var response = request.CreateResponse(statusCode);
        await response.WriteAsJsonAsync(payload, cancellationToken);
        return response;
    }
}
