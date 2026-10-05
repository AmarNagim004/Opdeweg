using System.Collections.Concurrent;
using System.Threading.Channels;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Opdeweg.Application.Features.Proximity;
using Opdeweg.Application.Interfaces;

namespace Opdeweg.Infrastructure.Services;

/// <summary>
/// Enforces proximity membership inside the SFU: removes participants who are no longer in the
/// group's authoritative roster and closes rooms of dissolved groups. Runs off the proximity hot
/// path so a slow or unavailable SFU never delays grouping.
/// </summary>
internal sealed partial class VoiceRoomReconciler(
    IPresenceStore store,
    IVoiceRoomAdmin admin,
    ILogger<VoiceRoomReconciler> logger) : BackgroundService, IVoiceRoomReconciler
{
    private static readonly TimeSpan CallTimeout = TimeSpan.FromSeconds(5);
    private readonly Channel<string> _channel = Channel.CreateUnbounded<string>(new UnboundedChannelOptions { SingleReader = true });
    private readonly ConcurrentDictionary<string, byte> _pending = new(StringComparer.Ordinal);

    public void Enqueue(string groupId)
    {
        if (_pending.TryAdd(groupId, 0))
        {
            _channel.Writer.TryWrite(groupId);
        }
    }

    internal async Task ReconcileAsync(string groupId, CancellationToken cancellationToken)
    {
        var room = RoomNames.ForGroup(groupId);
        var members = await store.GetGroupMembersAsync(groupId, cancellationToken);

        if (members.Count == 0)
        {
            await admin.DeleteRoomAsync(room, cancellationToken);
            return;
        }

        var allowed = (await store.GetSessionsAsync(members, cancellationToken)).Values
            .Select(s => s.Handle)
            .ToHashSet(StringComparer.Ordinal);

        foreach (var identity in await admin.ListParticipantIdentitiesAsync(room, cancellationToken))
        {
            if (!allowed.Contains(identity))
            {
                await admin.RemoveParticipantAsync(room, identity, cancellationToken);
                LogParticipantRemoved(logger, room);
            }
        }
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var groupId in _channel.Reader.ReadAllAsync(stoppingToken))
        {
            _pending.TryRemove(groupId, out _);
            try
            {
                using var timeout = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
                timeout.CancelAfter(CallTimeout);
                await ReconcileAsync(groupId, timeout.Token);
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            {
                LogReconcileFailed(logger, ex, groupId);
            }
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Removed unauthorised participant from voice room {Room}")]
    private static partial void LogParticipantRemoved(ILogger logger, string room);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Voice room reconciliation failed for group {GroupId}")]
    private static partial void LogReconcileFailed(ILogger logger, Exception exception, string groupId);
}
