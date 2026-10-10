namespace ListingSearch.Application.Searching;

public sealed record ListingDto(
    string Id,
    string Source,
    string Address,
    string City,
    string State,
    string Zip,
    decimal Price,
    int Bedrooms,
    decimal Bathrooms,
    int SquareFeet,
    DateOnly ListedDate,
    string Status,
    string Description,
    decimal RelevanceScore);

public sealed record SearchListingsResponse(
    IReadOnlyList<ListingDto> Items,
    int Page,
    int PageSize,
    int TotalCount,
    int TotalPages,
    bool HasNext,
    bool HasPrevious);
