using System.Text.Json.Nodes;
using Microsoft.Extensions.Options;
using Opdeweg.Application.DTOs;
using Opdeweg.Application.Interfaces;

namespace Opdeweg.Infrastructure.LiveKit;

/// <summary>
/// Issues LiveKit join tokens scoped to exactly one room, audio-only (microphone source), with no
/// data channel and no ability to change own metadata — the minimum a proximity voice client needs.
/// </summary>
internal sealed class LiveKitTokenIssuer(LiveKitJwt jwt, IOptions<LiveKitOptions> options) : IVoiceTokenIssuer
{
    public VoiceAccessDto CreateJoinToken(VoiceJoinGrant grant)
    {
        var video = new JsonObject
        {
            ["room"] = grant.Room,
            ["roomJoin"] = true,
            ["canPublish"] = true,
            ["canSubscribe"] = true,
            ["canPublishData"] = false,
            ["canUpdateOwnMetadata"] = false,
            ["canPublishSources"] = new JsonArray("microphone"),
        };

        var (token, expiresAt) = jwt.Create(grant.Identity, grant.DisplayName, video, TimeSpan.FromSeconds(options.Value.TokenTtlSeconds));
        return new VoiceAccessDto(options.Value.Url, token, grant.Room, grant.Identity, expiresAt);
    }
}
