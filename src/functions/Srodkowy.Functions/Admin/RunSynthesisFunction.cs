using System.Net;
using System.Text.Json;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Srodkowy.Functions.Services;

namespace Srodkowy.Functions.Admin;

public sealed class RunSynthesisFunction(StorySynthesisService synthesisService)
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    [Function("RunSynthesis")]
    public async Task<HttpResponseData> RunAsync(
        [HttpTrigger(AuthorizationLevel.Function, "post", Route = "ops/synthesis/run")]
        HttpRequestData request,
        CancellationToken cancellationToken)
    {
        var payload = await ReadRequestAsync(request, cancellationToken);

        if (payload is null)
        {
            return await WriteJsonAsync(request, HttpStatusCode.BadRequest, new { error = "Request body is required." }, cancellationToken);
        }

        try
        {
            var result = await synthesisService.RunAsync("http-ops-synthesis", payload, cancellationToken);
            return await WriteJsonAsync(request, HttpStatusCode.OK, result, cancellationToken);
        }
        catch (ArgumentException exception)
        {
            return await WriteJsonAsync(request, HttpStatusCode.BadRequest, new { error = exception.Message }, cancellationToken);
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

    private static async Task<StorySynthesisService.SynthesisRunRequest?> ReadRequestAsync(HttpRequestData request, CancellationToken cancellationToken)
    {
        using var reader = new StreamReader(request.Body, leaveOpen: true);
        var body = await reader.ReadToEndAsync(cancellationToken);

        if (string.IsNullOrWhiteSpace(body))
        {
            return null;
        }

        return JsonSerializer.Deserialize<StorySynthesisService.SynthesisRunRequest>(body, SerializerOptions);
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
