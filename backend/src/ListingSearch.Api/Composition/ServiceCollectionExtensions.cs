using ListingSearch.Application.Cities;
using ListingSearch.Application.Searching;
using ListingSearch.Domain.Ranking;
using ListingSearch.Infrastructure;

namespace ListingSearch.Api.Composition;

/// <summary>
/// Composition root: the only place that knows every layer and wires interfaces to implementations.
/// </summary>
internal static class ServiceCollectionExtensions
{
    public static IServiceCollection AddListingSearch(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddListingInfrastructure(configuration);

        // Built eagerly so an invalid Scoring section stops startup instead of failing the first search.
        var scoring = configuration.GetSection("Scoring").Get<ScoringOptions>() ?? new ScoringOptions();
        services.AddSingleton(scoring.ToPolicy());
        services.AddSingleton<RelevanceScorer>();
        services.AddSingleton<ListingRanker>();
        services.AddSingleton(TimeProvider.System);

        services.AddScoped<ISearchListingsHandler, SearchListingsHandler>();
        services.AddScoped<IGetCitiesHandler, GetCitiesHandler>();
        return services;
    }

    /// <summary>Settings shape for the "Scoring" configuration section; defaults come from the domain.</summary>
    private sealed class ScoringOptions
    {
        public double BudgetWeight { get; set; } = ScoringPolicy.DefaultBudgetWeight;

        public double RecencyWeight { get; set; } = ScoringPolicy.DefaultRecencyWeight;

        public double RecencyHalfLifeDays { get; set; } = ScoringPolicy.DefaultRecencyHalfLifeDays;

        public double OverBudgetPenalty { get; set; } = ScoringPolicy.DefaultOverBudgetPenalty;

        public ScoringPolicy ToPolicy() => new(BudgetWeight, RecencyWeight, RecencyHalfLifeDays, OverBudgetPenalty);
    }
}
