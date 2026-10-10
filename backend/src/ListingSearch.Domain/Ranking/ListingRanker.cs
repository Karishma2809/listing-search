using ListingSearch.Domain.Listings;

namespace ListingSearch.Domain.Ranking;

public sealed record RankedListing(Listing Listing, Relevance Relevance);

/// <summary>
/// Orders listings best-first. Ordering is total and deterministic, so paging is stable across requests:
///   1. higher relevance
///   2. newer listing  (a tie on score usually means similar fit; fresher inventory wins)
///   3. lower price
///   4. source, then id (ordinal) - arbitrary but fixed, so no two listings ever compare equal.
/// </summary>
public sealed class ListingRanker(RelevanceScorer scorer)
{
    public IReadOnlyList<RankedListing> Rank(IEnumerable<Listing> listings, TargetBudget? target, DateOnly today) =>
        listings
            .Select(listing => new RankedListing(listing, scorer.Score(listing, target, today)))
            .OrderByDescending(ranked => ranked.Relevance.Score)
            .ThenByDescending(ranked => ranked.Listing.ListedOn)
            .ThenBy(ranked => ranked.Listing.Price.Amount)
            .ThenBy(ranked => ranked.Listing.Key.Source, StringComparer.Ordinal)
            .ThenBy(ranked => ranked.Listing.Key.Id, StringComparer.Ordinal)
            .ToList();
}
