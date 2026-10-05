namespace Opdeweg.Domain.ValueObjects;

/// <summary>Spherical-earth geodesy helpers. Accurate to well under 0.5% for the sub-10km distances we care about.</summary>
public static class GeoMath
{
    /// <summary>IUGG mean Earth radius.</summary>
    public const double EarthRadiusMeters = 6_371_008.8;

    public static double DistanceMeters(GeoPoint a, GeoPoint b)
    {
        var lat1 = ToRadians(a.Latitude);
        var lat2 = ToRadians(b.Latitude);
        var dLat = lat2 - lat1;
        var dLon = ToRadians(b.Longitude - a.Longitude);

        var h = (Math.Sin(dLat / 2) * Math.Sin(dLat / 2)) +
                (Math.Cos(lat1) * Math.Cos(lat2) * Math.Sin(dLon / 2) * Math.Sin(dLon / 2));

        return 2 * EarthRadiusMeters * Math.Asin(Math.Min(1, Math.Sqrt(h)));
    }

    /// <summary>The point reached by travelling <paramref name="distanceMeters"/> from <paramref name="origin"/> on an initial bearing.</summary>
    public static GeoPoint Destination(GeoPoint origin, double bearingDegrees, double distanceMeters)
    {
        var angular = distanceMeters / EarthRadiusMeters;
        var bearing = ToRadians(bearingDegrees);
        var lat1 = ToRadians(origin.Latitude);
        var lon1 = ToRadians(origin.Longitude);

        var lat2 = Math.Asin((Math.Sin(lat1) * Math.Cos(angular)) + (Math.Cos(lat1) * Math.Sin(angular) * Math.Cos(bearing)));
        var lon2 = lon1 + Math.Atan2(
            Math.Sin(bearing) * Math.Sin(angular) * Math.Cos(lat1),
            Math.Cos(angular) - (Math.Sin(lat1) * Math.Sin(lat2)));

        var longitude = ((ToDegrees(lon2) + 540) % 360) - 180;
        return new GeoPoint(ToDegrees(lat2), longitude);
    }

    /// <summary>Smallest absolute difference between two compass headings, in [0, 180].</summary>
    public static double HeadingDifferenceDegrees(double a, double b)
    {
        var diff = Math.Abs(a - b) % 360;
        return diff > 180 ? 360 - diff : diff;
    }

    /// <summary>Snaps a point to the south-west corner of a grid cell, discarding precision for privacy.</summary>
    public static GeoPoint SnapToGrid(GeoPoint point, double gridDegrees)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(gridDegrees);

        var lat = Math.Clamp(Math.Floor(point.Latitude / gridDegrees) * gridDegrees, -90, 90);
        var lon = Math.Clamp(Math.Floor(point.Longitude / gridDegrees) * gridDegrees, -180, 180);
        return new GeoPoint(Math.Round(lat, 6), Math.Round(lon, 6));
    }

    private static double ToRadians(double degrees) => degrees * Math.PI / 180;

    private static double ToDegrees(double radians) => radians * 180 / Math.PI;
}
