using Opdeweg.Domain.Enums;

namespace Opdeweg.Domain.Entities;

/// <summary>
/// One way of signing in to a <see cref="User"/>. Password logins store a hash; social logins
/// (Apple, Google) will store the provider's stable subject in <see cref="ProviderKey"/>.
/// </summary>
public sealed class UserLogin
{
    public UserLogin(Guid id, Guid userId, AuthProvider provider, string providerKey, string? passwordHash, DateTimeOffset now)
    {
        Id = id;
        UserId = userId;
        Provider = provider;
        ProviderKey = providerKey;
        PasswordHash = passwordHash;
        CreatedAt = now;
    }

    private UserLogin()
    {
    }

    public Guid Id { get; private set; }

    public Guid UserId { get; private set; }

    public User? User { get; private set; }

    public AuthProvider Provider { get; private set; }

    public string ProviderKey { get; private set; } = string.Empty;

    public string? PasswordHash { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public static UserLogin ForPassword(Guid userId, string normalizedEmail, string passwordHash, DateTimeOffset now) =>
        new(Guid.CreateVersion7(), userId, AuthProvider.Password, normalizedEmail, passwordHash, now);

    public void ReplacePasswordHash(string passwordHash) => PasswordHash = passwordHash;
}
