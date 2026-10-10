using ListingSearch.Domain.Common;

namespace ListingSearch.Domain.Ranking;

/// <summary>
/// How well a listing fits the search, 0 (poor) to 100 (ideal), to two decimals. Rounding happens
/// once, here, so the number a user sees is the number that was sorted on and ties are real ties.
/// </summary>
public sealed record Relevance
{
    private Relevance(decimal score) => Score = score;

    public decimal Score { get; }

    /// <summary>Builds from a 0..1 fraction; values outside the range are clamped.</summary>
    public static Relevance FromFraction(double fraction)
    {
        if (double.IsNaN(fraction))
        {
            throw new DomainException("relevance cannot be NaN.");
        }

        var clamped = Math.Clamp(fraction, 0d, 1d);
        return new Relevance(Math.Round((decimal)clamped * 100m, 2, MidpointRounding.AwayFromZero));
    }
}
