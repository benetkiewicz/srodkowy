using System.Security.Cryptography;
using System.Text;

namespace Srodkowy.Functions.Services;

public static class ArticlePreparationText
{
    public static string BuildCleanupInput(string title, string markdown)
    {
        return string.Join(
            "\n\n",
            new[] { title.Trim(), markdown.Trim() }.Where(value => !string.IsNullOrWhiteSpace(value)));
    }

    public static string BuildEmbeddingInput(string title, string cleanedText, int maxInputCharacters)
    {
        var combined = string.Join(
            "\n\n",
            new[] { title.Trim(), cleanedText.Trim() }.Where(value => !string.IsNullOrWhiteSpace(value)));

        if (combined.Length <= maxInputCharacters)
        {
            return combined;
        }

        return combined[..maxInputCharacters].TrimEnd();
    }

    public static string ComputeHash(string content)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(content));
        return Convert.ToHexString(bytes);
    }
}
