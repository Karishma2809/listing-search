using ListingSearch.Domain.Common;
using ListingSearch.Domain.Listings;
using ListingSearch.Tests.Support;

namespace ListingSearch.Tests.Domain;

public class ListingTests
{
    [Fact]
    public void Identity_is_source_plus_id_so_the_same_id_from_two_feeds_is_two_listings()
    {
        var fromA = TestData.Listing(id: "7", source: "MLS_A");
        var fromB = TestData.Listing(id: "7", source: "MLS_B");
        var fromAAgain = TestData.Listing(id: "7", source: "MLS_A", price: 999_999m);

        Assert.NotEqual(fromA, fromB);
        Assert.Equal(fromA, fromAAgain);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Price_must_be_positive(int amount) =>
        Assert.Throws<DomainException>(() => new Price(amount));

    [Fact]
    public void Price_and_listing_can_be_printed_for_logs_and_failure_messages()
    {
        Assert.Equal("$450,000", new Price(450_000m).ToString());
        Assert.Contains("MLS_A/1", TestData.Listing().ToString());
    }

    [Fact]
    public void Negative_bedrooms_are_rejected() =>
        Assert.Throws<DomainException>(() => TestData.Listing(bedrooms: -1));

    [Fact]
    public void Studios_with_zero_bedrooms_are_allowed() =>
        Assert.Equal(0, TestData.Listing(bedrooms: 0).Bedrooms);

    [Theory]
    [InlineData(91d, 0d)]
    [InlineData(-91d, 0d)]
    [InlineData(0d, 181d)]
    [InlineData(0d, -181d)]
    [InlineData(double.NaN, 0d)]
    public void Coordinates_must_be_on_the_globe(double latitude, double longitude) =>
        Assert.Throws<DomainException>(() => new GeoPoint(latitude, longitude));

    [Fact]
    public void Blank_key_parts_are_rejected()
    {
        Assert.Throws<DomainException>(() => new ListingKey(" ", "1"));
        Assert.Throws<DomainException>(() => new ListingKey("MLS_A", ""));
    }

    [Fact]
    public void Address_state_is_normalised_but_the_street_is_kept_verbatim()
    {
        var address = new Address("123 Main Street, Unit 4B", new CityName("Springfield"), " va ", " 22150 ");

        Assert.Equal("123 Main Street, Unit 4B", address.Street);
        Assert.Equal("VA", address.State);
        Assert.Equal("22150", address.Zip);
    }
}
