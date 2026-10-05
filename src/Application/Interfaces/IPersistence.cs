using Opdeweg.Domain.Entities;
using Opdeweg.Domain.Enums;
using Opdeweg.Domain.ValueObjects;

namespace Opdeweg.Application.Interfaces;

public interface IUserRepository
{
    Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>Finds a login (with its user loaded) by provider and provider key.</summary>
    Task<UserLogin?> FindLoginAsync(AuthProvider provider, string providerKey, CancellationToken cancellationToken);

    Task<bool> EmailExistsAsync(string normalizedEmail, CancellationToken cancellationToken);

    void Add(User user, UserLogin login);

    void Remove(User user);
}

public interface IRefreshTokenRepository
{
    Task<RefreshToken?> FindByHashAsync(string tokenHash, CancellationToken cancellationToken);

    void Add(RefreshToken token);

    Task RevokeFamilyAsync(Guid familyId, DateTimeOffset now, CancellationToken cancellationToken);

    Task RevokeAllForUserAsync(Guid userId, DateTimeOffset now, CancellationToken cancellationToken);
}

public interface IDrivingSessionRepository
{
    Task<DrivingSession?> GetActiveAsync(Guid userId, CancellationToken cancellationToken);

    void Add(DrivingSession session);

    /// <summary>Records the coarse start cell on a session without loading it.</summary>
    Task RecordCoarseStartAreaAsync(Guid sessionId, GeoPoint coarseArea, CancellationToken cancellationToken);
}

public interface IUnitOfWork
{
    Task SaveChangesAsync(CancellationToken cancellationToken);
}
