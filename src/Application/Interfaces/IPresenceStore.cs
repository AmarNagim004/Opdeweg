using Opdeweg.Application.Features.Proximity;
using Opdeweg.Domain.ValueObjects;

namespace Opdeweg.Application.Interfaces;

/// <summary>The realtime projection of an active driving session, kept in the presence store.</summary>
public sealed record ActiveSession(
    Guid SessionId,
    Guid UserId,
    string Handle,
    string PublicName,
    string? PublicAvatarUrl,
    DateTimeOffset StartedAt,
    string? GroupId,
    bool CoarseAreaRecorded);

/// <summary>
/// The single most recent usable position of a driver. Only the latest fix is ever kept;
/// it expires automatically shortly after the driver goes quiet.
/// </summary>
public sealed record StoredPosition(
    GeoPoint Point,
    double? AccuracyMeters,
    double? SpeedMetersPerSecond,
    double? HeadingDegrees,
    DateTimeOffset ClientTimestamp,
    DateTimeOffset PositionAt,
    DateTimeOffset LastSeenAt);

public sealed record PresenceTimeouts(TimeSpan PositionTtl, TimeSpan SessionTtl);

/// <summary>
/// Realtime presence, spatial index and proximity-group membership (Redis in production).
/// Group membership is only mutated by the proximity processor, which serialises writers.
/// </summary>
public interface IPresenceStore
{
    Task SaveSessionAsync(ActiveSession session, PresenceTimeouts timeouts, DateTimeOffset seenAt, CancellationToken cancellationToken);

    Task<ActiveSession?> GetSessionAsync(Guid userId, CancellationToken cancellationToken);

    Task<IReadOnlyDictionary<Guid, ActiveSession>> GetSessionsAsync(IReadOnlyCollection<Guid> userIds, CancellationToken cancellationToken);

    Task UpdateSessionIdentityAsync(Guid userId, string publicName, string? publicAvatarUrl, CancellationToken cancellationToken);

    Task MarkCoarseAreaRecordedAsync(Guid userId, CancellationToken cancellationToken);

    Task<StoredPosition?> GetPositionAsync(Guid userId, CancellationToken cancellationToken);

    Task<IReadOnlyDictionary<Guid, StoredPosition>> GetPositionsAsync(IReadOnlyCollection<Guid> userIds, CancellationToken cancellationToken);

    /// <summary>Replaces the driver's latest position and spatial-index entry and refreshes liveness.</summary>
    Task SavePositionAsync(Guid userId, StoredPosition position, PresenceTimeouts timeouts, CancellationToken cancellationToken);

    /// <summary>Refreshes liveness without changing the stored position (e.g. a low-accuracy fix).</summary>
    Task TouchAsync(Guid userId, DateTimeOffset seenAt, PresenceTimeouts timeouts, CancellationToken cancellationToken);

    /// <summary>Spatial-index lookup: nearest drivers within a radius (approximate; callers re-check exact distance).</summary>
    Task<IReadOnlyList<Guid>> FindNearbyAsync(GeoPoint center, double radiusMeters, int limit, CancellationToken cancellationToken);

    Task<IReadOnlyList<Guid>> GetGroupMembersAsync(string groupId, CancellationToken cancellationToken);

    Task<IReadOnlyDictionary<string, IReadOnlyList<Guid>>> GetGroupRostersAsync(IReadOnlyCollection<string> groupIds, CancellationToken cancellationToken);

    /// <summary>Atomically applies an ordered list of group membership changes.</summary>
    Task ApplyMembershipChangesAsync(IReadOnlyList<GroupMembershipChange> changes, CancellationToken cancellationToken);

    /// <summary>Removes the driver's position and spatial-index entry (the session itself is kept).</summary>
    Task RemoveFromProximityAsync(Guid userId, CancellationToken cancellationToken);

    /// <summary>Removes every trace of the driver's realtime presence.</summary>
    Task RemoveSessionAsync(Guid userId, CancellationToken cancellationToken);

    /// <summary>Drivers whose last accepted update is older than <paramref name="seenBefore"/>.</summary>
    Task<IReadOnlyList<Guid>> GetDriversSeenBeforeAsync(DateTimeOffset seenBefore, bool onlyIndexed, int limit, CancellationToken cancellationToken);
}
