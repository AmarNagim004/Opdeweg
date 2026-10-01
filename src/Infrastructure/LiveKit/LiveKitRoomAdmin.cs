using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Options;
using Opdeweg.Application.Interfaces;

namespace Opdeweg.Infrastructure.LiveKit;

/// <summary>LiveKit RoomService over its Twirp JSON API, authenticated with short-lived admin tokens.</summary>
internal sealed class LiveKitRoomAdmin(IHttpClientFactory httpClientFactory, LiveKitJwt jwt, IOptions<LiveKitOptions> options) : IVoiceRoomAdmin
{
    public const string HttpClientName = "livekit";
    private static readonly TimeSpan AdminTokenTtl = TimeSpan.FromMinutes(1);

    public async Task<IReadOnlyList<string>> ListParticipantIdentitiesAsync(string room, CancellationToken cancellationToken)
    {
        using var response = await SendAsync("ListParticipants", new JsonObject { ["room"] = room }, AdminGrant(room), cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return [];
        }

        await EnsureSuccessAsync(response, cancellationToken);
        var body = await response.Content.ReadFromJsonAsync<JsonObject>(cancellationToken);
        return body?["participants"] is JsonArray participants
            ? participants.Select(p => p?["identity"]?.GetValue<string>()).OfType<string>().ToArray()
            : [];
    }

    public async Task RemoveParticipantAsync(string room, string identity, CancellationToken cancellationToken)
    {
        using var response = await SendAsync("RemoveParticipant", new JsonObject { ["room"] = room, ["identity"] = identity }, AdminGrant(room), cancellationToken);
        if (response.StatusCode != HttpStatusCode.NotFound)
        {
            await EnsureSuccessAsync(response, cancellationToken);
        }
    }

    public async Task DeleteRoomAsync(string room, CancellationToken cancellationToken)
    {
        var grant = new JsonObject { ["roomCreate"] = true };
        using var response = await SendAsync("DeleteRoom", new JsonObject { ["room"] = room }, grant, cancellationToken);
        if (response.StatusCode != HttpStatusCode.NotFound)
        {
            await EnsureSuccessAsync(response, cancellationToken);
        }
    }

    private static JsonObject AdminGrant(string room) => new() { ["roomAdmin"] = true, ["room"] = room };

    private async Task<HttpResponseMessage> SendAsync(string method, JsonObject body, JsonObject grant, CancellationToken cancellationToken)
    {
        var (token, _) = jwt.Create(identity: null, name: null, grant, AdminTokenTtl);
        var uri = new Uri(options.Value.ResolveApiBaseUri(), $"/twirp/livekit.RoomService/{method}");
        using var request = new HttpRequestMessage(HttpMethod.Post, uri) { Content = JsonContent.Create(body) };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var client = httpClientFactory.CreateClient(HttpClientName);
        return await client.SendAsync(request, cancellationToken);
    }

    private static async Task EnsureSuccessAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (!response.IsSuccessStatusCode)
        {
            var detail = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new HttpRequestException($"LiveKit RoomService returned {(int)response.StatusCode}: {detail}", null, response.StatusCode);
        }
    }
}
