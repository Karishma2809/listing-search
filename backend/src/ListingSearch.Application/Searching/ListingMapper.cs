using ListingSearch.Domain.Ranking;

namespace ListingSearch.Application.Searching;

internal static class ListingMapper
{
    public static ListingDto ToDto(RankedListing ranked)
    {
        var listing = ranked.Listing;
        return new ListingDto(
            listing.Key.Id,
            listing.Key.Source,
            listing.Address.Street,
            listing.Address.City.Display,
            listing.Address.State,
            listing.Address.Zip,
            listing.Price.Amount,
            listing.Bedrooms,
            listing.Bathrooms,
            listing.SquareFeet,
            listing.ListedOn,
            listing.Status.ToString().ToLowerInvariant(),
            listing.Description.Text,
            ranked.Relevance.Score);
    }
}
