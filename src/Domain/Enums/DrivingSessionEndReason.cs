namespace Opdeweg.Domain.Enums;

public enum DrivingSessionEndReason
{
    UserEnded = 1,
    SignedOut = 2,
    Idle = 3,
    Replaced = 4,
    AccountDeleted = 5,
    Orphaned = 6,
}
