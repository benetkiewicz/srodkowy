using System.Net;
using System.Text.RegularExpressions;

namespace Srodkowy.Functions.Services;

public static partial class ArticleContentConverter
{
    public static string ToPlainText(string markdown)
    {
        if (string.IsNullOrWhiteSpace(markdown))
        {
            return string.Empty;
        }

        var text = CodeFenceRegex().Replace(markdown, " ");
        text = ImageRegex().Replace(text, " ");
        text = LinkRegex().Replace(text, "${text}");
        text = FormattingRegex().Replace(text, string.Empty);
        text = LeadingMarkdownRegex().Replace(text, string.Empty);
        text = WhitespaceRegex().Replace(text, " ");

        return WebUtility.HtmlDecode(text).Trim();
    }

    public static string ExtractTitle(string markdown)
    {
        if (string.IsNullOrWhiteSpace(markdown))
        {
            return string.Empty;
        }

        foreach (var line in markdown.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var cleaned = LeadingMarkdownRegex().Replace(line, string.Empty).Trim();

            if (!string.IsNullOrWhiteSpace(cleaned))
            {
                return cleaned;
            }
        }

        return string.Empty;
    }

    [GeneratedRegex("```[\\s\\S]*?```")]
    private static partial Regex CodeFenceRegex();

    [GeneratedRegex("!\\[[^\\]]*\\]\\([^\\)]*\\)")]
    private static partial Regex ImageRegex();

    [GeneratedRegex("\\[(?<text>[^\\]]+)\\]\\([^\\)]*\\)")]
    private static partial Regex LinkRegex();

    [GeneratedRegex("[`*_~]")]
    private static partial Regex FormattingRegex();

    [GeneratedRegex("(?m)^\\s{0,3}(#{1,6}|>|[-+*])\\s*")]
    private static partial Regex LeadingMarkdownRegex();

    [GeneratedRegex("\\s+")]
    private static partial Regex WhitespaceRegex();
}
