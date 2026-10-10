using ListingSearch.Domain.Listings;

namespace ListingSearch.Domain.Search;

/// <summary>
/// One rule a listing must satisfy to be returned. Each filter is its own small specification, so a
/// new filter (status, pets, radius...) is one new class plus one line in <see cref="ListingCriteria"/>.
/// </summary>
public interface IListingSpecification
{
    bool IsSatisfiedBy(Listing listing);
}

public sealed record PriceRangeSpecification(PriceRange Range) : IListingSpecification
{
    public bool IsSatisfiedBy(Listing listing) => Range.Contains(listing.Price.Amount);
}

public sealed record MinBedroomsSpecification(int Minimum) : IListingSpecification
{
    public bool IsSatisfiedBy(Listing listing) => listing.Bedrooms >= Minimum;
}

public sealed record CitySpecification(CityName City) : IListingSpecification
{
    public bool IsSatisfiedBy(Listing listing) => listing.Address.City.Equals(City);
}

public sealed record KeywordSpecification(SearchTerms Terms) : IListingSpecification
{
    public bool IsSatisfiedBy(Listing listing) => listing.Description.ContainsAllTerms(Terms.Words);
}
