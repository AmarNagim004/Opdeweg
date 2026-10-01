using System.Globalization;
using Opdeweg.Application.Features.Proximity;
using Opdeweg.Application.Interfaces;
using Opdeweg.Domain.ValueObjects;
using StackExchange.Redis;

namespace Opdeweg.Infrastructure.Redis;

/// <summary>
/// Redis-backed presence: GEO index for candidate lookup (no O(n²) comparisons), latest-fix-only
/// position hashes with TTLs, and group rosters. Multi-key writes use MULTI/EXEC transactions.
/// </summary>
internal sealed class RedisPresenceStore(IConnectionMultiplexer redis, RedisKeys keys) : IPresenceStore
{
    private static readonly TimeSpan GroupTtl = TimeSpan.FromHours(12);
    private static readonly TimeSpan OrphanSessionFieldTtl = TimeSpan.FromHours(1);

    private IDatabase Db => redis.GetDatabase();

    public async Task SaveSessionAsync(ActiveSession session, PresenceTimeouts timeouts, DateTimeOffset seenAt, CancellationToken cancellationToken)
    {
        var key = keys.Session(session.UserId);
        var tx = Db.CreateTransaction();
        _ = tx.KeyDeleteAsync(key);
        _ = tx.HashSetAsync(key, SessionFields(session));
        _ = tx.KeyExpireAsync(key, timeouts.SessionTtl);
        _ = tx.SortedSetAddAsync(keys.Seen, RedisKeys.Member(session.UserId), seenAt.ToUnixTimeMilliseconds());
        await tx.ExecuteAsync();
    }

    public async Task<ActiveSession?> GetSessionAsync(Guid userId, CancellationToken cancellationToken) =>
        ParseSession(userId, await Db.HashGetAllAsync(keys.Session(userId)));

    public async Task<IReadOnlyDictionary<Guid, ActiveSession>> GetSessionsAsync(IReadOnlyCollection<Guid> userIds, CancellationToken cancellationToken)
    {
        var db = Db;
        var ids = userIds.Distinct().ToArray();
        var results = await Task.WhenAll(ids.Select(id => db.HashGetAllAsync(keys.Session(id))));
        var sessions = new Dictionary<Guid, ActiveSession>(ids.Length);
        for (var i = 0; i < ids.Length; i++)
        {
            if (ParseSession(ids[i], results[i]) is { } session)
            {
                sessions[ids[i]] = session;
            }
        }

        return sessions;
    }

    public async Task UpdateSessionIdentityAsync(Guid userId, string publicName, string? publicAvatarUrl, CancellationToken cancellationToken)
    {
        var key = keys.Session(userId);
        var tx = Db.CreateTransaction();
        tx.AddCondition(Condition.KeyExists(key));
        _ = tx.HashSetAsync(key, "name", publicName);
        _ = publicAvatarUrl is null ? tx.HashDeleteAsync(key, "av") : tx.HashSetAsync(key, "av", publicAvatarUrl);
        await tx.ExecuteAsync();
    }

    public async Task MarkCoarseAreaRecordedAsync(Guid userId, CancellationToken cancellationToken)
    {
        var key = keys.Session(userId);
        var tx = Db.CreateTransaction();
        tx.AddCondition(Condition.KeyExists(key));
        _ = tx.HashSetAsync(key, "car", 1);
        await tx.ExecuteAsync();
    }

    public async Task<StoredPosition?> GetPositionAsync(Guid userId, CancellationToken cancellationToken) =>
        ParsePosition(await Db.HashGetAllAsync(keys.Position(userId)));

    public async Task<IReadOnlyDictionary<Guid, StoredPosition>> GetPositionsAsync(IReadOnlyCollection<Guid> userIds, CancellationToken cancellationToken)
    {
        var db = Db;
        var ids = userIds.Distinct().ToArray();
        var results = await Task.WhenAll(ids.Select(id => db.HashGetAllAsync(keys.Position(id))));
        var positions = new Dictionary<Guid, StoredPosition>(ids.Length);
        for (var i = 0; i < ids.Length; i++)
        {
            if (ParsePosition(results[i]) is { } position)
            {
                positions[ids[i]] = position;
            }
        }

        return positions;
    }

    public async Task SavePositionAsync(Guid userId, StoredPosition position, PresenceTimeouts timeouts, CancellationToken cancellationToken)
    {
        var member = RedisKeys.Member(userId);
        var key = keys.Position(userId);
        var tx = Db.CreateTransaction();
        _ = tx.KeyDeleteAsync(key);
        _ = tx.HashSetAsync(key, PositionFields(position));
        _ = tx.KeyExpireAsync(key, timeouts.PositionTtl);
        _ = tx.GeoAddAsync(keys.Geo, position.Point.Longitude, position.Point.Latitude, member);
        _ = tx.SortedSetAddAsync(keys.Seen, member, position.LastSeenAt.ToUnixTimeMilliseconds());
        _ = tx.KeyExpireAsync(keys.Session(userId), timeouts.SessionTtl);
        await tx.ExecuteAsync();
    }

    public async Task TouchAsync(Guid userId, DateTimeOffset seenAt, PresenceTimeouts timeouts, CancellationToken cancellationToken)
    {
        var db = Db;
        var positionKey = keys.Position(userId);
        var seen = seenAt.ToUnixTimeMilliseconds();

        var tx = db.CreateTransaction();
        tx.AddCondition(Condition.KeyExists(positionKey));
        _ = tx.HashSetAsync(positionKey, "seen", seen);
        _ = tx.KeyExpireAsync(positionKey, timeouts.PositionTtl);
        await tx.ExecuteAsync();

        await Task.WhenAll(
            db.SortedSetAddAsync(keys.Seen, RedisKeys.Member(userId), seen),
            db.KeyExpireAsync(keys.Session(userId), timeouts.SessionTtl));
    }

    public async Task<IReadOnlyList<Guid>> FindNearbyAsync(GeoPoint center, double radiusMeters, int limit, CancellationToken cancellationToken)
    {
        var results = await Db.GeoSearchAsync(
            keys.Geo,
            center.Longitude,
            center.Latitude,
            new GeoSearchCircle(radiusMeters, GeoUnit.Meters),
            count: limit,
            demandClosest: true,
            order: Order.Ascending,
            options: GeoRadiusOptions.None);

        return results.Select(r => ParseMember(r.Member)).OfType<Guid>().ToArray();
    }

    public async Task<IReadOnlyList<Guid>> GetGroupMembersAsync(string groupId, CancellationToken cancellationToken) =>
        (await Db.SetMembersAsync(keys.Group(groupId))).Select(ParseMember).OfType<Guid>().ToArray();

    public async Task<IReadOnlyDictionary<string, IReadOnlyList<Guid>>> GetGroupRostersAsync(IReadOnlyCollection<string> groupIds, CancellationToken cancellationToken)
    {
        var db = Db;
        var ids = groupIds.Distinct(StringComparer.Ordinal).ToArray();
        var results = await Task.WhenAll(ids.Select(id => db.SetMembersAsync(keys.Group(id))));
        var rosters = new Dictionary<string, IReadOnlyList<Guid>>(StringComparer.Ordinal);
        for (var i = 0; i < ids.Length; i++)
        {
            rosters[ids[i]] = results[i].Select(ParseMember).OfType<Guid>().ToArray();
        }

        return rosters;
    }

    public async Task ApplyMembershipChangesAsync(IReadOnlyList<GroupMembershipChange> changes, CancellationToken cancellationToken)
    {
        if (changes.Count == 0)
        {
            return;
        }

        var tx = Db.CreateTransaction();
        foreach (var change in changes)
        {
            var groupKey = keys.Group(change.GroupId);
            var sessionKey = keys.Session(change.UserId);
            var member = RedisKeys.Member(change.UserId);

            if (change.Kind == MembershipChangeKind.Joined)
            {
                _ = tx.SetAddAsync(groupKey, member);
                _ = tx.KeyExpireAsync(groupKey, GroupTtl);
                _ = tx.HashSetAsync(sessionKey, "grp", change.GroupId);

                // Never leave a TTL-less partial hash behind if the session expired concurrently.
                _ = tx.KeyExpireAsync(sessionKey, OrphanSessionFieldTtl, ExpireWhen.HasNoExpiry);
            }
            else
            {
                // An emptied set is deleted by Redis automatically.
                _ = tx.SetRemoveAsync(groupKey, member);
                _ = tx.HashDeleteAsync(sessionKey, "grp");
            }
        }

        await tx.ExecuteAsync();
    }

    public async Task RemoveFromProximityAsync(Guid userId, CancellationToken cancellationToken)
    {
        var tx = Db.CreateTransaction();
        _ = tx.KeyDeleteAsync(keys.Position(userId));
        _ = tx.GeoRemoveAsync(keys.Geo, RedisKeys.Member(userId));
        await tx.ExecuteAsync();
    }

    public async Task RemoveSessionAsync(Guid userId, CancellationToken cancellationToken)
    {
        var member = RedisKeys.Member(userId);
        var tx = Db.CreateTransaction();
        _ = tx.KeyDeleteAsync(keys.Position(userId));
        _ = tx.GeoRemoveAsync(keys.Geo, member);
        _ = tx.SortedSetRemoveAsync(keys.Seen, member);
        _ = tx.KeyDeleteAsync(keys.Session(userId));
        await tx.ExecuteAsync();
    }

    public async Task<IReadOnlyList<Guid>> GetDriversSeenBeforeAsync(DateTimeOffset seenBefore, bool onlyIndexed, int limit, CancellationToken cancellationToken)
    {
        var db = Db;
        var members = await db.SortedSetRangeByScoreAsync(
            keys.Seen,
            double.NegativeInfinity,
            seenBefore.ToUnixTimeMilliseconds(),
            Exclude.Stop,
            take: limit);

        if (!onlyIndexed || members.Length == 0)
        {
            return members.Select(ParseMember).OfType<Guid>().ToArray();
        }

        // GEO sets are sorted sets: a score means the driver is still in the spatial index.
        var scores = await Task.WhenAll(members.Select(m => db.SortedSetScoreAsync(keys.Geo, m)));
        return members.Where((_, i) => scores[i] is not null).Select(ParseMember).OfType<Guid>().ToArray();
    }

    private static HashEntry[] SessionFields(ActiveSession session)
    {
        var fields = new List<HashEntry>
        {
            new("sid", session.SessionId.ToString("N")),
            new("h", session.Handle),
            new("name", session.PublicName),
            new("st", session.StartedAt.ToUnixTimeMilliseconds()),
            new("car", session.CoarseAreaRecorded ? 1 : 0),
        };

        if (session.PublicAvatarUrl is not null)
        {
            fields.Add(new HashEntry("av", session.PublicAvatarUrl));
        }

        if (session.GroupId is not null)
        {
            fields.Add(new HashEntry("grp", session.GroupId));
        }

        return [.. fields];
    }

    private static ActiveSession? ParseSession(Guid userId, HashEntry[] entries)
    {
        if (entries.Length == 0)
        {
            return null;
        }

        var map = entries.ToDictionary(e => e.Name.ToString(), e => e.Value);
        if (!map.TryGetValue("sid", out var sid) || !Guid.TryParse(sid.ToString(), out var sessionId) ||
            !map.TryGetValue("h", out var handle) || !map.TryGetValue("st", out var started))
        {
            return null;
        }

        return new ActiveSession(
            sessionId,
            userId,
            handle.ToString(),
            map.TryGetValue("name", out var name) ? name.ToString() : "Driver",
            map.TryGetValue("av", out var avatar) ? avatar.ToString() : null,
            DateTimeOffset.FromUnixTimeMilliseconds((long)started),
            map.TryGetValue("grp", out var group) ? group.ToString() : null,
            map.TryGetValue("car", out var car) && (long)car == 1);
    }

    private static HashEntry[] PositionFields(StoredPosition p)
    {
        var fields = new List<HashEntry>
        {
            new("lat", p.Point.Latitude.ToString("R", CultureInfo.InvariantCulture)),
            new("lon", p.Point.Longitude.ToString("R", CultureInfo.InvariantCulture)),
            new("cts", p.ClientTimestamp.ToUnixTimeMilliseconds()),
            new("pat", p.PositionAt.ToUnixTimeMilliseconds()),
            new("seen", p.LastSeenAt.ToUnixTimeMilliseconds()),
        };

        AddOptional(fields, "acc", p.AccuracyMeters);
        AddOptional(fields, "spd", p.SpeedMetersPerSecond);
        AddOptional(fields, "hdg", p.HeadingDegrees);
        return [.. fields];

        static void AddOptional(List<HashEntry> list, string name, double? value)
        {
            if (value is { } v)
            {
                list.Add(new HashEntry(name, v.ToString("R", CultureInfo.InvariantCulture)));
            }
        }
    }

    private static StoredPosition? ParsePosition(HashEntry[] entries)
    {
        if (entries.Length == 0)
        {
            return null;
        }

        var map = entries.ToDictionary(e => e.Name.ToString(), e => e.Value);
        if (!TryDouble(map, "lat", out var lat) || !TryDouble(map, "lon", out var lon) || !GeoPoint.IsValid(lat, lon) ||
            !map.TryGetValue("cts", out var cts) || !map.TryGetValue("pat", out var pat) || !map.TryGetValue("seen", out var seen))
        {
            return null;
        }

        return new StoredPosition(
            new GeoPoint(lat, lon),
            TryDouble(map, "acc", out var acc) ? acc : null,
            TryDouble(map, "spd", out var spd) ? spd : null,
            TryDouble(map, "hdg", out var hdg) ? hdg : null,
            DateTimeOffset.FromUnixTimeMilliseconds((long)cts),
            DateTimeOffset.FromUnixTimeMilliseconds((long)pat),
            DateTimeOffset.FromUnixTimeMilliseconds((long)seen));

        static bool TryDouble(Dictionary<string, RedisValue> map, string name, out double value)
        {
            value = 0;
            return map.TryGetValue(name, out var raw) &&
                double.TryParse(raw.ToString(), NumberStyles.Float, CultureInfo.InvariantCulture, out value);
        }
    }

    private static Guid? ParseMember(RedisValue value) => Guid.TryParseExact(value.ToString(), "N", out var id) ? id : null;
}
