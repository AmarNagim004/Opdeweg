using Opdeweg.Application.Features.Proximity;
using Opdeweg.Application.Interfaces;
using Opdeweg.Domain.ValueObjects;

namespace Opdeweg.UnitTests.TestSupport;

/// <summary>In-memory <see cref="IPresenceStore"/> mirroring the Redis implementation's semantics.</summary>
internal sealed class InMemoryPresenceStore : IPresenceStore
{
    public Dictionary<Guid, ActiveSession> Sessions { get; } = [];

    public Dictionary<Guid, StoredPosition> Positions { get; } = [];

    public HashSet<Guid> Indexed { get; } = [];

    public Dictionary<Guid, DateTimeOffset> Seen { get; } = [];

    public Dictionary<string, HashSet<Guid>> Groups { get; } = new(StringComparer.Ordinal);

    public Task SaveSessionAsync(ActiveSession session, PresenceTimeouts timeouts, DateTimeOffset seenAt, CancellationToken cancellationToken)
    {
        Sessions[session.UserId] = session;
        Seen[session.UserId] = seenAt;
        return Task.CompletedTask;
    }

    public Task<ActiveSession?> GetSessionAsync(Guid userId, CancellationToken cancellationToken) =>
        Task.FromResult(Sessions.GetValueOrDefault(userId));

    public Task<IReadOnlyDictionary<Guid, ActiveSession>> GetSessionsAsync(IReadOnlyCollection<Guid> userIds, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyDictionary<Guid, ActiveSession>>(
            userIds.Distinct().Where(Sessions.ContainsKey).ToDictionary(id => id, id => Sessions[id]));

    public Task UpdateSessionIdentityAsync(Guid userId, string publicName, string? publicAvatarUrl, CancellationToken cancellationToken)
    {
        if (Sessions.TryGetValue(userId, out var s))
        {
            Sessions[userId] = s with { PublicName = publicName, PublicAvatarUrl = publicAvatarUrl };
        }

        return Task.CompletedTask;
    }

    public Task MarkCoarseAreaRecordedAsync(Guid userId, CancellationToken cancellationToken)
    {
        if (Sessions.TryGetValue(userId, out var s))
        {
            Sessions[userId] = s with { CoarseAreaRecorded = true };
        }

        return Task.CompletedTask;
    }

    public Task<StoredPosition?> GetPositionAsync(Guid userId, CancellationToken cancellationToken) =>
        Task.FromResult(Positions.GetValueOrDefault(userId));

    public Task<IReadOnlyDictionary<Guid, StoredPosition>> GetPositionsAsync(IReadOnlyCollection<Guid> userIds, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyDictionary<Guid, StoredPosition>>(
            userIds.Distinct().Where(Positions.ContainsKey).ToDictionary(id => id, id => Positions[id]));

    public Task SavePositionAsync(Guid userId, StoredPosition position, PresenceTimeouts timeouts, CancellationToken cancellationToken)
    {
        Positions[userId] = position;
        Indexed.Add(userId);
        Seen[userId] = position.LastSeenAt;
        return Task.CompletedTask;
    }

    public Task TouchAsync(Guid userId, DateTimeOffset seenAt, PresenceTimeouts timeouts, CancellationToken cancellationToken)
    {
        if (Positions.TryGetValue(userId, out var p))
        {
            Positions[userId] = p with { LastSeenAt = seenAt };
        }

        Seen[userId] = seenAt;
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<Guid>> FindNearbyAsync(GeoPoint center, double radiusMeters, int limit, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<Guid>>(Indexed
            .Where(Positions.ContainsKey)
            .Select(id => (Id: id, Distance: GeoMath.DistanceMeters(center, Positions[id].Point)))
            .Where(x => x.Distance <= radiusMeters)
            .OrderBy(x => x.Distance)
            .Take(limit)
            .Select(x => x.Id)
            .ToArray());

    public Task<IReadOnlyList<Guid>> GetGroupMembersAsync(string groupId, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<Guid>>(Groups.TryGetValue(groupId, out var members) ? [.. members] : []);

    public Task<IReadOnlyDictionary<string, IReadOnlyList<Guid>>> GetGroupRostersAsync(IReadOnlyCollection<string> groupIds, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyDictionary<string, IReadOnlyList<Guid>>>(groupIds.Distinct(StringComparer.Ordinal).ToDictionary(
            g => g,
            g => (IReadOnlyList<Guid>)(Groups.TryGetValue(g, out var m) ? [.. m] : Array.Empty<Guid>()),
            StringComparer.Ordinal));

    public Task ApplyMembershipChangesAsync(IReadOnlyList<GroupMembershipChange> changes, CancellationToken cancellationToken)
    {
        foreach (var change in changes)
        {
            if (change.Kind == MembershipChangeKind.Joined)
            {
                if (!Groups.TryGetValue(change.GroupId, out var members))
                {
                    members = [];
                    Groups[change.GroupId] = members;
                }

                members.Add(change.UserId);
                if (Sessions.TryGetValue(change.UserId, out var s))
                {
                    Sessions[change.UserId] = s with { GroupId = change.GroupId };
                }
            }
            else
            {
                if (Groups.TryGetValue(change.GroupId, out var members) && members.Remove(change.UserId) && members.Count == 0)
                {
                    Groups.Remove(change.GroupId);
                }

                if (Sessions.TryGetValue(change.UserId, out var s))
                {
                    Sessions[change.UserId] = s with { GroupId = null };
                }
            }
        }

        return Task.CompletedTask;
    }

    public Task RemoveFromProximityAsync(Guid userId, CancellationToken cancellationToken)
    {
        Positions.Remove(userId);
        Indexed.Remove(userId);
        return Task.CompletedTask;
    }

    public Task RemoveSessionAsync(Guid userId, CancellationToken cancellationToken)
    {
        Positions.Remove(userId);
        Indexed.Remove(userId);
        Seen.Remove(userId);
        Sessions.Remove(userId);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<Guid>> GetDriversSeenBeforeAsync(DateTimeOffset seenBefore, bool onlyIndexed, int limit, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<Guid>>(Seen
            .Where(kv => kv.Value < seenBefore && (!onlyIndexed || Indexed.Contains(kv.Key)))
            .OrderBy(kv => kv.Value)
            .Take(limit)
            .Select(kv => kv.Key)
            .ToArray());
}
