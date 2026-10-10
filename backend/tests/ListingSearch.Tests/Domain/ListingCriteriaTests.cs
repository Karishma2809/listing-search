using ListingSearch.Domain.Search;
using ListingSearch.Tests.Support;

namespace ListingSearch.Tests.Domain;

public class PriceRangeTests
{
    [Fact]
    public void Bounds_are_inclusive()
    {
        var range = PriceRange.Create(100m, 200m).Value;

        Assert.True(range.Contains(100m));
        Assert.True(range.Contains(200m));
        Assert.False(range.Contains(99.99m));
        Assert.False(range.Contains(200.01m));
    }

    [Fact]
    public void Either_end_may_be_open()
    {
        Assert.True(PriceRange.Create(100m, null).Value.Contains(10_000_000m));
        Assert.True(PriceRange.Create(null, 100m).Value.Contains(1m));
        Assert.False(PriceRange.Create(null, null).Value.IsBounded);
    }

    [Fact]
    public void Equal_min_and_max_is_a_valid_exact_price() =>
        Assert.True(PriceRange.Create(500m, 500m).IsSuccess);

    [Fact]
    public void Min_greater_than_max_is_rejected_with_both_numbers_in_the_message()
    {
        var error = Assert.Single(PriceRange.Create(500m, 100m).Errors);

        Assert.Equal("minPrice", error.Field);
        Assert.Contains("500", error.Message);
        Assert.Contains("100", error.Message);
    }

    [Fact]
    public void Negative_prices_are_rejected_per_field()
    {
        var fields = PriceRange.Create(-2m, -1m).Errors.Select(e => e.Field);

        Assert.Equal(["minPrice", "maxPrice"], fields);
    }
}

public class ListingCriteriaTests
{
    private static ListingCriteria Criteria(
        decimal? minPrice = null,
        decimal? maxPrice = null,
        int? minBedrooms = null,
        string? city = null,
        string? keyword = null) =>
        ListingCriteria.Create(minPrice, maxPrice, minBedrooms, city, keyword).Value;

    [Fact]
    public void No_filters_matches_everything()
    {
        Assert.True(Criteria().IsSatisfiedBy(TestData.Listing()));
        Assert.True(ListingCriteria.None.IsSatisfiedBy(TestData.Listing()));
    }

    [Fact]
    public void Price_filter_uses_the_list_price_inclusively()
    {
        var criteria = Criteria(minPrice: 400_000m, maxPrice: 500_000m);

        Assert.True(criteria.IsSatisfiedBy(TestData.Listing(price: 400_000m)));
        Assert.True(criteria.IsSatisfiedBy(TestData.Listing(price: 500_000m)));
        Assert.False(criteria.IsSatisfiedBy(TestData.Listing(price: 399_999m)));
        Assert.False(criteria.IsSatisfiedBy(TestData.Listing(price: 500_001m)));
    }

    [Fact]
    public void Min_bedrooms_means_at_least()
    {
        var criteria = Criteria(minBedrooms: 3);

        Assert.True(criteria.IsSatisfiedBy(TestData.Listing(bedrooms: 3)));
        Assert.True(criteria.IsSatisfiedBy(TestData.Listing(bedrooms: 5)));
        Assert.False(criteria.IsSatisfiedBy(TestData.Listing(bedrooms: 2)));
    }

    [Fact]
    public void Zero_min_bedrooms_is_valid_and_excludes_nothing() =>
        Assert.True(Criteria(minBedrooms: 0).IsSatisfiedBy(TestData.Listing(bedrooms: 0)));

    [Theory]
    [InlineData("springfield")]
    [InlineData("SPRINGFIELD")]
    [InlineData("  Springfield  ")]
    public void City_matches_case_and_whitespace_insensitively(string city)
    {
        var criteria = Criteria(city: city);

        Assert.True(criteria.IsSatisfiedBy(TestData.Listing(city: "Springfield")));
        Assert.False(criteria.IsSatisfiedBy(TestData.Listing(city: "Fairfax")));
    }

    [Fact]
    public void City_must_match_exactly_not_by_prefix() =>
        Assert.False(Criteria(city: "Spring").IsSatisfiedBy(TestData.Listing(city: "Springfield")));

    [Fact]
    public void Keyword_is_matched_against_the_description_only()
    {
        var criteria = Criteria(keyword: "fenced");

        Assert.True(criteria.IsSatisfiedBy(TestData.Listing(description: "Updated kitchen, Fenced yard.")));
        Assert.False(criteria.IsSatisfiedBy(TestData.Listing(description: "Open plan.", street: "1 Fenced Rd")));
    }

    [Fact]
    public void All_filters_must_hold_together()
    {
        var criteria = Criteria(maxPrice: 500_000m, minBedrooms: 3, city: "Reston", keyword: "pool");

        Assert.True(criteria.IsSatisfiedBy(TestData.Listing(city: "Reston", price: 450_000m, bedrooms: 3, description: "Community pool.")));
        Assert.False(criteria.IsSatisfiedBy(TestData.Listing(city: "Reston", price: 450_000m, bedrooms: 2, description: "Community pool.")));
        Assert.False(criteria.IsSatisfiedBy(TestData.Listing(city: "Vienna", price: 450_000m, bedrooms: 3, description: "Community pool.")));
    }

    [Fact]
    public void Every_invalid_field_is_reported_at_once()
    {
        var result = ListingCriteria.Create(
            minPrice: 900m, maxPrice: 100m, minBedrooms: -1, city: new string('c', 101), keyword: "***");

        Assert.False(result.IsSuccess);
        Assert.Equal(
            ["city", "keyword", "minBedrooms", "minPrice"],
            result.Errors.Select(e => e.Field).Order().ToArray());
    }
}
