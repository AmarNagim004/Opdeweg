using System.ComponentModel.DataAnnotations;

namespace Opdeweg.Application.Options;

public sealed class AuthOptions
{
    public const string SectionName = "Auth";

    [Range(1, 365)]
    public int RefreshTokenLifetimeDays { get; set; } = 30;

    [Range(8, 128)]
    public int MinPasswordLength { get; set; } = 8;
}
