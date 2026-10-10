using ListingSearch.Domain.Common;

namespace ListingSearch.Domain.Listings;

/// <summary>
/// Free-text description. It is tokenised once at construction so keyword matching, which runs
/// for every listing on every search, never re-parses the text. Equality is on the text alone.
/// </summary>
public sealed class ListingDescription : IEquatable<ListingDescription>
{
    public ListingDescription(string? text)
    {
        Text = (text ?? string.Empty).Trim();
        Words = TextTokenizer.Tokenize(Text);
    }

    public string Text { get; }

    public IReadOnlyList<string> Words { get; }

    /// <summary>
    /// True when every term is the start of some word in the description (so "pet" finds "pets" and
    /// "walk" finds "walkable", but "pet" does not find "carpet"). Terms are expected lower-cased.
    /// </summary>
    public bool ContainsAllTerms(IEnumerable<string> terms) =>
        terms.All(term => Words.Any(word => word.StartsWith(term, StringComparison.Ordinal)));

    public bool Equals(ListingDescription? other) => other is not null && Text == other.Text;

    public override bool Equals(object? obj) => Equals(obj as ListingDescription);

    public override int GetHashCode() => Text.GetHashCode();

    public override string ToString() => Text;
}
