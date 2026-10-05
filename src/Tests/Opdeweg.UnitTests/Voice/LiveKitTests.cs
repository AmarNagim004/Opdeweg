using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using Opdeweg.Application.Interfaces;
using Opdeweg.Infrastructure.LiveKit;
using Opdeweg.Infrastructure.Services;
using Opdeweg.UnitTests.TestSupport;

namespace Opdeweg.UnitTests.Voice;

public sealed class LiveKitTests
{
    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 10, 1, 12, 0, 0, TimeSpan.Zero));
    private readonly IOptions<LiveKitOptions> _options = Options.Create(new LiveKitOptions
    {
        Url = "wss://voice.example.com",
        ApiKey = "APIkey",
        ApiSecret = "a-very-long-livekit-api-secret-for-tests-only",
        TokenTtlSeconds = 300,
    });

    [Fact]
    public void Join_tokens_are_short_lived_single_room_and_audio_only()
    {
        var jwt = new LiveKitJwt(_options, _time);
        var access = new LiveKitTokenIssuer(jwt, _options).CreateJoinToken(new VoiceJoinGrant("d0123456789ab", "Alice", "opd-abc"));

        var claims = jwt.Verify(access.Token);
        Assert.NotNull(claims);
        Assert.Equal("APIkey", claims["iss"]!.GetValue<string>());
        Assert.Equal("d0123456789ab", claims["sub"]!.GetValue<string>());
        Assert.Equal("Alice", claims["name"]!.GetValue<string>());
        Assert.InRange(claims["exp"]!.GetValue<long>() - _time.GetUtcNow().ToUnixTimeSeconds(), 299, 300);

        var video = claims["video"]!.AsObject();
        Assert.Equal("opd-abc", video["room"]!.GetValue<string>());
        Assert.True(video["roomJoin"]!.GetValue<bool>());
        Assert.False(video["canPublishData"]!.GetValue<bool>());
        Assert.False(video["canUpdateOwnMetadata"]!.GetValue<bool>());
        Assert.Equal(["microphone"], video["canPublishSources"]!.AsArray().Select(n => n!.GetValue<string>()));
        Assert.Null(video["roomAdmin"]);
        Assert.Equal("wss://voice.example.com", access.Url);
    }

    [Fact]
    public void Tokens_signed_with_another_secret_or_expired_are_rejected()
    {
        var jwt = new LiveKitJwt(_options, _time);
        var (token, _) = jwt.Create("x", null, new JsonObject(), TimeSpan.FromMinutes(1));

        var other = new LiveKitJwt(Options.Create(new LiveKitOptions { Url = "wss://x", ApiKey = "APIkey", ApiSecret = "another-secret-another-secret-123" }), _time);
        Assert.Null(other.Verify(token));

        _time.Advance(TimeSpan.FromMinutes(5));
        Assert.Null(jwt.Verify(token));
    }

    [Fact]
    public void Webhooks_are_verified_against_the_body_hash()
    {
        var jwt = new LiveKitJwt(_options, _time);
        var verifier = new LiveKitWebhookVerifier(jwt);
        const string body = """{"event":"participant_joined","room":{"name":"opd-abc"},"participant":{"identity":"d1"}}""";
        var hash = Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(body)));
        var (token, _) = jwt.CreateWithClaims(new JsonObject { ["sha256"] = hash }, TimeSpan.FromMinutes(5));

        var webhook = verifier.Verify(token, body);
        Assert.NotNull(webhook);
        Assert.Equal("participant_joined", webhook.Event);
        Assert.Equal("opd-abc", webhook.Room);
        Assert.Equal("d1", webhook.ParticipantIdentity);

        Assert.Null(verifier.Verify(token, body.Replace("d1", "d2", StringComparison.Ordinal)));
        Assert.Null(verifier.Verify(null, body));
    }

    [Fact]
    public async Task Room_reconciliation_evicts_non_members_and_closes_empty_rooms()
    {
        var store = new InMemoryPresenceStore();
        var admin = new FakeRoomAdmin();
        var alice = Guid.NewGuid();
        store.Sessions[alice] = new ActiveSession(Guid.NewGuid(), alice, "dalice", "Alice", null, _time.GetUtcNow(), "g1", true);
        store.Groups["g1"] = [alice];
        admin.Participants["opd-g1"] = ["dalice", "dmallory"];

        var reconciler = new VoiceRoomReconciler(store, admin, NullLogger<VoiceRoomReconciler>.Instance);
        await reconciler.ReconcileAsync("g1", CancellationToken.None);
        await reconciler.ReconcileAsync("gone", CancellationToken.None);

        Assert.Equal([("opd-g1", "dmallory")], admin.Removed);
        Assert.Equal(["opd-gone"], admin.Deleted);
    }

    private sealed class FakeRoomAdmin : IVoiceRoomAdmin
    {
        public Dictionary<string, List<string>> Participants { get; } = [];

        public List<(string Room, string Identity)> Removed { get; } = [];

        public List<string> Deleted { get; } = [];

        public Task<IReadOnlyList<string>> ListParticipantIdentitiesAsync(string room, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<string>>(Participants.GetValueOrDefault(room) ?? []);

        public Task RemoveParticipantAsync(string room, string identity, CancellationToken cancellationToken)
        {
            Removed.Add((room, identity));
            return Task.CompletedTask;
        }

        public Task DeleteRoomAsync(string room, CancellationToken cancellationToken)
        {
            Deleted.Add(room);
            return Task.CompletedTask;
        }
    }
}
