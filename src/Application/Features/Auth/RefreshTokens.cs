using System.Security.Cryptography;
using System.Text;

namespace Opdeweg.Application.Features.Auth;

/// <summary>Opaque, high-entropy refresh tokens. Only their SHA-256 hash is ever persisted.</summary>
public static class RefreshTokens
{
    public static string NewToken() => Base64UrlEncode(RandomNumberGenerator.GetBytes(32));

    public static string Hash(string token) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(token)));

    private static string Base64UrlEncode(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
