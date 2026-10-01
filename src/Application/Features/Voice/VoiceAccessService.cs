using Microsoft.Extensions.Logging;
using Opdeweg.Application.DTOs;
using Opdeweg.Application.Features.Proximity;
using Opdeweg.Application.Interfaces;
using Opdeweg.Application.Logging;

namespace Opdeweg.Application.Features.Voice;

/// <summary>
/// Server-side voice authorisation: a token is only ever issued for the single room of the
/// proximity group the backend has placed the driver in.
/// </summary>
public sealed class VoiceAccessService(IPresenceStore store, IVoiceTokenIssuer issuer, ILogger<VoiceAccessService> logger)
{
    public async Task<VoiceAccessDto?> IssueForCurrentGroupAsync(Guid userId, CancellationToken cancellationToken)
    {
        var session = await store.GetSessionAsync(userId, cancellationToken);
        if (session?.GroupId is not { } groupId)
        {
            Log.VoiceTokenRefused(logger, userId);
            return null;
        }

        var members = await store.GetGroupMembersAsync(groupId, cancellationToken);
        if (!members.Contains(userId))
        {
            Log.VoiceTokenRefused(logger, userId);
            return null;
        }

        return Issue(session, groupId);
    }

    /// <summary>Issues a token for a group the caller has already verified membership of.</summary>
    public VoiceAccessDto Issue(ActiveSession session, string groupId)
    {
        var room = RoomNames.ForGroup(groupId);
        var access = issuer.CreateJoinToken(new VoiceJoinGrant(session.Handle, session.PublicName, room));
        Log.VoiceTokenIssued(logger, session.UserId, room);
        return access;
    }
}
