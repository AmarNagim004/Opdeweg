using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Opdeweg.Application.Common;
using Opdeweg.Application.DTOs;
using Opdeweg.Application.Features.Driving;
using Opdeweg.Application.Interfaces;
using Opdeweg.Application.Logging;
using Opdeweg.Application.Options;
using Opdeweg.Domain.Entities;
using Opdeweg.Domain.Enums;

namespace Opdeweg.Application.Features.Auth;

/// <summary>
/// E-mail/password authentication with short-lived JWT access tokens and rotating refresh tokens.
/// Logins live in <see cref="UserLogin"/> so Apple/Google sign-in can be added as new providers.
/// </summary>
public sealed class AuthService(
    IUserRepository users,
    IRefreshTokenRepository refreshTokens,
    IUnitOfWork unitOfWork,
    IPasswordHasher passwordHasher,
    IAccessTokenIssuer accessTokens,
    DrivingSessionService drivingSessions,
    IOptions<AuthOptions> options,
    TimeProvider time,
    ILogger<AuthService> logger)
{
    private static string? s_dummyHash;

    public async Task<AuthResponse> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken)
    {
        var email = CredentialRules.NormalizeEmail(request.Email);
        CredentialRules.ValidatePassword(request.Password, options.Value.MinPasswordLength);
        var displayName = CredentialRules.NormalizeDisplayName(request.DisplayName);

        if (await users.EmailExistsAsync(email, cancellationToken))
        {
            throw AppException.Conflict("email_taken", "An account with this e-mail already exists.");
        }

        var now = time.GetUtcNow();
        var user = new User(Guid.CreateVersion7(), email, displayName, now);
        users.Add(user, UserLogin.ForPassword(user.Id, email, passwordHasher.Hash(request.Password), now));

        var response = IssueTokens(user, Guid.CreateVersion7(), now);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        Log.UserRegistered(logger, user.Id);
        return response;
    }

    public async Task<AuthResponse> LoginAsync(LoginRequest request, CancellationToken cancellationToken)
    {
        var email = (request.Email ?? string.Empty).Trim().ToLowerInvariant();
        var login = await users.FindLoginAsync(AuthProvider.Password, email, cancellationToken);

        if (login?.PasswordHash is null || login.User is null)
        {
            // Spend comparable time for unknown accounts so response timing does not reveal registrations.
            s_dummyHash ??= passwordHasher.Hash(Guid.NewGuid().ToString());
            passwordHasher.Verify(s_dummyHash, request.Password ?? string.Empty);
            Log.SignInFailed(logger);
            throw InvalidCredentials();
        }

        var verification = passwordHasher.Verify(login.PasswordHash, request.Password ?? string.Empty);
        if (verification == PasswordVerification.Failed)
        {
            Log.SignInFailed(logger);
            throw InvalidCredentials();
        }

        if (verification == PasswordVerification.SuccessRehashNeeded)
        {
            login.ReplacePasswordHash(passwordHasher.Hash(request.Password!));
        }

        var now = time.GetUtcNow();
        login.User.MarkActive(now);
        var response = IssueTokens(login.User, Guid.CreateVersion7(), now);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        Log.UserSignedIn(logger, login.User.Id);
        return response;
    }

    public async Task<AuthResponse> RefreshAsync(RefreshRequest request, CancellationToken cancellationToken)
    {
        var now = time.GetUtcNow();
        var existing = await refreshTokens.FindByHashAsync(RefreshTokens.Hash(request.RefreshToken ?? string.Empty), cancellationToken);
        if (existing is null)
        {
            throw InvalidRefreshToken();
        }

        if (existing.RevokedAt is not null)
        {
            // A rotated token was presented again: assume theft and kill the whole family.
            await refreshTokens.RevokeFamilyAsync(existing.FamilyId, now, cancellationToken);
            await unitOfWork.SaveChangesAsync(cancellationToken);
            Log.RefreshTokenReuse(logger, existing.UserId, existing.FamilyId);
            throw InvalidRefreshToken();
        }

        if (!existing.IsActive(now))
        {
            throw InvalidRefreshToken();
        }

        var user = await users.GetByIdAsync(existing.UserId, cancellationToken) ?? throw InvalidRefreshToken();
        user.MarkActive(now);
        var response = IssueTokens(user, existing.FamilyId, now, out var replacement);
        existing.Revoke(now, replacement.Id);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return response;
    }

    /// <summary>Revokes the presented refresh token family and ends any active driving session.</summary>
    public async Task LogoutAsync(Guid userId, LogoutRequest request, CancellationToken cancellationToken)
    {
        var now = time.GetUtcNow();
        if (!string.IsNullOrEmpty(request.RefreshToken))
        {
            var token = await refreshTokens.FindByHashAsync(RefreshTokens.Hash(request.RefreshToken), cancellationToken);
            if (token is not null && token.UserId == userId)
            {
                await refreshTokens.RevokeFamilyAsync(token.FamilyId, now, cancellationToken);
                await unitOfWork.SaveChangesAsync(cancellationToken);
            }
        }

        await drivingSessions.EndAsync(userId, DrivingSessionEndReason.SignedOut, cancellationToken);
    }

    public static MeDto ToMe(User user) =>
        new(user.Id, user.Email, user.DisplayName, user.AvatarUrl, user.ShareDisplayName, user.CreatedAt);

    private AuthResponse IssueTokens(User user, Guid familyId, DateTimeOffset now) => IssueTokens(user, familyId, now, out _);

    private AuthResponse IssueTokens(User user, Guid familyId, DateTimeOffset now, out RefreshToken refreshToken)
    {
        var access = accessTokens.Issue(user);
        var plain = RefreshTokens.NewToken();
        var expiresAt = now.AddDays(options.Value.RefreshTokenLifetimeDays);
        refreshToken = new RefreshToken(Guid.CreateVersion7(), user.Id, familyId, RefreshTokens.Hash(plain), now, expiresAt);
        refreshTokens.Add(refreshToken);
        return new AuthResponse(access.Token, access.ExpiresAt, plain, expiresAt, ToMe(user));
    }

    private static AppException InvalidCredentials() =>
        AppException.Unauthorized("invalid_credentials", "E-mail or password is incorrect.");

    private static AppException InvalidRefreshToken() =>
        AppException.Unauthorized("invalid_refresh_token", "Your session has expired. Please sign in again.");
}
