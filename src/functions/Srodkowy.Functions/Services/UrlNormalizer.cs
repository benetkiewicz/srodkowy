namespace Srodkowy.Functions.Services;

public static class UrlNormalizer
{
    private static readonly string[] BlockedExtensions = [".jpg", ".jpeg", ".png", ".gif", ".webp", ".svg", ".pdf", ".mp4", ".mp3", ".zip"];

    private static readonly string[] BlockedPathTokens =
    [
        "tag",
        "tags",
        "szukaj",
        "search",
        "kontakt",
        "contact",
        "autor",
        "author",
        "autorzy",
        "authors",
        "konto",
        "login",
        "logowanie",
        "newsletter",
        "program",
        "ramowka",
        "wideo",
        "video",
        "galeria",
        "gallery"
    ];

    public static string Normalize(string url)
    {
        var uri = new Uri(url, UriKind.Absolute);
        var builder = new UriBuilder(uri)
        {
            Scheme = uri.Scheme.ToLowerInvariant(),
            Host = uri.Host.ToLowerInvariant(),
            Fragment = string.Empty,
            Query = string.Empty
        };

        var normalizedPath = builder.Path.TrimEnd('/');
        builder.Path = string.IsNullOrEmpty(normalizedPath) ? "/" : normalizedPath;

        return builder.Uri.ToString();
    }

    public static bool IsCandidateArticleUrl(string sourceBaseUrl, string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            return false;
        }

        if (uri.Scheme is not ("http" or "https"))
        {
            return false;
        }

        var sourceHost = TrimWww(new Uri(sourceBaseUrl, UriKind.Absolute).Host);
        var candidateHost = TrimWww(uri.Host);

        if (!candidateHost.Equals(sourceHost, StringComparison.OrdinalIgnoreCase)
            && !candidateHost.EndsWith($".{sourceHost}", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var path = uri.AbsolutePath.ToLowerInvariant();

        if (string.IsNullOrWhiteSpace(path) || path == "/")
        {
            return false;
        }

        if (BlockedExtensions.Any(path.EndsWith))
        {
            return false;
        }

        if (ContainsBlockedToken(path))
        {
            return false;
        }

        var pathSegments = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
        return pathSegments.Length >= 2 || path.Length >= 25;
    }

    private static bool ContainsBlockedToken(string path) =>
        BlockedPathTokens.Any(token =>
            path.Equals($"/{token}", StringComparison.OrdinalIgnoreCase)
            || path.Contains($"/{token}/", StringComparison.OrdinalIgnoreCase)
            || path.EndsWith($"/{token}", StringComparison.OrdinalIgnoreCase));

    private static string TrimWww(string host) =>
        host.StartsWith("www.", StringComparison.OrdinalIgnoreCase) ? host[4..] : host;
}
