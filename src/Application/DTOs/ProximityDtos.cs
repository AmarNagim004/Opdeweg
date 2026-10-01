using Opdeweg.Application.Features.Proximity;

namespace Opdeweg.Application.DTOs;

/// <summary>
/// What another driver learns about you: a per-session pseudonym, a display name (or "Driver"),
/// and a rounded distance. Never coordinates, never internal IDs.
/// </summary>
public sealed record NearbyDriverDto(
    string Id,
    string DisplayName,
    string? AvatarUrl,
    int? ApproxDistanceMeters,
    bool InVoiceGroup);

public sealed record VoiceAccessDto(string Url, string Token, string Room, string Identity, DateTimeOffset ExpiresAt);

public sealed record ProximityGroupDto(string GroupId, IReadOnlyList<NearbyDriverDto> Members, VoiceAccessDto? Voice);

public sealed record ProximityGroupLeftDto(string GroupId, ProximityChangeReason Reason);

public sealed record ProximitySnapshotDto(
    bool SessionActive,
    ProximityGroupDto? Group,
    IReadOnlyList<NearbyDriverDto> Nearby,
    int NearbyCount,
    DateTimeOffset GeneratedAt)
{
    public static ProximitySnapshotDto Inactive(DateTimeOffset now) => new(false, null, [], 0, now);
}

public sealed record ClientConfigDto(ProximityConfigDto Proximity, LocationConfigDto Location);

public sealed record ProximityConfigDto(double JoinDistanceMeters, double LeaveDistanceMeters, int MaxGroupSize);

public sealed record LocationConfigDto(
    double DistanceThresholdMeters,
    int MaxIntervalSeconds,
    int MinIntervalMilliseconds,
    double HeadingChangeDegrees,
    double SpeedChangeMetersPerSecond);
