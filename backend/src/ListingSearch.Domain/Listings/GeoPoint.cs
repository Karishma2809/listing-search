using ListingSearch.Domain.Common;

namespace ListingSearch.Domain.Listings;

/// <summary>A WGS84 coordinate. Not used by today's filters; kept so a location-based search can build on it.</summary>
public sealed record GeoPoint
{
    public GeoPoint(double latitude, double longitude)
    {
        Guard.InRange(latitude, -90, 90, nameof(latitude));
        Guard.InRange(longitude, -180, 180, nameof(longitude));
        Latitude = latitude;
        Longitude = longitude;
    }

    public double Latitude { get; }

    public double Longitude { get; }
}
