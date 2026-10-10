using ListingSearch.Domain.Common;

namespace ListingSearch.Domain.Search;

/// <summary>Which slice of the ranked results the caller wants. Pages are 1-based.</summary>
public sealed record PageRequest
{
    public const int DefaultPageSize = 10;
    public const int MaxPageSize = 100;

    private PageRequest(int number, int size)
    {
        Number = number;
        Size = size;
    }

    public int Number { get; }

    public int Size { get; }

    /// <summary>Omitted values fall back to page 1 / <see cref="DefaultPageSize"/>; supplied values are never silently corrected.</summary>
    public static Result<PageRequest> Create(int? page, int? pageSize)
    {
        var errors = new List<ValidationError>();

        if (page < 1)
        {
            errors.Add(new ValidationError("page", "page must be 1 or greater."));
        }

        if (pageSize < 1 || pageSize > MaxPageSize)
        {
            errors.Add(new ValidationError("pageSize", $"pageSize must be between 1 and {MaxPageSize}."));
        }

        return errors.Count > 0
            ? Result<PageRequest>.Failure(errors)
            : Result<PageRequest>.Success(new PageRequest(page ?? 1, pageSize ?? DefaultPageSize));
    }
}
