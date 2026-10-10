namespace ListingSearch.Application.Searching;

/// <summary>
/// A search request exactly as a caller expressed it: raw, optional, unvalidated. Turning this into
/// domain objects (and rejecting it if it does not fit) is the handler's first job.
/// </summary>
public sealed record SearchListingsQuery(
    decimal? MinPrice = null,
    decimal? MaxPrice = null,
    int? MinBedrooms = null,
    string? City = null,
    string? Keyword = null,
    decimal? TargetBudget = null,
    int? Page = null,
    int? PageSize = null);
