using ListingSearch.Domain.Common;

namespace ListingSearch.Domain.Listings;

/// <summary>
/// Identity of a listing. A feed's <c>id</c> is only unique within that feed ("A1" from MLS_A and
/// "A1" from MLS_B would be different listings), so identity is the (source, id) pair.
/// </summary>
public sealed record ListingKey
{
    public ListingKey(string source, string id)
    {
        Source = Guard.NotBlank(source, nameof(source));
        Id = Guard.NotBlank(id, nameof(id));
    }

    public string Source { get; }

    public string Id { get; }

    public override string ToString() => $"{Source}/{Id}";
}
