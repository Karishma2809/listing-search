using ListingSearch.Domain.Search;

namespace ListingSearch.Domain.Listings;

/// <summary>
/// Port for the collection of <see cref="Listing"/> aggregates. Defined by the domain, implemented by
/// infrastructure, so the domain never learns whether listings live in memory, a database or a feed.
/// </summary>
public interface IListingRepository
{
    /// <summary>Returns every listing satisfying the criteria, in no particular order.</summary>
    Task<IReadOnlyList<Listing>> FindAsync(ListingCriteria criteria, CancellationToken cancellationToken = default);

    /// <summary>Returns each distinct city that has at least one listing.</summary>
    Task<IReadOnlyList<CityName>> GetCitiesAsync(CancellationToken cancellationToken = default);
}
