using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Opdeweg.Infrastructure.LiveKit;

public sealed record LiveKitWebhookEvent(string Event, string? Room, string? ParticipantIdentity);

/// <summary>
/// Verifies LiveKit webhooks: the Authorization header carries a JWT signed with the API secret
/// whose <c>sha256</c> claim must match the request body.
/// </summary>
public sealed class LiveKitWebhookVerifier
{
    private readonly LiveKitJwt _jwt;

    internal LiveKitWebhookVerifier(LiveKitJwt jwt) => _jwt = jwt;

    public LiveKitWebhookEvent? Verify(string? authorization, string body)
    {
        if (string.IsNullOrWhiteSpace(authorization))
        {
            return null;
        }

        var token = authorization.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase) ? authorization[7..] : authorization;
        var claims = _jwt.Verify(token.Trim());
        var expectedHash = Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(body)));
        if (claims?["sha256"]?.GetValue<string>() is not { } hash ||
            !CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(hash), Encoding.ASCII.GetBytes(expectedHash)))
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(body);
            var root = document.RootElement;
            var evt = root.TryGetProperty("event", out var e) ? e.GetString() : null;
            var room = root.TryGetProperty("room", out var r) && r.TryGetProperty("name", out var n) ? n.GetString() : null;
            var identity = root.TryGetProperty("participant", out var p) && p.TryGetProperty("identity", out var i) ? i.GetString() : null;
            return evt is null ? null : new LiveKitWebhookEvent(evt, room, identity);
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
