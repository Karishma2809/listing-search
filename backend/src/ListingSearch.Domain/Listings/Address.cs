using ListingSearch.Domain.Common;

namespace ListingSearch.Domain.Listings;

/// <summary>
/// Postal address as published by a feed. The street line is kept verbatim (it is free text and
/// differs between feeds: "Main St, Apt 4B" vs "Main Street, Unit 4B"); only city, state and zip
/// are normalised so they can be compared.
/// </summary>
public sealed record Address
{
    public Address(string street, CityName city, string state, string zip)
    {
        Street = Guard.NotBlank(street, nameof(street));
        City = city ?? throw new DomainException("city must be provided.");
        State = (state ?? string.Empty).Trim().ToUpperInvariant();
        Zip = (zip ?? string.Empty).Trim();
    }

    public string Street { get; }

    public CityName City { get; }

    public string State { get; }

    public string Zip { get; }

    public override string ToString() => $"{Street}, {City}, {State} {Zip}".TrimEnd();
}
