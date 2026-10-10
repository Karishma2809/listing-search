namespace ListingSearch.Domain.Common;

/// <summary>
/// Splits free text into lower-cased alphanumeric words. Used for both listing descriptions and
/// search keywords so the two sides are always normalised identically.
/// </summary>
public static class TextTokenizer
{
    public static IReadOnlyList<string> Tokenize(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return [];
        }

        var tokens = new List<string>();
        var start = -1;
        for (var i = 0; i <= text.Length; i++)
        {
            var isWordChar = i < text.Length && char.IsLetterOrDigit(text[i]);
            if (isWordChar && start < 0)
            {
                start = i;
            }
            else if (!isWordChar && start >= 0)
            {
                tokens.Add(text[start..i].ToLowerInvariant());
                start = -1;
            }
        }

        return tokens;
    }
}
