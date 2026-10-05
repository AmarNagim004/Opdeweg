using Microsoft.Extensions.Logging;
using Opdeweg.Application.Features.Proximity;
using Opdeweg.Domain.Enums;

namespace Opdeweg.Application.Logging;

/// <summary>
/// Structured, privacy-conscious log events. Identifiers only — coordinates are never logged.
/// </summary>
internal static partial class Log
{
    [LoggerMessage(EventId = 1000, Level = LogLevel.Information, Message = "User {UserId} registered")]
    public static partial void UserRegistered(ILogger logger, Guid userId);

    [LoggerMessage(EventId = 1001, Level = LogLevel.Information, Message = "User {UserId} signed in")]
    public static partial void UserSignedIn(ILogger logger, Guid userId);

    [LoggerMessage(EventId = 1002, Level = LogLevel.Warning, Message = "Failed sign-in attempt")]
    public static partial void SignInFailed(ILogger logger);

    [LoggerMessage(EventId = 1003, Level = LogLevel.Warning, Message = "Refresh token reuse detected for user {UserId}; token family {FamilyId} revoked")]
    public static partial void RefreshTokenReuse(ILogger logger, Guid userId, Guid familyId);

    [LoggerMessage(EventId = 1004, Level = LogLevel.Information, Message = "User {UserId} deleted their account")]
    public static partial void AccountDeleted(ILogger logger, Guid userId);

    [LoggerMessage(EventId = 2000, Level = LogLevel.Information, Message = "Driving session {SessionId} started for user {UserId}")]
    public static partial void DrivingSessionStarted(ILogger logger, Guid sessionId, Guid userId);

    [LoggerMessage(EventId = 2001, Level = LogLevel.Information, Message = "Driving session {SessionId} ended for user {UserId} ({Reason})")]
    public static partial void DrivingSessionEnded(ILogger logger, Guid sessionId, Guid userId, DrivingSessionEndReason reason);

    [LoggerMessage(EventId = 3000, Level = LogLevel.Debug, Message = "Location update accepted for user {UserId} (accuracy {AccuracyMeters} m)")]
    public static partial void LocationAccepted(ILogger logger, Guid userId, double? accuracyMeters);

    [LoggerMessage(EventId = 3001, Level = LogLevel.Information, Message = "Location update rejected for user {UserId}: {Reason}")]
    public static partial void LocationRejected(ILogger logger, Guid userId, string reason);

    [LoggerMessage(EventId = 3002, Level = LogLevel.Warning, Message = "Implausible movement for user {UserId}: implied speed {ImpliedSpeedMps:F0} m/s")]
    public static partial void ImplausibleMovement(ILogger logger, Guid userId, double impliedSpeedMps);

    [LoggerMessage(EventId = 3003, Level = LogLevel.Debug, Message = "Location update throttled for user {UserId}")]
    public static partial void LocationThrottled(ILogger logger, Guid userId);

    [LoggerMessage(EventId = 3004, Level = LogLevel.Debug, Message = "Low-accuracy fix for user {UserId} treated as heartbeat ({AccuracyMeters} m)")]
    public static partial void LocationLowAccuracy(ILogger logger, Guid userId, double? accuracyMeters);

    [LoggerMessage(EventId = 4000, Level = LogLevel.Information, Message = "User {UserId} moved from group {FromGroup} to {ToGroup} ({Reason})")]
    public static partial void ProximityGroupChanged(ILogger logger, Guid userId, string? fromGroup, string? toGroup, ProximityChangeReason reason);

    [LoggerMessage(EventId = 4001, Level = LogLevel.Error, Message = "Proximity notification failed for user {UserId}")]
    public static partial void NotificationFailed(ILogger logger, Exception exception, Guid userId);

    [LoggerMessage(EventId = 5000, Level = LogLevel.Information, Message = "Voice token issued to user {UserId} for room {Room}")]
    public static partial void VoiceTokenIssued(ILogger logger, Guid userId, string room);

    [LoggerMessage(EventId = 5001, Level = LogLevel.Warning, Message = "Voice token refused for user {UserId}: not a member of any proximity group")]
    public static partial void VoiceTokenRefused(ILogger logger, Guid userId);
}
