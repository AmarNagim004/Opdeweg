using System.ComponentModel.DataAnnotations;
using Opdeweg.Domain.Enums;

namespace Opdeweg.Application.DTOs;

public sealed record DrivingSessionDto(
    Guid Id,
    string Handle,
    DateTimeOffset StartedAt,
    DateTimeOffset? EndedAt,
    DrivingSessionEndReason? EndReason);

public sealed record DrivingSessionEndedDto(Guid SessionId, DrivingSessionEndReason Reason, DateTimeOffset EndedAt);

/// <summary>
/// A single position fix from the client. Values are validated server-side; nothing about the
/// client's coordinates or timestamps is trusted blindly.
/// </summary>
public sealed record LocationUpdateRequest(
    [Required] double? Latitude,
    [Required] double? Longitude,
    double? Speed,
    double? Heading,
    double? Accuracy,
    [Required] DateTimeOffset? Timestamp);

public enum LocationUpdateStatus
{
    Accepted,
    LowAccuracy,
    Throttled,
}

public sealed record LocationUpdateResponse(LocationUpdateStatus Status, ProximitySnapshotDto? Proximity);
