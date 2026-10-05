using System.ComponentModel.DataAnnotations;

namespace Opdeweg.Infrastructure.LiveKit;

public sealed class LiveKitOptions
{
    public const string SectionName = "LiveKit";

    /// <summary>WebSocket URL clients connect to (LIVEKIT_URL), e.g. wss://voice.example.com.</summary>
    [Required]
    public string Url { get; set; } = string.Empty;

    /// <summary>HTTP(S) URL the API uses for server calls (LIVEKIT_API_URL). Defaults to <see cref="Url"/> with an http(s) scheme.</summary>
    public string? ApiUrl { get; set; }

    [Required, MinLength(3)]
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>Server-only secret (LIVEKIT_API_SECRET). Never shipped to clients.</summary>
    [Required, MinLength(6)]
    public string ApiSecret { get; set; } = string.Empty;

    /// <summary>Join tokens are only needed to connect; LiveKit refreshes them for connected clients.</summary>
    [Range(30, 3600)]
    public int TokenTtlSeconds { get; set; } = 300;

    public bool IsValid() =>
        Uri.TryCreate(Url, UriKind.Absolute, out var url) && url.Scheme is "ws" or "wss" or "http" or "https" &&
        (string.IsNullOrWhiteSpace(ApiUrl) || (Uri.TryCreate(ApiUrl, UriKind.Absolute, out var api) && api.Scheme is "http" or "https"));

    public Uri ResolveApiBaseUri()
    {
        if (!string.IsNullOrWhiteSpace(ApiUrl))
        {
            return new Uri(ApiUrl);
        }

        var builder = new UriBuilder(Url);
        builder.Scheme = builder.Scheme switch
        {
            "wss" => "https",
            "ws" => "http",
            _ => builder.Scheme,
        };
        builder.Port = builder.Uri.IsDefaultPort ? -1 : builder.Port;
        return builder.Uri;
    }
}
