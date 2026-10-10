using ListingSearch.Domain.Common;

namespace ListingSearch.Domain.Listings;

/// <summary>
/// Aggregate root: one property listing as published by one feed. Identity is its <see cref="ListingKey"/>,
/// so two listings with the same details from different feeds are distinct entities (cross-feed
/// de-duplication is a separate concern, deliberately not modelled here).
/// </summary>
public sealed class Listing : IEquatable<Listing>
{
    public Listing(
        ListingKey key,
        Address address,
        Price price,
        int bedrooms,
        decimal bathrooms,
        int squareFeet,
        GeoPoint location,
        DateOnly listedOn,
        ListingStatus status,
        ListingDescription description)
    {
        if (!Enum.IsDefined(status))
        {
            throw new DomainException($"status '{status}' is not a known listing status.");
        }

        if (bedrooms < 0)
        {
            throw new DomainException($"bedrooms must not be negative (was {bedrooms}).");
        }

        if (bathrooms < 0)
        {
            throw new DomainException($"bathrooms must not be negative (was {bathrooms}).");
        }

        if (squareFeet <= 0)
        {
            throw new DomainException($"sqft must be greater than zero (was {squareFeet}).");
        }

        Key = key ?? throw new DomainException("key must be provided.");
        Address = address ?? throw new DomainException("address must be provided.");
        Price = price ?? throw new DomainException("price must be provided.");
        Location = location ?? throw new DomainException("location must be provided.");
        Description = description ?? throw new DomainException("description must be provided.");
        Bedrooms = bedrooms;
        Bathrooms = bathrooms;
        SquareFeet = squareFeet;
        ListedOn = listedOn;
        Status = status;
    }

    public ListingKey Key { get; }

    public Address Address { get; }

    public Price Price { get; }

    public int Bedrooms { get; }

    public decimal Bathrooms { get; }

    public int SquareFeet { get; }

    public GeoPoint Location { get; }

    public DateOnly ListedOn { get; }

    public ListingStatus Status { get; }

    public ListingDescription Description { get; }

    public bool Equals(Listing? other) => other is not null && Key.Equals(other.Key);

    public override bool Equals(object? obj) => Equals(obj as Listing);

    public override int GetHashCode() => Key.GetHashCode();

    public override string ToString() => $"{Key} {Address}";
}
