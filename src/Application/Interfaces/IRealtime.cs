using Opdeweg.Application.DTOs;
using Opdeweg.Application.Features.Proximity;

namespace Opdeweg.Application.Interfaces;

/// <summary>Pushes server-authoritative events to a user's connected devices (SignalR in production).</summary>
public interface IRealtimeNotifier
{
    Task ProximityGroupJoinedAsync(Guid userId, ProximityGroupDto group, CancellationToken cancellationToken);

    Task ProximityGroupLeftAsync(Guid userId, ProximityGroupLeftDto left, CancellationToken cancellationToken);

    Task NearbyUsersChangedAsync(Guid userId, ProximityGroupDto group, CancellationToken cancellationToken);

    Task DrivingSessionStartedAsync(Guid userId, DrivingSessionDto session, CancellationToken cancellationToken);

    Task DrivingSessionEndedAsync(Guid userId, DrivingSessionEndedDto ended, CancellationToken cancellationToken);
}

/// <summary>
/// Queue of proximity work. In-process today; the boundary allows moving proximity evaluation
/// to a dedicated, region-partitioned service (e.g. behind Redis Streams) without touching callers.
/// </summary>
public interface IProximityCommandQueue
{
    bool TryEnqueue(ProximityCommand command);

    /// <summary>Enqueues and waits until processed. Returns false on timeout (the work still runs).</summary>
    Task<bool> EnqueueAndWaitAsync(ProximityCommand command, TimeSpan timeout, CancellationToken cancellationToken);
}
