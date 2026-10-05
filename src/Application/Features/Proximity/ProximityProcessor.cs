using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Opdeweg.Application.Interfaces;
using Opdeweg.Application.Logging;
using Opdeweg.Application.Options;

namespace Opdeweg.Application.Features.Proximity;

public interface IProximityNotifier
{
    Task NotifyAsync(ProximityPlan plan, CancellationToken cancellationToken);
}

/// <summary>
/// Executes proximity commands: loads the local neighbourhood from the spatial index, runs the
/// pure <see cref="ProximityGroupingEngine"/>, persists the resulting membership changes and fans
/// out notifications. Callers must serialise invocations (see the proximity worker).
/// </summary>
public sealed class ProximityProcessor(
    IPresenceStore store,
    ProximityGroupingEngine engine,
    IProximityNotifier notifier,
    IOptions<ProximityOptions> options,
    TimeProvider time,
    ILogger<ProximityProcessor> logger)
{
    private readonly ProximityOptions _options = options.Value;

    /// <returns>Drivers that should be re-evaluated because this command displaced them.</returns>
    public Task<IReadOnlyCollection<Guid>> HandleAsync(ProximityCommand command, CancellationToken cancellationToken) =>
        command switch
        {
            EvaluateDriverCommand evaluate => EvaluateAsync(evaluate.UserId, cancellationToken),
            RemoveDriverCommand remove => RemoveAsync(remove, cancellationToken),
            _ => throw new ArgumentOutOfRangeException(nameof(command)),
        };

    private async Task<IReadOnlyCollection<Guid>> EvaluateAsync(Guid userId, CancellationToken cancellationToken)
    {
        var session = await store.GetSessionAsync(userId, cancellationToken);
        if (session is null)
        {
            // A late location update raced with session end: make sure nothing lingers in the index.
            await store.RemoveFromProximityAsync(userId, cancellationToken);
            return [];
        }

        var position = await store.GetPositionAsync(userId, cancellationToken);
        if (position is null)
        {
            return [];
        }

        // Spatial index → candidate set. Slightly over-fetch so index/haversine rounding never hides a
        // driver right at the boundary; the engine re-checks exact distances.
        var radius = (_options.JoinDistanceMeters * 1.02) + 25;
        var candidates = await store.FindNearbyAsync(position.Point, radius, _options.CandidateSearchLimit, cancellationToken);

        var world = await LoadWorldAsync([userId, .. candidates], cancellationToken);
        var plan = engine.Evaluate(world, userId, time.GetUtcNow());
        await CommitAsync(plan, cancellationToken);
        return plan.ReEvaluate;
    }

    private async Task<IReadOnlyCollection<Guid>> RemoveAsync(RemoveDriverCommand command, CancellationToken cancellationToken)
    {
        var world = await LoadWorldAsync([command.UserId], cancellationToken);
        var plan = engine.Remove(world, command.UserId, command.Reason);
        await CommitAsync(plan, cancellationToken);

        if (command.EndSession)
        {
            await store.RemoveSessionAsync(command.UserId, cancellationToken);
        }
        else
        {
            await store.RemoveFromProximityAsync(command.UserId, cancellationToken);
        }

        return plan.ReEvaluate;
    }

    private async Task<ProximityWorld> LoadWorldAsync(IReadOnlyCollection<Guid> seedIds, CancellationToken cancellationToken)
    {
        var ids = new HashSet<Guid>(seedIds);
        var sessions = new Dictionary<Guid, ActiveSession>(await store.GetSessionsAsync(ids, cancellationToken));

        var groupIds = sessions.Values.Select(s => s.GroupId).OfType<string>().ToHashSet(StringComparer.Ordinal);
        var rosters = groupIds.Count == 0
            ? new Dictionary<string, IReadOnlyList<Guid>>()
            : await store.GetGroupRostersAsync(groupIds, cancellationToken);

        var missing = rosters.Values.SelectMany(m => m).Where(ids.Add).ToArray();
        if (missing.Length > 0)
        {
            foreach (var (id, session) in await store.GetSessionsAsync(missing, cancellationToken))
            {
                sessions[id] = session;
            }
        }

        var positions = await store.GetPositionsAsync(ids, cancellationToken);

        var world = new ProximityWorld();
        foreach (var id in ids)
        {
            if (sessions.ContainsKey(id) && positions.TryGetValue(id, out var p))
            {
                world.AddDriver(new DriverState(id, p.Point, p.AccuracyMeters, p.LastSeenAt));
            }
        }

        // Members without a session or position are kept in the roster so the engine prunes them.
        foreach (var (groupId, members) in rosters)
        {
            foreach (var member in members)
            {
                world.AddMembership(member, groupId);
            }
        }

        return world;
    }

    private async Task CommitAsync(ProximityPlan plan, CancellationToken cancellationToken)
    {
        if (plan.IsEmpty)
        {
            return;
        }

        await store.ApplyMembershipChangesAsync(plan.Changes, cancellationToken);

        foreach (var transition in plan.Transitions)
        {
            Log.ProximityGroupChanged(logger, transition.UserId, transition.FromGroupId, transition.ToGroupId, transition.Reason);
        }

        await notifier.NotifyAsync(plan, cancellationToken);
    }
}
