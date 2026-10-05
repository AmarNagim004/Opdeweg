using Opdeweg.Domain.ValueObjects;

namespace Opdeweg.UnitTests.TestSupport;

internal static class Geo
{
    /// <summary>Amsterdam Centraal-ish; any mid-latitude origin works.</summary>
    public static readonly GeoPoint Origin = new(52.3791, 4.9003);

    /// <summary>A point exactly <paramref name="meters"/> east of <see cref="Origin"/> (great-circle).</summary>
    public static GeoPoint East(double meters) => GeoMath.Destination(Origin, 90, meters);

    public static GeoPoint North(double meters) => GeoMath.Destination(Origin, 0, meters);

    public static GeoPoint From(GeoPoint origin, double bearing, double meters) => GeoMath.Destination(origin, bearing, meters);
}
