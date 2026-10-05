using Microsoft.Extensions.Logging;
using Opdeweg.Application.DTOs;
using Opdeweg.Application.Interfaces;
using Opdeweg.Application.Logging;

namespace Opdeweg.Application.Features.Proximity;

/// <summary>
/// Turns a committed plan into realtime events and SFU room reconciliation:
/// joiners get <c>ProximityGroupJoined</c> (with a short-lived voice token for that one room),
/// remaining members get <c>NearbyUsersChanged</c>, drivers left without a group get
/// <c>ProximityGroupLeft</c>, and every touched room is reconciled so removed drivers are
/// disconnected server-side even if their client misbehaves.
/// </summary>
public sealed class ProximityNotifier(
    ProximityViewBuilder views,
    IRealtimeNotifier realtime,
    IVoiceRoomReconciler rooms,
    ILogger<ProximityNotifier> logger) : IProximityNotifier
{
    public async Task NotifyAsync(ProximityPlan plan, CancellationToken cancellationToken)
    {
        foreach (var groupId in plan.Rosters.Keys)
        {
            rooms.Enqueue(groupId);
        }

        var joinedTo = plan.Transitions
            .Where(t => t.ToGroupId is not null)
            .ToDictionary(t => t.UserId, t => t.ToGroupId!);
        var joiners = joinedTo.Keys.ToHashSet();

        foreach (var (groupId, members) in plan.Rosters)
        {
            if (members.Count == 0)
            {
                continue;
            }

            var groupViews = await views.BuildGroupViewsAsync(groupId, members, joiners, cancellationToken);
            foreach (var (member, view) in groupViews)
            {
                await SafeSendAsync(
                    member,
                    () => joinedTo.GetValueOrDefault(member) == groupId
                        ? realtime.ProximityGroupJoinedAsync(member, view, cancellationToken)
                        : realtime.NearbyUsersChangedAsync(member, view, cancellationToken));
            }
        }

        foreach (var transition in plan.Transitions.Where(t => t is { ToGroupId: null, FromGroupId: not null }))
        {
            var left = new ProximityGroupLeftDto(transition.FromGroupId!, transition.Reason);
            await SafeSendAsync(transition.UserId, () => realtime.ProximityGroupLeftAsync(transition.UserId, left, cancellationToken));
        }
    }

    private async Task SafeSendAsync(Guid userId, Func<Task> send)
    {
        try
        {
            await send();
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // A failed push must never break proximity processing; clients resync via snapshots.
            Log.NotificationFailed(logger, ex, userId);
        }
    }
}
