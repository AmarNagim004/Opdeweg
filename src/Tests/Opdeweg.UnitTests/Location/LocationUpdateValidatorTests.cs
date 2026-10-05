using Opdeweg.Application.DTOs;
using Opdeweg.Application.Features.Location;
using Opdeweg.Application.Interfaces;
using Opdeweg.UnitTests.TestSupport;

namespace Opdeweg.UnitTests.Location;

public sealed class LocationUpdateValidatorTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 1, 12, 0, 0, TimeSpan.Zero);
    private readonly LocationUpdateValidator _validator = new(TestOptions.Location(), TestOptions.Proximity());

    [Fact]
    public void A_sane_first_fix_is_accepted()
    {
        var verdict = _validator.Validate(Request(TestSupport.Geo.Origin.Latitude, TestSupport.Geo.Origin.Longitude), null, Now);

        Assert.Equal(LocationVerdictOutcome.Accepted, verdict.Outcome);
        Assert.NotNull(verdict.Point);
    }

    [Theory]
    [InlineData(91, 4)]
    [InlineData(-90.0001, 4)]
    [InlineData(52, 180.5)]
    [InlineData(0, 0)]
    [InlineData(double.NaN, 4)]
    public void Impossible_coordinates_are_rejected(double lat, double lon) =>
        AssertRejected(_validator.Validate(Request(lat, lon), null, Now), LocationRejectionReasons.InvalidCoordinates);

    [Fact]
    public void Missing_coordinates_are_rejected() =>
        AssertRejected(_validator.Validate(new LocationUpdateRequest(null, 4.9, null, null, 5, Now), null, Now), LocationRejectionReasons.InvalidCoordinates);

    [Fact]
    public void Stale_client_timestamps_are_rejected() =>
        AssertRejected(_validator.Validate(Request(52.37, 4.9, timestamp: Now.AddMinutes(-2)), null, Now), LocationRejectionReasons.StaleTimestamp);

    [Fact]
    public void Future_client_timestamps_are_rejected() =>
        AssertRejected(_validator.Validate(Request(52.37, 4.9, timestamp: Now.AddMinutes(5)), null, Now), LocationRejectionReasons.FutureTimestamp);

    [Fact]
    public void Small_clock_skew_is_tolerated() =>
        Assert.Equal(LocationVerdictOutcome.Accepted, _validator.Validate(Request(52.37, 4.9, timestamp: Now.AddSeconds(10)), null, Now).Outcome);

    [Theory]
    [InlineData(-1)]
    [InlineData(500)]
    public void Impossible_reported_speeds_are_rejected(double speed) =>
        AssertRejected(_validator.Validate(Request(52.37, 4.9, speed: speed), null, Now), LocationRejectionReasons.InvalidSpeed);

    [Fact]
    public void Updates_faster_than_the_minimum_interval_are_throttled()
    {
        var previous = Previous(TestSupport.Geo.Origin, Now.AddMilliseconds(-400));

        var verdict = _validator.Validate(Request(TestSupport.Geo.East(5)), previous, Now);

        Assert.Equal(LocationVerdictOutcome.Throttled, verdict.Outcome);
    }

    [Fact]
    public void Replayed_or_out_of_order_fixes_are_rejected()
    {
        var previous = Previous(TestSupport.Geo.Origin, Now.AddSeconds(-5));

        AssertRejected(_validator.Validate(Request(TestSupport.Geo.East(20), timestamp: Now.AddSeconds(-10)), previous, Now), LocationRejectionReasons.OutOfOrder);
    }

    [Fact]
    public void Low_accuracy_fixes_only_count_as_heartbeats()
    {
        var verdict = _validator.Validate(Request(TestSupport.Geo.Origin, accuracy: 400), null, Now);

        Assert.Equal(LocationVerdictOutcome.LowAccuracy, verdict.Outcome);
    }

    [Fact]
    public void Teleporting_is_rejected_as_implausible_movement()
    {
        var previous = Previous(TestSupport.Geo.Origin, Now.AddSeconds(-5));

        var verdict = _validator.Validate(Request(TestSupport.Geo.East(10_000)), previous, Now);

        AssertRejected(verdict, LocationRejectionReasons.ImplausibleMovement);
        Assert.True(verdict.ImpliedSpeedMetersPerSecond > 1000);
    }

    [Fact]
    public void Highway_speeds_are_plausible()
    {
        // 40 m/s (144 km/h) for 15 s.
        var previous = Previous(TestSupport.Geo.Origin, Now.AddSeconds(-15));

        Assert.Equal(LocationVerdictOutcome.Accepted, _validator.Validate(Request(TestSupport.Geo.East(600), speed: 40), previous, Now).Outcome);
    }

    [Fact]
    public void Large_jumps_after_a_long_gap_are_accepted()
    {
        var previous = Previous(TestSupport.Geo.Origin, Now.AddMinutes(-20));

        Assert.Equal(LocationVerdictOutcome.Accepted, _validator.Validate(Request(TestSupport.Geo.East(20_000)), previous, Now).Outcome);
    }

    private static LocationUpdateRequest Request(Domain.ValueObjects.GeoPoint point, double? speed = 10, double? accuracy = 8, DateTimeOffset? timestamp = null) =>
        Request(point.Latitude, point.Longitude, speed, accuracy, timestamp);

    private static LocationUpdateRequest Request(double lat, double lon, double? speed = 10, double? accuracy = 8, DateTimeOffset? timestamp = null) =>
        new(lat, lon, speed, 90, accuracy, timestamp ?? Now.AddMilliseconds(-200));

    private static StoredPosition Previous(Domain.ValueObjects.GeoPoint point, DateTimeOffset at) =>
        new(point, 8, 10, 90, at.AddMilliseconds(-200), at, at);

    private static void AssertRejected(LocationVerdict verdict, string reason)
    {
        Assert.Equal(LocationVerdictOutcome.Rejected, verdict.Outcome);
        Assert.Equal(reason, verdict.Reason);
    }
}
