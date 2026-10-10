using ListingSearch.Domain.Common;

namespace ListingSearch.Domain.Listings;

/// <summary>A list price in US dollars.</summary>
public sealed record Price
{
    public Price(decimal amount)
    {
        if (amount <= 0)
        {
            throw new DomainException($"price must be greater than zero (was {amount}).");
        }

        Amount = amount;
    }

    public decimal Amount { get; }

    // Invariant formatting on purpose: the app runs with InvariantGlobalization, where named cultures such as en-US do not exist.
    public override string ToString() =>
        Amount % 1 == 0 ? $"${Amount:N0}" : $"${Amount:N2}";
}
