using System.ComponentModel.DataAnnotations;

namespace Opdeweg.Application.Options;

public sealed class AuthOptions
{
    public const string SectionName = "Auth";

    [Range(1, 365)]
    public int RefreshTokenLifetimeDays { get; set; } = 30;

    [Range(8, 128)]
    public int MinPasswordLength { get; set; } = 8;

    /// <summary>
    /// A just-rotated refresh token presented again within this window is treated as a benign race
    /// (lost response + retry, two app contexts refreshing at once) rather than theft.
    /// </summary>
    [Range(0, 300)]
    public int RefreshReuseGraceSeconds { get; set; } = 30;
}
