namespace Opdeweg.Domain.ValueObjects;

/// <summary>
/// A WGS84 coordinate. Coordinates are sensitive personal data, so <see cref="ToString"/> is
/// deliberately redacted to keep them out of logs, exception messages and debugger dumps.
/// </summary>
public readonly record struct GeoPoint
{
    public GeoPoint(double latitude, double longitude)
    {
        if (!IsValid(latitude, longitude))
        {
            throw new ArgumentOutOfRangeException(nameof(latitude), "Coordinates are outside the WGS84 range.");
        }

        Latitude = latitude;
        Longitude = longitude;
    }

    public double Latitude { get; }

    public double Longitude { get; }

    public static bool IsValid(double latitude, double longitude) =>
        double.IsFinite(latitude) && double.IsFinite(longitude) &&
        latitude is >= -90 and <= 90 &&
        longitude is >= -180 and <= 180;

    public override string ToString() => "GeoPoint(redacted)";
}
