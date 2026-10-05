using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using Opdeweg.Application.Features.Driving;
using Opdeweg.Application.Features.Proximity;
using Opdeweg.Application.Features.Voice;
using Opdeweg.Application.Interfaces;
using Opdeweg.Domain.Entities;
using Opdeweg.Domain.ValueObjects;
using Opdeweg.UnitTests.TestSupport;

namespace Opdeweg.UnitTests.Proximity;

/// <summary>Wires the real proximity pipeline (processor, engine, notifier, voice) to in-memory fakes.</summary>
internal sealed class ProximityHarness
{
    public ProximityHarness()
    {
        Time = new FakeTimeProvider(new DateTimeOffset(2026, 10, 1, 12, 0, 0, TimeSpan.Zero));
        Voice = new VoiceAccessService(Store, TokenIssuer, NullLogger<VoiceAccessService>.Instance);
        var views = new ProximityViewBuilder(Store, Voice, TestOptions.Proximity());
        Query = new ProximityQueryService(Store, views, TestOptions.Proximity(), Time);
        var notifier = new ProximityNotifier(views, Realtime, Rooms, NullLogger<ProximityNotifier>.Instance);
        Processor = new ProximityProcessor(
            Store,
            new ProximityGroupingEngine(TestOptions.Proximity(), new SequentialGroupIds()),
            notifier,
            TestOptions.Proximity(),
            Time,
            NullLogger<ProximityProcessor>.Instance);

        Queue.Handler = command => RunAsync(command);
    }

    public FakeTimeProvider Time { get; }

    public InMemoryPresenceStore Store { get; } = new();

    public RecordingRealtimeNotifier Realtime { get; } = new();

    public RecordingRoomReconciler Rooms { get; } = new();

    public FakeVoiceTokenIssuer TokenIssuer { get; } = new();

    public RecordingProximityQueue Queue { get; } = new();

    public FakeDrivingSessionRepository SessionRepository { get; } = new();

    public FakeUserRepository Users { get; } = new();

    public VoiceAccessService Voice { get; }

    public ProximityQueryService Query { get; }

    public ProximityProcessor Processor { get; }

    public DrivingSessionService DrivingSessions() => new(
        SessionRepository,
        Users,
        new NoopUnitOfWork(),
        Store,
        Queue,
        Realtime,
        TestOptions.Proximity(),
        TestOptions.Driving(),
        Time,
        NullLogger<DrivingSessionService>.Instance);

    /// <summary>Creates a user with an active driving session (through the real session service).</summary>
    public async Task<Guid> StartDriverAsync(string name)
    {
        var user = new User(Guid.CreateVersion7(), $"{name.ToLowerInvariant()}@example.com", name, Time.GetUtcNow());
        Users.Users.Add(user);
        await DrivingSessions().StartAsync(user.Id, CancellationToken.None);
        return user.Id;
    }

    /// <summary>Stores a fix for the driver and runs the proximity evaluation (plus any knock-on re-evaluations).</summary>
    public async Task MoveAsync(Guid userId, GeoPoint point)
    {
        var now = Time.GetUtcNow();
        await Store.SavePositionAsync(userId, new StoredPosition(point, 5, 10, 90, now, now, now), new PresenceTimeouts(TimeSpan.FromMinutes(3), TimeSpan.FromMinutes(35)), CancellationToken.None);
        await RunAsync(new EvaluateDriverCommand(userId));
    }

    public async Task RunAsync(ProximityCommand command)
    {
        var pending = new Queue<ProximityCommand>([command]);
        while (pending.TryDequeue(out var next))
        {
            foreach (var id in await Processor.HandleAsync(next, CancellationToken.None))
            {
                pending.Enqueue(new EvaluateDriverCommand(id));
            }
        }
    }

    public string? GroupOf(Guid userId) => Store.Sessions.GetValueOrDefault(userId)?.GroupId;

    public string HandleOf(Guid userId) => Store.Sessions[userId].Handle;
}
