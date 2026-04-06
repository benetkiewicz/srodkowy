using System.Text.Json;
using Microsoft.Extensions.AI;

namespace Srodkowy.Functions.Services;

public interface IStorySynthesisModel
{
    Task<StorySynthesisDraftResponse> SynthesizeDraftAsync(StorySynthesisDraftRequest request, CancellationToken cancellationToken);

    Task<StorySynthesisMarkerSelectionResponse> SelectMarkersAsync(StorySynthesisMarkerSelectionRequest request, CancellationToken cancellationToken);
}

public sealed class OpenAiStorySynthesisModel(IChatClient chatClient) : IStorySynthesisModel
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    public async Task<StorySynthesisDraftResponse> SynthesizeDraftAsync(StorySynthesisDraftRequest request, CancellationToken cancellationToken)
    {
        var prompt = BuildDraftPrompt(request);
        var response = await chatClient.GetResponseAsync<StorySynthesisDraftResponse>(prompt, cancellationToken: cancellationToken);
        return response.Result;
    }

    public async Task<StorySynthesisMarkerSelectionResponse> SelectMarkersAsync(StorySynthesisMarkerSelectionRequest request, CancellationToken cancellationToken)
    {
        var prompt = BuildMarkerPrompt(request);
        var response = await chatClient.GetResponseAsync<StorySynthesisMarkerSelectionResponse>(prompt, cancellationToken: cancellationToken);
        return response.Result;
    }

    private static string BuildDraftPrompt(StorySynthesisDraftRequest request)
    {
        var payload = JsonSerializer.Serialize(request, SerializerOptions);

        return $"""
You generate Polish story packages for a news product that compares how left and right media describe the same event.

Return strict JSON with this shape:
- headline: string
- synthesis: string
- left: object with summary and excerptSnippetIds
- right: object with summary and excerptSnippetIds

Rules:
- write all generated text in Polish
- write a calm, factual, restrained synthesis focused on the shared event core
- do not fact-check, moralize, speculate about motives, or pick a side
- headline should be concise and neutral
- synthesis should be about 150-300 words
- left.excerptSnippetIds and right.excerptSnippetIds must contain only snippet IDs from ExcerptCandidates
- do not generate, rewrite, shorten, or edit excerpt text
- every selected snippet must come from the matching camp
- do not invent articleIds, sources, snippet IDs, quotes, or facts
- before returning, verify that every selected excerptSnippetId exists in ExcerptCandidates; if not, omit it
- left and right summaries should describe the narrative framing of that camp, not the truth of the event

Input JSON:
{payload}
""";
    }

    private static string BuildMarkerPrompt(StorySynthesisMarkerSelectionRequest request)
    {
        var payload = JsonSerializer.Serialize(request, SerializerOptions);

        return $"""
You select exact marker spans from a finished Polish synthesis.

Return strict JSON with this shape:
- markers: array of objects with markerCandidateId, kind, explanation

Rules:
- return between 1 and {request.MaxMarkers} markers
- choose only markerCandidateIds from MarkerCandidates
- do not invent marker text or candidate IDs
- each selected marker should highlight wording that reveals framing, emphasis, contrast, agency, scale, or conflict
- keep explanation short and factual in Polish
- if a candidate does not fit, omit it

Input JSON:
{payload}
""";
    }
}

public sealed record StorySynthesisDraftRequest(
    Guid CandidateClusterId,
    int Rank,
    IReadOnlyList<StorySynthesisArticleInput> Articles,
    IReadOnlyList<StorySynthesisExcerptCandidateInput> ExcerptCandidates);

public sealed record StorySynthesisArticleInput(
    Guid ArticleId,
    string SourceName,
    string Camp,
    string Url,
    DateTimeOffset? PublishedAt,
    string Title,
    string CleanedContentText);

public sealed record StorySynthesisExcerptCandidateInput(
    string SnippetId,
    Guid ArticleId,
    string Camp,
    string SourceName,
    string Text);

public sealed record StorySynthesisDraftResponse(
    string Headline,
    string Synthesis,
    StorySynthesisSideResponse? Left,
    StorySynthesisSideResponse? Right);

public sealed record StorySynthesisMarkerSelectionRequest(
    Guid CandidateClusterId,
    int MaxMarkers,
    string Synthesis,
    string LeftSummary,
    string RightSummary,
    IReadOnlyList<StorySynthesisMarkerCandidateInput> MarkerCandidates);

public sealed record StorySynthesisMarkerCandidateInput(
    string MarkerCandidateId,
    string Text,
    int StartOffset,
    int Length);

public sealed record StorySynthesisMarkerSelectionResponse(
    IReadOnlyList<StorySynthesisMarkerSelectionItem>? Markers);

public sealed record StorySynthesisMarkerSelectionItem(
    string MarkerCandidateId,
    string Kind,
    string Explanation);

public sealed record StorySynthesisSideResponse(
    string Summary,
    IReadOnlyList<string>? ExcerptSnippetIds);
