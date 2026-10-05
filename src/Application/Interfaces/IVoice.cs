using Opdeweg.Application.DTOs;

namespace Opdeweg.Application.Interfaces;

public sealed record VoiceJoinGrant(string Identity, string DisplayName, string Room);

/// <summary>Issues short-lived SFU access tokens scoped to exactly one room. Server-side only.</summary>
public interface IVoiceTokenIssuer
{
    VoiceAccessDto CreateJoinToken(VoiceJoinGrant grant);
}

/// <summary>Server-side SFU administration (LiveKit RoomService).</summary>
public interface IVoiceRoomAdmin
{
    Task<IReadOnlyList<string>> ListParticipantIdentitiesAsync(string room, CancellationToken cancellationToken);

    Task RemoveParticipantAsync(string room, string identity, CancellationToken cancellationToken);

    Task DeleteRoomAsync(string room, CancellationToken cancellationToken);
}

/// <summary>
/// Requests that a group's SFU room be brought in line with its authoritative roster:
/// participants who are no longer members are removed, and empty rooms are closed.
/// </summary>
public interface IVoiceRoomReconciler
{
    void Enqueue(string groupId);
}
