using ListingSearch.Domain.Common;

namespace ListingSearch.Domain.Ranking;

/// <summary>
/// The tunable numbers behind relevance. They are a product decision rather than a law of nature,
/// so they live in one explicit, validated object that can be configured without touching the formulas.
/// </summary>
public sealed record ScoringPolicy
{
    public const double DefaultBudgetWeight = 0.6;
    public const double DefaultRecencyWeight = 0.4;
    public const double DefaultRecencyHalfLifeDays = 30;
    public const double DefaultOverBudgetPenalty = 2;

    public ScoringPolicy(
        double budgetWeight = DefaultBudgetWeight,
        double recencyWeight = DefaultRecencyWeight,
        double recencyHalfLifeDays = DefaultRecencyHalfLifeDays,
        double overBudgetPenalty = DefaultOverBudgetPenalty)
    {
        if (!double.IsFinite(budgetWeight) || budgetWeight < 0 ||
            !double.IsFinite(recencyWeight) || recencyWeight < 0 ||
            budgetWeight + recencyWeight <= 0)
        {
            throw new DomainException("Scoring weights must be non-negative and not both zero.");
        }

        if (!double.IsFinite(recencyHalfLifeDays) || recencyHalfLifeDays <= 0)
        {
            throw new DomainException("recencyHalfLifeDays must be greater than zero.");
        }

        if (!double.IsFinite(overBudgetPenalty) || overBudgetPenalty < 1)
        {
            throw new DomainException("overBudgetPenalty must be at least 1 (going over budget must never cost less than going under).");
        }

        BudgetWeight = budgetWeight;
        RecencyWeight = recencyWeight;
        RecencyHalfLifeDays = recencyHalfLifeDays;
        OverBudgetPenalty = overBudgetPenalty;
    }

    public static ScoringPolicy Default { get; } = new();

    /// <summary>Share of the score decided by how close the price is to the target budget.</summary>
    public double BudgetWeight { get; }

    /// <summary>Share of the score decided by how recently the listing went live.</summary>
    public double RecencyWeight { get; }

    /// <summary>Days after which a listing's recency score has halved.</summary>
    public double RecencyHalfLifeDays { get; }

    /// <summary>How many times worse a dollar over budget is than a dollar under.</summary>
    public double OverBudgetPenalty { get; }
}
