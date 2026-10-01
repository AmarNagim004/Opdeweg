using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Opdeweg.Api.Configuration;
using Opdeweg.Api.Extensions;
using Opdeweg.Application.DTOs;
using Opdeweg.Application.Features.Proximity;
using Opdeweg.Application.Features.Voice;
using Opdeweg.Application.Interfaces;

namespace Opdeweg.Api.Hubs;

/// <summary>
/// Server → client events. Deliberately small: the backend is authoritative, so clients only
/// learn about membership changes, never raw positions. Participant join/leave and speaking state
/// inside a voice room come from the SFU itself and are not duplicated here.
/// </summary>
public interface IProximityClient
{
    Task ProximityGroupJoined(ProximityGroupDto group);

    Task ProximityGroupLeft(ProximityGroupLeftDto left);

    Task NearbyUsersChanged(ProximityGroupDto group);

    Task DrivingSessionStarted(DrivingSessionDto session);

    Task DrivingSessionEnded(DrivingSessionEndedDto ended);
}

[Authorize]
public sealed partial class ProximityHub(
    ProximityQueryService query,
    VoiceAccessService voice,
    ILogger<ProximityHub> logger) : Hub<IProximityClient>
{
    public const string Path = "/hubs/proximity";

    public override Task OnConnectedAsync()
    {
        LogConnected(logger, Context.User!.GetUserId(), Context.ConnectionId);
        return base.OnConnectedAsync();
    }

    public override Task OnDisconnectedAsync(Exception? exception)
    {
        LogDisconnected(logger, Context.User?.GetUserIdOrNull(), Context.ConnectionId, exception?.GetType().Name ?? "closed");
        return base.OnDisconnectedAsync(exception);
    }

    /// <summary>Full state resync after (re)connecting, including a voice token if the driver is in a group.</summary>
    public Task<ProximitySnapshotDto> GetSnapshot() =>
        query.GetSnapshotAsync(Context.User!.GetUserId(), includeVoice: true, Context.ConnectionAborted);

    /// <summary>Fresh voice token for the caller's current group, or null if the server has not placed them in one.</summary>
    public Task<VoiceAccessDto?> RequestVoiceToken() =>
        voice.IssueForCurrentGroupAsync(Context.User!.GetUserId(), Context.ConnectionAborted);

    [LoggerMessage(Level = LogLevel.Information, Message = "User {UserId} connected to realtime hub ({ConnectionId})")]
    private static partial void LogConnected(ILogger logger, Guid userId, string connectionId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Realtime connection {ConnectionId} of user {UserId} lost: {Reason}")]
    private static partial void LogDisconnected(ILogger logger, Guid? userId, string connectionId, string reason);
}

/// <summary>Routes <c>Clients.User(id)</c> by the JWT <c>sub</c> claim (inbound claim mapping is disabled).</summary>
internal sealed class SubjectUserIdProvider : IUserIdProvider
{
    public string? GetUserId(HubConnectionContext connection) => connection.User?.GetUserIdOrNull()?.ToString();
}

/// <summary>Per-user token bucket for hub method invocations.</summary>
internal sealed class HubRateLimitFilter : IHubFilter, IDisposable
{
    private readonly PartitionedRateLimiter<string> _limiter;

    public HubRateLimitFilter(RateLimitingOptions options) =>
        _limiter = PartitionedRateLimiter.Create<string, string>(key => RateLimitPartition.GetTokenBucketLimiter(
            key,
            _ => new TokenBucketRateLimiterOptions
            {
                TokenLimit = options.HubInvocationsPerTenSeconds,
                TokensPerPeriod = options.HubInvocationsPerTenSeconds,
                ReplenishmentPeriod = TimeSpan.FromSeconds(10),
                QueueLimit = 0,
            }));

    public async ValueTask<object?> InvokeMethodAsync(HubInvocationContext invocationContext, Func<HubInvocationContext, ValueTask<object?>> next)
    {
        var key = invocationContext.Context.UserIdentifier ?? invocationContext.Context.ConnectionId;
        using var lease = _limiter.AttemptAcquire(key);
        if (!lease.IsAcquired)
        {
            throw new HubException("rate_limited");
        }

        return await next(invocationContext);
    }

    public void Dispose() => _limiter.Dispose();
}

internal sealed class SignalRRealtimeNotifier(IHubContext<ProximityHub, IProximityClient> hub) : IRealtimeNotifier
{
    public Task ProximityGroupJoinedAsync(Guid userId, ProximityGroupDto group, CancellationToken cancellationToken) =>
        User(userId).ProximityGroupJoined(group);

    public Task ProximityGroupLeftAsync(Guid userId, ProximityGroupLeftDto left, CancellationToken cancellationToken) =>
        User(userId).ProximityGroupLeft(left);

    public Task NearbyUsersChangedAsync(Guid userId, ProximityGroupDto group, CancellationToken cancellationToken) =>
        User(userId).NearbyUsersChanged(group);

    public Task DrivingSessionStartedAsync(Guid userId, DrivingSessionDto session, CancellationToken cancellationToken) =>
        User(userId).DrivingSessionStarted(session);

    public Task DrivingSessionEndedAsync(Guid userId, DrivingSessionEndedDto ended, CancellationToken cancellationToken) =>
        User(userId).DrivingSessionEnded(ended);

    private IProximityClient User(Guid userId) => hub.Clients.User(userId.ToString());
}
