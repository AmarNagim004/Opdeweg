using System.ComponentModel.DataAnnotations;
using System.Text;

namespace Opdeweg.Infrastructure.Security;

public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    [Required]
    public string Issuer { get; set; } = "opdeweg-api";

    [Required]
    public string Audience { get; set; } = "opdeweg-mobile";

    /// <summary>HMAC signing secret (JWT_SECRET). At least 32 bytes; never committed.</summary>
    [Required]
    public string Secret { get; set; } = string.Empty;

    [Range(1, 120)]
    public int AccessTokenLifetimeMinutes { get; set; } = 15;

    public byte[] SecretBytes => Encoding.UTF8.GetBytes(Secret);

    public bool HasStrongSecret => SecretBytes.Length >= 32;
}
