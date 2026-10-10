using ListingSearch.Domain.Common;

namespace ListingSearch.Application.Searching;

/// <summary>The request was malformed or contradictory. Carries every problem found, not just the first.</summary>
public sealed class InvalidSearchRequestException(IReadOnlyList<ValidationError> errors)
    : Exception($"The search request is invalid: {string.Join(" ", errors.Select(e => e.Message))}")
{
    public IReadOnlyList<ValidationError> Errors { get; } = errors;
}

/// <summary>
/// The requested city does not appear in any feed. Reported as a failure (with near-miss suggestions)
/// rather than as an empty page, because "no such city" and "no listings match" need different reactions.
/// </summary>
public sealed class UnknownCityException(string city, IReadOnlyList<string> suggestions)
    : Exception($"No listings exist for city '{city}'.")
{
    public string City { get; } = city;

    public IReadOnlyList<string> Suggestions { get; } = suggestions;
}
