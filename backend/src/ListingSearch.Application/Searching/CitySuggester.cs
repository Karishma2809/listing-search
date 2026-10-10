using ListingSearch.Domain.Listings;

namespace ListingSearch.Application.Searching;

/// <summary>Finds known cities that look like a mistyped one, so the error can say "did you mean...".</summary>
internal static class CitySuggester
{
    private const int MaxDistance = 3;
    private const int MaxSuggestions = 5;

    public static IReadOnlyList<string> Suggest(string attempted, IEnumerable<CityName> known) =>
        known
            .Select(city => (city.Display, Distance: EditDistance(attempted.ToUpperInvariant(), city.Display.ToUpperInvariant())))
            .Where(candidate => candidate.Distance <= MaxDistance)
            .OrderBy(candidate => candidate.Distance)
            .ThenBy(candidate => candidate.Display, StringComparer.Ordinal)
            .Take(MaxSuggestions)
            .Select(candidate => candidate.Display)
            .ToList();

    /// <summary>Classic Levenshtein distance, two-row version.</summary>
    internal static int EditDistance(string a, string b)
    {
        var previous = Enumerable.Range(0, b.Length + 1).ToArray();
        var current = new int[b.Length + 1];

        for (var i = 1; i <= a.Length; i++)
        {
            current[0] = i;
            for (var j = 1; j <= b.Length; j++)
            {
                var substitution = previous[j - 1] + (a[i - 1] == b[j - 1] ? 0 : 1);
                current[j] = Math.Min(Math.Min(previous[j] + 1, current[j - 1] + 1), substitution);
            }

            (previous, current) = (current, previous);
        }

        return previous[b.Length];
    }
}
