using Microsoft.Extensions.Logging;
using Opdeweg.Application.Common;
using Opdeweg.Application.DTOs;
using Opdeweg.Application.Features.Auth;
using Opdeweg.Application.Features.Driving;
using Opdeweg.Application.Interfaces;
using Opdeweg.Application.Logging;
using Opdeweg.Domain.Enums;

namespace Opdeweg.Application.Features.Profile;

public sealed class ProfileService(
    IUserRepository users,
    IRefreshTokenRepository refreshTokens,
    IUnitOfWork unitOfWork,
    IPresenceStore store,
    DrivingSessionService drivingSessions,
    TimeProvider time,
    ILogger<ProfileService> logger)
{
    public async Task<MeDto> GetAsync(Guid userId, CancellationToken cancellationToken)
    {
        var user = await users.GetByIdAsync(userId, cancellationToken) ?? throw UnknownUser();
        return AuthService.ToMe(user);
    }

    public async Task<MeDto> UpdateAsync(Guid userId, UpdateProfileRequest request, CancellationToken cancellationToken)
    {
        var user = await users.GetByIdAsync(userId, cancellationToken) ?? throw UnknownUser();

        var displayName = request.DisplayName is null ? null : CredentialRules.NormalizeDisplayName(request.DisplayName);
        var avatarUrl = request.AvatarUrl is null ? null : CredentialRules.NormalizeAvatarUrl(request.AvatarUrl);
        user.UpdateProfile(displayName, avatarUrl, request.ShareDisplayName);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        // Keep what nearby drivers see in sync for an ongoing session.
        if (await store.GetSessionAsync(userId, cancellationToken) is not null)
        {
            await store.UpdateSessionIdentityAsync(
                userId,
                DrivingSessionService.PublicName(user),
                DrivingSessionService.PublicAvatar(user),
                cancellationToken);
        }

        return AuthService.ToMe(user);
    }

    /// <summary>Deletes the account and everything tied to it (sessions, logins, tokens, presence).</summary>
    public async Task DeleteAsync(Guid userId, CancellationToken cancellationToken)
    {
        var user = await users.GetByIdAsync(userId, cancellationToken) ?? throw UnknownUser();
        await drivingSessions.EndAsync(userId, DrivingSessionEndReason.AccountDeleted, cancellationToken);
        await refreshTokens.RevokeAllForUserAsync(userId, time.GetUtcNow(), cancellationToken);
        users.Remove(user);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        Log.AccountDeleted(logger, userId);
    }

    private static AppException UnknownUser() => AppException.Unauthorized("unknown_user", "The account no longer exists.");
}
