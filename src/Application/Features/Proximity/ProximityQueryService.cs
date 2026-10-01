using Microsoft.Extensions.Options;
using Opdeweg.Application.DTOs;
using Opdeweg.Application.Interfaces;
using Opdeweg.Application.Options;

namespace Opdeweg.Application.Features.Proximity;

/// <summary>Read-side: the current proximity snapshot for one driver (used on reconnect and in location responses).</summary>
public sealed class ProximityQueryService(
    IPresenceStore store,
    ProximityViewBuilder views,
    IOptions<ProximityOptions> options,
    TimeProvider time)
{
    private readonly ProximityOptions _options = options.Value;

    /// <param name="includeVoice">Mint a voice token for the current group. Only needed when the client must (re)connect.</param>
    public async Task<ProximitySnapshotDto> GetSnapshotAsync(Guid userId, bool includeVoice, CancellationToken cancellationToken)
    {
        var now = time.GetUtcNow();
        var session = await store.GetSessionAsync(userId, cancellationToken);
        if (session is null)
        {
            return ProximitySnapshotDto.Inactive(now);
        }

        ProximityGroupDto? group = null;
        var members = new HashSet<Guid>();
        if (session.GroupId is { } groupId)
        {
            var roster = await store.GetGroupMembersAsync(groupId, cancellationToken);
            if (roster.Contains(userId))
            {
                members.UnionWith(roster);
                var voiceFor = includeVoice ? new HashSet<Guid> { userId } : [];
                var built = await views.BuildGroupViewsAsync(groupId, roster, voiceFor, cancellationToken);
                group = built.GetValueOrDefault(userId);
            }
        }

        var nearby = new List<NearbyDriverDto>();
        var position = await store.GetPositionAsync(userId, cancellationToken);
        if (position is not null && _options.NearbyListLimit > 0)
        {
            var candidates = (await store.FindNearbyAsync(position.Point, _options.JoinDistanceMeters, _options.NearbyListLimit + members.Count + 1, cancellationToken))
                .Where(id => id != userId && !members.Contains(id))
                .ToArray();

            var sessions = await store.GetSessionsAsync(candidates, cancellationToken);
            var positions = await store.GetPositionsAsync(candidates, cancellationToken);
            nearby.AddRange(candidates
                .Where(id => sessions.ContainsKey(id) && positions.ContainsKey(id) && now - positions[id].LastSeenAt <= _options.StaleAfter)
                .Select(id => views.ToDriver(sessions[id], position.Point, positions[id].Point, inVoiceGroup: false))
                .Where(d => d.ApproxDistanceMeters <= _options.JoinDistanceMeters)
                .OrderBy(d => d.ApproxDistanceMeters)
                .Take(_options.NearbyListLimit));
        }

        var count = (group?.Members.Count ?? 0) + nearby.Count;
        return new ProximitySnapshotDto(true, group, nearby, count, now);
    }
}
