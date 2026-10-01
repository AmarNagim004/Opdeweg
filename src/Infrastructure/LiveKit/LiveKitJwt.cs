using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Options;

namespace Opdeweg.Infrastructure.LiveKit;

/// <summary>
/// Minimal HS256 JWT signing/verification in LiveKit's access-token format. Implemented directly
/// (rather than via a generic JWT library) because LiveKit API secrets are not guaranteed to meet
/// generic libraries' minimum key sizes, and the claim shape is LiveKit-specific.
/// </summary>
internal sealed class LiveKitJwt(IOptions<LiveKitOptions> options, TimeProvider time)
{
    private static readonly string Header = Base64Url("""{"alg":"HS256","typ":"JWT"}"""u8.ToArray());

    public (string Token, DateTimeOffset ExpiresAt) Create(string? identity, string? name, JsonObject videoGrant, TimeSpan ttl)
    {
        var claims = new JsonObject { ["video"] = videoGrant };
        if (identity is not null)
        {
            claims["sub"] = identity;
        }

        if (name is not null)
        {
            claims["name"] = name;
        }

        return CreateWithClaims(claims, ttl);
    }

    /// <summary>Signs arbitrary claims with the standard issuer/validity/jti claims added.</summary>
    public (string Token, DateTimeOffset ExpiresAt) CreateWithClaims(JsonObject claims, TimeSpan ttl)
    {
        var now = time.GetUtcNow();
        var expiresAt = now.Add(ttl);
        claims["iss"] = options.Value.ApiKey;
        claims["nbf"] = now.ToUnixTimeSeconds() - 5;
        claims["exp"] = expiresAt.ToUnixTimeSeconds();
        claims["jti"] = Guid.NewGuid().ToString("N");

        var body = Base64Url(JsonSerializer.SerializeToUtf8Bytes(claims));
        var unsigned = $"{Header}.{body}";
        return ($"{unsigned}.{Sign(unsigned)}", expiresAt);
    }

    /// <summary>Verifies signature, issuer and validity window; returns the payload on success.</summary>
    public JsonObject? Verify(string token)
    {
        var parts = token.Split('.');
        if (parts.Length != 3)
        {
            return null;
        }

        var expected = Encoding.ASCII.GetBytes(Sign($"{parts[0]}.{parts[1]}"));
        if (!CryptographicOperations.FixedTimeEquals(expected, Encoding.ASCII.GetBytes(parts[2])))
        {
            return null;
        }

        JsonObject? payload;
        try
        {
            payload = JsonNode.Parse(FromBase64Url(parts[1])) as JsonObject;
        }
        catch (Exception ex) when (ex is JsonException or FormatException)
        {
            return null;
        }

        if (payload is null || payload["iss"]?.GetValue<string>() != options.Value.ApiKey)
        {
            return null;
        }

        var now = time.GetUtcNow().ToUnixTimeSeconds();
        const long skew = 60;
        var exp = payload["exp"]?.GetValue<long>();
        var nbf = payload["nbf"]?.GetValue<long>();
        if ((exp is { } e && now > e + skew) || (nbf is { } n && now + skew < n))
        {
            return null;
        }

        return payload;
    }

    private string Sign(string data)
    {
        var key = Encoding.UTF8.GetBytes(options.Value.ApiSecret);
        return Base64Url(HMACSHA256.HashData(key, Encoding.UTF8.GetBytes(data)));
    }

    internal static string Base64Url(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    internal static byte[] FromBase64Url(string value)
    {
        var s = value.Replace('-', '+').Replace('_', '/');
        s = s.PadRight(s.Length + ((4 - (s.Length % 4)) % 4), '=');
        return Convert.FromBase64String(s);
    }
}
