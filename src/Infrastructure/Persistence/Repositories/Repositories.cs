using Microsoft.EntityFrameworkCore;
using Opdeweg.Application.Interfaces;
using Opdeweg.Domain.Entities;
using Opdeweg.Domain.Enums;
using Opdeweg.Domain.ValueObjects;

namespace Opdeweg.Infrastructure.Persistence.Repositories;

internal sealed class UserRepository(AppDbContext db) : IUserRepository
{
    public Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        db.Users.FirstOrDefaultAsync(u => u.Id == id, cancellationToken);

    public Task<UserLogin?> FindLoginAsync(AuthProvider provider, string providerKey, CancellationToken cancellationToken) =>
        db.UserLogins.Include(l => l.User).FirstOrDefaultAsync(l => l.Provider == provider && l.ProviderKey == providerKey, cancellationToken);

    public Task<bool> EmailExistsAsync(string normalizedEmail, CancellationToken cancellationToken) =>
        db.Users.AnyAsync(u => u.Email == normalizedEmail, cancellationToken);

    public void Add(User user, UserLogin login)
    {
        db.Users.Add(user);
        db.UserLogins.Add(login);
    }

    public void Remove(User user) => db.Users.Remove(user);
}

internal sealed class RefreshTokenRepository(AppDbContext db) : IRefreshTokenRepository
{
    public Task<RefreshToken?> FindByHashAsync(string tokenHash, CancellationToken cancellationToken) =>
        db.RefreshTokens.FirstOrDefaultAsync(t => t.TokenHash == tokenHash, cancellationToken);

    public void Add(RefreshToken token) => db.RefreshTokens.Add(token);

    public Task RevokeFamilyAsync(Guid familyId, DateTimeOffset now, CancellationToken cancellationToken) =>
        db.RefreshTokens
            .Where(t => t.FamilyId == familyId && t.RevokedAt == null)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.RevokedAt, now), cancellationToken);

    public Task RevokeAllForUserAsync(Guid userId, DateTimeOffset now, CancellationToken cancellationToken) =>
        db.RefreshTokens
            .Where(t => t.UserId == userId && t.RevokedAt == null)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.RevokedAt, now), cancellationToken);
}

internal sealed class DrivingSessionRepository(AppDbContext db) : IDrivingSessionRepository
{
    public Task<DrivingSession?> GetActiveAsync(Guid userId, CancellationToken cancellationToken) =>
        db.DrivingSessions.FirstOrDefaultAsync(s => s.UserId == userId && s.EndedAt == null, cancellationToken);

    public void Add(DrivingSession session) => db.DrivingSessions.Add(session);

    public Task RecordCoarseStartAreaAsync(Guid sessionId, GeoPoint coarseArea, CancellationToken cancellationToken) =>
        db.DrivingSessions
            .Where(s => s.Id == sessionId && s.CoarseStartArea == null)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.CoarseStartArea, coarseArea), cancellationToken);
}
