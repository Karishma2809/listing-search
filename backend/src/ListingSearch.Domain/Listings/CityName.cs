using System.Text.RegularExpressions;
using ListingSearch.Domain.Common;

namespace ListingSearch.Domain.Listings;

/// <summary>
/// A city, compared the way a human would: case-insensitively and ignoring stray whitespace.
/// Feeds disagree on this ("springfield", "Springfield ") so equality is on the normalised form
/// while <see cref="Display"/> keeps a readable spelling.
/// </summary>
public sealed partial class CityName : IEquatable<CityName>
{
    public const int MaxLength = 100;

    private readonly string _key;

    public CityName(string value)
    {
        Display = Whitespace().Replace(Guard.NotBlank(value, nameof(value)), " ");
        _key = Display.ToUpperInvariant();
    }

    public string Display { get; }

    /// <summary>Builds a filter value from user input; blank input means "no city filter".</summary>
    public static Result<CityName?> FromUserInput(string? raw, string field = "city")
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return Result<CityName?>.Success(null);
        }

        return raw.Trim().Length > MaxLength
            ? Result<CityName?>.Failure(field, $"{field} must be at most {MaxLength} characters.")
            : Result<CityName?>.Success(new CityName(raw));
    }

    public bool Equals(CityName? other) => other is not null && _key == other._key;

    public override bool Equals(object? obj) => Equals(obj as CityName);

    public override int GetHashCode() => _key.GetHashCode();

    public override string ToString() => Display;

    [GeneratedRegex(@"\s+")]
    private static partial Regex Whitespace();
}
