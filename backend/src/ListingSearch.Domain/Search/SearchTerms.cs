using ListingSearch.Domain.Common;

namespace ListingSearch.Domain.Search;

/// <summary>A free-text keyword reduced to its distinct lower-cased words. A listing must match every word.</summary>
public sealed record SearchTerms
{
    public const int MaxLength = 100;

    private SearchTerms(string text, IReadOnlyList<string> words)
    {
        Text = text;
        Words = words;
    }

    public string Text { get; }

    public IReadOnlyList<string> Words { get; }

    /// <summary>Blank input means "no keyword filter" (success with null); input with no searchable word is an error.</summary>
    public static Result<SearchTerms?> FromUserInput(string? raw, string field = "keyword")
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return Result<SearchTerms?>.Success(null);
        }

        var text = raw.Trim();
        if (text.Length > MaxLength)
        {
            return Result<SearchTerms?>.Failure(field, $"{field} must be at most {MaxLength} characters.");
        }

        var words = TextTokenizer.Tokenize(text).Distinct().ToList();
        return words.Count == 0
            ? Result<SearchTerms?>.Failure(field, $"{field} must contain at least one letter or digit.")
            : Result<SearchTerms?>.Success(new SearchTerms(text, words));
    }
}
