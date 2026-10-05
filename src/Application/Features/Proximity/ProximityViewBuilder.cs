using Microsoft.Extensions.Options;
using Opdeweg.Application.DTOs;
using Opdeweg.Application.Features.Voice;
using Opdeweg.Application.Interfaces;
using Opdeweg.Application.Options;
using Opdeweg.Domain.ValueObjects;

namespace Opdeweg.Application.Features.Proximity;

/// <summary>
/// Builds the privacy-minimised views drivers see of each other: pseudonymous handle, public name
/// and a rounded distance computed server-side. Coordinates never leave the server.
/// </summary>
public sealed class ProximityViewBuilder(IPresenceStore store, VoiceAccessService voice, IOptions<ProximityOptions> options)
{
    private readonly ProximityOptions _options = options.Value;

    /// <summary>Builds one roster view per member (each excludes the recipient and is measured from them).</summary>
    public async Task<IReadOnlyDictionary<Guid, ProximityGroupDto>> BuildGroupViewsAsync(
        string groupId,
        IReadOnlyCollection<Guid> members,
        IReadOnlySet<Guid> includeVoiceFor,
        CancellationToken cancellationToken)
    {
        var sessions = await store.GetSessionsAsync(members, cancellationToken);
        var positions = await store.GetPositionsAsync(members, cancellationToken);
        var views = new Dictionary<Guid, ProximityGroupDto>();

        foreach (var recipient in members)
        {
            if (!sessions.TryGetValue(recipient, out var recipientSession))
            {
                continue;
            }

            var origin = positions.GetValueOrDefault(recipient)?.Point;
            var roster = members
                .Where(id => id != recipient && sessions.ContainsKey(id))
                .Select(id => ToDriver(sessions[id], origin, positions.GetValueOrDefault(id)?.Point, inVoiceGroup: true))
                .OrderBy(d => d.ApproxDistanceMeters ?? int.MaxValue)
                .ToArray();

            var access = includeVoiceFor.Contains(recipient) ? voice.Issue(recipientSession, groupId) : null;
            views[recipient] = new ProximityGroupDto(groupId, roster, access);
        }

        return views;
    }

    public NearbyDriverDto ToDriver(ActiveSession session, GeoPoint? origin, GeoPoint? position, bool inVoiceGroup) =>
        new(session.Handle, session.PublicName, session.PublicAvatarUrl, RoundedDistance(origin, position), inVoiceGroup);

    private int? RoundedDistance(GeoPoint? origin, GeoPoint? position)
    {
        if (origin is not { } a || position is not { } b)
        {
            return null;
        }

        var step = _options.DistanceRoundingMeters;
        var rounded = (int)(Math.Round(GeoMath.DistanceMeters(a, b) / step) * step);
        return Math.Max(step, rounded);
    }
}
