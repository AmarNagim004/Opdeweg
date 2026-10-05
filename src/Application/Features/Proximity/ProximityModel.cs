using Opdeweg.Domain.ValueObjects;

namespace Opdeweg.Application.Features.Proximity;

public enum ProximityChangeReason
{
    /// <summary>Joined because every member is within the join distance.</summary>
    Proximity,

    /// <summary>Left because the driver moved beyond the leave distance of a member.</summary>
    OutOfRange,

    /// <summary>Moved because two nearby groups were merged.</summary>
    Merged,

    /// <summary>The group dropped below two members.</summary>
    GroupDissolved,

    SessionEnded,

    /// <summary>No recent location updates.</summary>
    Stale,
}

public enum MembershipChangeKind
{
    Joined,
    Left,
}

public sealed record GroupMembershipChange(Guid UserId, string GroupId, MembershipChangeKind Kind, ProximityChangeReason Reason);

/// <summary>A driver's net group transition within one evaluation.</summary>
public sealed record MembershipTransition(Guid UserId, string? FromGroupId, string? ToGroupId, ProximityChangeReason Reason);

/// <summary>A driver as seen by the grouping engine: latest usable fix plus liveness.</summary>
public sealed record DriverState(Guid UserId, GeoPoint Position, double? AccuracyMeters, DateTimeOffset LastSeenAt);

public abstract record ProximityCommand(Guid UserId);

/// <summary>Re-evaluate a driver's group membership after their position changed.</summary>
public sealed record EvaluateDriverCommand(Guid UserId) : ProximityCommand(UserId);

/// <summary>Remove a driver from proximity (stale, session ended, ...).</summary>
public sealed record RemoveDriverCommand(Guid UserId, ProximityChangeReason Reason, bool EndSession) : ProximityCommand(UserId);

public interface IGroupIdGenerator
{
    string NewGroupId();
}

internal sealed class RandomGroupIdGenerator : IGroupIdGenerator
{
    public string NewGroupId() => Convert.ToHexStringLower(System.Security.Cryptography.RandomNumberGenerator.GetBytes(8));
}

public static class RoomNames
{
    private const string Prefix = "opd-";

    public static string ForGroup(string groupId) => Prefix + groupId;

    public static bool TryGetGroupId(string room, out string groupId)
    {
        if (room.StartsWith(Prefix, StringComparison.Ordinal) && room.Length > Prefix.Length)
        {
            groupId = room[Prefix.Length..];
            return true;
        }

        groupId = string.Empty;
        return false;
    }
}
