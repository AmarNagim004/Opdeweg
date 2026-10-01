namespace Opdeweg.Domain.Entities;

public sealed class User
{
    public const int DisplayNameMinLength = 2;
    public const int DisplayNameMaxLength = 32;
    public const int EmailMaxLength = 254;
    public const int AvatarUrlMaxLength = 2048;

    private static readonly TimeSpan ActivityResolution = TimeSpan.FromMinutes(5);

    public User(Guid id, string email, string displayName, DateTimeOffset now)
    {
        Id = id;
        Email = email;
        DisplayName = displayName;
        CreatedAt = now;
        LastActiveAt = now;
    }

    private User()
    {
    }

    public Guid Id { get; private set; }

    /// <summary>Normalised (trimmed, lower-case) e-mail address. Never shown to other drivers.</summary>
    public string Email { get; private set; } = string.Empty;

    public string DisplayName { get; private set; } = string.Empty;

    public string? AvatarUrl { get; private set; }

    /// <summary>When false, nearby drivers only see a generic "Driver" label and no avatar.</summary>
    public bool ShareDisplayName { get; private set; } = true;

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset LastActiveAt { get; private set; }

    public ICollection<UserLogin> Logins { get; private set; } = new List<UserLogin>();

    public void UpdateProfile(string? displayName, string? avatarUrl, bool? shareDisplayName)
    {
        if (displayName is not null)
        {
            DisplayName = displayName;
        }

        if (avatarUrl is not null)
        {
            AvatarUrl = avatarUrl.Length == 0 ? null : avatarUrl;
        }

        if (shareDisplayName is not null)
        {
            ShareDisplayName = shareDisplayName.Value;
        }
    }

    /// <summary>Records activity at a coarse resolution to avoid a database write on every request.</summary>
    public bool MarkActive(DateTimeOffset now)
    {
        if (now - LastActiveAt < ActivityResolution)
        {
            return false;
        }

        LastActiveAt = now;
        return true;
    }
}
