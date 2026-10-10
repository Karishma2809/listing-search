using ListingSearch.Application.Cities;
using ListingSearch.Application.Searching;

namespace ListingSearch.Api.Http;

internal static class ListingEndpoints
{
    public static IEndpointRouteBuilder MapListingEndpoints(this IEndpointRouteBuilder app)
    {
        var api = app.MapGroup("/api");

        // GET /api/listings/search?minPrice=&maxPrice=&minBedrooms=&city=&keyword=&targetBudget=&page=&pageSize=
        api.MapGet("/listings/search", async (HttpRequest request, ISearchListingsHandler handler, CancellationToken cancellationToken) =>
            Results.Ok(await handler.HandleAsync(SearchQueryParser.Parse(request.Query), cancellationToken)));

        // GET /api/cities - the values the city filter accepts.
        api.MapGet("/cities", async (IGetCitiesHandler handler, CancellationToken cancellationToken) =>
            Results.Ok(await handler.HandleAsync(cancellationToken)));

        return app;
    }
}
