using System.Text.Json;
using ListingSearch.Domain.Common;
using ListingSearch.Domain.Listings;

namespace ListingSearch.Infrastructure.Feeds;

public sealed record FeedLoadResult(IReadOnlyList<Listing> Listings, IReadOnlyList<string> Rejections);

/// <summary>
/// Anti-corruption layer between an MLS JSON feed and the domain. Feed rows are untrusted: each one is
/// translated into a <see cref="Listing"/> on its own, and a row that cannot be (missing field, bad
/// number, unknown status, duplicate key) is rejected with a reason instead of taking the whole feed down.
/// A feed that is not a JSON array at all is a different matter and fails loudly.
/// </summary>
public static class JsonListingFeed
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);

    public static FeedLoadResult LoadFile(string path)
    {
        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"Listing feed not found at '{path}'. Set Listings:FeedPath to a JSON file.", path);
        }

        return Parse(File.ReadAllText(path));
    }

    public static FeedLoadResult Parse(string json)
    {
        using var document = ParseDocument(json);
        if (document.RootElement.ValueKind != JsonValueKind.Array)
        {
            throw new InvalidDataException("Listing feed must be a JSON array of listings.");
        }

        var listings = new List<Listing>();
        var seen = new HashSet<ListingKey>();
        var rejections = new List<string>();
        var index = 0;

        foreach (var element in document.RootElement.EnumerateArray())
        {
            var row = $"row {index++}";
            try
            {
                var listing = ToListing(element.Deserialize<FeedRow>(Options) ?? throw new DomainException("row is null."));
                if (seen.Add(listing.Key))
                {
                    listings.Add(listing);
                }
                else
                {
                    rejections.Add($"{row} ({listing.Key}): duplicate of an earlier listing with the same source and id.");
                }
            }
            catch (Exception ex) when (ex is DomainException or JsonException)
            {
                rejections.Add($"{row}: {ex.Message}");
            }
        }

        return new FeedLoadResult(listings, rejections);
    }

    private static JsonDocument ParseDocument(string json)
    {
        try
        {
            return JsonDocument.Parse(json);
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException($"Listing feed is not valid JSON: {ex.Message}", ex);
        }
    }

    private static Listing ToListing(FeedRow row) => new(
        new ListingKey(row.Source ?? "", row.Id ?? ""),
        new Address(row.Address ?? "", new CityName(row.City ?? ""), row.State ?? "", row.Zip ?? ""),
        new Price(Required(row.Price, "price")),
        Required(row.Bedrooms, "bedrooms"),
        Required(row.Bathrooms, "bathrooms"),
        Required(row.Sqft, "sqft"),
        new GeoPoint(Required(row.Latitude, "latitude"), Required(row.Longitude, "longitude")),
        Required(row.ListedDate, "listedDate"),
        ParseStatus(row.Status),
        new ListingDescription(row.Description));

    private static T Required<T>(T? value, string field) where T : struct =>
        value ?? throw new DomainException($"{field} is missing.");

    private static ListingStatus ParseStatus(string? status) => status?.Trim().ToLowerInvariant() switch
    {
        "active" => ListingStatus.Active,
        "pending" => ListingStatus.Pending,
        "sold" => ListingStatus.Sold,
        _ => throw new DomainException($"status '{status}' is not one of active, pending, sold."),
    };

    /// <summary>The feed's wire format. Everything is nullable so a missing field is reported, not defaulted.</summary>
    private sealed record FeedRow(
        string? Id,
        string? Source,
        string? Address,
        string? City,
        string? State,
        string? Zip,
        decimal? Price,
        int? Bedrooms,
        decimal? Bathrooms,
        int? Sqft,
        double? Latitude,
        double? Longitude,
        DateOnly? ListedDate,
        string? Status,
        string? Description);
}
