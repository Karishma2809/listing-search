using ListingSearch.Domain.Listings;
using ListingSearch.Domain.Search;

namespace ListingSearch.Infrastructure.Persistence;

/// <summary>
/// Immutable in-memory collection of listings. Built once from a feed and only ever read afterwards,
/// so it is safe to share between concurrent requests without locking.
/// </summary>
public sealed class InMemoryListingRepository : IListingRepository
{
    private readonly IReadOnlyList<Listing> _listings;
    private readonly IReadOnlyList<CityName> _cities;

    public InMemoryListingRepository(IEnumerable<Listing> listings)
    {
        _listings = listings.ToList();
        _cities = _listings.Select(listing => listing.Address.City).Distinct().ToList();
    }

    public Task<IReadOnlyList<Listing>> FindAsync(ListingCriteria criteria, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<Listing>>(_listings.Where(criteria.IsSatisfiedBy).ToList());

    public Task<IReadOnlyList<CityName>> GetCitiesAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(_cities);
}
