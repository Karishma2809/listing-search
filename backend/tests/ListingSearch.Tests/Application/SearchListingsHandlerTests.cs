using ListingSearch.Application.Cities;
using ListingSearch.Application.Searching;
using ListingSearch.Domain.Listings;
using ListingSearch.Domain.Ranking;
using ListingSearch.Infrastructure.Persistence;
using ListingSearch.Tests.Support;

namespace ListingSearch.Tests.Application;

public class SearchListingsHandlerTests
{
    private static SearchListingsHandler HandlerFor(params Listing[] listings) => new(
        new InMemoryListingRepository(listings),
        new ListingRanker(new RelevanceScorer(ScoringPolicy.Default)),
        new FixedClock(TestData.Today));

    private static readonly Listing[] Market =
    [
        TestData.Listing(id: "1", city: "Springfield", price: 450_000m, bedrooms: 2, listedOn: "2026-08-29", description: "Top-floor condo. Pet friendly."),
        TestData.Listing(id: "2", city: "Springfield", price: 525_000m, bedrooms: 3, listedOn: "2026-09-02", description: "Updated kitchen, fenced yard."),
        TestData.Listing(id: "3", city: "Fairfax", price: 399_000m, bedrooms: 2, listedOn: "2026-09-01", description: "Cozy starter home, no pets."),
        TestData.Listing(id: "4", city: "Reston", price: 610_000m, bedrooms: 4, listedOn: "2026-09-03", description: "Spacious family home. Pets welcome."),
    ];

    [Fact]
    public async Task No_filters_returns_every_listing_with_default_paging()
    {
        var response = await HandlerFor(Market).HandleAsync(new SearchListingsQuery());

        Assert.Equal(4, response.TotalCount);
        Assert.Equal(4, response.Items.Count);
        Assert.Equal(1, response.Page);
        Assert.Equal(10, response.PageSize);
        Assert.Equal(1, response.TotalPages);
    }

    [Fact]
    public async Task Filters_narrow_the_results()
    {
        var response = await HandlerFor(Market).HandleAsync(
            new SearchListingsQuery(MaxPrice: 530_000m, MinBedrooms: 3, City: "springfield"));

        Assert.Equal(["2"], response.Items.Select(i => i.Id));
    }

    [Fact]
    public async Task Keyword_filter_matches_description_words()
    {
        var response = await HandlerFor(Market).HandleAsync(new SearchListingsQuery(Keyword: "pets"));

        Assert.Equal(["3", "4"], response.Items.Select(i => i.Id).Order());
    }

    [Fact]
    public async Task A_known_city_whose_filters_exclude_everything_is_an_empty_success_not_an_error()
    {
        var response = await HandlerFor(Market).HandleAsync(new SearchListingsQuery(City: "Fairfax", MinBedrooms: 9));

        Assert.Empty(response.Items);
        Assert.Equal(0, response.TotalCount);
        Assert.Equal(0, response.TotalPages);
        Assert.False(response.HasNext);
    }

    [Fact]
    public async Task Filters_that_match_nothing_anywhere_are_an_empty_success()
    {
        var response = await HandlerFor(Market).HandleAsync(new SearchListingsQuery(Keyword: "swimming"));

        Assert.Empty(response.Items);
    }

    [Fact]
    public async Task An_unknown_city_is_an_error_with_suggestions_for_near_misses()
    {
        var thrown = await Assert.ThrowsAsync<UnknownCityException>(() =>
            HandlerFor(Market).HandleAsync(new SearchListingsQuery(City: "Sprngfield")));

        Assert.Equal("Sprngfield", thrown.City);
        Assert.Equal(["Springfield"], thrown.Suggestions);
    }

    [Fact]
    public async Task An_unknown_city_with_no_near_miss_has_no_suggestions()
    {
        var thrown = await Assert.ThrowsAsync<UnknownCityException>(() =>
            HandlerFor(Market).HandleAsync(new SearchListingsQuery(City: "Tokyo-on-Thames")));

        Assert.Empty(thrown.Suggestions);
    }

    [Fact]
    public async Task Every_invalid_parameter_is_reported_together()
    {
        var thrown = await Assert.ThrowsAsync<InvalidSearchRequestException>(() =>
            HandlerFor(Market).HandleAsync(new SearchListingsQuery(
                MinPrice: 500m, MaxPrice: 100m, TargetBudget: 0m, Page: 0, PageSize: -3)));

        Assert.Equal(
            ["minPrice", "page", "pageSize", "targetBudget"],
            thrown.Errors.Select(e => e.Field).Order().ToArray());
    }

    [Fact]
    public async Task Validation_runs_before_any_lookup_so_a_bad_request_never_reports_an_unknown_city() =>
        await Assert.ThrowsAsync<InvalidSearchRequestException>(() =>
            HandlerFor(Market).HandleAsync(new SearchListingsQuery(City: "Nowhere", MinPrice: 9m, MaxPrice: 1m)));

    [Fact]
    public async Task Pages_partition_the_ranked_results_without_gaps_or_overlap()
    {
        var handler = HandlerFor(Enumerable.Range(1, 12).Select(n => TestData.Listing(id: n.ToString(), listedOn: $"2026-09-{n:00}")).ToArray());

        var pages = new List<SearchListingsResponse>();
        for (var page = 1; page <= 3; page++)
        {
            pages.Add(await handler.HandleAsync(new SearchListingsQuery(Page: page, PageSize: 5)));
        }

        var ids = pages.SelectMany(p => p.Items).Select(i => i.Id).ToList();
        Assert.Equal(12, ids.Distinct().Count());
        Assert.Equal([5, 5, 2], pages.Select(p => p.Items.Count));
        Assert.Equal("12", ids[0]);   // newest first when there is no budget
        Assert.All(pages, p => Assert.Equal(12, p.TotalCount));
    }

    [Fact]
    public async Task A_page_past_the_end_is_an_error_not_an_empty_page()
    {
        var thrown = await Assert.ThrowsAsync<InvalidSearchRequestException>(() =>
            HandlerFor(Market).HandleAsync(new SearchListingsQuery(Page: 2, PageSize: 10)));

        Assert.Equal("page", Assert.Single(thrown.Errors).Field);
    }

    [Fact]
    public async Task Target_budget_reorders_but_never_removes_results()
    {
        var withoutBudget = await HandlerFor(Market).HandleAsync(new SearchListingsQuery());
        var withBudget = await HandlerFor(Market).HandleAsync(new SearchListingsQuery(TargetBudget: 610_000m));

        Assert.Equal(withoutBudget.TotalCount, withBudget.TotalCount);
        Assert.Equal("4", withBudget.Items[0].Id);   // priced exactly on target
    }

    [Fact]
    public async Task Scores_are_descending_and_use_the_injected_clock()
    {
        var response = await HandlerFor(Market).HandleAsync(new SearchListingsQuery(TargetBudget: 450_000m));

        var scores = response.Items.Select(i => i.RelevanceScore).ToList();
        Assert.Equal(scores.OrderByDescending(s => s), scores);

        var laterClock = new SearchListingsHandler(
            new InMemoryListingRepository(Market),
            new ListingRanker(new RelevanceScorer(ScoringPolicy.Default)),
            new FixedClock(TestData.Today.AddDays(60)));
        var later = await laterClock.HandleAsync(new SearchListingsQuery(TargetBudget: 450_000m));
        Assert.True(later.Items.Max(i => i.RelevanceScore) < response.Items.Max(i => i.RelevanceScore));
    }

    [Fact]
    public async Task Dto_exposes_the_fields_the_ui_needs()
    {
        var item = (await HandlerFor(Market).HandleAsync(new SearchListingsQuery(City: "Reston"))).Items.Single();

        Assert.Equal("4", item.Id);
        Assert.Equal("MLS_A", item.Source);
        Assert.Equal("1 Main St", item.Address);
        Assert.Equal(610_000m, item.Price);
        Assert.Equal(4, item.Bedrooms);
        Assert.Equal("active", item.Status);
        Assert.True(item.RelevanceScore > 0);
    }
}

public class GetCitiesHandlerTests
{
    [Fact]
    public async Task Cities_are_distinct_across_spelling_and_sorted()
    {
        var repository = new InMemoryListingRepository(
        [
            TestData.Listing(id: "1", city: "Vienna"),
            TestData.Listing(id: "2", city: "springfield"),
            TestData.Listing(id: "3", city: "Springfield "),
            TestData.Listing(id: "4", city: "Fairfax"),
        ]);

        var cities = await new GetCitiesHandler(repository).HandleAsync();

        Assert.Equal(["Fairfax", "springfield", "Vienna"], cities);
    }

    [Fact]
    public async Task No_listings_means_no_cities() =>
        Assert.Empty(await new GetCitiesHandler(new InMemoryListingRepository([])).HandleAsync());
}

public class CitySuggesterTests
{
    [Theory]
    [InlineData("kitten", "sitting", 3)]
    [InlineData("same", "same", 0)]
    [InlineData("", "abc", 3)]
    [InlineData("abc", "", 3)]
    public void Edit_distance_is_levenshtein(string a, string b, int expected) =>
        Assert.Equal(expected, CitySuggester.EditDistance(a, b));

    [Fact]
    public void Suggestions_are_closest_first_and_capped()
    {
        var known = new[] { "Fairfax", "Fairfield", "Fairview", "Fairmont", "Fairhaven", "Fairlawn", "Zzzzzz" }
            .Select(name => new CityName(name)).ToList();

        var suggestions = CitySuggester.Suggest("Fairfax", known);

        Assert.Equal("Fairfax", suggestions[0]);
        Assert.True(suggestions.Count <= 5);
        Assert.DoesNotContain("Zzzzzz", suggestions);
    }
}
