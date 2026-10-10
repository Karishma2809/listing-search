using ListingSearch.Domain.Listings;

namespace ListingSearch.Tests.Support;

internal static class TestData
{
    /// <summary>The "today" every scoring test is pinned to, so results never depend on the real clock.</summary>
    public static readonly DateOnly Today = new(2026, 10, 9);

    public static Listing Listing(
        string id = "1",
        string source = "MLS_A",
        string street = "1 Main St",
        string city = "Springfield",
        decimal price = 400_000m,
        int bedrooms = 3,
        string listedOn = "2026-10-09",
        string description = "A nice home.",
        ListingStatus status = ListingStatus.Active) => new(
            new ListingKey(source, id),
            new Address(street, new CityName(city), "VA", "22150"),
            new Price(price),
            bedrooms,
            2m,
            1_500,
            new GeoPoint(38.8, -77.2),
            DateOnly.Parse(listedOn),
            status,
            new ListingDescription(description));

    public static IReadOnlyList<Listing> Many(int count) =>
        Enumerable.Range(1, count).Select(n => Listing(id: n.ToString())).ToList();
}

internal sealed class FixedClock(DateOnly today) : TimeProvider
{
    public override DateTimeOffset GetUtcNow() =>
        new(today.ToDateTime(new TimeOnly(12, 0)), TimeSpan.Zero);
}
