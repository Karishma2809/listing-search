using ListingSearch.Domain.Common;

namespace ListingSearch.Domain.Search;

/// <summary>An inclusive price band where either end may be open. Invariant: Min &lt;= Max when both are set.</summary>
public sealed record PriceRange
{
    private PriceRange(decimal? min, decimal? max)
    {
        Min = min;
        Max = max;
    }

    public decimal? Min { get; }

    public decimal? Max { get; }

    public bool IsBounded => Min is not null || Max is not null;

    public static Result<PriceRange> Create(decimal? min, decimal? max)
    {
        var errors = new List<ValidationError>();

        if (min < 0)
        {
            errors.Add(new ValidationError("minPrice", "minPrice must be zero or greater."));
        }

        if (max < 0)
        {
            errors.Add(new ValidationError("maxPrice", "maxPrice must be zero or greater."));
        }

        if (min is not null && max is not null && min > max)
        {
            errors.Add(new ValidationError("minPrice", $"minPrice ({min:0.##}) must not be greater than maxPrice ({max:0.##})."));
        }

        return errors.Count == 0
            ? Result<PriceRange>.Success(new PriceRange(min, max))
            : Result<PriceRange>.Failure(errors);
    }

    public bool Contains(decimal amount) =>
        (Min is null || amount >= Min) && (Max is null || amount <= Max);
}
