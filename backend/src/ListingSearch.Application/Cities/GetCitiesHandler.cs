using ListingSearch.Domain.Listings;

namespace ListingSearch.Application.Cities;

public interface IGetCitiesHandler
{
    Task<IReadOnlyList<string>> HandleAsync(CancellationToken cancellationToken = default);
}

/// <summary>Use case: the cities a user can meaningfully filter by, alphabetically.</summary>
public sealed class GetCitiesHandler(IListingRepository repository) : IGetCitiesHandler
{
    public async Task<IReadOnlyList<string>> HandleAsync(CancellationToken cancellationToken = default)
    {
        var cities = await repository.GetCitiesAsync(cancellationToken);
        return cities.Select(city => city.Display).Order(StringComparer.OrdinalIgnoreCase).ToList();
    }
}
