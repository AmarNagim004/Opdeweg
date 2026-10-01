using Opdeweg.Application.DTOs;
using Opdeweg.Application.Features.Proximity;
using Opdeweg.Application.Interfaces;
using Opdeweg.Domain.Entities;
using Opdeweg.Domain.Enums;
using Opdeweg.Domain.ValueObjects;

namespace Opdeweg.UnitTests.TestSupport;

internal sealed class RecordingRealtimeNotifier : IRealtimeNotifier
{
    public List<(Guid UserId, ProximityGroupDto Group)> Joined { get; } = [];

    public List<(Guid UserId, ProximityGroupLeftDto Left)> Left { get; } = [];

    public List<(Guid UserId, ProximityGroupDto Group)> RosterChanged { get; } = [];

    public List<(Guid UserId, DrivingSessionDto Session)> SessionsStarted { get; } = [];

    public List<(Guid UserId, DrivingSessionEndedDto Ended)> SessionsEnded { get; } = [];

    public Task ProximityGroupJoinedAsync(Guid userId, ProximityGroupDto group, CancellationToken cancellationToken)
    {
        Joined.Add((userId, group));
        return Task.CompletedTask;
    }

    public Task ProximityGroupLeftAsync(Guid userId, ProximityGroupLeftDto left, CancellationToken cancellationToken)
    {
        Left.Add((userId, left));
        return Task.CompletedTask;
    }

    public Task NearbyUsersChangedAsync(Guid userId, ProximityGroupDto group, CancellationToken cancellationToken)
    {
        RosterChanged.Add((userId, group));
        return Task.CompletedTask;
    }

    public Task DrivingSessionStartedAsync(Guid userId, DrivingSessionDto session, CancellationToken cancellationToken)
    {
        SessionsStarted.Add((userId, session));
        return Task.CompletedTask;
    }

    public Task DrivingSessionEndedAsync(Guid userId, DrivingSessionEndedDto ended, CancellationToken cancellationToken)
    {
        SessionsEnded.Add((userId, ended));
        return Task.CompletedTask;
    }
}

internal sealed class FakeVoiceTokenIssuer : IVoiceTokenIssuer
{
    public List<VoiceJoinGrant> Issued { get; } = [];

    public VoiceAccessDto CreateJoinToken(VoiceJoinGrant grant)
    {
        Issued.Add(grant);
        return new VoiceAccessDto("wss://voice.test", $"token-for-{grant.Identity}-{grant.Room}", grant.Room, grant.Identity, DateTimeOffset.UnixEpoch.AddMinutes(5));
    }
}

internal sealed class RecordingRoomReconciler : IVoiceRoomReconciler
{
    public List<string> Enqueued { get; } = [];

    public void Enqueue(string groupId) => Enqueued.Add(groupId);
}

internal sealed class SequentialGroupIds : IGroupIdGenerator
{
    private int _next;

    public string NewGroupId() => $"g{++_next}";
}

/// <summary>Records commands; tests drain them through a processor explicitly.</summary>
internal sealed class RecordingProximityQueue : IProximityCommandQueue
{
    public List<ProximityCommand> Commands { get; } = [];

    public Func<ProximityCommand, Task>? Handler { get; set; }

    public bool TryEnqueue(ProximityCommand command)
    {
        Commands.Add(command);
        return true;
    }

    public async Task<bool> EnqueueAndWaitAsync(ProximityCommand command, TimeSpan timeout, CancellationToken cancellationToken)
    {
        Commands.Add(command);
        if (Handler is not null)
        {
            await Handler(command);
            return true;
        }

        return false;
    }
}

internal sealed class FakeDrivingSessionRepository : IDrivingSessionRepository
{
    public List<DrivingSession> Sessions { get; } = [];

    public Task<DrivingSession?> GetActiveAsync(Guid userId, CancellationToken cancellationToken) =>
        Task.FromResult(Sessions.FirstOrDefault(s => s.UserId == userId && s.IsActive));

    public void Add(DrivingSession session) => Sessions.Add(session);

    public Task RecordCoarseStartAreaAsync(Guid sessionId, GeoPoint coarseArea, CancellationToken cancellationToken)
    {
        Sessions.Single(s => s.Id == sessionId).RecordCoarseStartArea(coarseArea);
        return Task.CompletedTask;
    }
}

internal sealed class FakeUserRepository : IUserRepository
{
    public List<User> Users { get; } = [];

    public Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken) => Task.FromResult(Users.FirstOrDefault(u => u.Id == id));

    public Task<UserLogin?> FindLoginAsync(AuthProvider provider, string providerKey, CancellationToken cancellationToken) => Task.FromResult<UserLogin?>(null);

    public Task<bool> EmailExistsAsync(string normalizedEmail, CancellationToken cancellationToken) => Task.FromResult(Users.Any(u => u.Email == normalizedEmail));

    public void Add(User user, UserLogin login) => Users.Add(user);

    public void Remove(User user) => Users.Remove(user);
}

internal sealed class NoopUnitOfWork : IUnitOfWork
{
    public int Saves { get; private set; }

    public Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        Saves++;
        return Task.CompletedTask;
    }
}
