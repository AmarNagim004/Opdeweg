using Microsoft.Extensions.Options;
using Opdeweg.Application.Common;
using Opdeweg.Application.DTOs;
using Opdeweg.Application.Interfaces;
using Opdeweg.Application.Options;
using Opdeweg.Domain.ValueObjects;

namespace Opdeweg.Application.Features.Location;

public enum LocationVerdictOutcome
{
    Accepted,

    /// <summary>Valid but too imprecise to move the driver: counts as a liveness heartbeat only.</summary>
    LowAccuracy,

    /// <summary>Arrived sooner than the minimum update interval; silently dropped.</summary>
    Throttled,

    Rejected,
}

public sealed record LocationVerdict(LocationVerdictOutcome Outcome, GeoPoint? Point = null, string? Reason = null, double? ImpliedSpeedMetersPerSecond = null)
{
    public AppException ToException() => Reason switch
    {
        LocationRejectionReasons.ImplausibleMovement or LocationRejectionReasons.OutOfOrder =>
            AppException.Unprocessable(Reason, "The location update was rejected."),
        _ => AppException.Validation(Reason ?? "invalid_location", "The location update is invalid."),
    };
}

public static class LocationRejectionReasons
{
    public const string InvalidCoordinates = "invalid_coordinates";
    public const string InvalidAccuracy = "invalid_accuracy";
    public const string InvalidSpeed = "invalid_speed";
    public const string InvalidHeading = "invalid_heading";
    public const string MissingTimestamp = "missing_timestamp";
    public const string StaleTimestamp = "stale_timestamp";
    public const string FutureTimestamp = "future_timestamp";
    public const string OutOfOrder = "out_of_order";
    public const string ImplausibleMovement = "implausible_movement";
}

/// <summary>
/// Pure server-side validation of client location updates. Nothing from the client is trusted:
/// coordinates, timestamps, speed and implied movement are all checked, and the update rate is bounded.
/// </summary>
public sealed class LocationUpdateValidator(IOptions<LocationOptions> locationOptions, IOptions<ProximityOptions> proximityOptions)
{
    private readonly LocationOptions _location = locationOptions.Value;
    private readonly ProximityOptions _proximity = proximityOptions.Value;

    public LocationVerdict Validate(LocationUpdateRequest request, StoredPosition? previous, DateTimeOffset now)
    {
        if (request.Latitude is not { } lat || request.Longitude is not { } lon ||
            !GeoPoint.IsValid(lat, lon) || (lat == 0 && lon == 0))
        {
            return Reject(LocationRejectionReasons.InvalidCoordinates);
        }

        if (request.Accuracy is { } accuracy && (!double.IsFinite(accuracy) || accuracy < 0 || accuracy > _location.MaxReportedAccuracyMeters))
        {
            return Reject(LocationRejectionReasons.InvalidAccuracy);
        }

        if (request.Speed is { } speed && (!double.IsFinite(speed) || speed < 0 || speed > _location.MaxPlausibleSpeedMetersPerSecond * 1.5))
        {
            return Reject(LocationRejectionReasons.InvalidSpeed);
        }

        if (request.Heading is { } heading && (!double.IsFinite(heading) || heading < 0 || heading > 360))
        {
            return Reject(LocationRejectionReasons.InvalidHeading);
        }

        if (request.Timestamp is not { } timestamp)
        {
            return Reject(LocationRejectionReasons.MissingTimestamp);
        }

        if (now - timestamp > TimeSpan.FromSeconds(_location.MaxTimestampAgeSeconds))
        {
            return Reject(LocationRejectionReasons.StaleTimestamp);
        }

        if (timestamp - now > TimeSpan.FromSeconds(_location.MaxTimestampSkewSeconds))
        {
            return Reject(LocationRejectionReasons.FutureTimestamp);
        }

        var point = new GeoPoint(lat, lon);

        if (previous is not null)
        {
            if (now - previous.LastSeenAt < TimeSpan.FromMilliseconds(_location.MinUpdateIntervalMilliseconds))
            {
                return new LocationVerdict(LocationVerdictOutcome.Throttled);
            }

            if (timestamp <= previous.ClientTimestamp)
            {
                return Reject(LocationRejectionReasons.OutOfOrder);
            }
        }

        if (request.Accuracy is { } fixAccuracy && fixAccuracy > _proximity.MaxUsableAccuracyMeters)
        {
            return new LocationVerdict(LocationVerdictOutcome.LowAccuracy, point);
        }

        if (previous is not null)
        {
            // Server receive times are trusted; client timestamps are not. One second floor absorbs
            // network jitter that bunches two genuinely spaced updates together.
            var elapsed = Math.Max(1, (now - previous.PositionAt).TotalSeconds);
            var distance = GeoMath.DistanceMeters(previous.Point, point);
            var tolerance = _location.MovementToleranceMeters + (previous.AccuracyMeters ?? 0) + (request.Accuracy ?? 0);
            var allowed = (_location.MaxPlausibleSpeedMetersPerSecond * elapsed) + tolerance;
            if (distance > allowed)
            {
                return Reject(LocationRejectionReasons.ImplausibleMovement, distance / elapsed);
            }
        }

        return new LocationVerdict(LocationVerdictOutcome.Accepted, point);
    }

    private static LocationVerdict Reject(string reason, double? impliedSpeed = null) =>
        new(LocationVerdictOutcome.Rejected, Reason: reason, ImpliedSpeedMetersPerSecond: impliedSpeed);
}
