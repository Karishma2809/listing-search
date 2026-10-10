using System.Net;
using System.Text.Json;
using ListingSearch.Domain.Listings;
using ListingSearch.Domain.Search;
using ListingSearch.Tests.Support;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace ListingSearch.Tests.Api;

/// <summary>What happens when things go wrong around the edges: crashes, bad configuration, hostile input.</summary>
public class RobustnessTests
{
    private sealed class ExplodingRepository : IListingRepository
    {
        public Task<IReadOnlyList<Listing>> FindAsync(ListingCriteria criteria, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("secret connection string: Server=prod;Password=hunter2");

        public Task<IReadOnlyList<CityName>> GetCitiesAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<CityName>>([]);
    }

    private static WebApplicationFactory<Program> Factory(Action<IWebHostBuilder> configure) =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(configure);

    private static IEnumerable<Exception> Chain(Exception? exception)
    {
        for (var current = exception; current is not null; current = current.InnerException)
        {
            yield return current;
        }
    }

    [Fact]
    public async Task An_unexpected_failure_is_a_generic_500_problem_that_leaks_nothing()
    {
        using var factory = Factory(builder =>
        {
            builder.UseEnvironment("Production");
            builder.ConfigureTestServices(services => services.AddSingleton<IListingRepository>(new ExplodingRepository()));
        });

        var response = await factory.CreateClient().GetAsync("/api/listings/search");
        var raw = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal(500, JsonDocument.Parse(raw).RootElement.GetProperty("status").GetInt32());
        Assert.DoesNotContain("hunter2", raw);
        Assert.DoesNotContain("InvalidOperationException", raw);
        Assert.DoesNotContain("   at ", raw);
    }

    [Fact]
    public void A_feed_that_is_not_a_json_array_stops_startup_instead_of_the_first_request()
    {
        var path = Path.GetTempFileName();
        File.WriteAllText(path, "{ \"oops\": true }");
        try
        {
            using var factory = Factory(builder => builder.UseSetting("Listings:FeedPath", path));

            var thrown = Assert.ThrowsAny<Exception>(() => factory.CreateClient());

            Assert.Contains(Chain(thrown), e => e is InvalidDataException && e.Message.Contains("JSON array"));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void A_missing_feed_file_stops_startup()
    {
        using var factory = Factory(builder => builder.UseSetting("Listings:FeedPath", Path.Combine(Path.GetTempPath(), "no-such-feed.json")));

        var thrown = Assert.ThrowsAny<Exception>(() => factory.CreateClient());

        Assert.Contains(Chain(thrown), e => e is FileNotFoundException);
    }

    [Fact]
    public void Nonsensical_scoring_configuration_stops_startup()
    {
        using var factory = Factory(builder => builder.UseSetting("Scoring:BudgetWeight", "-1"));

        var thrown = Assert.ThrowsAny<Exception>(() => factory.CreateClient());

        Assert.Contains(Chain(thrown), e => e.Message.Contains("weights"));
    }

    [Fact]
    public async Task Scoring_configuration_is_honoured()
    {
        using var factory = Factory(builder =>
        {
            builder.UseSetting("Scoring:BudgetWeight", "1");
            builder.UseSetting("Scoring:RecencyWeight", "0");
            builder.ConfigureTestServices(services => services.AddSingleton<TimeProvider>(new FixedClock(TestData.Today)));
        });

        // Budget-only scoring: a listing priced exactly on target scores 100 whatever its age.
        var raw = await factory.CreateClient().GetStringAsync("/api/listings/search?targetBudget=450000&pageSize=1");

        var top = JsonDocument.Parse(raw).RootElement.GetProperty("items")[0];
        Assert.Equal("A1", top.GetProperty("id").GetString());
        Assert.Equal(100m, top.GetProperty("relevanceScore").GetDecimal());
    }

    [Theory]
    [InlineData("targetBudget=0.0000000000000000000000000001")]       // 1e-28: price/target would overflow decimal
    [InlineData("targetBudget=79228162514264337593543950335")]        // decimal.MaxValue
    [InlineData("minPrice=79228162514264337593543950335")]
    [InlineData("maxPrice=0")]
    [InlineData("minBedrooms=2147483647")]
    [InlineData("minPrice=5.")]
    [InlineData("minPrice=.5")]
    [InlineData("minBedrooms=%2B3")]                                    // "+3"
    [InlineData("city=%20%20&keyword=%20")]                             // whitespace only = not provided
    [InlineData("MINPRICE=100&Page=1&PAGESIZE=3")]                      // parameter names are case-insensitive
    public async Task Extreme_but_legal_values_never_crash(string query)
    {
        using var factory = Factory(builder =>
            builder.ConfigureTestServices(services => services.AddSingleton<TimeProvider>(new FixedClock(TestData.Today))));

        var response = await factory.CreateClient().GetAsync($"/api/listings/search?{query}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Theory]
    [InlineData("minPrice=79228162514264337593543950336")]             // one past decimal.MaxValue
    [InlineData("minPrice=1,5")]
    [InlineData("minPrice=%EF%BC%91")]                                  // full-width digit
    [InlineData("minBedrooms=9999999999")]
    [InlineData("page=%00")]
    public async Task Hostile_values_are_a_clean_400(string query)
    {
        using var factory = Factory(_ => { });

        var response = await factory.CreateClient().GetAsync($"/api/listings/search?{query}");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }
}
