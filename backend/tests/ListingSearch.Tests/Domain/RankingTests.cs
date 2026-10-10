using ListingSearch.Domain.Common;
using ListingSearch.Domain.Ranking;
using ListingSearch.Tests.Support;

namespace ListingSearch.Tests.Domain;

public class TargetBudgetTests
{
    [Fact]
    public void Omitted_budget_means_no_preference()
    {
        var result = TargetBudget.FromUserInput(null);

        Assert.True(result.IsSuccess);
        Assert.Null(result.Value);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public void Zero_or_negative_budget_is_rejected(int amount)
    {
        var error = Assert.Single(TargetBudget.FromUserInput(amount).Errors);

        Assert.Equal("targetBudget", error.Field);
    }
}

public class ScoringPolicyTests
{
    [Fact]
    public void Defaults_are_valid() => Assert.Equal(0.6, ScoringPolicy.Default.BudgetWeight);

    [Theory]
    [InlineData(-0.1, 0.4, 30d, 2d)]
    [InlineData(0.6, -0.4, 30d, 2d)]
    [InlineData(0d, 0d, 30d, 2d)]
    [InlineData(0.6, 0.4, 0d, 2d)]
    [InlineData(0.6, 0.4, 30d, 0.5)]
    [InlineData(double.NaN, 0.4, 30d, 2d)]
    public void Nonsensical_policies_are_refused(double budget, double recency, double halfLife, double penalty) =>
        Assert.Throws<DomainException>(() => new ScoringPolicy(budget, recency, halfLife, penalty));
}

public class RelevanceTests
{
    [Fact]
    public void Fraction_is_scaled_to_a_hundred_and_rounded_to_two_places() =>
        Assert.Equal(33.33m, Relevance.FromFraction(1d / 3).Score);

    [Theory]
    [InlineData(-3d, 0)]
    [InlineData(7d, 100)]
    public void Out_of_range_fractions_are_clamped(double fraction, int expected) =>
        Assert.Equal(expected, Relevance.FromFraction(fraction).Score);

    [Fact]
    public void NaN_is_refused() =>
        Assert.Throws<DomainException>(() => Relevance.FromFraction(double.NaN));
}

public class RelevanceScorerTests
{
    private static readonly RelevanceScorer Scorer = new(ScoringPolicy.Default);
    private static readonly TargetBudget Target = TargetBudget.FromUserInput(500_000m).Value!;

    private static decimal Score(decimal price, string listedOn, TargetBudget? target = null) =>
        Scorer.Score(TestData.Listing(price: price, listedOn: listedOn), target, TestData.Today).Score;

    [Fact]
    public void A_brand_new_listing_exactly_on_budget_scores_a_perfect_hundred() =>
        Assert.Equal(100m, Score(500_000m, "2026-10-09", Target));

    [Fact]
    public void Recency_halves_every_half_life()
    {
        Assert.Equal(100m, Score(400_000m, "2026-10-09"));
        Assert.Equal(50m, Score(400_000m, "2026-09-09"));   // 30 days old
        Assert.Equal(25m, Score(400_000m, "2026-08-10"));   // 60 days old
    }

    [Fact]
    public void Without_a_budget_the_price_is_irrelevant_to_the_score() =>
        Assert.Equal(Score(100_000m, "2026-10-01"), Score(900_000m, "2026-10-01"));

    [Fact]
    public void Closer_to_budget_scores_higher_at_equal_age()
    {
        var onBudget = Score(500_000m, "2026-10-01", Target);
        var near = Score(480_000m, "2026-10-01", Target);
        var far = Score(350_000m, "2026-10-01", Target);

        Assert.True(onBudget > near);
        Assert.True(near > far);
    }

    [Fact]
    public void Over_budget_is_penalised_more_than_the_same_gap_under_budget()
    {
        var under = Score(450_000m, "2026-10-01", Target);
        var over = Score(550_000m, "2026-10-01", Target);

        Assert.True(under > over);
    }

    [Fact]
    public void Budget_fit_is_linear_and_bottoms_out_at_zero()
    {
        Assert.Equal(1d, Scorer.BudgetFit(500_000m, 500_000m), 6);
        Assert.Equal(0.9, Scorer.BudgetFit(450_000m, 500_000m), 6);   // 10% under -> -0.1
        Assert.Equal(0.8, Scorer.BudgetFit(550_000m, 500_000m), 6);   // 10% over, doubled -> -0.2
        Assert.Equal(0d, Scorer.BudgetFit(750_000m, 500_000m), 6);    // 50% over: 0
        Assert.Equal(0d, Scorer.BudgetFit(5_000_000m, 500_000m), 6);  // never negative
        Assert.Equal(0d, Scorer.BudgetFit(1m, 500_000m), 4);          // 99.9998% under: effectively no fit
    }

    [Theory]
    [InlineData("1000000", "0.0000000000000000000000000001")]                  // ratio far beyond decimal range
    [InlineData("79228162514264337593543950335", "0.0000001")]
    [InlineData("0.0000000000000000000000000001", "79228162514264337593543950335")]
    public void Budget_fit_survives_extreme_price_to_budget_ratios(string price, string target)
    {
        var fit = Scorer.BudgetFit(decimal.Parse(price), decimal.Parse(target));

        Assert.Equal(0d, fit);
    }

    [Fact]
    public void Recency_beats_a_small_price_gap_but_not_a_large_one()
    {
        var freshAndSlightlyOff = Score(510_000m, "2026-10-08", Target);
        var staleAndExact = Score(500_000m, "2026-06-01", Target);
        var freshAndWayOff = Score(900_000m, "2026-10-08", Target);

        Assert.True(freshAndSlightlyOff > staleAndExact);
        Assert.True(staleAndExact > freshAndWayOff);
    }

    [Fact]
    public void A_listing_dated_in_the_future_counts_as_brand_new() =>
        Assert.Equal(100m, Score(400_000m, "2027-01-01"));

    [Fact]
    public void Weights_are_respected_and_renormalised()
    {
        // Budget-only policy: age must not matter at all.
        var budgetOnly = new RelevanceScorer(new ScoringPolicy(budgetWeight: 1, recencyWeight: 0));

        var oldButOnBudget = budgetOnly.Score(TestData.Listing(price: 500_000m, listedOn: "2020-01-01"), Target, TestData.Today);

        Assert.Equal(100m, oldButOnBudget.Score);
    }
}

public class ListingRankerTests
{
    private static readonly ListingRanker Ranker = new(new RelevanceScorer(ScoringPolicy.Default));

    private static string[] Ids(IEnumerable<RankedListing> ranked) =>
        ranked.Select(r => $"{r.Listing.Key.Source}/{r.Listing.Key.Id}").ToArray();

    [Fact]
    public void Best_score_comes_first()
    {
        var target = TargetBudget.FromUserInput(400_000m).Value;
        var listings = new[]
        {
            TestData.Listing(id: "far", price: 800_000m),
            TestData.Listing(id: "exact", price: 400_000m),
            TestData.Listing(id: "near", price: 420_000m),
        };

        Assert.Equal(["MLS_A/exact", "MLS_A/near", "MLS_A/far"], Ids(Ranker.Rank(listings, target, TestData.Today)));
    }

    [Fact]
    public void Without_a_budget_the_newest_listing_wins()
    {
        var listings = new[]
        {
            TestData.Listing(id: "old", listedOn: "2026-08-01"),
            TestData.Listing(id: "new", listedOn: "2026-10-05"),
            TestData.Listing(id: "mid", listedOn: "2026-09-10"),
        };

        Assert.Equal(["MLS_A/new", "MLS_A/mid", "MLS_A/old"], Ids(Ranker.Rank(listings, null, TestData.Today)));
    }

    [Fact]
    public void Tied_scores_fall_back_to_newer_then_cheaper_then_source_then_id()
    {
        // Same listing date => same recency score; no budget => price is irrelevant to the score => a genuine tie.
        var listings = new[]
        {
            TestData.Listing(id: "2", source: "MLS_B", price: 300_000m),
            TestData.Listing(id: "1", source: "MLS_B", price: 300_000m),
            TestData.Listing(id: "9", source: "MLS_A", price: 300_000m),
            TestData.Listing(id: "5", source: "MLS_A", price: 200_000m),
        };

        var ranked = Ranker.Rank(listings, null, TestData.Today);

        Assert.Single(ranked.Select(r => r.Relevance.Score).Distinct());
        Assert.Equal(["MLS_A/5", "MLS_A/9", "MLS_B/1", "MLS_B/2"], Ids(ranked));
    }

    [Fact]
    public void Tied_scores_prefer_the_newer_listing()
    {
        // Recency would normally separate these two, so use a budget-only policy to force a true tie on score.
        var budgetOnly = new ListingRanker(new RelevanceScorer(new ScoringPolicy(budgetWeight: 1, recencyWeight: 0)));
        var target = TargetBudget.FromUserInput(400_000m).Value;
        var listings = new[]
        {
            TestData.Listing(id: "older", price: 400_000m, listedOn: "2026-09-01"),
            TestData.Listing(id: "newer", price: 400_000m, listedOn: "2026-10-01"),
        };

        Assert.Equal(["MLS_A/newer", "MLS_A/older"], Ids(budgetOnly.Rank(listings, target, TestData.Today)));
    }

    [Fact]
    public void Order_does_not_depend_on_input_order()
    {
        var listings = Enumerable.Range(1, 8)
            .Select(n => TestData.Listing(id: n.ToString(), price: 300_000m + (n % 3) * 10_000m, listedOn: $"2026-09-{10 + (n % 4):00}"))
            .ToList();
        var target = TargetBudget.FromUserInput(310_000m).Value;

        var forward = Ids(Ranker.Rank(listings, target, TestData.Today));
        var backward = Ids(Ranker.Rank(Enumerable.Reverse(listings), target, TestData.Today));

        Assert.Equal(forward, backward);
    }

    [Fact]
    public void Ranking_nothing_returns_nothing() =>
        Assert.Empty(Ranker.Rank([], null, TestData.Today));
}
