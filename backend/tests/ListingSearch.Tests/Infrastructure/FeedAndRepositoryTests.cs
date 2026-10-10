using ListingSearch.Domain.Search;
using ListingSearch.Infrastructure.Feeds;
using ListingSearch.Infrastructure.Persistence;
using ListingSearch.Tests.Support;

namespace ListingSearch.Tests.Infrastructure;

public class JsonListingFeedTests
{
    private const string GoodRow = """
        { "id": "A1", "source": "MLS_A", "address": "123 Main St", "city": "Springfield", "state": "VA", "zip": "22150",
          "price": 450000, "bedrooms": 2, "bathrooms": 1.5, "sqft": 980, "latitude": 38.78, "longitude": -77.18,
          "listedDate": "2026-08-29", "status": "active", "description": "Bright condo." }
        """;

    private static string Feed(params string[] rows) => $"[{string.Join(",", rows)}]";

    private static string Without(string field) =>
        System.Text.RegularExpressions.Regex.Replace(GoodRow, $"\"{field}\": [^,]+,?", string.Empty);

    [Fact]
    public void The_shipped_sample_feed_loads_completely()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Data", "sample_listings.json");

        var result = JsonListingFeed.LoadFile(path);

        Assert.Equal(12, result.Listings.Count);
        Assert.Empty(result.Rejections);
    }

    [Fact]
    public void A_valid_row_becomes_a_listing()
    {
        var listing = Assert.Single(JsonListingFeed.Parse(Feed(GoodRow)).Listings);

        Assert.Equal("MLS_A/A1", listing.Key.ToString());
        Assert.Equal(450_000m, listing.Price.Amount);
        Assert.Equal(1.5m, listing.Bathrooms);
        Assert.Equal(new DateOnly(2026, 8, 29), listing.ListedOn);
    }

    [Theory]
    [InlineData("price")]
    [InlineData("bedrooms")]
    [InlineData("listedDate")]
    [InlineData("latitude")]
    public void A_row_missing_a_required_field_is_rejected_and_the_rest_survive(string field)
    {
        var result = JsonListingFeed.Parse(Feed(GoodRow, Without(field).Replace("A1", "A2")));

        Assert.Single(result.Listings);
        Assert.Contains(field, Assert.Single(result.Rejections));
    }

    [Fact]
    public void An_unknown_status_is_rejected() =>
        Assert.Single(JsonListingFeed.Parse(Feed(GoodRow.Replace("\"active\"", "\"withdrawn\""))).Rejections);

    [Fact]
    public void A_status_is_case_insensitive() =>
        Assert.Single(JsonListingFeed.Parse(Feed(GoodRow.Replace("\"active\"", "\"Pending\""))).Listings);

    [Fact]
    public void A_value_of_the_wrong_type_rejects_only_that_row()
    {
        var result = JsonListingFeed.Parse(Feed(GoodRow.Replace("\"price\": 450000", "\"price\": \"lots\"").Replace("A1", "A2"), GoodRow));

        Assert.Single(result.Listings);
        Assert.Single(result.Rejections);
    }

    [Fact]
    public void A_domain_rule_violation_rejects_the_row() =>
        Assert.Single(JsonListingFeed.Parse(Feed(GoodRow.Replace("450000", "-5"))).Rejections);

    [Fact]
    public void A_repeated_source_and_id_keeps_the_first_and_rejects_the_rest()
    {
        var result = JsonListingFeed.Parse(Feed(GoodRow, GoodRow.Replace("450000", "999999")));

        Assert.Equal(450_000m, Assert.Single(result.Listings).Price.Amount);
        Assert.Contains("duplicate", Assert.Single(result.Rejections));
    }

    [Fact]
    public void The_same_id_from_different_sources_is_not_a_duplicate() =>
        Assert.Equal(2, JsonListingFeed.Parse(Feed(GoodRow, GoodRow.Replace("MLS_A", "MLS_B"))).Listings.Count);

    [Theory]
    [InlineData("null")]
    [InlineData("42")]
    [InlineData("\"just a string\"")]
    [InlineData("[1, 2]")]
    [InlineData("{}")]
    public void A_row_that_is_not_a_listing_object_is_rejected_without_losing_the_good_rows(string badRow)
    {
        var result = JsonListingFeed.Parse(Feed(GoodRow, badRow));

        Assert.Single(result.Listings);
        Assert.Single(result.Rejections);
    }

    [Fact]
    public void Numeric_ids_and_sources_are_rejected_because_the_contract_says_string() =>
        Assert.Single(JsonListingFeed.Parse(Feed(GoodRow.Replace("\"A1\"", "12"))).Rejections);

    [Fact]
    public void Blank_identity_or_address_fields_are_rejected()
    {
        Assert.Single(JsonListingFeed.Parse(Feed(GoodRow.Replace("\"A1\"", "\"  \""))).Rejections);
        Assert.Single(JsonListingFeed.Parse(Feed(GoodRow.Replace("\"Springfield\"", "\"\""))).Rejections);
        Assert.Single(JsonListingFeed.Parse(Feed(GoodRow.Replace("\"123 Main St\"", "null"))).Rejections);
    }

    [Fact]
    public void An_empty_array_is_a_valid_empty_feed()
    {
        var result = JsonListingFeed.Parse("[]");

        Assert.Empty(result.Listings);
        Assert.Empty(result.Rejections);
    }

    [Theory]
    [InlineData("{ \"not\": \"an array\" }")]
    [InlineData("not json at all")]
    [InlineData("")]
    public void A_feed_that_is_not_a_json_array_fails_loudly(string json) =>
        Assert.Throws<InvalidDataException>(() => JsonListingFeed.Parse(json));

    [Fact]
    public void A_missing_file_names_the_path_and_the_setting() =>
        Assert.Contains("Listings:FeedPath", Assert.Throws<FileNotFoundException>(() => JsonListingFeed.LoadFile("nope.json")).Message);
}

public class InMemoryListingRepositoryTests
{
    [Fact]
    public async Task Find_returns_only_listings_satisfying_the_criteria()
    {
        var repository = new InMemoryListingRepository(
        [
            TestData.Listing(id: "1", price: 300_000m),
            TestData.Listing(id: "2", price: 600_000m),
        ]);
        var criteria = ListingCriteria.Create(null, 400_000m, null, null, null).Value;

        var found = await repository.FindAsync(criteria);

        Assert.Equal("1", Assert.Single(found).Key.Id);
    }

    [Fact]
    public async Task Find_on_an_empty_repository_is_empty() =>
        Assert.Empty(await new InMemoryListingRepository([]).FindAsync(ListingCriteria.None));

    [Fact]
    public async Task Cities_are_distinct_ignoring_case()
    {
        var repository = new InMemoryListingRepository(
        [
            TestData.Listing(id: "1", city: "Springfield"),
            TestData.Listing(id: "2", city: "SPRINGFIELD"),
            TestData.Listing(id: "3", city: "Reston"),
        ]);

        Assert.Equal(2, (await repository.GetCitiesAsync()).Count);
    }
}
