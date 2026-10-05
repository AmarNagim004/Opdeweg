using Opdeweg.Domain.ValueObjects;
using Opdeweg.UnitTests.TestSupport;

namespace Opdeweg.UnitTests.Geometry;

public sealed class GeoMathTests
{
    [Theory]
    [InlineData(0, 999)]
    [InlineData(45, 1000)]
    [InlineData(90, 1050)]
    [InlineData(200, 1101)]
    public void Destination_and_distance_round_trip(double bearing, double meters)
    {
        var destination = GeoMath.Destination(TestSupport.Geo.Origin, bearing, meters);

        Assert.Equal(meters, GeoMath.DistanceMeters(TestSupport.Geo.Origin, destination), precision: 6);
    }

    [Fact]
    public void Known_distance_amsterdam_to_utrecht_is_about_35_km()
    {
        var amsterdam = new GeoPoint(52.3676, 4.9041);
        var utrecht = new GeoPoint(52.0907, 5.1214);

        Assert.InRange(GeoMath.DistanceMeters(amsterdam, utrecht), 34_000, 35_500);
    }

    [Theory]
    [InlineData(10, 350, 20)]
    [InlineData(0, 180, 180)]
    [InlineData(90, 90, 0)]
    [InlineData(359, 1, 2)]
    public void Heading_difference_wraps_around(double a, double b, double expected) =>
        Assert.Equal(expected, GeoMath.HeadingDifferenceDegrees(a, b), precision: 9);

    [Fact]
    public void Snap_to_grid_discards_precision()
    {
        var snapped = GeoMath.SnapToGrid(new GeoPoint(52.37912, 4.90031), 0.05);

        Assert.Equal(52.35, snapped.Latitude, precision: 9);
        Assert.Equal(4.90, snapped.Longitude, precision: 9);
    }

    [Fact]
    public void Coordinates_are_redacted_from_string_output() =>
        Assert.DoesNotContain("52", new GeoPoint(52.3791, 4.9003).ToString(), StringComparison.Ordinal);

    [Theory]
    [InlineData(91, 0)]
    [InlineData(-91, 0)]
    [InlineData(0, 181)]
    [InlineData(double.NaN, 0)]
    [InlineData(0, double.PositiveInfinity)]
    public void Invalid_coordinates_are_rejected(double lat, double lon)
    {
        Assert.False(GeoPoint.IsValid(lat, lon));
        Assert.Throws<ArgumentOutOfRangeException>(() => new GeoPoint(lat, lon));
    }
}
