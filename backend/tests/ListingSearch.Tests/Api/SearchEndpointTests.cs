using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ListingSearch.Application.Searching;
using ListingSearch.Tests.Support;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace ListingSearch.Tests.Api;

/// <summary>End-to-end through the real HTTP pipeline, the real sample feed and real JSON.</summary>
public class SearchEndpointTests(SearchEndpointTests.Factory factory) : IClassFixture<SearchEndpointTests.Factory>
{
    public sealed class Factory : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(Microsoft.AspNetCore.Hosting.IWebHostBuilder builder) =>
            builder.ConfigureTestServices(services => services.AddSingleton<TimeProvider>(new FixedClock(TestData.Today)));
    }

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private HttpClient Client => factory.CreateClient();

    private async Task<SearchListingsResponse> Search(string query)
    {
        var response = await Client.GetAsync($"/api/listings/search{query}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<SearchListingsResponse>(Json))!;
    }

    private async Task<(HttpStatusCode Status, JsonElement Body, string ContentType)> Failure(string url)
    {
        var response = await Client.GetAsync(url);
        var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement.Clone();
        return (response.StatusCode, body, response.Content.Headers.ContentType?.MediaType ?? "");
    }

    private static string[] ErrorMessages(JsonElement body, string field) =>
        body.GetProperty("errors").GetProperty(field).EnumerateArray().Select(e => e.GetString()!).ToArray();

    [Fact]
    public async Task Search_with_no_parameters_returns_the_first_page_of_all_twelve_sample_listings()
    {
        var result = await Search("");

        Assert.Equal(12, result.TotalCount);
        Assert.Equal(10, result.Items.Count);
        Assert.Equal(2, result.TotalPages);
        Assert.True(result.HasNext);
        Assert.False(result.HasPrevious);
    }

    [Fact]
    public async Task The_second_page_holds_the_remainder()
    {
        var result = await Search("?page=2");

        Assert.Equal(2, result.Items.Count);
        Assert.False(result.HasNext);
        Assert.True(result.HasPrevious);
    }

    [Fact]
    public async Task Json_uses_camel_case_and_includes_the_relevance_score()
    {
        var raw = await Client.GetStringAsync("/api/listings/search?pageSize=1");

        using var document = JsonDocument.Parse(raw);
        var first = document.RootElement.GetProperty("items")[0];
        Assert.True(first.TryGetProperty("relevanceScore", out var score));
        Assert.True(score.GetDecimal() > 0);
        Assert.True(first.TryGetProperty("address", out _));
        Assert.True(first.TryGetProperty("bedrooms", out _));
        Assert.Equal("2026-09-04", first.GetProperty("listedDate").GetString());   // A5, the newest listing
    }

    [Fact]
    public async Task City_filter_is_case_insensitive()
    {
        var result = await Search("?city=sPRINGfield");

        Assert.Equal(["A1", "A2", "B7", "B8"], result.Items.Select(i => i.Id).Order());
    }

    [Fact]
    public async Task All_filters_combine()
    {
        var result = await Search("?minPrice=400000&maxPrice=530000&minBedrooms=3&city=Springfield&keyword=kitchen");

        Assert.Equal(["A2", "B8"], result.Items.Select(i => i.Id).Order());
    }

    [Fact]
    public async Task Empty_form_fields_are_treated_as_not_provided()
    {
        var result = await Search("?minPrice=&maxPrice=&minBedrooms=&city=&keyword=&targetBudget=&page=&pageSize=");

        Assert.Equal(12, result.TotalCount);
    }

    [Fact]
    public async Task Target_budget_puts_the_best_fit_first()
    {
        var result = await Search("?targetBudget=450000");

        Assert.Equal("A1", result.Items[0].Id);
        Assert.Equal(result.Items.Select(i => i.RelevanceScore).OrderByDescending(s => s), result.Items.Select(i => i.RelevanceScore));
    }

    [Fact]
    public async Task Scores_are_computed_against_the_injected_clock_not_the_real_one()
    {
        // 2026-10-09 is the pinned "today": A1 is 41 days old, A5 is 35. Real time would give different numbers.
        var result = await Search("?targetBudget=450000&pageSize=3");

        Assert.Equal([("A1", 75.51m), ("B7", 74.28m), ("A5", 72.48m)], result.Items.Select(i => (i.Id, i.RelevanceScore)));
    }

    [Fact]
    public async Task A_known_city_with_no_matching_listings_is_an_empty_200()
    {
        var result = await Search("?city=Fairfax&minBedrooms=6");

        Assert.Empty(result.Items);
        Assert.Equal(0, result.TotalCount);
    }

    [Fact]
    public async Task Min_price_above_max_price_is_a_400_problem_naming_the_field()
    {
        var (status, body, contentType) = await Failure("/api/listings/search?minPrice=900000&maxPrice=100000");

        Assert.Equal(HttpStatusCode.BadRequest, status);
        Assert.Equal("application/problem+json", contentType);
        Assert.Contains("minPrice", ErrorMessages(body, "minPrice")[0]);
        Assert.Equal(400, body.GetProperty("status").GetInt32());
    }

    [Theory]
    [InlineData("pageSize=0", "pageSize")]
    [InlineData("pageSize=-1", "pageSize")]
    [InlineData("pageSize=101", "pageSize")]
    [InlineData("page=0", "page")]
    [InlineData("minPrice=-5", "minPrice")]
    [InlineData("minBedrooms=-1", "minBedrooms")]
    [InlineData("targetBudget=0", "targetBudget")]
    [InlineData("keyword=%21%21%21", "keyword")]
    public async Task Out_of_range_values_are_a_400_naming_the_field(string query, string field)
    {
        var (status, body, _) = await Failure($"/api/listings/search?{query}");

        Assert.Equal(HttpStatusCode.BadRequest, status);
        Assert.NotEmpty(ErrorMessages(body, field));
    }

    [Theory]
    [InlineData("minPrice=cheap", "minPrice", "number")]
    [InlineData("minBedrooms=2.5", "minBedrooms", "whole number")]
    [InlineData("page=one", "page", "whole number")]
    [InlineData("pageSize=99999999999", "pageSize", "whole number")]
    [InlineData("targetBudget=1e5", "targetBudget", "number")]
    public async Task Unparseable_values_are_a_400_that_quotes_what_was_sent(string query, string field, string expected)
    {
        var (status, body, _) = await Failure($"/api/listings/search?{query}");

        Assert.Equal(HttpStatusCode.BadRequest, status);
        Assert.Contains(expected, ErrorMessages(body, field)[0]);
        Assert.Contains(query.Split('=')[1], ErrorMessages(body, field)[0]);
    }

    [Fact]
    public async Task A_misspelt_parameter_is_rejected_instead_of_silently_ignored()
    {
        var (status, body, _) = await Failure("/api/listings/search?minBedroom=3");

        Assert.Equal(HttpStatusCode.BadRequest, status);
        Assert.Contains("not a supported parameter", ErrorMessages(body, "minBedroom")[0]);
    }

    [Fact]
    public async Task A_repeated_parameter_is_rejected()
    {
        var (status, body, _) = await Failure("/api/listings/search?city=Reston&city=Vienna");

        Assert.Equal(HttpStatusCode.BadRequest, status);
        Assert.Contains("only once", ErrorMessages(body, "city")[0]);
    }

    private static string[] ErrorFields(JsonElement body) =>
        body.GetProperty("errors").EnumerateObject().Select(p => p.Name).Order().ToArray();

    [Fact]
    public async Task All_malformed_values_are_reported_together()
    {
        var (_, body, _) = await Failure("/api/listings/search?minPrice=abc&page=x&bogus=1");

        Assert.Equal(["bogus", "minPrice", "page"], ErrorFields(body));
    }

    [Fact]
    public async Task All_rule_violations_are_reported_together()
    {
        var (_, body, _) = await Failure("/api/listings/search?minPrice=9&maxPrice=1&pageSize=0&page=-1");

        Assert.Equal(["minPrice", "page", "pageSize"], ErrorFields(body));
    }

    [Fact]
    public async Task An_unknown_city_is_a_404_with_suggestions()
    {
        var (status, body, contentType) = await Failure("/api/listings/search?city=Sprngfield");

        Assert.Equal(HttpStatusCode.NotFound, status);
        Assert.Equal("application/problem+json", contentType);
        Assert.Equal(["Springfield"], body.GetProperty("suggestions").EnumerateArray().Select(s => s.GetString()));
        Assert.Contains("Did you mean Springfield", ErrorMessages(body, "city")[0]);
    }

    [Fact]
    public async Task A_page_past_the_end_is_a_400()
    {
        var (status, body, _) = await Failure("/api/listings/search?page=3");

        Assert.Equal(HttpStatusCode.BadRequest, status);
        Assert.Contains("2 pages", ErrorMessages(body, "page")[0]);
    }

    [Fact]
    public async Task Cities_endpoint_lists_each_city_once_alphabetically()
    {
        var cities = await Client.GetFromJsonAsync<string[]>("/api/cities", Json);

        Assert.Equal(["Chantilly", "Fairfax", "Manassas", "Reston", "Springfield", "Vienna"], cities!);
    }

    [Fact]
    public async Task Unknown_routes_are_a_plain_404() =>
        Assert.Equal(HttpStatusCode.NotFound, (await Client.GetAsync("/api/nothing")).StatusCode);
}
