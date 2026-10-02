using System.Security.Cryptography;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Opdeweg.Application.Common;
using Opdeweg.Application.DTOs;
using Opdeweg.Application.Features.Proximity;
using Opdeweg.Application.Interfaces;
using Opdeweg.Application.Logging;
using Opdeweg.Application.Options;
using Opdeweg.Domain.Entities;
using Opdeweg.Domain.Enums;

namespace Opdeweg.Application.Features.Driving;

public static class PresenceTimeoutsFactory
{
    /// <summary>Positions expire shortly after going stale; sessions after the idle timeout.</summary>
    public static PresenceTimeouts Create(ProximityOptions proximity, DrivingSessionOptions driving) =>
        new(proximity.StaleAfter * 3, driving.IdleTimeout + TimeSpan.FromMinutes(5));
}

/// <summary>
/// Driving sessions gate everything location- and voice-related: only drivers with an active
/// session are tracked, indexed and grouped.
/// </summary>
public sealed class DrivingSessionService(
    IDrivingSessionRepository sessions,
    IUserRepository users,
    IUnitOfWork unitOfWork,
    IPresenceStore store,
    IProximityCommandQueue proximityQueue,
    IRealtimeNotifier realtime,
    IOptions<ProximityOptions> proximityOptions,
    IOptions<DrivingSessionOptions> drivingOptions,
    TimeProvider time,
    ILogger<DrivingSessionService> logger)
{
    /// <summary>What others see when a driver hides their name.</summary>
    public const string AnonymousName = "Rijder";
    private static readonly TimeSpan RemovalTimeout = TimeSpan.FromSeconds(3);

    public async Task<DrivingSessionDto?> GetCurrentAsync(Guid userId, CancellationToken cancellationToken)
    {
        var session = await sessions.GetActiveAsync(userId, cancellationToken);
        return session is null ? null : ToDto(session);
    }

    /// <summary>Starts a session, or resumes the active one (idempotent; also restores lost presence).</summary>
    public async Task<DrivingSessionDto> StartAsync(Guid userId, CancellationToken cancellationToken)
    {
        var now = time.GetUtcNow();
        var user = await users.GetByIdAsync(userId, cancellationToken)
            ?? throw AppException.Unauthorized("unknown_user", "Dit account bestaat niet meer.");

        var session = await sessions.GetActiveAsync(userId, cancellationToken);
        var isNew = session is null;
        if (session is null)
        {
            session = new DrivingSession(Guid.CreateVersion7(), userId, NewHandle(), now);
            sessions.Add(session);
            user.MarkActive(now);
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }

        var presence = await store.GetSessionAsync(userId, cancellationToken);
        if (presence?.SessionId != session.Id)
        {
            var active = new ActiveSession(
                session.Id,
                userId,
                session.Handle,
                PublicName(user),
                PublicAvatar(user),
                session.StartedAt,
                GroupId: null,
                CoarseAreaRecorded: session.CoarseStartArea is not null || !drivingOptions.Value.StoreCoarseStartArea);
            await store.SaveSessionAsync(active, PresenceTimeoutsFactory.Create(proximityOptions.Value, drivingOptions.Value), now, cancellationToken);
        }

        var dto = ToDto(session);
        if (isNew)
        {
            Log.DrivingSessionStarted(logger, session.Id, userId);
            await realtime.DrivingSessionStartedAsync(userId, dto, cancellationToken);
        }

        return dto;
    }

    /// <summary>Ends the active session and removes the driver from every proximity structure and voice room.</summary>
    public async Task EndAsync(Guid userId, DrivingSessionEndReason reason, CancellationToken cancellationToken)
    {
        var now = time.GetUtcNow();
        var session = await sessions.GetActiveAsync(userId, cancellationToken);
        if (session is not null && session.End(reason, now))
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }

        var processed = await proximityQueue.EnqueueAndWaitAsync(
            new RemoveDriverCommand(userId, ProximityChangeReason.SessionEnded, EndSession: true),
            RemovalTimeout,
            cancellationToken);

        if (!processed)
        {
            // The queued removal still runs; make sure no further location updates are accepted meanwhile.
            await store.RemoveSessionAsync(userId, cancellationToken);
        }

        if (session is not null)
        {
            Log.DrivingSessionEnded(logger, session.Id, userId, reason);
            await realtime.DrivingSessionEndedAsync(userId, new DrivingSessionEndedDto(session.Id, reason, now), cancellationToken);
        }
    }

    public static string PublicName(User user) => user.ShareDisplayName ? user.DisplayName : AnonymousName;

    public static string? PublicAvatar(User user) => user.ShareDisplayName ? user.AvatarUrl : null;

    private static DrivingSessionDto ToDto(DrivingSession session) =>
        new(session.Id, session.Handle, session.StartedAt, session.EndedAt, session.EndReason);

    private static string NewHandle() => "d" + Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(6));
}
