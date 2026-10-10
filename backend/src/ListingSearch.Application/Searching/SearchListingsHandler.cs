using ListingSearch.Domain.Common;
using ListingSearch.Domain.Listings;
using ListingSearch.Domain.Ranking;
using ListingSearch.Domain.Search;

namespace ListingSearch.Application.Searching;

public interface ISearchListingsHandler
{
    /// <exception cref="InvalidSearchRequestException">A parameter is invalid, or the page is past the last page.</exception>
    /// <exception cref="UnknownCityException">The city filter names a city no feed has.</exception>
    Task<SearchListingsResponse> HandleAsync(SearchListingsQuery query, CancellationToken cancellationToken = default);
}

/// <summary>
/// Use case: filter, rank and page listings. Orchestrates only - validation rules live in the
/// domain's factories, filtering in <see cref="ListingCriteria"/>, scoring in <see cref="ListingRanker"/>.
/// </summary>
public sealed class SearchListingsHandler(
    IListingRepository repository,
    ListingRanker ranker,
    TimeProvider clock) : ISearchListingsHandler
{
    public async Task<SearchListingsResponse> HandleAsync(SearchListingsQuery query, CancellationToken cancellationToken = default)
    {
        var (criteria, target, paging) = Validate(query);

        if (criteria.City is { } city)
        {
            var knownCities = await repository.GetCitiesAsync(cancellationToken);
            if (!knownCities.Contains(city))
            {
                throw new UnknownCityException(city.Display, CitySuggester.Suggest(city.Display, knownCities));
            }
        }

        var matches = await repository.FindAsync(criteria, cancellationToken);
        var today = DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime);
        var ranked = ranker.Rank(matches, target, today);

        var page = Page<RankedListing>.Create(ranked, paging);
        if (!page.IsSuccess)
        {
            throw new InvalidSearchRequestException(page.Errors);
        }

        return ToResponse(page.Value);
    }

    private static (ListingCriteria Criteria, TargetBudget? Target, PageRequest Paging) Validate(SearchListingsQuery query)
    {
        var criteria = ListingCriteria.Create(query.MinPrice, query.MaxPrice, query.MinBedrooms, query.City, query.Keyword);
        var target = TargetBudget.FromUserInput(query.TargetBudget);
        var paging = PageRequest.Create(query.Page, query.PageSize);

        // Gather every problem before failing so the caller can fix them all in one go.
        var errors = criteria.Errors.Concat(target.Errors).Concat(paging.Errors).ToList();
        if (errors.Count > 0)
        {
            throw new InvalidSearchRequestException(errors);
        }

        return (criteria.Value, target.Value, paging.Value);
    }

    private static SearchListingsResponse ToResponse(Page<RankedListing> page) => new(
        page.Items.Select(ListingMapper.ToDto).ToList(),
        page.Number,
        page.Size,
        page.TotalCount,
        page.TotalPages,
        page.HasNext,
        page.HasPrevious);
}
