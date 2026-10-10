using ListingSearch.Domain.Common;
using ListingSearch.Domain.Listings;

namespace ListingSearch.Domain.Search;

/// <summary>
/// The filter half of a search: which listings qualify. Always built through <see cref="Create"/>,
/// so a criteria object that exists is a valid one. Ranking and paging are separate concepts.
/// </summary>
public sealed class ListingCriteria
{
    private readonly IReadOnlyList<IListingSpecification> _specifications;

    private ListingCriteria(
        PriceRange price,
        int? minBedrooms,
        CityName? city,
        SearchTerms? keyword)
    {
        Price = price;
        MinBedrooms = minBedrooms;
        City = city;
        Keyword = keyword;

        var specifications = new List<IListingSpecification>();
        if (price.IsBounded)
        {
            specifications.Add(new PriceRangeSpecification(price));
        }

        if (minBedrooms is not null)
        {
            specifications.Add(new MinBedroomsSpecification(minBedrooms.Value));
        }

        if (city is not null)
        {
            specifications.Add(new CitySpecification(city));
        }

        if (keyword is not null)
        {
            specifications.Add(new KeywordSpecification(keyword));
        }

        _specifications = specifications;
    }

    /// <summary>Matches everything.</summary>
    public static ListingCriteria None { get; } = new(PriceRange.Create(null, null).Value, null, null, null);

    public PriceRange Price { get; }

    public int? MinBedrooms { get; }

    public CityName? City { get; }

    public SearchTerms? Keyword { get; }

    public static Result<ListingCriteria> Create(
        decimal? minPrice,
        decimal? maxPrice,
        int? minBedrooms,
        string? city,
        string? keyword)
    {
        var errors = new List<ValidationError>();

        var price = PriceRange.Create(minPrice, maxPrice);
        errors.AddRange(price.Errors);

        if (minBedrooms < 0)
        {
            errors.Add(new ValidationError("minBedrooms", "minBedrooms must be zero or greater."));
        }

        var cityName = CityName.FromUserInput(city);
        errors.AddRange(cityName.Errors);

        var terms = SearchTerms.FromUserInput(keyword);
        errors.AddRange(terms.Errors);

        return errors.Count > 0
            ? Result<ListingCriteria>.Failure(errors)
            : Result<ListingCriteria>.Success(new ListingCriteria(price.Value, minBedrooms, cityName.Value, terms.Value));
    }

    public bool IsSatisfiedBy(Listing listing) => _specifications.All(s => s.IsSatisfiedBy(listing));
}
