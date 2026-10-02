using System.ComponentModel.DataAnnotations;

namespace Opdeweg.Application.DTOs;

/// <summary>Validation messages people can see (Dutch, like the app).</summary>
internal static class FieldMessages
{
    public const string Email = "Vul een geldig e-mailadres in.";
    public const string Password = "Kies een wachtwoord van 8 tot 128 tekens.";
    public const string PasswordMissing = "Vul je wachtwoord in.";
    public const string DisplayName = "Je naam moet 2 tot 32 tekens zijn.";
    public const string AvatarUrl = "Deze link naar je profielfoto is te lang.";
}

public sealed record RegisterRequest(
    [Required(ErrorMessage = FieldMessages.Email), EmailAddress(ErrorMessage = FieldMessages.Email), MaxLength(254, ErrorMessage = FieldMessages.Email)] string Email,
    [Required(ErrorMessage = FieldMessages.Password), MinLength(8, ErrorMessage = FieldMessages.Password), MaxLength(128, ErrorMessage = FieldMessages.Password)] string Password,
    [Required(ErrorMessage = FieldMessages.DisplayName), MinLength(2, ErrorMessage = FieldMessages.DisplayName), MaxLength(32, ErrorMessage = FieldMessages.DisplayName)] string DisplayName);

public sealed record LoginRequest(
    [Required(ErrorMessage = FieldMessages.Email), MaxLength(254, ErrorMessage = FieldMessages.Email)] string Email,
    [Required(ErrorMessage = FieldMessages.PasswordMissing), MaxLength(128, ErrorMessage = FieldMessages.Password)] string Password);

public sealed record RefreshRequest([Required, MaxLength(256)] string RefreshToken);

public sealed record LogoutRequest([MaxLength(256)] string? RefreshToken);

public sealed record AuthResponse(
    string AccessToken,
    DateTimeOffset AccessTokenExpiresAt,
    string RefreshToken,
    DateTimeOffset RefreshTokenExpiresAt,
    MeDto User);

/// <summary>The signed-in user's own profile. Never sent to other drivers.</summary>
public sealed record MeDto(
    Guid Id,
    string Email,
    string DisplayName,
    string? AvatarUrl,
    bool ShareDisplayName,
    DateTimeOffset CreatedAt);

public sealed record UpdateProfileRequest(
    [MinLength(2, ErrorMessage = FieldMessages.DisplayName), MaxLength(32, ErrorMessage = FieldMessages.DisplayName)] string? DisplayName,
    [MaxLength(2048, ErrorMessage = FieldMessages.AvatarUrl)] string? AvatarUrl,
    bool? ShareDisplayName);
