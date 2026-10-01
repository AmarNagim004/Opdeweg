using System.ComponentModel.DataAnnotations;

namespace Opdeweg.Application.DTOs;

public sealed record RegisterRequest(
    [Required, EmailAddress, MaxLength(254)] string Email,
    [Required, MinLength(8), MaxLength(128)] string Password,
    [Required, MinLength(2), MaxLength(32)] string DisplayName);

public sealed record LoginRequest(
    [Required, MaxLength(254)] string Email,
    [Required, MaxLength(128)] string Password);

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
    [MinLength(2), MaxLength(32)] string? DisplayName,
    [MaxLength(2048)] string? AvatarUrl,
    bool? ShareDisplayName);
