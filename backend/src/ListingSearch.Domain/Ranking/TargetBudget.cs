using ListingSearch.Domain.Common;

namespace ListingSearch.Domain.Ranking;

/// <summary>
/// The price the searcher would ideally pay. Unlike minPrice/maxPrice it removes nothing from the
/// results; it only reorders them.
/// </summary>
public sealed record TargetBudget
{
    private TargetBudget(decimal amount) => Amount = amount;

    public decimal Amount { get; }

    /// <summary>Omitted means "no budget preference" (success with null); zero or negative is an error.</summary>
    public static Result<TargetBudget?> FromUserInput(decimal? raw, string field = "targetBudget")
    {
        if (raw is null)
        {
            return Result<TargetBudget?>.Success(null);
        }

        return raw <= 0
            ? Result<TargetBudget?>.Failure(field, $"{field} must be greater than zero.")
            : Result<TargetBudget?>.Success(new TargetBudget(raw.Value));
    }
}
