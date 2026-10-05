using Opdeweg.Domain.Enums;
using Opdeweg.Domain.ValueObjects;

namespace Opdeweg.Domain.Entities;

/// <summary>
/// An explicit period during which a user participates in proximity voice. No location trail is
/// persisted: only an optional, deliberately coarse start cell used for regional capacity planning,
/// which is purged after a retention period.
/// </summary>
public sealed class DrivingSession
{
    public DrivingSession(Guid id, Guid userId, string handle, DateTimeOffset startedAt)
    {
        Id = id;
        UserId = userId;
        Handle = handle;
        StartedAt = startedAt;
    }

    private DrivingSession()
    {
    }

    public Guid Id { get; private set; }

    public Guid UserId { get; private set; }

    /// <summary>
    /// Random, per-session pseudonym shown to other drivers and used as the voice identity, so
    /// nearby drivers cannot correlate a user across sessions or learn internal IDs.
    /// </summary>
    public string Handle { get; private set; } = string.Empty;

    public DateTimeOffset StartedAt { get; private set; }

    public DateTimeOffset? EndedAt { get; private set; }

    public DrivingSessionEndReason? EndReason { get; private set; }

    /// <summary>Grid-snapped (~5 km) start area, or null once purged / when disabled.</summary>
    public GeoPoint? CoarseStartArea { get; private set; }

    public bool IsActive => EndedAt is null;

    public bool End(DrivingSessionEndReason reason, DateTimeOffset now)
    {
        if (!IsActive)
        {
            return false;
        }

        EndedAt = now;
        EndReason = reason;
        return true;
    }

    public void RecordCoarseStartArea(GeoPoint coarseArea) => CoarseStartArea ??= coarseArea;
}
