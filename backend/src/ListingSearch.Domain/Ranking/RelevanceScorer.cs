using ListingSearch.Domain.Listings;

namespace ListingSearch.Domain.Ranking;

/// <summary>
/// Domain service that turns a listing into a <see cref="Relevance"/>. Pure: the clock is passed in
/// as <c>today</c>, so the same inputs always give the same score.
///
/// score = 100 * (BudgetWeight * budgetFit + RecencyWeight * recency) / (BudgetWeight + RecencyWeight)
///
///   budgetFit   1 at the target price, falling linearly with the relative gap; 0 once the gap is 100%
///               (or 100% / OverBudgetPenalty when the price is above target).
///   recency     0.5 ^ (ageInDays / RecencyHalfLifeDays): 1 for a listing that went live today, 0.5 after
///               one half-life, approaching (never reaching) 0.
///
/// With no target budget there is nothing to fit, so budgetFit is dropped and the score is recency alone.
/// </summary>
public sealed class RelevanceScorer(ScoringPolicy policy)
{
    public Relevance Score(Listing listing, TargetBudget? target, DateOnly today)
    {
        var recency = RecencyFit(listing.ListedOn, today);
        if (target is null)
        {
            return Relevance.FromFraction(recency);
        }

        var budget = BudgetFit(listing.Price.Amount, target.Amount);
        var totalWeight = policy.BudgetWeight + policy.RecencyWeight;
        return Relevance.FromFraction((policy.BudgetWeight * budget + policy.RecencyWeight * recency) / totalWeight);
    }

    public double BudgetFit(decimal price, decimal target)
    {
        // Divide in double, not decimal: a legal but tiny target (1e-28) makes price/target overflow decimal.
        var relativeGap = ((double)price - (double)target) / (double)target;
        var penalty = relativeGap > 0 ? relativeGap * policy.OverBudgetPenalty : -relativeGap;
        return Math.Max(0d, 1d - penalty);
    }

    public double RecencyFit(DateOnly listedOn, DateOnly today)
    {
        // A listing dated in the future (clock skew, bad feed data) counts as brand new, not as "more than new".
        var ageDays = Math.Max(0, today.DayNumber - listedOn.DayNumber);
        return Math.Pow(0.5, ageDays / policy.RecencyHalfLifeDays);
    }
}
