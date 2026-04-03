using System.Net;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Srodkowy.Functions.Services;

namespace Srodkowy.Functions.Ingestion;

public sealed class RunIngestionFunction(IngestionService ingestionService)
{
    [Function("RunIngestion")]
    public async Task<HttpResponseData> RunAllAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "ingestion/run")]
        HttpRequestData request,
        CancellationToken cancellationToken)
    {
        var result = await ingestionService.RunAsync(null, "http-all", cancellationToken);
        return await WriteJsonAsync(request, HttpStatusCode.OK, result, cancellationToken);
    }

    [Function("RunIngestionForSource")]
    public async Task<HttpResponseData> RunForSourceAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "ingestion/run/{sourceId:guid}")]
        HttpRequestData request,
        string sourceId,
        CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(sourceId, out var parsedSourceId))
        {
            return await WriteJsonAsync(
                request,
                HttpStatusCode.BadRequest,
                new { error = $"Invalid source id '{sourceId}'." },
                cancellationToken);
        }

        try
        {
            var result = await ingestionService.RunAsync(parsedSourceId, $"http-source:{parsedSourceId}", cancellationToken);
            return await WriteJsonAsync(request, HttpStatusCode.OK, result, cancellationToken);
        }
        catch (KeyNotFoundException exception)
        {
            return await WriteJsonAsync(request, HttpStatusCode.NotFound, new { error = exception.Message }, cancellationToken);
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
